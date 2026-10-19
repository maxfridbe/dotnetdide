using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DotnetDide.Relay;

public static class RelayHost
{
    private static readonly HashSet<string> Methods = new(StringComparer.Ordinal) { "GET", "HEAD", "OPTIONS", "POST", "PUT", "PATCH", "DELETE" };
    private static readonly HashSet<string> ForbiddenHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host", "Connection", "Keep-Alive", "Transfer-Encoding", "TE", "Trailer", "Upgrade", "Proxy-Authorization", "Proxy-Authenticate",
        "Cookie", "Cookie2", "Set-Cookie", "Set-Cookie2", "Content-Length", "Origin", "Referer", "Forwarded", "Via"
    };
    public static WebApplication Create(RelayOptions options, string[]? args = null)
    {
        options.Validate();
        var builder = WebApplication.CreateBuilder(args ?? []);
        builder.WebHost.ConfigureKestrel(server => server.Limits.MaxRequestBodySize = 12 * 1024 * 1024);
        builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy => policy.WithOrigins(options.Origins).WithMethods("POST").WithHeaders("Authorization", "Content-Type")));
        var app = builder.Build();
        var handler = new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = DecompressionMethods.All, UseProxy = !string.IsNullOrEmpty(options.Proxy), ConnectTimeout = TimeSpan.FromSeconds(15) };
        if (!string.IsNullOrEmpty(options.Proxy))
        {
            var uri = new Uri(options.Proxy);
            var proxy = new WebProxy(uri);
            if (uri.UserInfo.Length > 0)
            {
                var parts = uri.UserInfo.Split(':', 2);
                proxy.Credentials = new NetworkCredential(Uri.UnescapeDataString(parts[0]), parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "");
            }
            handler.Proxy = proxy;
        }
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        app.Lifetime.ApplicationStopped.Register(client.Dispose);
        var expectedToken = SHA256.HashData(Encoding.UTF8.GetBytes("Bearer " + options.Token));
        app.UseCors();
        app.MapPost("/relay", async (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!options.Origins.Contains(context.Request.Headers.Origin.ToString(), StringComparer.Ordinal)) return Results.StatusCode(403);
            var suppliedToken = SHA256.HashData(Encoding.UTF8.GetBytes(context.Request.Headers.Authorization.ToString()));
            if (!CryptographicOperations.FixedTimeEquals(expectedToken, suppliedToken)) return Results.StatusCode(401);
            try
            {
                var input = await context.Request.ReadFromJsonAsync<RelayRequest>(context.RequestAborted);
                if (input is null || input.Method is null || !Methods.Contains(input.Method) ||
                    !Uri.TryCreate(input.Url, UriKind.Absolute, out var target) || target.Scheme is not ("http" or "https") || target.UserInfo.Length != 0 || target.Fragment.Length != 0)
                    return Results.BadRequest(new { error = "Invalid HTTP request." });
                if (!options.Targets.Contains(target.GetLeftPart(UriPartial.Authority), StringComparer.Ordinal)) return Results.StatusCode(403);
                if ((input.BodyBase64?.Length ?? 0) > (RelayOptions.MaxBytes + 2) / 3 * 4) return Results.StatusCode(413);
                var bytes = input.BodyBase64 is null ? null : Convert.FromBase64String(input.BodyBase64);
                if ((bytes?.Length ?? 0) > RelayOptions.MaxBytes) return Results.StatusCode(413);
                if (bytes is not null && input.Method is "GET" or "HEAD") return Results.BadRequest(new { error = "GET/HEAD bodies are unsupported." });
                using var request = new HttpRequestMessage(new HttpMethod(input.Method), target);
                if (bytes is not null) request.Content = new ByteArrayContent(bytes);
                if ((input.Headers?.Count ?? 0) > 64) return Results.BadRequest(new { error = "Too many headers." });
                foreach (var header in input.Headers ?? [])
                {
                    if (ForbiddenHeaders.Contains(header.Key) || header.Key.StartsWith("Sec-", StringComparison.OrdinalIgnoreCase) || header.Key.StartsWith("Proxy-", StringComparison.OrdinalIgnoreCase) || header.Key.StartsWith("X-Forwarded-", StringComparison.OrdinalIgnoreCase)) continue;
                    if (header.Key.Length > 256 || header.Value is null || header.Value.Length > 8192 || header.Value.Contains('\r') || header.Value.Contains('\n')) return Results.BadRequest(new { error = "Invalid header." });
                    if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value) && request.Content is not null) request.Content.Headers.Add(header.Key, header.Value);
                }
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                // Never follow redirects into a destination that the host did not authorize.
                if ((int)response.StatusCode is >= 300 and < 400) return Results.Json(new { error = "Relay redirects are disabled. Request the final allowed URL explicitly." }, statusCode: 502);
                if (response.Content.Headers.ContentLength > RelayOptions.MaxBytes) return Results.StatusCode(413);
                using var body = new MemoryStream();
                using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
                var buffer = new byte[16384];
                int count;
                while ((count = await stream.ReadAsync(buffer, timeout.Token)) > 0)
                {
                    if (body.Length + count > RelayOptions.MaxBytes) return Results.StatusCode(413);
                    body.Write(buffer, 0, count);
                }
                var headers = response.Headers.Concat(response.Content.Headers).Where(h => !ForbiddenHeaders.Contains(h.Key)).ToDictionary(h => h.Key, h => string.Join(", ", h.Value), StringComparer.OrdinalIgnoreCase);
                return Results.Json(new { status = (int)response.StatusCode, headers, bodyBase64 = Convert.ToBase64String(body.ToArray()) });
            }
            catch (OperationCanceledException) { return Results.StatusCode(504); }
            catch (HttpRequestException) { return Results.Json(new { error = "Upstream request failed. Check relay connectivity and proxy configuration." }, statusCode: 502); }
            catch (Exception error) when (error is JsonException or FormatException or ArgumentException or BadHttpRequestException)
            { return Results.BadRequest(new { error = "Invalid relay request." }); }
        });
        return app;
    }
    private sealed record RelayRequest(string Url, string Method, Dictionary<string, string>? Headers, string? BodyBase64);
}
