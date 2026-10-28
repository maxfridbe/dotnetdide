# Browser HTTP, proxies and Tor

`Dotnetdide.Net` and ordinary `System.Net.Http.HttpClient` share the host's network
permission and HTTP route. Network access defaults to **off**. File operations
remain local to the runspace. `System.IO.Stream` can read an HTTP response using
`await Net.OpenReadAsync(url)`; `System.IO` is not a raw networking API.

## HTTP methods and browser options

| API | Result / behavior |
| --- | --- |
| `GetStringAsync`, `GetBytesAsync`, `GetJsonAsync<T>` | Decoded value; throw on unsuccessful HTTP status |
| `PostJsonAsync(url, value)` | JSON request, text response; throws on unsuccessful HTTP status |
| `HeadAsync`, `OptionsAsync`, `DeleteAsync` | Response with status and headers |
| `PostAsync`, `PutAsync`, `PatchAsync` | Supply `HttpContent` for text, binary, JSON or multipart uploads |
| `PostFormAsync(url, fields)` | URL-encoded form; returns response |
| `RequestAsync(method, url, content?, options?, cancellationToken?)` | Generic method; owns content; caller disposes response |
| `SendAsync(request, options, cancellationToken?)` | Custom headers and fetch settings; caller owns request and response |
| `OpenReadAsync(url)` | Async HTTP `Stream`; dispose after reading |
| `DownloadFileAsync(url, path)` | Save to virtual filesystem; replace destination only on success |

All helpers accept a cancellation token. Response-returning methods preserve error
statuses (e.g. 404/409) for inspection rather than throwing automatically.

```csharp
using System.Net.Http;
using Dotnetdide;
using (var request = new HttpRequestMessage(HttpMethod.Patch, "https://api.example/items/42"))
{
    // Only if this API requires X-Client and allows it in its CORS preflight.
    request.Headers.Add("X-Client", "dotnetdide");
    request.Content = new StringContent("{\"done\":true}", System.Text.Encoding.UTF8, "application/json");
    using (var response = await Net.SendAsync(request, new BrowserRequestOptions
    {
        Credentials = "omit", Mode = "cors", Redirect = "error", Cache = "no-store"
    }))
    {
        Console.WriteLine((int)response.StatusCode);
        Console.WriteLine(await response.Content.ReadAsStringAsync());
    }
}
await Net.DownloadFileAsync("https://api.example/result.csv", "result.csv");
// The embedding host can expose result.csv in its session Files pane.
```

Direct fetch accepts `Credentials` = `omit` / `same-origin` / `include`, `Mode` =
`cors` / `same-origin`, and `Redirect` = `follow` / `error` / `manual`.
`Cache` accepts the fetch cache values; `only-if-cached` requires `same-origin`.
Use `include` only with an endpoint you trust: credentialed CORS requires explicit
origin and credential permission from that server. Browser cookie policy still
applies. Custom methods/headers commonly trigger a CORS preflight. Response headers
must be CORS-exposed by the destination to be readable. `no-cors` is deliberately
rejected because opaque responses cannot be read. Manual cross-origin redirects
can also produce unreadable opaque responses. There is no CORS bypass flag.

### Errata: custom HTTP request headers

