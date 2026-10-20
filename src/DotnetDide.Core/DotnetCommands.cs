using System.Globalization;
using System.Reflection;
using System.Text;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace DotnetDide;

public sealed record BuildResult(bool Success, string Output, IReadOnlyList<CodeDiagnostic> Diagnostics,
    string? AssemblyPath, string? PdbPath)
{
    public override string ToString() => Success ? $"Build succeeded: {AssemblyPath}" : "Build failed. See compilation output.";
}
public sealed record ProgramRunResult(bool Success, BuildResult Build, string Stdout, string Stderr, int? ExitCode, RunError? Error)
{
    public override string ToString() => Error is not null ? $"{Error.Type}: {Error.Message}" : !Build.Success ? "Build failed. Program was not run." : $"Program exited with code {ExitCode}.";
}

// Public because the dynamically compiled submission accesses this globals type.
public sealed class RunspaceGlobals
{
    public DotnetCommands Dotnet { get; }
    internal RunspaceGlobals(DotnetCommands dotnet) => Dotnet = dotnet;
}

/// <summary>Compile workspace programs with Roslyn; available as Dotnet in submissions.</summary>
public sealed class DotnetCommands
{
    private readonly WorkspaceFileSystem _files;
    private readonly IReadOnlyList<MetadataReference> _references;
    private readonly Action<Assembly> _register;
    private readonly Func<string>? _getDebugInfo;
    internal DotnetCommands(WorkspaceFileSystem files, IReadOnlyList<MetadataReference> references, Action<Assembly> register, Func<string>? getDebugInfo = null)
    { _files = files; _references = references; _register = register; _getDebugInfo = getDebugInfo; }

    /// <summary>Host diagnostics JSON captured when this submission was requested.</summary>
    public string GetDebugInfo() => _getDebugInfo?.Invoke() ?? throw new NotSupportedException("This host does not provide debug snapshots.");

    public BuildResult Build(string? path = null)
    {
        var built = Compile(path); Console.Write(built.Result.Output); return built.Result;
    }
    public async Task<ProgramRunResult> Run(string? path = null, string[]? args = null)
    {
        var built = Compile(path); Console.Write(built.Result.Output);
        if (!built.Result.Success) return new(false, built.Result, "", "", null, null);
        if (built.EntryType is null) return new(false, built.Result, "", "", null, new("DotnetDide.NoEntryPoint", "This project builds a library. Set OutputType to Exe to run it."));
        using var stdout = new Runspace.BoundedWriter(); using var stderr = new Runspace.BoundedWriter();
        var oldOut = Console.Out; var oldError = Console.Error; var oldDirectory = Directory.GetCurrentDirectory();
        int? exitCode = null; RunError? error = null;
        try
        {
            Console.SetOut(stdout); Console.SetError(stderr); Directory.SetCurrentDirectory(built.Directory!);
            var assembly = Assembly.Load(built.Image!); _register(assembly);
            // Invoke the source entry point, not the synchronous wrapper generated for async Main.
            // Blocking on the wrapper's Task would stall browser-wasm's single thread.
            var method = assembly.GetType(built.EntryType, throwOnError: true)!.GetMethod(built.EntryMethod!,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null,
                built.HasArguments ? [typeof(string[])] : Type.EmptyTypes, null)!;
            var value = method.Invoke(null, built.HasArguments ? [args ?? []] : null);
            if (value is Task task)
            {
                await task.ConfigureAwait(false);
                value = task.GetType().GetProperty("Result")?.GetValue(task);
            }
            exitCode = value is int code ? code : 0;
        }
        catch (Exception failure)
        {
            var cause = failure is TargetInvocationException { InnerException: not null } wrapper ? wrapper.InnerException! : failure;
            error = new(cause.GetType().FullName!, cause.Message);
        }
        finally { Console.SetOut(oldOut); Console.SetError(oldError); Directory.SetCurrentDirectory(oldDirectory); }
        var output = stdout.ToString(); var errors = stderr.ToString(); Console.Write(output); Console.Error.Write(errors);
        return new(error is null && exitCode == 0, built.Result, output, errors, exitCode, error);
    }

    private sealed record Compiled(BuildResult Result, byte[]? Image = null, string? Directory = null,
        string? EntryType = null, string? EntryMethod = null, bool HasArguments = false);

