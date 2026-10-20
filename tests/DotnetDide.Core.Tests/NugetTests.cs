using System.IO.Compression;
using System.Net;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
namespace DotnetDide.Tests;

public sealed class NugetTests
{
    private sealed class Registry : HttpMessageHandler
    {
        public Dictionary<string, byte[]> Files { get; } = [];
        public int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            Assert.Equal("api.nuget.org", request.RequestUri!.Host);
            return Task.FromResult(Files.TryGetValue(request.RequestUri.AbsolutePath, out var bytes)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
        public void Add(string name, string body, string dependencies = "", string? unsupported = null)
        {
            var references = References();
            using var assembly = new MemoryStream();
            var compiled = CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(body)], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)).Emit(assembly);
            Assert.True(compiled.Success, string.Join("\n", compiled.Diagnostics));
            using var bytes = new MemoryStream();
            using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, true))
            {
                using (var writer = new StreamWriter(zip.CreateEntry(name + ".nuspec").Open()))
                    writer.Write($"<package><metadata><id>{name}</id><version>1.0.0</version>{dependencies}</metadata></package>");
                using (var stream = zip.CreateEntry("lib/net8.0/" + name + ".dll").Open()) stream.Write(assembly.ToArray());
                if (unsupported is not null) using (var writer = new StreamWriter(zip.CreateEntry(unsupported).Open())) writer.Write("unsupported");
            }
            Files[$"/v3-flatcontainer/{name.ToLowerInvariant()}/index.json"] = Encoding.UTF8.GetBytes("{\"versions\":[\"1.0.0\",\"2.0.0-preview\"]}");
            Files[$"/v3-flatcontainer/{name.ToLowerInvariant()}/1.0.0/{name.ToLowerInvariant()}.1.0.0.nupkg"] = bytes.ToArray();
        }
    }
    private static MetadataReference[] References() => ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)).ToArray();

    [Fact]
    public async Task DownloadsManagedDependenciesAndMakesThemAvailableToScriptsAndBuilds()
    {
        var root = Path.Combine(Path.GetTempPath(), "nuget-test-" + Guid.NewGuid());
        try
        {
            using var registry = new Registry();
            registry.Add("Leaf", "public static class Leaf { public static int Value => 42; }");
            registry.Add("Example", "public static class Example { public static int Value => 7; }", "<dependencies><group targetFramework=\"net8.0\"><dependency id=\"Leaf\" version=\"[1.0.0]\" /></group></dependencies>");
            using var http = new HttpClient(registry);
            using var r = new Runspace(new WorkspaceFileSystem(root), References(), nugetAccess: true, nugetClient: http);
            var load = await r.RunAsync("await DotnetDide.Nuget.Load(\"Example\")");
            Assert.True(load.Success, load.Error?.ToString()); Assert.Equal(3, registry.Requests);
            Assert.Equal("49", (await r.RunAsync("Leaf.Value + Example.Value")).Value);
            r.Files.WriteFile("Program.cs", Encoding.UTF8.GetBytes("System.Console.Write(Leaf.Value);"));
            var run = await r.RunAsync("var built = await Dotnet.Run(\"Program.cs\"); built.Success");
            Assert.Equal("True", run.Value); Assert.Contains("42", run.Stdout);
            Assert.True((await r.RunAsync("await DotnetDide.Nuget.Load(\"Example\", \"1.0.0\")")).Success);
            Assert.Equal(3, registry.Requests);
            Assert.False((await r.RunAsync("await DotnetDide.Nuget.Load(\"Example\", \"2.0.0\")")).Success);
            r.SetNugetAccess(false);
            Assert.False((await r.RunAsync("await DotnetDide.Nuget.Load(\"Example\")")).Success);
            Assert.Equal("42", (await r.RunAsync("Leaf.Value")).Value); // Revocation blocks loads, not already loaded code.
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("runtimes/linux-x64/native/libfoo.so")]
    [InlineData("build/example.targets")]
    [InlineData("../escape.dll")]
    public async Task RejectsUnsupportedArchivesBeforeInstallingAssemblies(string path)
    {
        var root = Path.Combine(Path.GetTempPath(), "nuget-test-" + Guid.NewGuid());
        try
        {
            using var registry = new Registry(); registry.Add("UnsafePackage", "public static class UnsafePackage { public static int Value => 42; }", unsupported: path);
            using var http = new HttpClient(registry);
            using var r = new Runspace(new WorkspaceFileSystem(root), References(), nugetAccess: true, nugetClient: http);
            Assert.False((await r.RunAsync("await DotnetDide.Nuget.Load(\"UnsafePackage\", \"1.0.0\")")).Success);
            Assert.False((await r.RunAsync("UnsafePackage.Value")).Success);
            Assert.Empty(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Fact]
    public async Task PermissionDefaultsOffAndInvalidInputNeverReachesNetwork()
    {
        var root = Path.Combine(Path.GetTempPath(), "nuget-test-" + Guid.NewGuid());
        try
        {
            using var registry = new Registry(); using var http = new HttpClient(registry);
            using var r = new Runspace(new WorkspaceFileSystem(root), References(), nugetClient: http);
            Assert.Contains("disabled", (await r.RunAsync("await DotnetDide.Nuget.Load(\"Example\")")).Error!.Message);
            r.SetNugetAccess(true);
            Assert.False((await r.RunAsync("await DotnetDide.Nuget.Load(\"../escape\")")).Success);
            Assert.False((await r.RunAsync("await DotnetDide.Nuget.Load(\"Example\", \"[1,2]\")")).Success);
            Assert.Equal(0, registry.Requests);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
