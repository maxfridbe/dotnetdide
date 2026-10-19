using Microsoft.Extensions.FileProviders;

var root = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/site/wwwroot");
if (!File.Exists(Path.Combine(root, "index.html")))
    throw new DirectoryNotFoundException("Publish DotnetDide.Browser first, then pass its wwwroot folder.");
var builder = WebApplication.CreateBuilder();
builder.WebHost.UseUrls("http://127.0.0.1:" + (args.Length > 1 ? int.Parse(args[1]) : 5080));
var app = builder.Build();
var provider = new PhysicalFileProvider(root);
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = provider });
app.UseStaticFiles(new StaticFileOptions { FileProvider = provider, ServeUnknownFileTypes = true });
await app.RunAsync();
