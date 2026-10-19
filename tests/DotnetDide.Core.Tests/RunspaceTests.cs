using Microsoft.CodeAnalysis;
namespace DotnetDide.Tests;

public sealed class RunspaceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotnetdide-run-" + Guid.NewGuid());
    private static readonly MetadataReference[] References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Select(path => MetadataReference.CreateFromFile(path)).ToArray();
    private Runspace Create(string name = "one") => new(new WorkspaceFileSystem(Path.Combine(_root, name)), References);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    private static async Task<RunResult> Run(Runspace runspace, string code)
    { var result = await runspace.RunAsync(code); Assert.True(result.Success, string.Join("\\n", result.Diagnostics.Select(d => d.Message)) + result.Error); return result; }
    [Fact]
    public async Task VariablesFunctionsAndImportsPersist()
    {
        using var r = Create(); await Run(r, "using System.Linq; int answer = 40; int Add(int x) => x + answer;");
        Assert.Equal("42", (await Run(r, "Add(new[]{1,1}.Sum())")).Value);
        Assert.Equal("41", (await Run(r, "++answer")).Value);
    }
    [Fact]
    public async Task AwaitAndOutputAreCapturedSeparately()
    {
        using var r = Create(); var result = await Run(r, "await Task.Delay(1); Console.Write(\"hello\"); Console.Error.Write(\"warning\"); 42");
        Assert.Equal("hello", result.Stdout); Assert.Equal("warning", result.Stderr); Assert.Equal("42", result.Value);
    }
    [Fact]
    public async Task CompileErrorHasLocationAndDoesNotDestroyState()
    {
        using var r = Create(); await Run(r, "int answer = 42;"); var bad = await r.RunAsync("unknownName + 1");
        Assert.False(bad.Success); Assert.Contains(bad.Diagnostics, d => d.Id == "CS0103" && d.Line == 1 && d.Column == 1);
        Assert.Equal("42", (await Run(r, "answer")).Value);
    }
    [Fact]
    public async Task RuntimeExceptionRetainsEarlierStateAndFileSideEffects()
    {
        using var r = Create(); await Run(r, "int answer = 1;");
        var result = await r.RunAsync("answer=42; File.WriteAllText(\"made.txt\",\"saved\"); Console.Write(\"before\"); throw new Exception(\"boom\");");
        Assert.False(result.Success); Assert.Equal("boom", result.Error?.Message); Assert.Equal("before", result.Stdout);
        Assert.Equal("42", (await Run(r, "answer")).Value); Assert.Equal("saved", System.Text.Encoding.UTF8.GetString(r.Files.ReadFile("made.txt")));
    }
    [Fact]
    public async Task FileApiAndProgramsShareTheSameWorkspace()
    {
        using var r = Create(); r.Files.WriteFile("input.bin", [0, 255]);
        await Run(r, "Directory.CreateDirectory(\"outputs\"); File.WriteAllBytes(\"outputs/copy.bin\",File.ReadAllBytes(\"input.bin\"));");
        Assert.Equal(new byte[] { 0, 255 }, r.Files.ReadFile("outputs/copy.bin"));
    }
    [Fact]
    public async Task RunspacesAndResetIsolateVariables()
    {
        using var a = Create(); using var b = Create("two"); await Run(a, "int secret=42; File.WriteAllText(\"keep\",\"yes\");");
        Assert.False((await b.RunAsync("secret")).Success); Assert.Empty(b.Files.ReadDirectory());
        a.Reset(); Assert.False((await a.RunAsync("secret")).Success); Assert.Equal("yes", System.Text.Encoding.UTF8.GetString(a.Files.ReadFile("keep")));
    }
    [Fact]
    public async Task ConcurrentCallsAreSerializedAndConsoleRestored()
    {
        using var r = Create(); var oldOut = Console.Out; var cwd = Directory.GetCurrentDirectory(); await Run(r, "int count=0;");
        var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => r.RunAsync("await Task.Delay(5); Console.Write(++count);")));
        Assert.All(results, x => Assert.True(x.Success)); Assert.Equal(["1", "2", "3"], results.Select(x => x.Stdout));
        Assert.Same(oldOut, Console.Out); Assert.Equal(cwd, Directory.GetCurrentDirectory());
    }
    [Fact]
    public async Task CancellationBeforeExecutionDoesNotRunCode()
    {
        using var r = Create(); using var c = new CancellationTokenSource(); c.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => r.RunAsync("File.WriteAllText(\"bad\",\"bad\");", c.Token));
        Assert.Empty(r.Files.ReadDirectory());
    }
}
