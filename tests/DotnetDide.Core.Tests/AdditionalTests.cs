using Microsoft.CodeAnalysis;
namespace DotnetDide.Tests;

public sealed class AdditionalTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotnetdide-more-" + Guid.NewGuid());
    private static readonly MetadataReference[] References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Select(path => MetadataReference.CreateFromFile(path)).ToArray();
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    [Fact]
    public async Task ManySubmissionsAndDeclaredTypesContinueWorking()
    {
        using var r = new Runspace(new WorkspaceFileSystem(_root), References);
        Assert.True((await r.RunAsync("record Item(int Value); var item=new Item(2); int count=0;")).Success);
        for (var i = 0; i < 24; i++) Assert.True((await r.RunAsync("count += item.Value;")).Success);
        Assert.Equal("48", (await r.RunAsync("count")).Value);
    }
    [Fact]
    public async Task OutputIsBoundedAndStatementHasNoValue()
    {
        using var r = new Runspace(new WorkspaceFileSystem(_root), References);
        var result = await r.RunAsync("Console.Out.WriteLine(new string('x',1_100_000).AsSpan());");
        Assert.True(result.Success); Assert.InRange(result.Stdout.Length, 1_000_000, 1_000_100);
        Assert.Contains("[output truncated]", result.Stdout); Assert.Null(result.Value);
    }
    [Fact]
    public void ConflictingSnapshotPathsAndVersionsDoNotOverwriteFiles()
    {
        var fs = new WorkspaceFileSystem(_root); fs.WriteFile("keep", [42]);
        Assert.Throws<ArgumentException>(() => fs.Import(new(2, [], [])));
        Assert.Throws<ArgumentException>(() => fs.Import(new(1, ["a/b"], [new("a", "AA==")])));
        Assert.Equal(new byte[] { 42 }, fs.ReadFile("keep"));
    }
    [Fact]
    public async Task DisposedRunspaceCannotBeReused()
    {
        var r = new Runspace(new WorkspaceFileSystem(_root), References); r.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => r.RunAsync("1+1")); Assert.Throws<ObjectDisposedException>(r.Reset);
    }
}
