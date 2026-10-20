using System.Text;
using Microsoft.CodeAnalysis;
namespace DotnetDide.Tests;

public sealed class MediaTests
{
    [Theory]
    [InlineData("images", false)]
    [InlineData("images", true)]
    [InlineData("audio", false)]
    [InlineData("audio", true)]
    public async Task BundledMediaExamplesWorkInScriptsAndCompiledPrograms(string name, bool compiled)
    {
        var root = Path.Combine(Path.GetTempPath(), "dotnetdide-media-" + Guid.NewGuid());
        try
        {
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path));
            using var runspace = new Runspace(new WorkspaceFileSystem(root), references);
            var code = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "media-examples", name + ".cs"));
            if (compiled)
            {
                runspace.Files.WriteFile("Program.cs", Encoding.UTF8.GetBytes(code));
                code = "var result = await Dotnet.Run(\"Program.cs\"); if (!result.Success) throw new Exception(result.Build.Output + result.Error);";
            }
            var result = await runspace.RunAsync(code);
            Assert.True(result.Success, string.Join("\n", result.Diagnostics.Select(d => d.Message)) + result.Error);
            foreach (var file in name == "images" ? new[] { "gradient.png", "gradient.jpg" } : ["tone.wav", "tone.opus"])
                Assert.True(runspace.Files.ReadFile("media/" + file).Length > 20);
            Assert.Contains("Created media/", result.Stdout);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
