using System.Net.Http.Json;

namespace Dotnetdide;

/// <summary>Async HTTP helpers. In browser builds requests use fetch, CORS and the host network policy.</summary>
public static class Net
{
    private static readonly HttpClient Client = new();
    public static Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) =>
        Client.GetStringAsync(url, cancellationToken);
    public static Task<byte[]> GetBytesAsync(string url, CancellationToken cancellationToken = default) =>
        Client.GetByteArrayAsync(url, cancellationToken);
    public static Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default) =>
        Client.SendAsync(request, cancellationToken);
    public static async Task<string> PostJsonAsync<T>(string url, T value, CancellationToken cancellationToken = default)
    {
        using var response = await Client.PostAsJsonAsync(url, value, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
