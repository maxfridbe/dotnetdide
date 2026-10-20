# Interactive sites with DotnetDide.Wire

`Wire` implements ASP.NET Core's `IServer` contract over messages to a browser
worker. Middleware, routing, `HttpContext` and `System.IO` run in .NET 11 WASM.
A virtual port identifies a server in that runspace; it does **not** open a TCP
socket or make `http://localhost:8080` reachable from the browser or another device.

Enable the separate **Interactive sites** permission in the playground or host.
The SDK option is `siteAccess: true`; it defaults to false. Outbound C# HTTP still
requires `networkAccess`, and package downloads require `nugetAccess`. A local
site needs neither of those permissions. Existing image/audio defaults are unchanged.

## A runnable site

The [complete notebook example](https://github.com/maxfridbe/dotnetdide/blob/master/src/DotnetDide.Browser/wwwroot/examples/site.cs) creates HTML, CSS and classic
JavaScript, exposes GET/POST routes, and writes submitted notes to `note.txt`.
Select **Host an interactive site** in the playground, enable **Interactive sites**,
run it, and choose **Open / Reload**. Use **Stop site** before running it again.

This smaller program can be saved as `Site.cs` and run with
`await Dotnet.Run("Site.cs")` from a submission:

```csharp
using System;
using System.IO;
using DotnetDide;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

Directory.CreateDirectory("site");
File.WriteAllText("site/index.html", """
<h1>My C# site</h1>
<button onclick="fetch('/api/hello').then(r=>r.text()).then(t=>document.querySelector('output').textContent=t)">Call C#</button>
<output></output>
""");
var app = Wire.CreateApplication();
app.UseRouting();
app.UseEndpoints(routes => {
    routes.MapGet("/api/hello", async context => {
        await context.Response.WriteAsync("Hello from C#");
    });
});
await Wire.ListenAsync(8080, app, "site");
Console.WriteLine("Ready on virtual port 8080");
```

For a project, put that code in `app/Program.cs` and this in `app/Site.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
</Project>
```

Then `await Dotnet.Run("app/Site.csproj")`. Do not add `FrameworkReference` or use
`Microsoft.NET.Sdk.Web`: the required managed assemblies are already referenced.
See [llm.md](../llm.md) for the supported compilation subset. A program's relative
web root and request working directory are captured when it registers its server.
For this project, `site/index.html` and request-created `note.txt` belong under `app/`.

`ListenAsync` returns immediately. Do not call a blocking host `Run`,
`WaitForShutdown`, or use an infinite loop to keep the server alive. The worker
retains it after the submission/program returns. `Wire.Stop(8080)` stops that
port; the returned `WireServer` also implements `StopAsync` and `Dispose`.

## Routes and supported ASP.NET features

`Wire.CreateApplication()` supplies routing, logging, dependency injection scopes
and diagnostic services. Use `Use`, `UseRouting`, `UseEndpoints` and explicit
`HttpContext` route handlers. Request method/path/query, headers, buffered body,
status, response headers, async response writes, `OnStarting`, `OnCompleted` and
`RequestAborted` are provided. A custom pipeline may register services through
`IApplicationBuilder.ApplicationServices`, and a host can use
`Wire.CreateServer(port, webRoot)` with its own `IHttpApplication<TContext>`.

For JSON, use explicit serialization in a handler:

```csharp
context.Response.ContentType = "application/json";
await context.Response.WriteAsync(
    System.Text.Json.JsonSerializer.Serialize(new { answer = 42 }));
```

.NET 11 RC2 WASM currently throws `InvalidProgramException` for the tested
minimal-API automatic object response/`WriteAsJsonAsync` path. Use the explicit
handler above. The native .NET version supports more paths; native success alone
does not establish WASM compatibility. Full WebApplication hosting, Kestrel,
MVC, Razor and their dependencies are not bundled.

A 404 with no started response falls back to files under `webRoot` for GET/HEAD.
Directory paths serve `index.html`; missing files stay 404. Traversal and symlinks
are rejected. Cookies, authorization and transport framing headers are not imported
from the iframe, and `Set-Cookie` is removed from responses. Keep application
state in the worker or session files rather than browser cookies.

## External APIs from site routes

A page can call `fetch('/api/weather')` and have that C# route call an external
API using ordinary `System.Net.Http.HttpClient`. Enable both `siteAccess` and
`networkAccess`. Site permission alone authorizes only the virtual server and
preview; a disabled network permission produces “Network access is disabled by
the host”. Direct C# HTTP still obeys destination CORS. Preview JavaScript cannot
fetch external URLs directly, even when the C# network permission is enabled.

In Lethetic, enable **Network access** under **Connections → Runtime & security**,
then run the saved site file again: changing this setting restarts its .NET worker.
Files and chat remain in the session. The standalone SDK instead allows
`await runtime.setNetworkAccess(true)` while idle without replacing the worker.

Preserve the original exception details when diagnosing a failure. A generic
fetch error does not distinguish CORS, connectivity, or remote service failures;
do not replace every such error with a claim about an ad blocker. Use
`context.RequestAborted` for outbound calls; the entire route has a 15-second
cooperative deadline, including multiple sequential API calls.

## JavaScript embedding

```js
import { loadDotnetdide, createWirePreview } from './dotnetdide.js';
const runtime = await loadDotnetdide({ siteAccess: true });
// Write Site.cs, then compile/run it, or submit its code directly.
const result = await runtime.runCSharpAsync(source);
if (!result.success) throw new Error(JSON.stringify(result));
console.log(await runtime.listSites()); // [{ port: 8080, webRoot: 'site' }]
const preview = await createWirePreview({
  iframe: document.querySelector('iframe'), port: 8080, path: '/',
  request: value => runtime.requestSite(value),
});
// await preview.navigate('/other.html'); await preview.reload();
// preview.destroy(); // closes only the viewer; the server stays registered
// await runtime.setSiteAccess(false); // stops every site in this worker
```

Serve `wire-preview.js` and `wire-preview-host.html` beside the SDK. The helper
sets `sandbox="allow-scripts allow-forms"` without `allow-same-origin`; do not
weaken it. Form events work, but CSP prevents native form navigation. Use
`preventDefault()` and `fetch` in submit handlers. The preview uses its own
restricted document CSP so the parent need not allow inline scripts. The **parent
page must set `frame-src 'self'`** (or restrict it to the SDK's dedicated origin)
to prevent the generated page navigating its iframe to an external site.

The helper authenticates its message channel by frame window and a fresh token.
Only bounded virtual HTTP messages cross it. The child cannot read the parent's
DOM, storage, session credentials, or runtime reference. Keep model keys out of
runspace files; generated C# has access to that runspace's files. These host API
permissions are not a security boundary for arbitrary hostile C# interop.

The host API also works without a viewer:

```js
const response = await runtime.requestSite({
  port: 8080, method: 'POST', path: '/api/note',
  headers: { 'Content-Type': ['text/plain'] },
  body: btoa('Hello from the host'), // base64 bytes; encode UTF-8 for non-ASCII
});
// { status, headers: { name: [values] }, body: 'base64...', error: null }
```

`requestSite` queues with C# submissions and file operations. Its default deadline
is 20 seconds; abort/timeout terminates the worker. ASP.NET handlers get a 15-second
cooperative `RequestAborted` token. A handler that ignores cancellation is stopped
by the worker deadline, not by native runspace cancellation. Eight servers are
allowed; each request/response body is bounded to 2 MiB, request headers to 64
entries/16 KiB, and paths to 8 KiB. No streaming, SSE, sockets or WebSockets.

## Preview scope and session lifecycle

Classic scripts, linked CSS, inline styles, images, audio/video, local links and
`fetch` to the same virtual port work. Static assets are fetched through C# and
inlined before the page is displayed. Dynamic asset URLs, ES module imports,
CSS `@import`, XHR, workers, service workers and cross-port/external fetch are not
implemented. A transparent browser HTTP origin would require a separately hosted
origin and a service-worker transport; this release uses an isolated message bridge.

The SDK does not persist files automatically. Export a workspace after requests
that modify files and import it when restoring a session. Lethetic does this after
each request, shows created files in Files, and imports edits before the next
request. Its smaller per-file/session quotas still apply. Compiled routes do not
change when their source is edited: stop the port and rerun the program.

Reset, worker termination/reload, mode or session changes stop sites. Revoking
`siteAccess` stops all registered ports immediately when its queued operation runs.
Lethetic restarts the worker on permission changes, retaining completed saved
files and conversation. Sites/variables are never replayed automatically.
