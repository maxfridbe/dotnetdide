using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using Microsoft.CodeAnalysis;

[assembly: System.Runtime.Versioning.SupportedOSPlatform("browser")]
namespace DotnetDide.Browser;

public static partial class RuntimeBridge
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static Runspace? _runspace;
    private static Runspace Current => _runspace ?? throw new InvalidOperationException("Initialize the runtime first.");

    [JSExport]
    public static async Task<string> Initialize(string[] assemblyUrls)
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
        _runspace = new(new WorkspaceFileSystem("/home/dotnetdide"), references);
        return Environment.Version.ToString();
    }

    [JSExport] public static async Task<string> Run(string code) => JsonSerializer.Serialize(await Current.RunAsync(code), Json);
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
