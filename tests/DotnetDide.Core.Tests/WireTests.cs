using Microsoft.CodeAnalysis;
namespace DotnetDide.Tests;

public sealed class WireTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotnetdide-wire-" + Guid.NewGuid());
    private static readonly MetadataReference[] References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Select(path => MetadataReference.CreateFromFile(path)).ToArray();
    private Runspace Create(bool enabled = true) => new(new WorkspaceFileSystem(_root), References, siteAccess: enabled);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    private static async Task Run(Runspace runspace, string code)
    {
        var result = await runspace.RunAsync(code);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics.Select(d => d.Message)) + result.Error);
    }
    [Fact]
    public async Task SiteOutputAndHostDebugSnapshotAreAvailable()
    {
        using var runspace = new Runspace(new WorkspaceFileSystem(_root), References, siteAccess: true,
            getDebugInfo: () => "{\"version\":1,\"console\":[\"iframe log\"]}");
        var snapshot = await runspace.RunAsync("Dotnet.GetDebugInfo()");
        Assert.True(snapshot.Success); Assert.Contains("iframe log", snapshot.Value);
        await Run(runspace, """
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            var app = DotnetDide.Wire.CreateApplication();
            app.Run(async context => {
                Console.WriteLine("handler output"); Console.Error.WriteLine("handler error");
                var logger = (Microsoft.Extensions.Logging.ILoggerFactory)app.ApplicationServices.GetService(typeof(Microsoft.Extensions.Logging.ILoggerFactory))!;
                Microsoft.Extensions.Logging.LoggerExtensions.LogWarning(logger.CreateLogger("demo"), "logger warning");
                await context.Response.WriteAsync("ok");
            });
            await DotnetDide.Wire.ListenAsync(8080, app);
            """);
        var response = await runspace.RequestAsync(new(8080, "GET", "/"));
        Assert.Equal(200, response.Status);
        Assert.Contains("handler output", response.Stdout); Assert.Contains("handler error", response.Stderr); Assert.Contains("logger warning", response.Stderr);
    }

    [Fact]
    public async Task AspNetMiddlewareRoutesAndSessionFilesWorkWithoutSockets()
    {
        using var runspace = Create();
        await Run(runspace, """
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            Directory.CreateDirectory("site"); File.WriteAllText("site/index.html", "<h1>Wire</h1>");
            var app = DotnetDide.Wire.CreateApplication();
            app.Use(async (context, next) => { context.Response.Headers["X-Wire"] = "middleware"; await next(context); });
            app.UseRouting();
            app.UseEndpoints(routes => {
                routes.MapGet("/api/hello", () => new { answer = 42 });
                routes.MapPost("/api/save", async context => {
                    using var reader = new StreamReader(context.Request.Body);
                    var text = await reader.ReadToEndAsync();
                    File.WriteAllText("saved.txt", text); context.Response.StatusCode = 201;
                    await context.Response.WriteAsync(text);
                });
            });
            var server = await DotnetDide.Wire.ListenAsync(8080, app, "site");
            """);
        Assert.Single(runspace.WireSites);
        var response = await runspace.RequestAsync(new(8080, "GET", "/api/hello"));
        Assert.Equal(200, response.Status); Assert.Contains("42", System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(response.Body)));
        Assert.Equal("middleware", response.Headers["X-Wire"].Single());
        var posted = await runspace.RequestAsync(new(8080, "POST", "/api/save", Body: Convert.ToBase64String("hello"u8.ToArray())));
        Assert.Equal(201, posted.Status); Assert.Equal("hello", File.ReadAllText(Path.Combine(_root, "saved.txt")));
        var page = await runspace.RequestAsync(new(8080, "GET", "/"));
        Assert.Equal(200, page.Status); Assert.Contains("Wire", System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(page.Body)));
        Assert.Equal(404, (await runspace.RequestAsync(new(8080, "GET", "/missing"))).Status);
        Assert.Empty((await runspace.RequestAsync(new(8080, "HEAD", "/"))).Body);
        await Run(runspace, "await server.StopAsync(default);"); Assert.Empty(runspace.WireSites);
        Assert.Equal(503, (await runspace.RequestAsync(new(8080, "GET", "/"))).Status);
    }
    [Fact]
    public async Task PermissionResetAndPathBoundsAreEnforced()
    {
        using var runspace = Create(false);
        var denied = await runspace.RunAsync("DotnetDide.Wire.CreateApplication()");
        Assert.False(denied.Success); Assert.Contains("disabled", denied.Error!.Message);
        runspace.SetSiteAccess(true);
        await Run(runspace, "Directory.CreateDirectory(\"site\"); File.WriteAllText(\"outside.txt\",\"private\"); var a=DotnetDide.Wire.CreateApplication(); var s=await DotnetDide.Wire.ListenAsync(8080,a,\"site\");");
        foreach (var path in new[] { "/../outside.txt", "/%2e%2e/outside.txt", "/..%2foutside.txt", "/a%5c..%5coutside.txt" })
            Assert.NotEqual(200, (await runspace.RequestAsync(new(8080, "GET", path))).Status);
        var duplicate = await runspace.RunAsync("await DotnetDide.Wire.ListenAsync(8080,DotnetDide.Wire.CreateApplication())");
        Assert.False(duplicate.Success); Assert.Single(runspace.WireSites);
        runspace.SetSiteAccess(false); Assert.Empty(runspace.WireSites);
        Assert.Equal(403, (await runspace.RequestAsync(new(8080, "GET", "/"))).Status);
        runspace.SetSiteAccess(true);
        await Run(runspace, "await DotnetDide.Wire.ListenAsync(8080,DotnetDide.Wire.CreateApplication());");
        runspace.Reset(); Assert.Empty(runspace.WireSites);
    }
    [Fact]
    public async Task CompiledProjectKeepsRoutesDirectoryAndResponseLifecycleAfterMainReturns()
    {
        using var runspace = Create();
        Directory.CreateDirectory(Path.Combine(_root, "app"));
        File.WriteAllText(Path.Combine(_root, "app", "Site.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework><OutputType>Exe</OutputType></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(_root, "app", "Program.cs"), """
            using System;
            using System.IO;
            using System.Threading.Tasks;
            using DotnetDide;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            Directory.CreateDirectory("site"); File.WriteAllText("site/index.html", "compiled site");
            var app = Wire.CreateApplication();
            app.Use(async (context, next) => {
                context.Response.OnStarting(async () => { await Task.Yield(); context.Response.Headers["X-Starting"] = "yes"; });
                context.Response.OnCompleted(() => { File.WriteAllText("completed.txt", "yes"); return Task.CompletedTask; });
                await next(context);
            });
            app.UseRouting(); app.UseEndpoints(routes => {
                routes.MapPost("/save", async context => { File.WriteAllText("request.txt", "ok"); await context.Response.WriteAsync("saved"); });
                routes.MapGet("/big", async context => await context.Response.Body.WriteAsync(new byte[3 * 1024 * 1024]));
                routes.MapGet("/slow", async context => await Task.Delay(10000, context.RequestAborted));
            });
            await Wire.ListenAsync(9000, app, "site");
            """);
        await Run(runspace, "var built = await Dotnet.Run(\"app/Site.csproj\"); if(!built.Success) throw new Exception(built.Error + built.Build.Output);");
        Assert.Equal("app/site", runspace.WireSites.Single().WebRoot);
        var page = await runspace.RequestAsync(new(9000, "GET", "/"));
        Assert.Equal(200, page.Status); Assert.Equal("yes", page.Headers["X-Starting"].Single());
        Assert.Equal("compiled site", System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(page.Body)));
        Assert.Equal("yes", File.ReadAllText(Path.Combine(_root, "app", "completed.txt")));
        Assert.Equal(200, (await runspace.RequestAsync(new(9000, "POST", "/save"))).Status);
        Assert.True(File.Exists(Path.Combine(_root, "app", "request.txt")));
        Assert.Equal(500, (await runspace.RequestAsync(new(9000, "GET", "/big"))).Status);
        Assert.Equal(413, (await runspace.RequestAsync(new(9000, "POST", "/save", Body: new string('A', 3 * 1024 * 1024)))).Status);
        using var cancel = new CancellationTokenSource(100);
        Assert.Equal(504, (await runspace.RequestAsync(new(9000, "GET", "/slow"), cancel.Token)).Status);
        await Run(runspace, "DotnetDide.Wire.Stop(9000);"); Assert.Empty(runspace.WireSites);
    }

}