    private Compiled Compile(string? path)
    {
        try
        {
            var input = new ProjectReader(_files).Read(path);
            if (input.Sources.Length == 0) throw new ArgumentException("No C# source files found for this build.");
            var parse = new CSharpParseOptions(input.LanguageVersion, preprocessorSymbols: input.Defines);
            var trees = input.Sources.Select(source => CSharpSyntaxTree.ParseText(
                SourceText.From(Encoding.UTF8.GetString(_files.ReadFile(source)), Encoding.UTF8), parse, Relative(source))).ToList();
            if (input.Usings.Length > 0) trees.Add(CSharpSyntaxTree.ParseText(
                SourceText.From(string.Join('\n', input.Usings.Select(ns => $"global using {ns};")), Encoding.UTF8), parse, "dotnetdide.global-usings.g.cs"));
            var options = new CSharpCompilationOptions(input.OutputKind, optimizationLevel: input.Optimize ? OptimizationLevel.Release : OptimizationLevel.Debug,
                allowUnsafe: input.Unsafe, nullableContextOptions: input.Nullable, generalDiagnosticOption: input.WarningsAsErrors ? ReportDiagnostic.Error : ReportDiagnostic.Default);
            // Unique identities let rebuilt programs coexist in the same WASM runtime.
            var compilation = CSharpCompilation.Create(input.Name + "_" + Guid.NewGuid().ToString("N"), trees, _references, options);
            using var dll = new MemoryStream(); using var pdb = new MemoryStream();
            var emitted = compilation.Emit(dll, pdb, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));
            var diagnostics = emitted.Diagnostics.Where(d => d.Severity != DiagnosticSeverity.Hidden).Select(ToDiagnostic).ToArray();
            if (!emitted.Success) return new(Result(false, diagnostics));
            var directory = Path.Combine(input.Directory, "bin"); _files.Mkdir(directory);
            var assemblyPath = Path.Combine(directory, input.Name + ".dll"); var pdbPath = Path.Combine(directory, input.Name + ".pdb");
            var image = dll.ToArray(); _files.WriteFile(assemblyPath, image); _files.WriteFile(pdbPath, pdb.ToArray());
            var entry = compilation.GetEntryPoint(default);
            return new(Result(true, diagnostics, Relative(assemblyPath), Relative(pdbPath)), image, input.Directory,
                entry is null ? null : MetadataName(entry.ContainingType), entry?.MetadataName, entry?.Parameters.Length == 1);
        }
        catch (Exception error) when (error is ArgumentException or IOException or XmlException or UnauthorizedAccessException)
        {
            return new(Result(false, [new("DND1000", error.Message, "Error", 1, 1, path)]));
        }
    }
    private string Relative(string path) => Path.GetRelativePath(_files.RootPath, path).Replace('\\', '/');
    private static string MetadataName(INamedTypeSymbol type) => type.ContainingType is not null ? MetadataName(type.ContainingType) + "+" + type.MetadataName
        : type.ContainingNamespace.IsGlobalNamespace ? type.MetadataName : type.ContainingNamespace.ToDisplayString() + "." + type.MetadataName;
    private static CodeDiagnostic ToDiagnostic(Diagnostic diagnostic)
    {
        var span = diagnostic.Location.GetLineSpan(); var start = span.StartLinePosition;
        return new(diagnostic.Id, diagnostic.GetMessage(CultureInfo.InvariantCulture), diagnostic.Severity.ToString(), start.Line + 1, start.Character + 1,
            diagnostic.Location.IsInSource ? span.Path : null);
    }
    private static BuildResult Result(bool success, CodeDiagnostic[] diagnostics, string? dll = null, string? pdb = null)
    {
        var output = new StringBuilder();
        foreach (var diagnostic in diagnostics) output.AppendLine($"{diagnostic.FilePath ?? "build"}({diagnostic.Line},{diagnostic.Column}): {diagnostic.Severity.ToLowerInvariant()} {diagnostic.Id}: {diagnostic.Message}");
        output.AppendLine(success ? $"Build succeeded -> {dll}" : "Build failed.");
        output.AppendLine($"{diagnostics.Count(d => d.Severity == "Warning")} warning(s), {diagnostics.Count(d => d.Severity == "Error")} error(s).");
        if (pdb is not null) output.AppendLine($"Symbols -> {pdb}");
        return new(success, output.ToString(), diagnostics, dll, pdb);
    }
}
