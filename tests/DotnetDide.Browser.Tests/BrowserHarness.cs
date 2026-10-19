using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace DotnetDide.Browser.Tests;

/// <summary>Direct Chrome DevTools Protocol from .NET; no Node, Playwright or npm dependency.</summary>
internal sealed class BrowserHarness : IAsyncDisposable
{
    private readonly string _profile = Path.Combine(Path.GetTempPath(), "dotnetdide-chrome-" + Guid.NewGuid());
    private readonly ClientWebSocket _socket = new();
    private readonly List<string> _errors = [];
    private WebApplication? _server;
    private Process? _chrome;
    private int _id;
    public string Root { get; }
    public int NetworkRequests { get; private set; }
    public BrowserHarness()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json"))) directory = directory.Parent;
        Root = directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
    public async Task StartAsync(bool blockMonaco = false)
    {
        var site = Path.Combine(Root, "artifacts/site/wwwroot");
        if (!File.Exists(Path.Combine(site, "dotnetdide.js"))) throw new InvalidOperationException("Publish the browser project to artifacts/site first.");
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        _server = builder.Build();
        _server.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(site), RequestPath = "/dotnetdide", ServeUnknownFileTypes = true });
        _server.MapGet("/network-probe", () => { NetworkRequests++; return "network-ok"; });
        _server.MapPost("/network-echo", async (Microsoft.AspNetCore.Http.HttpRequest request) => {
            using var reader = new StreamReader(request.Body); return await reader.ReadToEndAsync();
        });
        await _server.StartAsync();
        var origin = _server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        Directory.CreateDirectory(_profile);
        var chrome = Environment.GetEnvironmentVariable("CHROME_BIN") ?? "/usr/bin/google-chrome";
        var start = new ProcessStartInfo(chrome) { RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in new[] { "--headless=new", "--window-size=1280,1000", "--no-sandbox", "--remote-debugging-port=0", "--no-first-run", "--disable-background-networking", "--disable-dev-shm-usage", "--user-data-dir=" + _profile, "about:blank" }) start.ArgumentList.Add(argument);
        _chrome = Process.Start(start)!;
        _chrome.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (_errors) { if (_errors.Count < 100) _errors.Add(e.Data); } }; _chrome.BeginErrorReadLine();
        _chrome.BeginOutputReadLine();
        var portFile = Path.Combine(_profile, "DevToolsActivePort");
        for (var i = 0; !File.Exists(portFile) && i < 200; i++) await Task.Delay(50);
        if (!File.Exists(portFile)) throw new InvalidOperationException("Chrome failed to start: " + string.Join('\n', _errors));
        var port = (await File.ReadAllLinesAsync(portFile))[0];
        using var http = new HttpClient();
        using var targets = JsonDocument.Parse(await http.GetStringAsync($"http://127.0.0.1:{port}/json/list"));
        var target = targets.RootElement.EnumerateArray().First(x => x.GetProperty("type").GetString() == "page");
        await _socket.ConnectAsync(new Uri(target.GetProperty("webSocketDebuggerUrl").GetString()!), CancellationToken.None);
        await CallAsync("Page.enable", new { });
        await CallAsync("Runtime.enable", new { });
        if (blockMonaco)
        {
            await CallAsync("Network.enable", new { });
            await CallAsync("Network.setBlockedURLs", new { urls = new[] { "*vendor/monaco/*" } });
        }
        await CallAsync("Page.navigate", new { url = origin + "/dotnetdide/index.html" });
        for (var i = 0; i < 100; i++)
        {
            if ((await EvaluateAsync("return document.readyState === 'complete' && location.pathname.endsWith('index.html')")).GetBoolean()) break;
            await Task.Delay(50);
        }
        await EvaluateAsync("globalThis.api = await import('./dotnetdide.js')");
    }
    private async Task<JsonElement> CallAsync(string method, object parameters)
    {
        var id = ++_id;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await _socket.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { id, method, @params = parameters })), WebSocketMessageType.Text, true, timeout.Token);
        var buffer = new byte[65536];
        while (true)
        {
            using var message = new MemoryStream(); WebSocketReceiveResult received;
            do { received = await _socket.ReceiveAsync(buffer, timeout.Token); message.Write(buffer, 0, received.Count); } while (!received.EndOfMessage);
            using var json = JsonDocument.Parse(message.ToArray()); var root = json.RootElement;
            if (!root.TryGetProperty("id", out var responseId) || responseId.GetInt32() != id) continue;
            if (root.TryGetProperty("error", out var error)) throw new InvalidOperationException(error.ToString());
            return root.GetProperty("result").Clone();
        }
    }
    public async Task<JsonElement> EvaluateAsync(string script)
    {
        var result = await CallAsync("Runtime.evaluate", new { expression = "(async()=>{" + script + "})()", awaitPromise = true, returnByValue = true });
        if (result.TryGetProperty("exceptionDetails", out var error)) throw new InvalidOperationException(error.ToString());
        return result.GetProperty("result").TryGetProperty("value", out var value) ? value.Clone() : default;
    }
    public async Task WaitForAsync(string expression)
    {
        for (var attempt = 0; attempt < 600; attempt++)
        {
            if ((await EvaluateAsync("return " + expression)).GetBoolean()) return;
            await Task.Delay(50);
        }
        throw new TimeoutException("Browser condition failed: " + expression);
    }
    public async Task InsertTextAsync(string text) => await CallAsync("Input.insertText", new { text });
    public async Task PressKeyAsync(string key, int virtualKey, int modifiers = 0)
    {
        await CallAsync("Input.dispatchKeyEvent", new { type = "keyDown", key, windowsVirtualKeyCode = virtualKey, modifiers });
        await CallAsync("Input.dispatchKeyEvent", new { type = "keyUp", key, windowsVirtualKeyCode = virtualKey, modifiers });
    }
    public async Task SetViewportAsync(int width, int height)
    {
        await CallAsync("Emulation.setDeviceMetricsOverride", new { width, height, deviceScaleFactor = 1, mobile = true });
    }
    public async Task ScreenshotAsync(string name = "demo.png")
    {
        var result = await CallAsync("Page.captureScreenshot", new { format = "png" });
        await File.WriteAllBytesAsync(Path.Combine(Root, "artifacts", name), Convert.FromBase64String(result.GetProperty("data").GetString()!));
    }
    public async ValueTask DisposeAsync()
    {
        _socket.Dispose();
        if (_chrome is not null) { if (!_chrome.HasExited) _chrome.Kill(entireProcessTree: true); await _chrome.WaitForExitAsync(); _chrome.Dispose(); }
        if (_server is not null) await _server.DisposeAsync();
        if (Directory.Exists(_profile)) Directory.Delete(_profile, true);
    }
}
