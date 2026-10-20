using System.Globalization;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DotnetDide;

/// <summary>Stateful Roslyn submissions. Use one dedicated browser worker per runspace.</summary>
public sealed class Runspace : IDisposable
{
    // Console and the current directory are runtime-global, including across native test instances.
    private static readonly SemaphoreSlim RuntimeGate = new(1, 1);
    private readonly List<MetadataReference> _references;
    private readonly PackageLoader _packages;
    private readonly WireRegistry _wire;
    public IReadOnlyList<WireSite> WireSites => _wire.Sites;
    private readonly HttpClient? _ownedNugetClient;
    private readonly Dictionary<string, Assembly> _assemblies = new(StringComparer.Ordinal);
    private readonly string _id = Guid.NewGuid().ToString("N");
    private readonly RunspaceGlobals _globals;
    private CSharpCompilation? _previous;
    private object[] _states = new object[16];
    private int _sequence, _submissions, _pending;
    private bool _disposed;
    public WorkspaceFileSystem Files { get; }

    public Runspace(WorkspaceFileSystem files, IEnumerable<MetadataReference> references, bool nugetAccess = false, HttpClient? nugetClient = null, bool siteAccess = false)
    {
        Files = files; _references = references.ToList();
        _wire = new(files) { Enabled = siteAccess };
        _ownedNugetClient = nugetClient is null ? new HttpClient() : null;
        _packages = new(nugetClient ?? _ownedNugetClient!, _references, additions =>
        {
            var metadata = additions.Select(a => MetadataReference.CreateFromImage(a.Bytes)).ToArray();
            foreach (var addition in additions)
            {
                var assembly = Assembly.Load(addition.Bytes);
                _assemblies[assembly.GetName().Name!] = assembly;
            }
            _references.AddRange(metadata);
        }) { Enabled = nugetAccess };
        _globals = new(new DotnetCommands(Files, _references, assembly => _assemblies[assembly.GetName().Name!] = assembly));
        _states[0] = _globals;
        AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
    }
    private Assembly? ResolveAssembly(object? sender, ResolveEventArgs args) =>
        _assemblies.GetValueOrDefault(new AssemblyName(args.Name).Name!);

