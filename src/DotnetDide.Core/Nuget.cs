using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace DotnetDide;

public sealed record LoadedPackage(string Name, string Version);
public sealed record PackageLoadResult(string Name, string Version, IReadOnlyList<LoadedPackage> Packages);

/// <summary>Download managed NuGet.org packages into the current runspace. Await in a separate submission before using their types.</summary>
public static class Nuget
{
    internal static readonly AsyncLocal<PackageLoader?> Current = new();
    public static Task<PackageLoadResult> Load(string name, string? version = null) =>
        (Current.Value ?? throw new InvalidOperationException("Nuget.Load must run inside a DotnetDide submission.")).Load(name, version);
}

internal sealed class PackageLoader
{
    private const int MaxDownload = 16 * 1024 * 1024, MaxExpanded = 64 * 1024 * 1024, MaxPackages = 32;
    private static readonly NuGetFramework Target = NuGetFramework.ParseFolder("net11.0");
    private static readonly Regex PackageId = new("^[A-Za-z0-9][A-Za-z0-9_.-]{0,99}$", RegexOptions.CultureInvariant);
    private readonly HttpClient _http;
    private readonly Action<IReadOnlyList<(string Name, byte[] Bytes)>> _install;
    private readonly Dictionary<string, NuGetVersion> _loaded = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Version> _assemblies;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _loadedBytes;
    internal bool Enabled { get; set; }

    internal PackageLoader(HttpClient http, IEnumerable<MetadataReference> references,
        Action<IReadOnlyList<(string Name, byte[] Bytes)>> install)
    {
        _http = http; _install = install; _assemblies = new(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in references.OfType<PortableExecutableReference>())
        {
            if (reference.GetMetadata() is not AssemblyMetadata assembly) continue;
            var reader = assembly.GetModules()[0].GetMetadataReader(); var definition = reader.GetAssemblyDefinition();
            _assemblies[reader.GetString(definition.Name)] = definition.Version;
        }
        // Published defaults are already supplied to Roslyn and do not need downloading again.
        foreach (var (name, version) in new[] { ("StbImageSharp", "2.30.16"), ("StbImageWriteSharp", "1.16.7"),
            ("NAudio.Core", "3.3.0"), ("Concentus", "2.2.2"), ("Concentus.Oggfile", "1.0.7"),
            ("NuGet.Frameworks", "7.9.0"), ("NuGet.Versioning", "7.9.0"), ("System.Numerics.Tensors", "9.0.0") })
            if (_assemblies.ContainsKey(name)) _loaded[name] = NuGetVersion.Parse(version);
    }

