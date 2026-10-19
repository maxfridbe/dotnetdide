using System.Net.Http.Json;

namespace Dotnetdide;

/// <summary>Async HTTP helpers. Direct browser requests use fetch and CORS; a host relay can route HTTP through an upstream proxy.</summary>
public static class Net
{
    private static readonly HttpClient Client = new();
    public static Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) =>
        Client.GetStringAsync(url, cancellationToken);
    public static Task<byte[]> GetBytesAsync(string url, CancellationToken cancellationToken = default) =>
        Client.GetByteArrayAsync(url, cancellationToken);
    public static Task<T?> GetJsonAsync<T>(string url, CancellationToken cancellationToken = default) =>
        Client.GetFromJsonAsync<T>(url, cancellationToken);
    /// <summary>Dispose the returned stream. In WASM it is an asynchronous HTTP stream, not a raw TCP socket.</summary>
    public static Task<Stream> OpenReadAsync(string url, CancellationToken cancellationToken = default) =>
        Client.GetStreamAsync(url, cancellationToken);
    /// <summary>Download to a temporary file then replace destination only after success.</summary>
    public static async Task DownloadFileAsync(string url, string path, CancellationToken cancellationToken = default)
    {
        var destination = Path.GetFullPath(path);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".download";
        try
        {
            using (var input = await OpenReadAsync(url, cancellationToken))
            using (var output = File.Create(temporary))
                await input.CopyToAsync(output, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }
    /// <summary>The caller owns the request and returned response, including non-success responses.</summary>
    public static Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default) =>
        Client.SendAsync(request, cancellationToken);
    public static Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, BrowserRequestOptions options, CancellationToken cancellationToken = default)
    {
        options.Apply(request);
        return Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }
    /// <summary>Owns content; caller must dispose the returned response. Status and headers remain available on HTTP errors.</summary>
    public static async Task<HttpResponseMessage> RequestAsync(string method, string url, HttpContent? content = null,
        BrowserRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), url) { Content = content };
        options?.Apply(request);
        return await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }
    public static Task<HttpResponseMessage> HeadAsync(string url, CancellationToken cancellationToken = default) => RequestAsync("HEAD", url, cancellationToken: cancellationToken);
    public static Task<HttpResponseMessage> OptionsAsync(string url, CancellationToken cancellationToken = default) => RequestAsync("OPTIONS", url, cancellationToken: cancellationToken);
    public static Task<HttpResponseMessage> DeleteAsync(string url, CancellationToken cancellationToken = default) => RequestAsync("DELETE", url, cancellationToken: cancellationToken);
    public static Task<HttpResponseMessage> PostAsync(string url, HttpContent content, CancellationToken cancellationToken = default) => RequestAsync("POST", url, content, cancellationToken: cancellationToken);
    public static Task<HttpResponseMessage> PutAsync(string url, HttpContent content, CancellationToken cancellationToken = default) => RequestAsync("PUT", url, content, cancellationToken: cancellationToken);
    public static Task<HttpResponseMessage> PatchAsync(string url, HttpContent content, CancellationToken cancellationToken = default) => RequestAsync("PATCH", url, content, cancellationToken: cancellationToken);
    public static Task<HttpResponseMessage> PostFormAsync(string url, IEnumerable<KeyValuePair<string, string>> fields, CancellationToken cancellationToken = default) =>
        PostAsync(url, new FormUrlEncodedContent(fields), cancellationToken);
    public static async Task<string> PostJsonAsync<T>(string url, T value, CancellationToken cancellationToken = default)
    {
        using var response = await Client.PostAsJsonAsync(url, value, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}

/// <summary>Fetch options understood by .NET's browser HTTP handler. These do not bypass CORS.</summary>
public sealed class BrowserRequestOptions
{
    public string Credentials { get; init; } = "omit";
    public string Mode { get; init; } = "cors";
    public string Redirect { get; init; } = "follow";
    public string Cache { get; init; } = "default";
    public void Apply(HttpRequestMessage request)
    {
        if (Credentials is not ("omit" or "same-origin" or "include")) throw new ArgumentException("Credentials must be omit, same-origin or include.");
        if (Mode is not ("cors" or "same-origin")) throw new ArgumentException("Mode must be cors or same-origin. no-cors responses are opaque and cannot be read.");
        if (Redirect is not ("follow" or "error" or "manual")) throw new ArgumentException("Redirect must be follow, error or manual.");
        if (Cache is not ("default" or "no-store" or "reload" or "no-cache" or "force-cache" or "only-if-cached")) throw new ArgumentException("Invalid fetch cache option.");
        if (Cache == "only-if-cached" && Mode != "same-origin") throw new ArgumentException("only-if-cached requires same-origin mode.");
        request.Options.Set(new HttpRequestOptionsKey<IDictionary<string, object>>("WebAssemblyFetchOptions"),
            new Dictionary<string, object> { ["credentials"] = Credentials, ["mode"] = Mode, ["redirect"] = Redirect, ["cache"] = Cache });
    }
}
