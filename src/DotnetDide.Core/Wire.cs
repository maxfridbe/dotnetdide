using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace DotnetDide;

public sealed record WireSite(int Port, string? WebRoot);
public sealed record WireRequest(int Port, string Method, string Path, Dictionary<string, string[]>? Headers = null, string? Body = null);
public sealed record WireResponse(int Status, Dictionary<string, string[]> Headers, string Body, string? Error = null);

/// <summary>ASP.NET Core middleware and IServer hosting over browser messages, without listening sockets.</summary>
public static class Wire
{
    internal static readonly AsyncLocal<WireRegistry?> Current = new();
    private static WireRegistry Registry => Current.Value ?? throw new InvalidOperationException("Wire must run inside a DotnetDide submission.");
    public static IApplicationBuilder CreateApplication()
    {
        Registry.CheckPermission();
        var services = new ServiceCollection(); services.AddLogging(); services.AddRouting();
        services.AddSingleton(_ => new System.Diagnostics.DiagnosticListener("DotnetDide.Wire"));
        services.AddSingleton<System.Diagnostics.DiagnosticSource>(provider => provider.GetRequiredService<System.Diagnostics.DiagnosticListener>());
        var provider = services.BuildServiceProvider(); Registry.Own(provider);
        return new ApplicationBuilder(provider);
    }
    public static bool Stop(int port) => Registry.Stop(port);
    public static WireServer CreateServer(int port, string? webRoot = null) => Registry.Create(port, webRoot);
    public static async Task<WireServer> ListenAsync(int port, IApplicationBuilder application, string? webRoot = null, CancellationToken cancellationToken = default)
    {
        var server = CreateServer(port, webRoot);
        try { await server.StartAsync(new MiddlewareApplication(application.Build(), application.ApplicationServices), cancellationToken); return server; }
        catch { server.Dispose(); throw; }
    }
    private sealed class MiddlewareApplication(RequestDelegate pipeline, IServiceProvider services) : IHttpApplication<DefaultHttpContext>
    {
        public DefaultHttpContext CreateContext(IFeatureCollection features)
        {
            var context = new DefaultHttpContext(features);
            context.ServiceScopeFactory = services.GetRequiredService<IServiceScopeFactory>();
            return context;
        }
        public Task ProcessRequestAsync(DefaultHttpContext context) => pipeline(context);
        public void DisposeContext(DefaultHttpContext context, Exception? exception) => context.Uninitialize();
    }
}

internal sealed class WireRegistry(WorkspaceFileSystem files) : IDisposable
{
    private readonly Dictionary<int, WireServer> _servers = [];
    private readonly List<IDisposable> _owned = [];
    internal bool Enabled { get; set; }
    internal IReadOnlyList<WireSite> Sites => _servers.Values.Where(s => s.Started).Select(s => new WireSite(s.Port, s.WebRoot)).OrderBy(s => s.Port).ToArray();
    internal void CheckPermission() { if (!Enabled) throw new UnauthorizedAccessException("Interactive site access is disabled by the host."); }
    internal void Own(IDisposable value) => _owned.Add(value);
    internal WireServer Create(int port, string? webRoot)
    {
        CheckPermission();
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        if (_servers.ContainsKey(port)) throw new InvalidOperationException($"Virtual port {port} is already registered.");
        if (_servers.Count >= 8) throw new InvalidOperationException("At most eight virtual sites can be registered.");
        var directory = files.ResolvePath(Directory.GetCurrentDirectory(), true);
        if (webRoot is not null) webRoot = Path.GetRelativePath(files.RootPath, files.ResolvePath(Path.GetFullPath(webRoot), true));
        var server = new WireServer(port, webRoot, directory, files, () => _servers.Remove(port));
        _servers.Add(port, server); return server;
    }
    internal bool Stop(int port) { CheckPermission(); if (!_servers.TryGetValue(port, out var server)) return false; server.Dispose(); return true; }
    internal Task<WireResponse> RequestAsync(WireRequest request, CancellationToken cancellationToken)
    {
        if (!Enabled) return Task.FromResult(WireServer.Error(403, "Interactive site access is disabled."));
        return _servers.TryGetValue(request.Port, out var server)
            ? server.RequestAsync(request, cancellationToken) : Task.FromResult(WireServer.Error(503, "Virtual site is not running."));
    }
    public void Dispose()
    {
        foreach (var server in _servers.Values.ToArray()) server.Dispose();
        _servers.Clear(); foreach (var value in _owned) value.Dispose(); _owned.Clear();
    }
}