    public async Task<RunResult> RunAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code); ObjectDisposedException.ThrowIf(_disposed, this);
        Interlocked.Increment(ref _pending);
        var entered = false;
        try
        {
            await RuntimeGate.WaitAsync(cancellationToken).ConfigureAwait(false); entered = true;
            cancellationToken.ThrowIfCancellationRequested();
            var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Preview, kind: SourceCodeKind.Script),
                path: "submission.csx", cancellationToken: cancellationToken);
            var name = $"DotnetDide_{_id}_{++_sequence}";
            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                usings: ProjectReader.ScriptUsings,
                scriptClassName: "Submission", optimizationLevel: OptimizationLevel.Release);
            var compilation = CSharpCompilation.CreateScriptCompilation(name, tree, _references, options, _previous, typeof(object), globalsType: typeof(RunspaceGlobals));
            using var stream = new MemoryStream();
            var emitted = compilation.Emit(stream, cancellationToken: cancellationToken);
            var diagnostics = emitted.Diagnostics.Select(d =>
            {
                var start = d.Location.GetLineSpan().StartLinePosition;
                return new CodeDiagnostic(d.Id, d.GetMessage(CultureInfo.InvariantCulture), d.Severity.ToString(), start.Line + 1, start.Character + 1);
            }).ToArray();
            if (!emitted.Success) return new(false, "", "", null, diagnostics, null);
            cancellationToken.ThrowIfCancellationRequested();
            var assembly = Assembly.Load(stream.ToArray()); _assemblies.Add(name, assembly);
            if (_states.Length < _submissions + 2) Array.Resize(ref _states, _states.Length * 2);
            using var stdout = new BoundedWriter(); using var stderr = new BoundedWriter();
            var oldOut = Console.Out; var oldError = Console.Error; var oldDirectory = Directory.GetCurrentDirectory();
            try
            {
                Console.SetOut(stdout); Console.SetError(stderr); Directory.SetCurrentDirectory(Files.RootPath);
                Nuget.Current.Value = _packages; Wire.Current.Value = _wire;
                var factory = assembly.GetType("Submission", throwOnError: true)!.GetMethod("<Factory>", BindingFlags.Public | BindingFlags.Static)!;
                var value = await ((Task<object>)factory.Invoke(null, [_states])!).ConfigureAwait(false);
                _previous = compilation; _submissions++;
                return new(true, stdout.ToString(), stderr.ToString(), value is null ? null : Convert.ToString(value, CultureInfo.InvariantCulture), diagnostics, null);
            }
            catch (Exception error)
            {
                var cause = error is TargetInvocationException { InnerException: not null } wrapper ? wrapper.InnerException! : error;
                return new(false, stdout.ToString(), stderr.ToString(), null, diagnostics, new(cause.GetType().FullName!, cause.Message));
            }
            finally { Wire.Current.Value = null; Nuget.Current.Value = null; Console.SetOut(oldOut); Console.SetError(oldError); Directory.SetCurrentDirectory(oldDirectory); }
        }
        finally { if (entered) RuntimeGate.Release(); Interlocked.Decrement(ref _pending); }
    }
    public void SetSiteAccess(bool enabled)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Volatile.Read(ref _pending) != 0) throw new InvalidOperationException("Cannot change permissions on a busy runspace.");
        if (!enabled) _wire.Dispose();
        _wire.Enabled = enabled;
    }
    public async Task<WireResponse> RequestAsync(WireRequest request, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this); Interlocked.Increment(ref _pending);
        var entered = false;
        try
        {
            await RuntimeGate.WaitAsync(cancellationToken); entered = true;
            var previous = Directory.GetCurrentDirectory(); var stdout = Console.Out; var stderr = Console.Error;
            using var output = new BoundedWriter(); using var errors = new BoundedWriter();
            try
            {
                Directory.SetCurrentDirectory(Files.RootPath); Console.SetOut(output); Console.SetError(errors);
                Wire.Current.Value = _wire; Nuget.Current.Value = _packages;
                return await _wire.RequestAsync(request, cancellationToken);
            }
            finally { Directory.SetCurrentDirectory(previous); Console.SetOut(stdout); Console.SetError(stderr); Wire.Current.Value = null; Nuget.Current.Value = null; }
        }
        finally { if (entered) RuntimeGate.Release(); Interlocked.Decrement(ref _pending); }
    }
    public void SetNugetAccess(bool enabled)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Volatile.Read(ref _pending) != 0) throw new InvalidOperationException("Cannot change permissions on a busy runspace.");
        _packages.Enabled = enabled;
    }
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Volatile.Read(ref _pending) != 0) throw new InvalidOperationException("Cannot reset a busy runspace.");
        _wire.Dispose(); _previous = null; _states = new object[16]; _states[0] = _globals; _submissions = 0;
        // Loaded assemblies cannot be unloaded in browser-wasm. Terminate the worker to reclaim them.
    }
    public void Dispose()
    {
        if (_disposed) return;
        if (Volatile.Read(ref _pending) != 0) throw new InvalidOperationException("Cannot dispose a busy runspace.");
        _wire.Dispose(); _disposed = true; AppDomain.CurrentDomain.AssemblyResolve -= ResolveAssembly;
        _assemblies.Clear(); _states = []; _previous = null; _ownedNugetClient?.Dispose();
    }
    internal sealed class BoundedWriter : StringWriter
    {
        private const int Limit = 1_000_000;
        private bool _truncated;
        public override void Write(char value)
        {
            if (GetStringBuilder().Length < Limit) base.Write(value);
            else if (!_truncated) { _truncated = true; base.Write("\n[output truncated]\n"); }
        }
        public override void Write(string? value) { if (value is not null) foreach (var character in value) Write(character); }
        public override void Write(char[] buffer, int index, int count) { foreach (var character in buffer.AsSpan(index, count)) Write(character); }
    }
}
