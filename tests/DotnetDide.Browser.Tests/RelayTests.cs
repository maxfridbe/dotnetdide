using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DotnetDide.Relay;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotnetDide.Browser.Tests;

public sealed class RelayTests
{
    private const string Token = "test-relay-token-not-a-secret-123456";
    private const string Page = "https://allowed.example";
    private static string Origin(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    private static async Task<WebApplication> Target()
    {
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.MapMethods("/echo", ["GET", "HEAD", "OPTIONS", "DELETE", "POST", "PUT", "PATCH"], async (HttpContext context) =>
        {
            context.Response.Headers["X-Method"] = context.Request.Method;
            context.Response.Headers["X-Authorization"] = context.Request.Headers.Authorization;
            context.Response.Headers.SetCookie = "must-not-reach-browser=secret";
            context.Response.StatusCode = context.Request.Method == "DELETE" ? 409 : 200;
            await context.Request.Body.CopyToAsync(context.Response.Body);
        });
        app.MapGet("/text", () => "relayed bytes");
        app.MapGet("/slow", async (HttpContext context) => { await context.Response.StartAsync(); await Task.Delay(1000); await context.Response.WriteAsync("late"); });
        app.MapGet("/redirect", () => Results.Redirect("http://unapproved.invalid/private"));
        app.MapGet("/large", () => Results.Bytes(new byte[RelayOptions.MaxBytes + 1]));
        await app.StartAsync(); return app;
    }
    private static async Task<WebApplication> Relay(string origin, string target, string? proxy = null)
    {
        var app = RelayHost.Create(new RelayOptions(Token, [origin], [target], proxy), ["--urls", "http://127.0.0.1:0", "--Logging:LogLevel:Default", "None"]);
        await app.StartAsync(); return app;
    }
    private static async Task<HttpResponseMessage> Send(HttpClient http, string relay, string url, string method = "GET", string origin = Page, string token = Token, byte[]? body = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, relay + "/relay");
        request.Headers.Add("Origin", origin); request.Headers.Add("Authorization", "Bearer " + token);
        request.Content = JsonContent.Create(new {url, method, headers = new Dictionary<string,string> { ["Authorization"] = "Bearer target-key", ["Cookie"] = "forbidden=yes" }, bodyBase64 = body is null ? null : Convert.ToBase64String(body)});
        return await http.SendAsync(request);
    }
    [Fact]
    public async Task RelayErrorsRetainBoundedDetailsWithoutSecretsOrDirectFallback()
    {
        await using var browser = new BrowserHarness(); await browser.StartAsync();
        await browser.EvaluateAsync("""
            const {relayFetch} = await import('./relay-fetch.js');
            const relay = {endpoint: 'https://relay.example/relay', token: 'private-relay-token-123456789'};
            const request = 'https://target.example/private?key=secret-query';
            let calls = 0;
            const fail = async (body, type = 'text/plain') => {
                try {
                    await relayFetch(async () => { calls++; return new Response(body, {status: 502, headers: {'content-type': type}}); }, relay, request,
                        {headers: {Authorization: 'Bearer target-secret'}, credentials: 'omit'});
                    throw Error('Expected rejection');
                } catch (error) { return error.message; }
            };
            const text = await fail(`Tor connection failed: DNS lookup failed ${relay.token} Bearer target-secret ${request} secret-query`);
            if (!text.includes('DNS lookup failed') || !text.includes('502') || !text.includes('No direct fallback')) throw Error(text);
            for (const secret of [relay.token, 'target-secret', 'secret-query', '/private']) if (text.includes(secret)) throw Error('Secret leaked');
            const json = await fail(JSON.stringify({error: 'TLS handshake failed'}), 'application/json');
            if (!json.includes('TLS handshake failed')) throw Error(json);
            const html = await fail('<html>private page</html>', 'text/html');
            if (html.includes('private page')) throw Error('HTML leaked');
            let cancelled = false;
            const large = await fail(new ReadableStream({pull(c) { c.enqueue(new TextEncoder().encode('x'.repeat(8192))); }, cancel() { cancelled = true; }}));
            if (!cancelled || large.length > 1300 || !large.includes('truncated')) throw Error('Unbounded error response');
            if (calls !== 4) throw Error('Unexpected retry or fallback');
            const target = await relayFetch(async () => new Response(JSON.stringify({status:403,headers:{},bodyBase64:btoa('denied')})), relay, request);
            if (target.status !== 403 || await target.text() !== 'denied') throw Error('Target rejection was replaced');
            """);
    }
    [Fact]
    public async Task RelayEnforcesOriginsTokensTargetsAndBoundsWhilePreservingMethodsBodiesAndErrors()
    {
        await using var target = await Target(); await using var relay = await Relay(Page, Origin(target)); using var http = new HttpClient();
        foreach (var method in new[] {"GET", "HEAD", "OPTIONS", "DELETE", "POST", "PUT", "PATCH"})
        {
            var body = method is "POST" or "PUT" or "PATCH" ? new byte[] {0,255,128} : null;
            using var response = await Send(http, Origin(relay), Origin(target) + "/echo", method, body:body);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(Page, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(method == "DELETE" ? 409 : 200, result.GetProperty("status").GetInt32());
            var headers = result.GetProperty("headers").EnumerateObject().ToDictionary(h => h.Name.ToLowerInvariant(), h => h.Value.GetString());
            Assert.Equal(method, headers["x-method"]); Assert.Equal("Bearer target-key", headers["x-authorization"]);
            Assert.False(headers.ContainsKey("set-cookie"));
            Assert.Equal(body is null ? "" : "AP+A", result.GetProperty("bodyBase64").GetString());
        }
        using var badOrigin = await Send(http, Origin(relay), Origin(target)+"/text", origin:"https://evil.example"); Assert.Equal(HttpStatusCode.Forbidden, badOrigin.StatusCode);
        using var badToken = await Send(http, Origin(relay), Origin(target)+"/text", token:"wrong"); Assert.Equal(HttpStatusCode.Unauthorized, badToken.StatusCode);
        using var badTarget = await Send(http, Origin(relay), "http://unapproved.invalid/private"); Assert.Equal(HttpStatusCode.Forbidden, badTarget.StatusCode);
        using var redirect = await Send(http, Origin(relay), Origin(target)+"/redirect"); Assert.Equal(HttpStatusCode.BadGateway, redirect.StatusCode);
        using var large = await Send(http, Origin(relay), Origin(target)+"/large"); Assert.Equal(HttpStatusCode.RequestEntityTooLarge, large.StatusCode);
        using var connect = await Send(http, Origin(relay), Origin(target)+"/text", "CONNECT"); Assert.Equal(HttpStatusCode.BadRequest, connect.StatusCode);
        using var preflight = new HttpRequestMessage(HttpMethod.Options, Origin(relay)+"/relay");
        preflight.Headers.Add("Origin", Page); preflight.Headers.Add("Access-Control-Request-Method", "POST"); preflight.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        using var cors = await http.SendAsync(preflight); Assert.Equal(HttpStatusCode.NoContent, cors.StatusCode);
        Assert.Equal(Page, cors.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }
    [Fact]
    public async Task NativeRelayUsesSocks5IncludingRemoteDns()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var served = Task.Run(async () =>
        {
            using var connection = await listener.AcceptTcpClientAsync(timeout.Token); using var stream = connection.GetStream();
            var greeting = new byte[2]; await stream.ReadExactlyAsync(greeting, timeout.Token); Assert.Equal(5, greeting[0]);
            var methods = new byte[greeting[1]]; await stream.ReadExactlyAsync(methods, timeout.Token);
            await stream.WriteAsync(new byte[] {5,0}, timeout.Token);
            var command = new byte[4]; await stream.ReadExactlyAsync(command, timeout.Token); Assert.Equal(new byte[] {5,1,0,3}, command);
            var length = new byte[1]; await stream.ReadExactlyAsync(length, timeout.Token);
            var hostname = new byte[length[0]]; await stream.ReadExactlyAsync(hostname, timeout.Token); Assert.Equal("remote-dns.invalid", Encoding.ASCII.GetString(hostname));
            var targetPort = new byte[2]; await stream.ReadExactlyAsync(targetPort, timeout.Token); Assert.Equal(new byte[] {0,80}, targetPort);
            await stream.WriteAsync(new byte[] {5,0,0,1,127,0,0,1,0,80}, timeout.Token);
            var bytes = new List<byte>(); var one = new byte[1];
            while (bytes.Count < 8192) { await stream.ReadExactlyAsync(one, timeout.Token); bytes.Add(one[0]); if (Encoding.ASCII.GetString(bytes.ToArray()).EndsWith("\r\n\r\n", StringComparison.Ordinal)) break; }
            Assert.StartsWith("GET /text HTTP/1.1", Encoding.ASCII.GetString(bytes.ToArray()));
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 9\r\nConnection: close\r\n\r\nvia-socks"), timeout.Token);
        }, timeout.Token);
        await using var relay = await Relay(Page, "http://remote-dns.invalid", $"socks5://127.0.0.1:{port}"); using var http = new HttpClient();
        using var response = await Send(http, Origin(relay), "http://remote-dns.invalid/text");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("via-socks", Encoding.UTF8.GetString(Convert.FromBase64String(result.GetProperty("bodyBase64").GetString()!)));
        await served;
    }
    [Fact]
    public async Task BrowserRelayHandlesCorsHttpClientFilesCancellationAndNoDirectFallback()
    {
        await using var browser = new BrowserHarness(); await browser.StartAsync();
        var page = (await browser.EvaluateAsync("return location.origin;")).GetString()!;
        await using var target = await Target(); await using var relay = await Relay(page, Origin(target));
        Assert.False((await browser.EvaluateAsync($"try {{ await fetch({JsonSerializer.Serialize(Origin(target)+"/text")}); return true; }} catch {{ return false; }}")).GetBoolean());
        var settings = JsonSerializer.Serialize(new {networkAccess=true, relay=new {endpoint=Origin(relay)+"/relay", token=Token}});
        await browser.EvaluateAsync($"globalThis.routed = await api.loadDotnetdide({settings}); globalThis.targetUrl={JsonSerializer.Serialize(Origin(target))};");
        var result = await browser.EvaluateAsync("""
            return await routed.runCSharpAsync(`
                Console.Write(await new System.Net.Http.HttpClient().GetStringAsync(${JSON.stringify(targetUrl + '/text')}));
                await Dotnetdide.Net.DownloadFileAsync(${JSON.stringify(targetUrl + '/text')}, "via-relay.txt");
                Console.Write(File.ReadAllText("via-relay.txt"));
                using (var response = await Dotnetdide.Net.PutAsync(${JSON.stringify(targetUrl + '/echo')}, new System.Net.Http.ByteArrayContent(new byte[] {0,255,128}))) {
                    if (response.Headers.GetValues("X-Method").Single() != "PUT" || Convert.ToBase64String(await response.Content.ReadAsByteArrayAsync()) != "AP+A") throw new Exception("Relay binary PUT failed");
                }
                using (var cts = new System.Threading.CancellationTokenSource(100)) {
                    try { await Dotnetdide.Net.GetStringAsync(${JSON.stringify(targetUrl + '/slow')}, cts.Token); }
                    catch (OperationCanceledException) { Console.Write("cancelled"); }
                }
            `);
            """);
        Assert.True(result.GetProperty("success").GetBoolean(), result.ToString());
        Assert.Equal("relayed bytesrelayed bytescancelled", result.GetProperty("stdout").GetString());
        await browser.EvaluateAsync("await routed.setNetworkAccess(false);");
        var denied = await browser.EvaluateAsync("return await routed.runCSharpAsync('await Dotnetdide.Net.GetStringAsync(' + JSON.stringify(targetUrl+'/text') + ')');");
        Assert.False(denied.GetProperty("success").GetBoolean());
        await relay.StopAsync();
        await browser.EvaluateAsync("await routed.setNetworkAccess(true);");
        var failed = await browser.EvaluateAsync("return await routed.runCSharpAsync('await new System.Net.Http.HttpClient().GetStringAsync(' + JSON.stringify(location.origin+'/network-probe') + ')');");
        Assert.False(failed.GetProperty("success").GetBoolean()); Assert.Equal(0, browser.NetworkRequests);
        await browser.EvaluateAsync("routed.terminate();");
        var offline = await browser.EvaluateAsync($"const local = await api.loadDotnetdide({{relay:{{endpoint:{JsonSerializer.Serialize(Origin(relay)+"/relay")}}}}}); const result = await local.runCSharpAsync('6 * 7'); local.terminate(); return result;");
        Assert.Equal("42", offline.GetProperty("value").GetString());
        foreach (var endpoint in new[] {"socks5://localhost:9050", "https://bridges.torproject.org/"})
            Assert.True((await browser.EvaluateAsync($"try {{ await api.loadDotnetdide({{relay:{{endpoint:{JsonSerializer.Serialize(endpoint)},token:{JsonSerializer.Serialize(Token)}}}}}); return false; }} catch (e) {{ return /SOCKS|Tor/.test(e.message); }}")).GetBoolean());
    }
}
