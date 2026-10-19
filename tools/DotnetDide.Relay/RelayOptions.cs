namespace DotnetDide.Relay;

public sealed record RelayOptions(string Token, string[] Origins, string[] Targets, string? Proxy = null)
{
    public const int MaxBytes = 8 * 1024 * 1024;
    public static RelayOptions FromEnvironment() => new(
        Environment.GetEnvironmentVariable("DOTNETDIDE_RELAY_TOKEN") ?? "",
        Split("DOTNETDIDE_RELAY_ORIGINS"), Split("DOTNETDIDE_RELAY_TARGETS"),
        Environment.GetEnvironmentVariable("DOTNETDIDE_RELAY_PROXY"));
    private static string[] Split(string name) => (Environment.GetEnvironmentVariable(name) ?? "").Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    public void Validate()
    {
        if (Token.Length < 24 || Token.Length > 1024 || Token.Any(c => c <= 32 || c >= 127)) throw new ArgumentException("Set a relay token of 24–1024 printable ASCII characters without spaces.");
        if (Origins.Length == 0 || Targets.Length == 0) throw new ArgumentException("Configure exact allowed page origins and target origins; wildcards are not supported.");
        foreach (var value in Origins.Concat(Targets))
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length != 0 || value != uri.GetLeftPart(UriPartial.Authority))
                throw new ArgumentException("Allowlist entries must be exact HTTP(S) origins without paths, query, credentials or trailing slash.");
        if (!string.IsNullOrEmpty(Proxy) && (!Uri.TryCreate(Proxy, UriKind.Absolute, out var proxy) || proxy.Scheme is not ("http" or "https" or "socks5") || proxy.AbsolutePath != "/" || proxy.Query.Length != 0 || proxy.Fragment.Length != 0 || proxy.Host.Equals("bridges.torproject.org", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Upstream proxy must be an HTTP(S) or SOCKS5 proxy URL. Tor bridge addresses belong in your Tor client; bridges.torproject.org is not a proxy.");
    }
}
