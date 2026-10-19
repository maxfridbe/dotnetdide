namespace DotnetDide;

/// <summary>A workspace over System.IO. In browser-wasm these are real in-memory runtime files.</summary>
public sealed class WorkspaceFileSystem
{
    public string RootPath { get; }
    public const int MaximumSnapshotBytes = 64 * 1024 * 1024;
    public const int MaximumSnapshotEntries = 4096;

    public WorkspaceFileSystem(string rootPath)
    {
        RootPath = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar);
        if (RootPath.Length == 0) throw new ArgumentException("The workspace cannot be the filesystem root.", nameof(rootPath));
        Directory.CreateDirectory(RootPath);
        RejectLink(RootPath);
    }

    private string Resolve(string path, bool allowRoot = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (path.Contains('\\') || path.Contains('\0')) throw new ArgumentException("Use forward-slash workspace paths.", nameof(path));
        var full = Path.GetFullPath(path, RootPath);
        if (full != RootPath && !full.StartsWith(RootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Path escapes the workspace.", nameof(path));
        if (!allowRoot && full == RootPath) throw new ArgumentException("Operation requires a child path.", nameof(path));
        RejectLink(RootPath);
        var current = RootPath;
        foreach (var part in Path.GetRelativePath(RootPath, full).Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, part);
            // GetAttributes also detects dangling symbolic links on supported platforms.
            if (Path.Exists(current) || new FileInfo(current).LinkTarget is not null) RejectLink(current);
        }
        return full;
    }

    private static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Symbolic links are not supported in workspace operations.");
    }

    public void WriteFile(string path, byte[] data) => File.WriteAllBytes(Resolve(path), data);
    public byte[] ReadFile(string path) => File.ReadAllBytes(Resolve(path));
    public void Mkdir(string path) => Directory.CreateDirectory(Resolve(path));
    public FileEntry Stat(string path)
    {
        var full = Resolve(path, true);
        var attributes = File.GetAttributes(full);
        var directory = (attributes & FileAttributes.Directory) != 0;
        return new(Path.GetFileName(full), directory, directory ? 0 : new FileInfo(full).Length);
    }
    public IReadOnlyList<FileEntry> ReadDirectory(string path = ".") =>
        Directory.EnumerateFileSystemEntries(Resolve(path, true)).Select(Stat).OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
    public void Delete(string path, bool recursive = false)
    {
        var full = Resolve(path);
        if (Directory.Exists(full)) Directory.Delete(full, recursive);
        else { if (!File.Exists(full)) throw new FileNotFoundException("File does not exist.", path); File.Delete(full); }
    }
    public void Rename(string path, string destination)
    {
        var from = Resolve(path); var to = Resolve(destination);
        if (Directory.Exists(from)) Directory.Move(from, to); else File.Move(from, to);
    }

    public WorkspaceSnapshot Export()
    {
        var directories = new List<string>(); var files = new List<WorkspaceFile>(); long bytes = 0;
        void Visit(string parent)
        {
            foreach (var entry in ReadDirectory(parent))
            {
                var path = parent == "." ? entry.Name : parent + "/" + entry.Name;
                if (directories.Count + files.Count >= MaximumSnapshotEntries) throw new IOException("Snapshot entry limit exceeded.");
                if (entry.IsDirectory) { directories.Add(path); Visit(path); }
                else
                {
                    bytes += entry.Size;
                    if (bytes > MaximumSnapshotBytes) throw new IOException("Snapshot byte limit exceeded.");
                    files.Add(new(path, Convert.ToBase64String(ReadFile(path))));
                }
            }
        }
        Visit("."); return new(1, directories, files);
    }

    public void Import(WorkspaceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Version != 1 || snapshot.Directories is null || snapshot.Files is null)
            throw new ArgumentException("Unsupported workspace snapshot.", nameof(snapshot));
        if ((long)snapshot.Directories.Count + snapshot.Files.Count > MaximumSnapshotEntries)
            throw new ArgumentException("Snapshot entry limit exceeded.", nameof(snapshot));
        var paths = new HashSet<string>(StringComparer.Ordinal);
        void ValidatePath(string path)
        {
            if (string.IsNullOrEmpty(path) || Path.IsPathRooted(path) || path.Split('/').Any(p => p is "" or "." or "..") || path.Contains('\\') || path.Contains('\0'))
                throw new ArgumentException("Snapshot paths must be canonical relative paths.", nameof(snapshot));
            if (!paths.Add(path)) throw new ArgumentException("Duplicate snapshot path.", nameof(snapshot));
        }
        foreach (var path in snapshot.Directories) ValidatePath(path);
        var decoded = new List<(string Path, byte[] Data)>(); long bytes = 0;
        foreach (var file in snapshot.Files)
        {
            if (file is null || file.Data is null) throw new ArgumentException("Invalid snapshot file.", nameof(snapshot));
            ValidatePath(file.Path);
            if (file.Data.Length > MaximumSnapshotBytes * 4L / 3 + 4) throw new ArgumentException("Snapshot byte limit exceeded.", nameof(snapshot));
            byte[] data;
            try { data = Convert.FromBase64String(file.Data); }
            catch (FormatException error) { throw new ArgumentException("Invalid base64 file contents.", nameof(snapshot), error); }
            bytes += data.Length;
            if (bytes > MaximumSnapshotBytes) throw new ArgumentException("Snapshot byte limit exceeded.", nameof(snapshot));
            decoded.Add((file.Path, data));
        }
        var filePaths = decoded.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            for (var parent = Path.GetDirectoryName(path); !string.IsNullOrEmpty(parent); parent = Path.GetDirectoryName(parent))
                if (filePaths.Contains(parent)) throw new ArgumentException("A file cannot contain another entry.", nameof(snapshot));
        }
        // Build off to the side; validation and write failures never partially replace the current workspace.
        var stage = RootPath + ".import-" + Guid.NewGuid().ToString("N");
        var backup = RootPath + ".backup-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(stage);
        try
        {
            foreach (var path in snapshot.Directories) Directory.CreateDirectory(Path.Combine(stage, path));
            foreach (var (path, data) in decoded)
            {
                var full = Path.Combine(stage, path); Directory.CreateDirectory(Path.GetDirectoryName(full)!); File.WriteAllBytes(full, data);
            }
            RejectLink(RootPath); Directory.Move(RootPath, backup);
            try { Directory.Move(stage, RootPath); }
            catch { Directory.Move(backup, RootPath); throw; }
            Directory.Delete(backup, true);
        }
        finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
    }
}
