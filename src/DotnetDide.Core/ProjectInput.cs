using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DotnetDide;

internal sealed record ProjectInput(string Name, string Directory, string[] Sources, string[] Usings,
    OutputKind OutputKind, LanguageVersion LanguageVersion, NullableContextOptions Nullable,
    bool Unsafe, bool Optimize, bool WarningsAsErrors, string[] Defines);

/// <summary>A deliberately bounded project reader, not an MSBuild evaluator.</summary>
internal sealed class ProjectReader(WorkspaceFileSystem files)
{
    internal static readonly string[] ScriptUsings = ["System", "System.IO", "System.Linq", "System.Collections.Generic", "System.Threading.Tasks"];
    internal static readonly string[] SdkUsings = ["System", "System.Collections.Generic", "System.IO", "System.Linq", "System.Net.Http", "System.Threading", "System.Threading.Tasks"];
    private static readonly HashSet<string> Properties = ["TargetFramework", "OutputType", "AssemblyName", "ImplicitUsings", "Nullable", "LangVersion", "AllowUnsafeBlocks", "Optimize", "TreatWarningsAsErrors", "DefineConstants", "EnableDefaultCompileItems"];

    public ProjectInput Read(string? path)
    {
        var full = files.ResolvePath(Path.GetFullPath(path ?? ".", System.IO.Directory.GetCurrentDirectory()), true);
        if (System.IO.Directory.Exists(full))
        {
            var projects = files.ReadDirectory(full).Where(f => !f.IsDirectory && f.Name.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (projects.Length > 1) throw new ArgumentException("Multiple project files found. Pass an explicit .csproj path.");
            if (projects.Length == 1) full = Path.Combine(full, projects[0].Name);
            else return Loose(Path.GetFileName(full), full, FindSources(full).ToArray());
        }
        if (!File.Exists(full)) throw new FileNotFoundException("Build input does not exist.", path);
        if (full.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return Loose(Path.GetFileNameWithoutExtension(full), Path.GetDirectoryName(full)!, [full]);
        if (!full.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Build input must be a .cs file, .csproj file or directory.");
        return Project(full);
    }
    private static ProjectInput Loose(string name, string directory, string[] sources) => new(name, directory, sources,
        ScriptUsings, OutputKind.ConsoleApplication, LanguageVersion.Preview, NullableContextOptions.Disable, false, true, false, []);

    private ProjectInput Project(string path)
    {
        using var stream = new MemoryStream(files.ReadFile(path));
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var root = XDocument.Load(reader).Root ?? throw new ArgumentException("Project XML is empty.");
        if (root.Name != "Project" || (string?)root.Attribute("Sdk") != "Microsoft.NET.Sdk") Unsupported("Only <Project Sdk=\"Microsoft.NET.Sdk\">");
        Attributes(root, "Sdk");
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        var items = new List<XElement>();
        foreach (var group in root.Elements())
        {
            Attributes(group);
            if (group.Name == "PropertyGroup")
            {
                foreach (var property in group.Elements())
                {
                    Attributes(property);
                    if (!Properties.Contains(property.Name.ToString()) || property.HasElements) Unsupported(property.Name.ToString());
                    var value = property.Value.Trim(); Expressions(value);
                    properties[property.Name.ToString()] = value;
                }
            }
            else if (group.Name == "ItemGroup") items.AddRange(group.Elements());
            else Unsupported(group.Name.ToString());
        }
        string Get(string name, string fallback) => properties.GetValueOrDefault(name, fallback);
        bool Flag(string name, bool fallback) => Get(name, fallback.ToString()).ToLowerInvariant() switch
        {
            "true" or "enable" => true, "false" or "disable" => false,
            _ => throw new ArgumentException($"Invalid {name} value."),
        };
        if (Get("TargetFramework", "net11.0") != "net11.0") Unsupported("TargetFramework values other than net11.0");
        var output = Get("OutputType", "Library").ToLowerInvariant() switch
        {
            "exe" => OutputKind.ConsoleApplication, "library" => OutputKind.DynamicallyLinkedLibrary,
            _ => throw new ArgumentException("Only Exe and Library OutputType values are supported."),
        };
        if (!LanguageVersionFacts.TryParse(Get("LangVersion", "preview"), out var language)) throw new ArgumentException("Invalid LangVersion.");
        var nullable = Get("Nullable", "disable") switch
        {
            "enable" => NullableContextOptions.Enable, "disable" => NullableContextOptions.Disable,
            "warnings" => NullableContextOptions.Warnings, "annotations" => NullableContextOptions.Annotations,
            _ => throw new ArgumentException("Invalid Nullable value."),
        };
        var directory = Path.GetDirectoryName(path)!;
        var available = FindSources(directory).ToArray();
        var sources = new HashSet<string>(Flag("EnableDefaultCompileItems", true) ? available : [], StringComparer.Ordinal);
        var usings = new HashSet<string>(Flag("ImplicitUsings", false) ? SdkUsings : [], StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (item.Name == "Using")
            {
                Attributes(item, "Include");
                if (item.HasElements || item.Attribute("Include") is null) Unsupported("Using without a simple Include");
                usings.Add(item.Attribute("Include")!.Value); continue;
            }
            if (item.Name != "Compile") Unsupported(item.Name.ToString());
            Attributes(item, "Include", "Remove", "Exclude");
            var include = (string?)item.Attribute("Include"); var remove = (string?)item.Attribute("Remove");
            if (item.HasElements || (include is null) == (remove is null) || (remove is not null && item.Attribute("Exclude") is not null)) Unsupported("Compile item metadata or combined operations");
            var patterns = Patterns(include ?? remove!);
            var excludes = Patterns((string?)item.Attribute("Exclude") ?? "");
            if (remove is not null) sources.RemoveWhere(source => Matches(patterns, Path.GetRelativePath(directory, source)));
            else
            {
                foreach (var pattern in patterns.Where(p => !p.Contains('*') && !p.Contains('?')))
                {
                    var explicitFile = files.ResolvePath(Path.Combine(directory, pattern));
                    if (!File.Exists(explicitFile)) throw new FileNotFoundException($"Compile input does not exist: {pattern}");
                    if (!Matches(excludes, pattern) && !sources.Add(explicitFile)) throw new ArgumentException($"Duplicate Compile input: {pattern}. Disable default compile items or remove the explicit Include.");
                }
                var globs = patterns.Where(p => p.Contains('*') || p.Contains('?')).ToArray();
                foreach (var source in available)
                {
                    var relative = Path.GetRelativePath(directory, source);
                    if (Matches(globs, relative) && !Matches(excludes, relative) && !sources.Add(source)) throw new ArgumentException($"Duplicate Compile input: {relative}.");
                }
            }
        }
        var defines = Get("DefineConstants", "").Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (defines.Any(d => !SyntaxFacts.IsValidIdentifier(d))) throw new ArgumentException("DefineConstants must contain C# identifiers separated by semicolons.");
        var name = Get("AssemblyName", Path.GetFileNameWithoutExtension(path));
        if (string.IsNullOrWhiteSpace(name) || name.Any(c => !(char.IsLetterOrDigit(c) || c is '.' or '_' or '-')) || name is "." or "..") throw new ArgumentException("AssemblyName must be a simple file name using letters, numbers, dots, underscores or hyphens.");
        return new(name, directory, sources.Order(StringComparer.Ordinal).ToArray(), usings.Order(StringComparer.Ordinal).ToArray(), output,
            language, nullable, Flag("AllowUnsafeBlocks", false), Flag("Optimize", true), Flag("TreatWarningsAsErrors", false), defines);
    }
    private IEnumerable<string> FindSources(string directory)
    {
        foreach (var entry in files.ReadDirectory(directory))
        {
            if (entry.IsDirectory)
            {
                if (entry.Name is "bin" or "obj" || entry.Name.StartsWith('.')) continue;
                foreach (var child in FindSources(Path.Combine(directory, entry.Name))) yield return child;
            }
            else if (entry.Name.EndsWith(".cs", StringComparison.Ordinal)) yield return Path.Combine(directory, entry.Name);
        }
    }
    private static string[] Patterns(string text)
    {
        var patterns = text.Replace('\\', '/').Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (patterns.Any(p => Path.IsPathRooted(p) || p.Split('/').Contains(".."))) throw new ArgumentException("Compile patterns must stay inside the project directory.");
        return patterns.Select(p => p.StartsWith("./", StringComparison.Ordinal) ? p[2..] : p).ToArray();
    }
    private static bool Matches(string[] patterns, string path) => patterns.Any(pattern => Regex.IsMatch(path.Replace('\\', '/'),
        "^" + Regex.Escape(pattern).Replace(@"\*\*/", "(?:.*/)?").Replace(@"\*\*", ".*").Replace(@"\*", "[^/]*").Replace(@"\?", "[^/]") + "$", RegexOptions.CultureInvariant));
    private static void Attributes(XElement element, params string[] supported)
    {
        foreach (var attribute in element.Attributes())
        {
            if (!supported.Contains(attribute.Name.ToString())) Unsupported($"{element.Name}/@{attribute.Name}");
            Expressions(attribute.Value);
        }
    }
    private static void Expressions(string value) { if (value.Contains("$(", StringComparison.Ordinal) || value.Contains("@(", StringComparison.Ordinal) || value.Contains("%(", StringComparison.Ordinal)) Unsupported("MSBuild expressions"); }
    private static void Unsupported(string feature) => throw new ArgumentException($"{feature} is not supported by browser builds. Use the .NET SDK outside the browser for this project feature.");
}
