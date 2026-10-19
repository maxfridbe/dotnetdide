using System.Text;
namespace DotnetDide.Tests;

public sealed class WorkspaceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotnetdide-test-" + Guid.NewGuid());
    private WorkspaceFileSystem Create() => new(_root);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    [Fact]
    public void TextAndBinaryRoundTripThroughSystemIO()
    {
        var fs = Create(); fs.Mkdir("inputs");
        fs.WriteFile("inputs/hello.txt", Encoding.UTF8.GetBytes("héllo 🌍"));
        Assert.Equal("héllo 🌍", File.ReadAllText(Path.Combine(_root, "inputs/hello.txt")));
        File.WriteAllBytes(Path.Combine(_root, "raw.bin"), [0, 255, 128]);
        Assert.Equal(new byte[] { 0, 255, 128 }, fs.ReadFile("raw.bin"));
        Assert.Equal(3, fs.Stat("raw.bin").Size);
        Assert.Equal(["inputs", "raw.bin"], fs.ReadDirectory().Select(e => e.Name));
    }
    [Fact]
    public void RenameAndDeletePreserveBytes()
    {
        var fs = Create(); fs.Mkdir("data/empty"); fs.WriteFile("data/a", [1, 2]);
        fs.Rename("data/a", "data/b"); Assert.Equal(new byte[] { 1, 2 }, fs.ReadFile("data/b"));
        Assert.Throws<IOException>(() => fs.Delete("data"));
        fs.Delete("data", true); Assert.Empty(fs.ReadDirectory());
    }
    [Theory]
    [InlineData("../escape")]
    [InlineData("/etc/passwd")]
    [InlineData("a/../../escape")]
    public void PathsCannotEscapeWorkspace(string path)
    { var fs = Create(); Assert.Throws<ArgumentException>(() => fs.WriteFile(path, [1])); }
    [Fact]
    public void SnapshotRestoresFilesAndEmptyDirectories()
    {
        var fs = Create(); fs.Mkdir("data/empty"); fs.WriteFile("data/a.bin", [0, 255]);
        var snapshot = fs.Export(); fs.Delete("data", true); fs.WriteFile("obsolete", [2]); fs.Import(snapshot);
        Assert.Equal(new byte[] { 0, 255 }, fs.ReadFile("data/a.bin"));
        Assert.True(fs.Stat("data/empty").IsDirectory); Assert.False(File.Exists(Path.Combine(_root, "obsolete")));
    }
    [Theory]
    [InlineData("../outside", "AQ==")]
    [InlineData("bad", "not base64")]
    public void InvalidSnapshotDoesNotReplaceExistingFiles(string path, string data)
    {
        var fs = Create(); fs.WriteFile("keep", [42]);
        Assert.ThrowsAny<ArgumentException>(() => fs.Import(new(1, [], [new(path, data)])));
        Assert.Equal(new byte[] { 42 }, fs.ReadFile("keep"));
    }
    [Fact]
    public void DuplicateSnapshotPathsAreRejectedBeforeMutation()
    {
        var fs = Create(); fs.WriteFile("keep", [42]);
        Assert.Throws<ArgumentException>(() => fs.Import(new(1, [], [new("a", "AQ=="), new("a", "Ag==")])));
        Assert.Equal(new byte[] { 42 }, fs.ReadFile("keep"));
    }
    [Fact]
    public void SymlinksCannotBeReadOrExported()
    {
        var fs = Create(); File.CreateSymbolicLink(Path.Combine(_root, "link"), "/etc/passwd");
        Assert.Throws<IOException>(() => fs.ReadFile("link"));
        Assert.Throws<IOException>(() => fs.Export());
    }
}
