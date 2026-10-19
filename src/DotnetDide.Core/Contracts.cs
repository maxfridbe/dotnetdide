namespace DotnetDide;

public sealed record CodeDiagnostic(string Id, string Message, string Severity, int Line, int Column, string? FilePath = null);
public sealed record RunError(string Type, string Message);
public sealed record RunResult(bool Success, string Stdout, string Stderr, string? Value,
    IReadOnlyList<CodeDiagnostic> Diagnostics, RunError? Error);
public sealed record WorkspaceFile(string Path, string Data);
public sealed record WorkspaceSnapshot(int Version, IReadOnlyList<string> Directories, IReadOnlyList<WorkspaceFile> Files);
public sealed record FileEntry(string Name, bool IsDirectory, long Size);