    internal async Task<PackageLoadResult> Load(string name, string? version)
    {
        if (!Enabled) throw new UnauthorizedAccessException("NuGet package access is disabled by the host.");
        if (!PackageId.IsMatch(name ?? "")) throw new ArgumentException("Invalid NuGet package name.");
        NuGetVersion? requested = null;
        if (version is not null && !NuGetVersion.TryParse(version, out requested)) throw new ArgumentException("Use an exact NuGet version, not a range.");
        await _gate.WaitAsync();
        try
        {
            if (!Enabled) throw new UnauthorizedAccessException("NuGet package access is disabled by the host.");
            var planned = new Dictionary<string, NuGetVersion>(_loaded, StringComparer.OrdinalIgnoreCase);
            var additions = new List<(string Name, byte[] Bytes)>();
            var identities = new Dictionary<string, Version>(_assemblies, StringComparer.OrdinalIgnoreCase);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var downloaded = 0; long expanded = 0;
            var root = await Resolve(name!, requested is null ? null : new VersionRange(requested, true, requested, true), root: true);
            // All archives/dependencies are validated before changing compiler references.
            _install(additions);
            foreach (var pair in planned) _loaded[pair.Key] = pair.Value;
            foreach (var pair in identities) _assemblies[pair.Key] = pair.Value;
            _loadedBytes += additions.Sum(a => a.Bytes.Length);
            return new(name!, root.ToNormalizedString(), visited.Order(StringComparer.OrdinalIgnoreCase)
                .Select(id => new LoadedPackage(id, planned[id].ToNormalizedString())).ToArray());

            async Task<NuGetVersion> Resolve(string id, VersionRange? range, bool root = false)
            {
                if (!PackageId.IsMatch(id)) throw new InvalidDataException("Invalid dependency package name.");
                visited.Add(id);
                if (planned.TryGetValue(id, out var existing))
                {
                    if (range is not null && !range.Satisfies(existing)) throw new InvalidOperationException($"{id} {existing} is already loaded/selected and conflicts with {range}. Restart the worker to change versions.");
                    return existing;
                }
                if (planned.Count >= MaxPackages) throw new InvalidOperationException("NuGet package limit reached (32, including defaults).");
                var lower = id.ToLowerInvariant();
                NuGetVersion selected;
                if (range?.MinVersion is not null && range.MinVersion == range.MaxVersion && range.IsMinInclusive && range.IsMaxInclusive) selected = range.MinVersion;
                else
                {
                    var index = await Fetch(lower + "/index.json", 1024 * 1024);
                    using var json = JsonDocument.Parse(index);
                    var versions = json.RootElement.GetProperty("versions").EnumerateArray().Select(v => NuGetVersion.Parse(v.GetString()!))
                        .Where(v => (!v.IsPrerelease || range?.MinVersion?.IsPrerelease == true) && (range is null || range.Satisfies(v))).Order().ToArray();
                    if (versions.Length == 0) throw new InvalidOperationException($"No compatible stable version of {id} satisfies {range}.");
                    selected = root ? versions[^1] : versions[0];
                }
                planned.Add(id, selected); // Detect cycles and version conflicts before recursion.
                var normalized = selected.ToNormalizedString().ToLowerInvariant();
                var bytes = await Fetch($"{lower}/{normalized}/{lower}.{normalized}.nupkg", MaxDownload);
                using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
                if (zip.Entries.Count > 2048) throw new InvalidDataException("Package has too many archive entries.");
                foreach (var entry in zip.Entries)
                {
                    expanded += entry.Length;
                    if (entry.Length > MaxDownload || expanded > MaxExpanded) throw new InvalidDataException("Expanded package size limit exceeded.");
                    var path = entry.FullName.Replace('\\', '/');
                    if (path.StartsWith('/') || path.Split('/').Any(p => p is ".." or ".")) throw new InvalidDataException("Unsafe package path.");
                    if (!path.EndsWith('/') && new[] { "runtimes/", "native/", "build/", "buildTransitive/", "buildMultiTargeting/", "analyzers/" }
                        .Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                        throw new NotSupportedException("Packages with native/runtime-specific assets, build targets or analyzers are not supported.");
                }
                var specs = zip.Entries.Where(e => !e.FullName.Contains('/') && e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (specs.Length != 1) throw new InvalidDataException("Expected one package manifest.");
                using var manifest = specs[0].Open();
                using var xml = XmlReader.Create(manifest, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, MaxCharactersInDocument = 1024 * 1024 });
                var metadata = XDocument.Load(xml).Root?.Elements().Single(e => e.Name.LocalName == "metadata") ?? throw new InvalidDataException("Missing package metadata.");
                string? Value(string key) => metadata.Elements().SingleOrDefault(e => e.Name.LocalName == key)?.Value;
                if (!string.Equals(Value("id"), id, StringComparison.OrdinalIgnoreCase) || NuGetVersion.Parse(Value("version") ?? "") != selected)
                    throw new InvalidDataException("Package identity does not match the requested package.");
                var dependencies = metadata.Elements().SingleOrDefault(e => e.Name.LocalName == "dependencies");
                if (dependencies is not null)
                {
                    var groups = dependencies.Elements().Where(e => e.Name.LocalName == "group").ToArray();
                    IEnumerable<XElement> chosen = dependencies.Elements().Where(e => e.Name.LocalName == "dependency");
                    if (groups.Length > 0)
                    {
                        var pairs = groups.Select(g => (Group: g, Framework: string.IsNullOrEmpty((string?)g.Attribute("targetFramework")) ? NuGetFramework.AnyFramework : NuGetFramework.Parse((string)g.Attribute("targetFramework")!))).ToArray();
                        var nearest = new FrameworkReducer().GetNearest(Target, pairs.Select(p => p.Framework));
                        if (nearest is null) throw new NotSupportedException("No compatible dependency group for net11.0.");
                        chosen = pairs.First(p => p.Framework.Equals(nearest)).Group.Elements().Where(e => e.Name.LocalName == "dependency");
                    }
                    foreach (var dependency in chosen)
                        await Resolve((string?)dependency.Attribute("id") ?? "", VersionRange.Parse((string?)dependency.Attribute("version") ?? "(,)"));
                }
                var libraries = zip.Entries.Where(e => e.FullName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase) && e.FullName.Split('/').Length == 3).ToArray();
                var frameworks = libraries.Select(e => NuGetFramework.ParseFolder(e.FullName.Split('/')[1])).Distinct().ToArray();
                if (frameworks.Length > 0)
                {
                    var nearest = new FrameworkReducer().GetNearest(Target, frameworks) ?? throw new NotSupportedException("No compatible managed library for net11.0.");
                    foreach (var entry in libraries.Where(e => NuGetFramework.ParseFolder(e.FullName.Split('/')[1]).Equals(nearest) && e.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
                    {
                        using var input = entry.Open(); using var output = new MemoryStream(); await input.CopyToAsync(output);
                        var image = output.ToArray();
                        using var pe = new PEReader(new MemoryStream(image));
                        if (!pe.HasMetadata || pe.PEHeaders.CorHeader is null || (pe.PEHeaders.CorHeader.Flags & CorFlags.ILOnly) == 0) throw new NotSupportedException("Only managed IL assemblies can be loaded.");
                        var reader = pe.GetMetadataReader(); var assembly = reader.GetAssemblyDefinition(); var assemblyName = reader.GetString(assembly.Name);
                        if (identities.TryGetValue(assemblyName, out var loadedVersion))
                        {
                            if (loadedVersion != assembly.Version) throw new InvalidOperationException($"Assembly {assemblyName} is already loaded at {loadedVersion}; requested {assembly.Version}.");
                            // Never replace an existing framework or package assembly.
                            continue;
                        }
                        identities.Add(assemblyName, assembly.Version); additions.Add((assemblyName, image));
                        if (_loadedBytes + additions.Sum(a => a.Bytes.Length) > MaxExpanded) throw new InvalidDataException("Loaded assembly size limit exceeded.");
                    }
                }
                else if (dependencies is null) throw new NotSupportedException("Package has no compatible managed library or dependencies.");
                return selected;
            }
            async Task<byte[]> Fetch(string relative, int limit)
            {
                using var response = await _http.GetAsync("https://api.nuget.org/v3-flatcontainer/" + relative, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("NuGet download size limit exceeded.");
                using var stream = await response.Content.ReadAsStreamAsync(); using var output = new MemoryStream();
                var buffer = new byte[16384]; int read;
                while ((read = await stream.ReadAsync(buffer)) != 0)
                {
                    downloaded += read;
                    if (output.Length + read > limit || downloaded > MaxExpanded) throw new InvalidDataException("NuGet download size limit exceeded.");
                    output.Write(buffer, 0, read);
                }
                return output.ToArray();
            }
        }
        finally { _gate.Release(); }
    }
}
