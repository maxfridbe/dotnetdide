using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using Microsoft.CodeAnalysis;

[assembly: System.Runtime.Versioning.SupportedOSPlatform("browser")]
namespace DotnetDide.Browser;

public static partial class RuntimeBridge
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static Runspace? _runspace;
    private static string _debugInfo = "{}";
    [JSExport] public static void SetDebugInfo(string value) => _debugInfo = value;
    private static Runspace Current => _runspace ?? throw new InvalidOperationException("Initialize the runtime first.");

    [JSExport]
    public static async Task<string> Initialize(string[] assemblyUrls, bool nugetAccess, bool siteAccess)
    {
        if (_runspace is not null) throw new InvalidOperationException("Runtime already initialized.");
        using var http = new HttpClient();
        var references = new List<MetadataReference>();
        // Like WasmSharp, Roslyn reads the real runtime assemblies instead of relying on Assembly.Location in WASM.
        foreach (var url in assemblyUrls)
        {
            var bytes = await http.GetByteArrayAsync(url);
            references.Add(MetadataReference.CreateFromImage(bytes));
        }
        _runspace = new(new WorkspaceFileSystem("/home/dotnetdide"), references, nugetAccess, siteAccess: siteAccess, getDebugInfo: () => _debugInfo);
        return Environment.Version.ToString();
    }

    [JSExport] public static async Task<string> Run(string code) => JsonSerializer.Serialize(await Current.RunAsync(code), Json);
    [JSExport] public static string Assemblies() => JsonSerializer.Serialize(AppDomain.CurrentDomain.GetAssemblies()
        .OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal)
        .Select(assembly => new { Name = assembly.GetName().Name ?? "", Version = assembly.GetName().Version?.ToString() ?? "",
            FullName = assembly.FullName ?? "", IsDynamic = assembly.IsDynamic }), Json);
    [JSExport] public static void SetSiteAccess(bool enabled) => Current.SetSiteAccess(enabled);
    [JSExport] public static string Sites() => JsonSerializer.Serialize(Current.WireSites, Json);
    [JSExport] public static async Task<string> Request(string request) => JsonSerializer.Serialize(await Current.RequestAsync(JsonSerializer.Deserialize<WireRequest>(request, Json) ?? throw new ArgumentException("Missing request.")), Json);
    private static WireStream? _stream;
    [JSExport]
    public static async Task<string> BeginStream(string request, int maximumBytes)
    {
        if (_stream is not null) throw new InvalidOperationException("A site stream is already active.");
        _stream = Current.RequestStream(JsonSerializer.Deserialize<WireRequest>(request, Json) ?? throw new ArgumentException("Missing request."), maximumBytes);
        return JsonSerializer.Serialize(await _stream.Headers, Json);
    }
    private static byte[] _chunk = [];
    [JSExport]
    public static async Task<int> ReadStream()
    { _chunk = await (_stream ?? throw new InvalidOperationException("No site stream.")).ReadAsync(); return _chunk.Length; }
    [JSExport]
    [return: JSMarshalAs<JSType.Array<JSType.Number>>]
    public static byte[] TakeStreamChunk() { var chunk = _chunk; _chunk = []; return chunk; }
    [JSExport] public static void CancelStream() => _stream?.Cancel();
    [JSExport]
    public static async Task<string> FinishStream()
    {
        var stream = _stream ?? throw new InvalidOperationException("No site stream.");
        try { return JsonSerializer.Serialize(await stream.Completion, Json); }
        finally { stream.Dispose(); _stream = null; }
    }
    [JSExport] public static void SetNugetAccess(bool enabled) => Current.SetNugetAccess(enabled);
    [JSExport] public static void Reset() => Current.Reset();

    [JSExport]
    public static string Files(string request)
    {
        var command = JsonSerializer.Deserialize<FileCommand>(request, Json) ?? throw new ArgumentException("Missing file command.");
        var fs = Current.Files;
        object? result = null;
        switch (command.Op)
        {
            case "cwd": result = fs.RootPath; break;
            case "read": result = Convert.ToBase64String(fs.ReadFile(command.Path)); break;
            case "write": fs.WriteFile(command.Path, Convert.FromBase64String(command.Data ?? throw new ArgumentException("Missing file bytes."))); break;
            case "mkdir": fs.Mkdir(command.Path); break;
            case "list": result = fs.ReadDirectory(command.Path).Select(x => x.Name).ToArray(); break;
            case "stat": result = fs.Stat(command.Path); break;
            case "unlink":
                if (fs.Stat(command.Path).IsDirectory) throw new IOException("Use rmdir for directories.");
                fs.Delete(command.Path); break;
            case "rmdir":
                if (!fs.Stat(command.Path).IsDirectory) throw new IOException("Path is not a directory.");
                fs.Delete(command.Path, command.Recursive); break;
            case "rename": fs.Rename(command.Path, command.Destination ?? throw new ArgumentException("Missing destination.")); break;
            case "export": result = fs.Export(); break;
            case "import": fs.Import(command.Snapshot ?? throw new ArgumentException("Missing snapshot.")); break;
            default: throw new ArgumentException("Unknown filesystem operation.");
        }
        return JsonSerializer.Serialize(result, Json);
    }
    private sealed record FileCommand(string Op, string Path = ".", string? Data = null, string? Destination = null, bool Recursive = false, WorkspaceSnapshot? Snapshot = null);
}
public static class Program { public static void Main() { } }
