using Microsoft.Extensions.FileProviders;

var requestedRoot = args.Length > 0 ? args[0] : "artifacts/site/wwwroot";
var root = Path.GetFullPath(requestedRoot);
// dotnet run starts in the project directory; also resolve repository-relative paths.
if (!Path.IsPathRooted(requestedRoot) && !File.Exists(Path.Combine(root, "index.html")))
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json"))) directory = directory.Parent;
    if (directory is not null) root = Path.GetFullPath(requestedRoot, directory.FullName);
}
if (!File.Exists(Path.Combine(root, "index.html")))
    throw new DirectoryNotFoundException("Publish DotnetDide.Browser first, then pass its wwwroot folder.");
var builder = WebApplication.CreateBuilder();
builder.WebHost.UseUrls("http://127.0.0.1:" + (args.Length > 1 ? int.Parse(args[1]) : 5080));
var app = builder.Build();
var provider = new PhysicalFileProvider(root);
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = provider });
app.UseStaticFiles(new StaticFileOptions { FileProvider = provider, ServeUnknownFileTypes = true });
await app.RunAsync();