Leave `User-Agent` to the browser for direct requests. Avoid desktop-style code
such as `http.DefaultRequestHeaders.Add("User-Agent", "my-app")`: Firefox may
preflight that non-safelisted header, and the destination may reject it. Chrome
can silently drop a custom User-Agent instead, so a Chrome-only test can miss the
failure. See [MDN's User-Agent note](https://developer.mozilla.org/en-US/docs/Glossary/Forbidden_request_header).

Custom headers are supported when the destination permits them. Earlier examples
using `X-Client` assume the server approves that header and the request method;
they do not establish that arbitrary APIs accept extra headers.

- Add only API-required headers, preferably per `HttpRequestMessage` instead of
  shared `DefaultRequestHeaders` across unrelated endpoints.
- `Authorization`, `X-*` headers and JSON Content-Type can require a preflight.
  The destination must approve the origin, method and requested headers.
  `Authorization` must be explicitly named in Access-Control-Allow-Headers.
- Avoid Content-Type on a bodyless GET. Leave `Host`, `Origin`, `Cookie` and
  `Content-Length` to the browser.
- Access-Control-Allow-* belongs in server responses; adding it to a request
  does not grant access. Network permission and `no-cors` cannot bypass CORS.

See [CORS header rules](https://developer.mozilla.org/en-US/docs/Glossary/CORS-safelisted_request_header)
and [server header approval](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Access-Control-Allow-Headers).

This was reproduced with a compiled Wire route called from the opaque iframe:
Open-Meteo's geocoding request failed in Firefox with the custom User-Agent and
succeeded after removing it. Keep the original exception message when diagnosing
fetch failures; a successful network self-test does not establish that every API
accepts your program's headers. Also validate response content: a successful HTTP
status may still contain no location matching a ZIP code.

## Optional native .NET relay

A browser cannot open raw SOCKS/TCP connections or set `HttpClientHandler.Proxy`.
Instead, deploy the **optional ASP.NET Core relay** included in the release ZIP's
`relay/` directory. It forwards HTTP(S) to explicitly allowed target origins,
optionally through an upstream HTTP(S) or SOCKS5 proxy. No Node.js is used.

The relay requires a .NET 11 ASP.NET Core runtime (or SDK) compatible with the
pinned RC2 build. The static WASM site itself requires neither a server nor an SDK.
Run from the extracted release directory, or publish from source:

```sh
# Source build, if not using the published relay/ directory:
dotnet publish tools/DotnetDide.Relay -c Release -o artifacts/relay

# Use the exact page ORIGIN: no path and no trailing slash. Separate entries by ;.
export DOTNETDIDE_RELAY_ORIGINS='https://maxfridbe.github.io'
export DOTNETDIDE_RELAY_TARGETS='https://api.example;https://downloads.example'
export DOTNETDIDE_RELAY_TOKEN="$(openssl rand -hex 32)"
# Optional: omit for native direct HTTP. Ambient system proxy settings are ignored.
export DOTNETDIDE_RELAY_PROXY='socks5://127.0.0.1:9050'
# From a release ZIP; for source builds use artifacts/relay/DotnetDide.Relay.dll.
dotnet relay/DotnetDide.Relay.dll --urls http://127.0.0.1:5081
```

For a remote relay, place an HTTPS reverse proxy in front of the loopback listener
and route `/relay` to it. Enter that HTTPS endpoint and the generated token in
Lethetic's **Connections → Runtime & security → .NET HTTP route**. For an SDK host:

```js
const runtime = await loadDotnetdide({
  networkAccess: true,
  relay: { endpoint: "https://your-relay.example/relay", token: relayToken }
});
await runtime.runCSharpAsync(`
    Console.WriteLine(await new System.Net.Http.HttpClient()
        .GetStringAsync("https://api.example/data"));
`);
```

The relay token is distinct from target API credentials. Send target credentials
explicitly in `HttpRequestMessage.Headers`; the relay's own bearer token is never
forwarded to the target. Store browser tokens in memory/tab storage, not session
exports, source code, URLs or static site assets. A missing token still permits
local computation, but relay requests fail. Set the route at runspace creation;
`setNetworkAccess` keeps the chosen route. Lethetic restarts the worker on a route
change and keeps the same session and completed files.

CORS applies between browser and relay. The target need not expose CORS because
that hop runs on the native server. Allowlist the target's exact HTTP(S) origin,
including a non-default port. No wildcard origins or automatic redirects are
accepted. Use only destinations you trust; the operator's target allowlist is the
relay's access policy. HTTPS pages may be unable to reach HTTP localhost because
of mixed-content or local-network browser restrictions; HTTPS relays are preferred.

The relay authenticates every POST and checks the page Origin as well as its target
allowlist. It supports GET, HEAD, OPTIONS, POST, PUT, PATCH and DELETE. It buffers at
most **8 MiB per request/response body**, with a **30-second total upstream timeout**
and **15-second connection timeout**. Request disconnect/cancellation aborts the
upstream operation. Body streams from relayed HTTP are buffered, not live streaming.
Browser cookies, `Set-Cookie`, proxy/hop-by-hop headers and browser-controlled
origin headers are not forwarded. `credentials: include` is rejected on this route.
WebSocket, EventSource, WebTransport and XHR are blocked when the HTTP relay is set;
use HTTP fetch/HttpClient. Redirects produce a relay error: request the final
allowlisted URL explicitly. **Relay failure never falls back to direct requests.**

The route covers user HTTP after runtime bootstrap. It does not proxy runtime
asset downloads, embedding-page/model requests, or another runtime such as Pyodide.
This is not a whole-browser Tor route or a hostile-code sandbox: arbitrary JS
interop/dynamic imports can escape cooperative wrappers. Browser/system networking
or external isolation is needed for whole-application routing.

### Relay v1 protocol

`POST /relay`, `Content-Type: application/json`, `Authorization: Bearer <token>`;
Origin must match the configured page origin. The body is
`{url, method, headers: {name: value}, bodyBase64: string|null}`. Null means no body;
GET/HEAD bodies are rejected. A successful relay exchange returns HTTP 200 with
`{status, headers: {name: value}, bodyBase64}` including target HTTP errors. Relay
errors use 400/401/403/413/502/504. Bodies are binary-safe base64. The outer response
uses `Cache-Control: no-store`; target response headers appear inside the envelope.
CORS preflight allows POST and authorization/content-type only. The protocol is
bounded HTTP forwarding, not a CONNECT tunnel or generic public proxy.

## Tor bridges and SOCKS

**https://bridges.torproject.org/ is a bridge distribution service, not a proxy
endpoint.** A bridge line (obfs4, WebTunnel, etc.) configures a Tor client and its
appropriate pluggable transport; it cannot be used as an HTTP relay URL.

1. Obtain a bridge through [Tor's official bridge instructions](https://support.torproject.org/tor-browser/circumvention/getting-bridges/).
2. Configure that bridge and the required transport in your Tor client, following
   its documentation. This project does not install Tor or interpret bridge lines.
3. Point `DOTNETDIDE_RELAY_PROXY` to that client's SOCKS5 listener (commonly
   `socks5://127.0.0.1:9050`; use the actual configured port).
4. Put the relay's HTTPS `/relay` URL and relay token in the browser settings.

The native SOCKS5 path sends the destination hostname to the proxy for remote DNS;
this is covered by an integration test. HTTP(S) upstream proxies and optional
`username:password@host:port` proxy credentials are supported server-side. Treat
proxy configuration as a server secret; do not copy it into browser/session settings.
No live Tor connectivity or anonymity guarantee is implied by the SOCKS test.

## Platform references

- [Microsoft: browser HttpClient, fetch and CORS](https://learn.microsoft.com/en-us/aspnet/core/blazor/call-web-api)
- [.NET browser HTTP handler implementation](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Net.Http/src/System/Net/Http/BrowserHttpHandler/BrowserHttpHandler.cs)
- [.NET native SOCKS proxy support](https://devblogs.microsoft.com/dotnet/dotnet-6-networking-improvements/)
- [Fetch methods, credentials and response visibility](https://developer.mozilla.org/en-US/docs/Web/API/Fetch_API/Using_Fetch)

### Diagnosing outbound HTTP failures

A target HTTP 403 means the destination rejected the request; it does not prove rate limiting or that Network access is off. A relay HTTP 502 means the relay could not complete forwarding. Preserve the bounded, sanitized relay explanation (DNS/connect/TLS/timeout/policy) instead of guessing about ad blockers or CORS. Tor exit addresses may be rejected, but do not claim that without evidence and never bypass Tor with direct HTTP. Relay requests omit browser cookies and do not execute website JavaScript; a challenge page requires a different authorized source/API. Save successful search results to session files before issuing more searches; repeated requests may be rejected.