/// <summary>A real ASP.NET Core IServer using bounded, buffered requests over a virtual port.</summary>
public sealed class WireServer : IServer
{
    public const int MaximumBodyBytes = 2 * 1024 * 1024;
    private readonly WorkspaceFileSystem _files;
    private readonly Action _remove;
    private readonly string _directory;
    private Func<IFeatureCollection, Func<Task>, Task>? _process;
    private bool _disposed;
    internal bool Started => _process is not null;
    public int Port { get; }
    public string? WebRoot { get; }
    public IFeatureCollection Features { get; } = new FeatureCollection();
    internal WireServer(int port, string? webRoot, string directory, WorkspaceFileSystem files, Action remove)
    {
        Port = port; WebRoot = webRoot; _directory = directory; _files = files; _remove = remove;
        var addresses = new AddressesFeature(); addresses.Addresses.Add($"http://localhost:{port}"); Features.Set<IServerAddressesFeature>(addresses);
    }
    public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken) where TContext : notnull
    {
        ObjectDisposedException.ThrowIf(_disposed, this); cancellationToken.ThrowIfCancellationRequested();
        if (_process is not null) throw new InvalidOperationException("Site is already started.");
        _process = async (features, finish) =>
        {
            var context = application.CreateContext(features); Exception? failure = null;
            try { await application.ProcessRequestAsync(context); await finish(); }
            catch (Exception error) { failure = error; throw; }
            finally { await ((WireResponseFeature)features.Get<IHttpResponseFeature>()!).FinishCallbacksAsync(); application.DisposeContext(context, failure); }
        };
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken) { Dispose(); return Task.CompletedTask; }
    public void Dispose() { if (_disposed) return; _disposed = true; _process = null; _remove(); }
    internal static WireResponse Error(int status, string error) => new(status, new(StringComparer.OrdinalIgnoreCase), "", error);
    internal async Task<WireResponse> RequestAsync(WireRequest request, CancellationToken cancellationToken)
    {
        if (_process is null) return Error(503, "Virtual site is not running.");
        if (request.Path is null || request.Path.Length > 8192 || !request.Path.StartsWith('/') || request.Path.StartsWith("//") || request.Path.Contains('#')) return Error(400, "Invalid request path.");
        if (request.Method is null || !new[] { "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS" }.Contains(request.Method)) return Error(405, "Unsupported method.");
        var parts = request.Path.Split('?', 2); string path;
        try { path = Uri.UnescapeDataString(parts[0]); } catch (UriFormatException) { return Error(400, "Invalid path encoding."); }
        if (path.Contains('\\') || path.Any(char.IsControl) || path.Split('/').Any(s => s is "." or "..")) return Error(400, "Unsafe request path.");
        if ((request.Body?.Length ?? 0) > MaximumBodyBytes * 4 / 3 + 4) return Error(413, "Request body exceeds 2 MiB.");
        byte[] body; try { body = Convert.FromBase64String(request.Body ?? ""); } catch (FormatException) { return Error(400, "Invalid request body."); }
        if (body.Length > MaximumBodyBytes) return Error(413, "Request body exceeds 2 MiB.");
        var headers = new HeaderDictionary();
        if ((request.Headers?.Count ?? 0) > 64) return Error(431, "Too many headers.");
        var headerBytes = 0;
        foreach (var pair in request.Headers ?? [])
        {
            if (pair.Value is null || pair.Key.Length == 0 || pair.Key.Any(c => !char.IsAsciiLetterOrDigit(c) && !"!#$%&'*+-.^_`|~".Contains(c))) return Error(400, "Invalid header.");
            foreach (var value in pair.Value) { if (value is null || value.Any(char.IsControl)) return Error(400, "Invalid header."); headerBytes += pair.Key.Length + value.Length; }
            if (headerBytes > 16384) return Error(431, "Headers exceed 16 KiB.");
            // Transport owns authority, framing and credentials; never import browser cookies.
            if (new[] { "cookie", "authorization", "host", "content-length", "connection", "transfer-encoding" }.Contains(pair.Key.ToLowerInvariant())) continue;
            headers[pair.Key] = pair.Value;
        }
        headers["Host"] = $"localhost:{Port}"; headers.ContentLength = body.Length;
        using var input = new MemoryStream(body, false);
        var feature = new HttpRequestFeature { Protocol = "HTTP/1.1", Scheme = "http", Method = request.Method, Path = path, PathBase = "", QueryString = parts.Length > 1 ? "?" + parts[1] : "", RawTarget = request.Path, Headers = headers, Body = input };
        using var response = new WireResponseFeature();
        var features = new FeatureCollection(); features.Set<IHttpRequestFeature>(feature); features.Set<IHttpResponseFeature>(response); features.Set<IHttpResponseBodyFeature>(response);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(15));
        features.Set<IHttpRequestLifetimeFeature>(new RequestLifetime(timeout.Token));
        var previousDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_directory);
            await _process(features, async () =>
            {
                if (response.StatusCode == 404 && !response.HasStarted && response.Length == 0 && WebRoot is not null && request.Method is "GET" or "HEAD")
                    await ServeFile(path, response, timeout.Token);
                await response.CompleteAsync();
            });
            var resultHeaders = response.Headers.ToDictionary(p => p.Key, p => p.Value.Select(v => v ?? "").ToArray(), StringComparer.OrdinalIgnoreCase);
            resultHeaders.Remove("Set-Cookie");
            return new(response.StatusCode, resultHeaders, request.Method == "HEAD" ? "" : Convert.ToBase64String(response.Bytes));
        }
        catch (OperationCanceledException) { return Error(504, "Site request cancelled or timed out."); }
        catch (Exception error) { return Error(500, error.ToString()); }
        finally { await response.FinishCallbacksAsync(); Directory.SetCurrentDirectory(previousDirectory); }
    }
    private async Task ServeFile(string path, WireResponseFeature response, CancellationToken cancellationToken)
    {
        var relative = path.TrimStart('/'); if (relative.Length == 0 || relative.EndsWith('/')) relative += "index.html";
        var root = _files.ResolvePath(WebRoot!, true);
        var full = Path.GetFullPath(relative, root);
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) return;
        try
        {
            var stat = _files.Stat(full); if (stat.IsDirectory) return;
            if (stat.Size > MaximumBodyBytes) { response.StatusCode = 413; return; }
            var bytes = _files.ReadFile(full); response.StatusCode = 200;
            response.Headers.ContentType = Path.GetExtension(relative).ToLowerInvariant() switch
            {
                ".html" or ".htm" => "text/html; charset=utf-8",
                ".js" or ".mjs" => "text/javascript; charset=utf-8",
                ".css" => "text/css; charset=utf-8",
                ".json" => "application/json",
                ".svg" => "image/svg+xml",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".wasm" => "application/wasm",
                ".wav" => "audio/wav",
                ".ogg" or ".opus" => "audio/ogg",
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                _ => "application/octet-stream"
            };
            response.Headers.ContentLength = bytes.Length; await response.Stream.WriteAsync(bytes, cancellationToken);
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
        catch (ArgumentException) { response.StatusCode = 400; }
        catch (IOException) { response.StatusCode = 403; }
    }
    private sealed class AddressesFeature : IServerAddressesFeature
    { public ICollection<string> Addresses { get; } = new List<string>(); public bool PreferHostingUrls { get; set; } }
    private sealed class RequestLifetime(CancellationToken token) : IHttpRequestLifetimeFeature
    { public CancellationToken RequestAborted { get; set; } = token; public void Abort() => throw new OperationCanceledException("Request aborted.", RequestAborted); }
}
