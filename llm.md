# Writing C# for dotnetdide

dotnetdide runs .NET 11 (currently RC2) and Roslyn inside a browser worker. Generate
C# programs that solve the user's task and save useful source files and outputs in
the workspace. The user can download those files from the web interface. Code is
compiled to .NET IL and executed by the browser runtime; this is not a native OS
shell, a full installed .NET SDK, or a WebAssembly AOT compiler.

## Debug your own application

You can inspect your application's real console output and virtual HTTP requests.
Use the same diagnostics that power the playground's **DevTools → Network / Console / Assemblies**
panels; do not guess an error from a blank preview. Enable **Interactive sites** to
host a site. Reading diagnostics itself needs no additional permission and does
not enable external networking.

1. Run the program or site, then reproduce the failing action in its iframe or
   call a route through the host's `runtime.requestSite(...)`.
2. After that operation completes, run a **new C# submission** to inspect the
   captured diagnostics:

```csharp
using System.Text.Json;
var debugJson = Dotnet.GetDebugInfo();
using (var debug = JsonDocument.Parse(debugJson))
{
    foreach (var entry in debug.RootElement.GetProperty("console").EnumerateArray())
    {
        if (entry.GetProperty("level").GetString() == "error")
            Console.WriteLine($"{entry.GetProperty("source")}: {entry.GetProperty("message")}");
    }
    foreach (var request in debug.RootElement.GetProperty("network").EnumerateArray())
        Console.WriteLine($"{request.GetProperty("method")} {request.GetProperty("path")} → {request.GetProperty("status")}");
}
File.WriteAllText("debug-info.json", debugJson); // Downloadable from workspace files.
```

3. Inspect failing status codes, `error`, `requestBody.text`, `responseBody.text`,
   compiler diagnostics and stack traces. Fix the source, rebuild/run it, reproduce
   the action again, and read a fresh snapshot to verify the fix.

`Dotnet.GetDebugInfo()` returns a JSON **string** supplied by the browser host when
`runCSharpAsync` is called. Await earlier operations first. It includes previous
submissions and iframe activity already delivered to the host, not output still
being produced by the current submission. `Dotnet` is the interactive helper;
compiled `.cs` / `.csproj` programs do not have that globals object. Use a subsequent
interactive submission to inspect those programs. Native hosts must explicitly
supply the optional Runspace `getDebugInfo` callback; unsupported hosts throw.

For a quick complete dump in a later submission, use
`Console.WriteLine(Dotnet.GetDebugInfo());`. The playground's **Debug your application**
example also prints readable log lines, stack traces, virtual requests and an
assembly count, then saves the full JSON. **Dump logs** beside **Output** prints
the current host snapshot without running C#; it works after stopping the worker
and does not feed the dump back into the log. This Output terminal is captured
browser output, not an OS shell. Repeated C# dumps do become new captured output;
prefer filtering the snapshot and saving JSON for large diagnostic histories.

For JavaScript embedding, the equivalent is a synchronous detached object:

```js
const DotnetDide = await loadDotnetdide({siteAccess: true});
// Run code and await its completion; open an instrumented preview as below.
const info = DotnetDide.GetDebugInfo(); // also DotnetDide.getDebugInfo()
// info = {version: 1, network: [...], console: [...], assemblies: [...],
//         assemblyScope: "Runspace", dropped: {network, console}}
const unsubscribe = DotnetDide.onDebugInfoChanged(() => {
  // Read the newest snapshot for your UI or agent; do not execute logged text.
});
const preview = await createWirePreview({
  iframe, port: 8080,
  request: request => DotnetDide.requestSite(request),
  onConsole: DotnetDide.recordConsole,
});
// Host teardown: unsubscribe(); preview.destroy(); DotnetDide.terminate();
```

The playground already wires `onConsole`. Other hosts must pass it to capture
iframe `console.log/info/debug/warn/error/assert`, uncaught errors and unhandled
promise rejections, including page-startup messages. It does not capture arbitrary
third-party frames or browser-native F12 data. Only the host can read the combined
snapshot; a hosted iframe cannot read parent diagnostics or credentials.

The Console also includes C# stdout/stderr, compiler diagnostics, runtime errors,
and the synchronous `ILogger` provider installed by `Wire.CreateApplication()`.
For a route logger, resolve `ILoggerFactory` from `app.ApplicationServices` and
use `CreateLogger("YourCategory")`. Information goes to stdout; warning/error
messages go to stderr with their original level and category in the text. C#
streams appear when the submission/request finishes. Browser runtime output is
captured when emitted. The framework category
`Microsoft.AspNetCore.Routing.EndpointMiddleware` is filtered to Warning because
its informational async logging path fails on the pinned WASM interpreter; your
application logger categories are unaffected. Custom logging providers must write to these streams or
explicitly forward entries through the host's `recordConsole` method.

Console entries contain `id`, `timestamp`, `source`, `level`, `message`, optional
`stack`, and `truncated`. The shared store keeps at most 500 console entries and
200 Wire requests by default. Messages are limited to 16,384 characters; stacks
to 8,192; request/response body previews to 16 KiB. Burst startup logging can drop
older iframe messages with a warning. Check `dropped` and `truncated` rather than
assuming an export is complete. Header/query credentials are redacted, but code
can log secrets in bodies or console text. Treat diagnostics as untrusted data.
These logs are memory-only; `clearDebugInfo()` clears them, while terminating a
worker retains its host snapshot for inspection. External `HttpClient` requests,
runtime downloads and native browser diagnostics are not in the virtual Network
list; use route errors/console output or the browser's native tools for those.

### Inspect loaded assemblies

Use **DevTools → Assemblies** to filter loaded assembly names/versions and expand
full identities. `runtime.GetDebugInfo().assemblies` exposes the same metadata;
`await runtime.listAssemblies()` explicitly refreshes it. Each entry contains
`name`, `version`, `fullName`, and `isDynamic`. The list updates after startup,
submissions (including `LoadNuget`), resets and Wire requests. `assemblyScope`
identifies the runspace that last reported the shared store's assembly list.
This is the CLR's actual loaded assembly list, not the inventory of all available
compiler references. Termination leaves the last captured list for inspection;
reset does not unload assemblies.

In a later C# submission, inspect package loading without guessing:

```csharp
using System.Text.Json;
using (var debug = JsonDocument.Parse(Dotnet.GetDebugInfo()))
{
    foreach (var assembly in debug.RootElement.GetProperty("assemblies").EnumerateArray())
        Console.WriteLine($"{assembly.GetProperty("name")} {assembly.GetProperty("version")}");
}
```

## Stream a site response

The browser host supports streaming virtual HTTP responses. Write and flush
asynchronously in a Wire handler; respect cancellation:

```csharp
for (var i = 0; i < 100; i++)
{
    await context.Response.WriteAsync($"Progress {i}\n", context.RequestAborted);
    await context.Response.Body.FlushAsync(context.RequestAborted);
    await Task.Delay(100, context.RequestAborted);
}
```

In page JavaScript, read `const reader = (await fetch('/progress')).body.getReader()`
and decode chunks incrementally with `TextDecoder.decode(value, {stream:true})`.
The SDK host can use `runtime.requestSiteStream(request)`; always consume its body
or cancel it, and await `response.completed` before inspecting final diagnostics.
Use async output: synchronous writes have only 64 KiB of staging space. Default
limits are 16 MiB/request and 64 MiB/response; the trusted SDK host may configure
`siteResponseLimitBytes` up to 512 MiB. Streaming uses <=64 KiB binary chunks,
backpressure, and a 30-second idle deadline. It does not enlarge workspace quotas.
Requests remain serialized; files checkpoint after completion in Lethetic.
HTML/media preparation and legacy `requestSite` still buffer complete bodies;
Python sites in Lethetic also remain buffered. See [Wire limits](docs/wire.md#streaming-and-transfer-limits).

## Default imports

Interactive submissions (`runCSharpAsync`) automatically import exactly:

```csharp
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
```

Additional `using` directives, variables, types and functions from successful
submissions persist until `reset()` or worker termination. The current directory
starts at `/home/dotnetdide` for each submission. `Dotnet` is a supplied helper
object, not an imported namespace; no `using` is required to call it.

Loose `.cs` files and source-only directory builds through `Dotnet.Build/Run`
receive the same five imports as generated global usings. They are regular C#
programs, not script submissions: include top-level statements or a valid static
`Main`, and terminate statements with semicolons. They cannot see interactive
variables, functions or extra imports. They share workspace files.

For `.csproj` builds, imports follow that project's settings instead:

- With `ImplicitUsings` absent or disabled, no namespaces are automatically imported.
- With `<ImplicitUsings>enable</ImplicitUsings>`, the generated global imports are
  `System`, `System.Collections.Generic`, `System.IO`, `System.Linq`,
  `System.Net.Http`, `System.Threading`, and `System.Threading.Tasks`.
- Add ordinary `using` directives to source files, or project items such as
  `<Using Include="System.Text.Json" />` to import a namespace globally.

## Additional namespaces you can import

All published runtime assemblies are already supplied to Roslyn as references.
Import a namespace using its C# name; do not append `.dll` or install a package
for built-in libraries. Imports do not recursively import child namespaces.

| Task | Common additional namespaces |
| --- | --- |
| Text and encoding | `System.Text`, `System.Text.RegularExpressions`, `System.Globalization` |
| JSON | `System.Text.Json`, `System.Text.Json.Nodes`, `System.Text.Json.Serialization` |
| XML | `System.Xml`, `System.Xml.Linq`, `System.Xml.Serialization`, `System.Xml.XPath` |
| Archives | `System.IO.Compression`, `System.Formats.Tar` |
| Collections | `System.Collections`, `System.Collections.Concurrent`, `System.Collections.Immutable`, `System.Collections.ObjectModel` |
| Numeric and binary data | `System.Numerics`, `System.Buffers`, `System.Buffers.Binary` |
| Async coordination | `System.Threading`, `System.Threading.Channels` |
| Browser HTTP requests | `System.Net.Http`, `System.Net.Http.Headers`, `System.Net.Http.Json` |
| Hashing and cryptography | `System.Security.Cryptography` |
| Reflection and attributes | `System.Reflection`, `System.ComponentModel`, `System.ComponentModel.DataAnnotations` |
| C# parsing and compilation | `Microsoft.CodeAnalysis`, `Microsoft.CodeAnalysis.CSharp`, `Microsoft.CodeAnalysis.CSharp.Syntax` |

For all namespace names present in the pinned runtime metadata, see the
[namespace inventory](docs/runtime-namespaces.md). The separate
[assembly inventory](docs/runtime-assemblies.md) lists DLLs. Namespace availability
does not guarantee that every type or method works in a browser: OS-dependent APIs
can throw `PlatformNotSupportedException`. Direct network requests require the remote
server to allow browser requests (CORS); an optional host relay can forward HTTP. Native processes, arbitrary OS networking,
registry access and native filesystem watchers are unavailable. Additional compatible managed packages can be loaded with the permission-gated
`await DotnetDide.Nuget.Load("name", "version")` API. There is no full MSBuild/NuGet
project restore, ASP.NET server host, or desktop UI framework.

## Build and run workspace files

Inside an interactive submission:

```csharp
File.WriteAllText("hello.cs", """
await Task.Delay(10);
Console.WriteLine($"Hello, {args[0]}!");
File.WriteAllText("result.txt", "42");
return 0;
""");

var build = Dotnet.Build("hello.cs");
if (build.Success)
{
    var run = await Dotnet.Run("hello.cs", new[] { "browser" });
    Console.WriteLine($"Exit code: {run.ExitCode}");
}
```

`Build` is synchronous and does not execute the program. `Run` returns a task:
**always await `Dotnet.Run(...)`** so execution finishes within the submission's
output-capture lifetime. `Run` rebuilds the supplied source before executing it;
it never falls back to an older DLL after a compilation error. Supported entry
points include top-level programs and `Main` returning `void`, `int`, `Task`, or
`Task<int>`, with either no arguments or `string[] args`.

```csharp
var build = Dotnet.Build();                  // discover in current directory
var build2 = Dotnet.Build("app/Demo.csproj");
var build3 = Dotnet.Build("app");            // discover inside this directory
var run = await Dotnet.Run("app/Demo.csproj", new[] { "argument" });
```

With no path or a directory path, the helper selects the single `.csproj` directly
in that directory. Multiple project files produce an error: select one explicitly.
If there is no project, it compiles the directory's `.cs` files recursively as an
executable. A direct `.cs` path compiles only that file. Default discovery skips
`bin`, `obj`, and hidden directories. Empty inputs and escaping workspace paths
produce build diagnostics.

Successful builds create `bin/<name>.dll` and `bin/<name>.pdb` beside the project or
source file. The PDB uses portable symbols. These are managed .NET artifacts, not
standalone WASM applications; no runtime/configuration package is generated.
Each compilation uses a unique internal assembly identity to allow rebuilds in
one worker. Failed builds leave earlier successful files intact, but return null
artifact paths and do not execute those files.

Programs run with their project/source directory as the working directory; the
caller's current directory and console streams are restored afterward. Program
files are visible immediately through `System.IO`, the browser `FS` API and the
workspace download list. Program declarations do not enter the interactive script
chain. `reset()` retains files and the `Dotnet` helper.

### Results and compilation output

Both helpers print compilation output to the submission's stdout, including file,
line, column, diagnostic ID and build status. `Run` also forwards program stdout
and stderr to the containing submission. Inspect the returned result for failures:
the outer submission can succeed while the requested program fails.

| Build result property | Meaning |
| --- | --- |
| `Success` | Compilation and artifact writing succeeded |
| `Output` | Human-readable build log |
| `Diagnostics` | `Id`, `Message`, `Severity`, `FilePath`, one-based `Line` and `Column` |
| `AssemblyPath`, `PdbPath` | Workspace-relative paths on success; null on failure |

| Run result property | Meaning |
| --- | --- |
| `Success` | Build succeeded, no exception and exit code zero |
| `Build` | The build result above |
| `Stdout`, `Stderr` | Program output, excluding the build log |
| `ExitCode` | Program's integer result, or zero for void/Task; null if it did not finish |
| `Error` | Exception `Type` and `Message`, or a missing-entry-point error |

Build input/project errors use diagnostic ID `DND1000`; compiler diagnostics use
Roslyn IDs such as `CS0103`. A library can build successfully but cannot be run.
Program output is bounded to one million characters per stream. Stop/abort of the
browser worker interrupts execution and discards its in-memory state; there is no
OS process to kill and no stdin bridge. Do not use `Environment.Exit`.

## Supported project subset

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

This is a Roslyn project reader, **not MSBuild**. It supports:

- SDK `Microsoft.NET.Sdk` and target `net11.0` (the target defaults to net11.0).
- `OutputType` (`Exe` or `Library`, default Library), `AssemblyName`, `LangVersion`
  (default preview), `Nullable`, `AllowUnsafeBlocks`, `Optimize` (default true),
  `TreatWarningsAsErrors`, `DefineConstants`, `ImplicitUsings` and
  `EnableDefaultCompileItems`.
- Recursive default `.cs` inputs, and `Compile Include`, `Exclude` and `Remove`
  with relative paths, semicolon-separated patterns, `*`, `?` and `**` globs.
  Compile patterns stay inside the project directory. Explicit duplicate inputs
  are errors; use `EnableDefaultCompileItems=false` when listing all sources.
- Simple `Using Include` items for additional global namespaces.

Package/project/assembly references, analyzers, source generators, resources,
content-copy items, other SDKs/targets, conditions, property expansion, imports and
custom targets are not supported. Unsupported explicit project XML fails with a
diagnostic instead of being silently ignored. `Directory.Build.props/targets`,
solutions and `global.json` are not evaluated. SDK-generated target-framework
preprocessor symbols and assembly attributes are not generated; use
`DefineConstants` for conditional compilation. Use the full .NET SDK outside the
browser when these features are needed.

## Practical generation guidance

- Use `System.IO` for files and create parent directories before writing.
- Save reusable source as `.cs` or `.csproj` plus `.cs` files, then build/run it.
- Use top-level `await` in interactive code. For disposable objects at script
  scope, use `using (...) { ... }`; top-level `using var` declarations are not
  accepted by this script mode. Ordinary compiled `.cs` programs support them.
- Do not use `Console.ReadLine`, process launches, native libraries, or shell commands.
- Await work before returning; unawaited background work outlives output capture.
- Download/export important files before refreshing. Files are in memory;
  snapshots preserve files, not live C# variables or loaded assemblies.

## Browser network access

Network access defaults to **off**. The host opts in with
`loadDotnetdide({ networkAccess: true })` and can change it while idle with
`await runtime.setNetworkAccess(false)` without losing variables or files. This gates new HTTP requests; use
`runtime.terminate()` to stop outstanding requests and code immediately.
The setting controls worker `fetch` (including ordinary `HttpClient`), XHR,
WebSocket, EventSource and WebTransport. Runtime assets load during startup
before user code runs. Model/API requests made by the embedding page are separate.

```csharp
var text = await Dotnetdide.Net.GetStringAsync("https://example.org/data.txt");
var bytes = await Dotnetdide.Net.GetBytesAsync("https://example.org/image.png");
File.WriteAllBytes("image.png", bytes);
var reply = await Dotnetdide.Net.PostJsonAsync("https://example.org/api", new { answer = 42 });
// More methods: HeadAsync, OptionsAsync, DeleteAsync, PostAsync, PutAsync, PatchAsync,
// PostFormAsync and RequestAsync(method, url, content, options).
// Custom headers/fetch settings: SendAsync(request, new Dotnetdide.BrowserRequestOptions { Credentials = "omit" }).
// Async stream: OpenReadAsync(url). Save directly: DownloadFileAsync(url, "result.csv").
// Dispose the returned HttpResponseMessage; GetString/GetBytes/PostJson check HTTP status.
```

Use the exact capitalization `Dotnetdide.Net`. `System.Net.Http` is available
with an explicit `using` in scripts. Await network I/O. Direct HTTP obeys destination CORS and browser
mixed-content rules. The host can configure `relay: { endpoint, token }` for an
optional native .NET HTTP relay with SOCKS5 support; ordinary `HttpClient` uses it
too. The relay is included in release ZIPs under `relay/`. See
[HTTP methods, browser options, relay and Tor setup](docs/networking.md).
`bridges.torproject.org` supplies Tor bridge addresses; it is not a proxy endpoint.
Raw `System.Net.Sockets` and `HttpClientHandler.Proxy` remain unsupported in WASM;
`Net.OpenReadAsync` exposes HTTP as an async `System.IO.Stream`.
Native .NET runspace hosts must supply their own network policy.

### Errata: custom HTTP request headers

For direct browser HTTP, do not set `User-Agent`: Firefox may preflight it while
Chrome silently drops it. Leave `Host`, `Origin`, `Cookie` and `Content-Length`
to the browser too. Add only API-required custom headers, preferably on each
`HttpRequestMessage`. `Authorization`, `X-*` headers and JSON Content-Type can
require CORS preflight approval; avoid Content-Type on bodyless GETs.
Access-Control-Allow-* is a server response setting, not a client workaround.
Neither Network access nor `no-cors` overrides destination policy. Preserve
original exceptions, test the affected browser and validate response content.
See [the header errata and corrected example](docs/networking.md#errata-custom-http-request-headers).

This switch is a capability control, **not a hostile-code security sandbox**:
arbitrary JS interop/dynamic module imports can reach browser APIs outside these
wrappers. Run trusted code, keep credentials out of runspaces, and enforce network
isolation at the embedding origin/CSP layer when executing untrusted code.

For disposable resources in script submissions, use `using (...) { ... }` blocks.
Top-level `using var` declarations are not accepted by the script parser; regular
compiled `.cs` programs support them.

## Bundled media and optional NuGet packages

StbImageSharp 2.30.16, StbImageWriteSharp 1.16.7, NAudio.Core 3.3.0,
Concentus 2.2.2 and Concentus.Oggfile 1.0.7 are included by default. They need
explicit namespace imports but no package downloads or permissions. Use
`StbImageSharp.ImageResult.FromMemory(bytes, StbImageSharp.ColorComponents.RedGreenBlueAlpha)`
to decode images, and `new StbImageWriteSharp.ImageWriter().WritePng(rgba, width,
height, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream)` to save PNG.
Qualify ColorComponents because both Stb libraries define it. For WAV use
`NAudio.Wave.WaveFileWriter(path, new NAudio.Wave.WaveFormat(48000, 16, 1))`
and `WriteSamples(floatSamples, 0, floatSamples.Length)`. NAudio 3 sample providers
use `Read(samples.AsSpan())`. Dispose streams before file sync.

For Opus set `Concentus.OpusCodecFactory.AttemptToUseNativeLibrary = false`, create
an encoder with `OpusCodecFactory.CreateEncoder(48000, 1,
Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO)`, then create
`Concentus.Oggfile.OpusOggWriteStream(encoder, stream)`, call `WriteSamples` and
`Finish()`. Save as `.opus`/`.ogg`, not raw codec packets. PNG/JPEG and WAV preview
in Lethetic Files; Ogg Opus playback depends on browser support. Desktop audio,
System.Drawing.Bitmap/Graphics, ImageSharp and SkiaSharp are not bundled.

With host `nugetAccess: true`, first submit:

```csharp
await DotnetDide.Nuget.Load("Newtonsoft.Json", "13.0.4");
```

In the **next submission**, use its types or compile files that import them:

```csharp
Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(new { answer = 42 }));
```

Load returns Name, Version and Packages; omitting the version reuses an existing
version or selects the latest stable one. Pin versions for reproducibility.
Downloads come directly from NuGet.org, independently of general HTTP permission.
Compatible managed dependencies are resolved; native/build-tool packages and
conflicting versions fail. No PackageReference restore is performed by Dotnet.Build.
Exact-version archives are cached in browser Cache Storage, not session files.
Reload packages after worker restart; cached packages still require permission.
Do not request NuGet access for the bundled media libraries.

Read [media and package guide](docs/media-and-packages.md) for complete runnable
image/audio examples, permissions, cache clearing, limits and compatibility.
The release includes `examples/images.cs` and `examples/audio.cs` in wwwroot.

## Interactive sites: ASP.NET IServer

The bundled `DotnetDide.Wire` supports virtual HTTP ports in the current runspace.
The host must enable `siteAccess` (off by default); Network/NuGet are separate.
Add explicit usings for `DotnetDide`, `Microsoft.AspNetCore.Builder` and
`Microsoft.AspNetCore.Http`. `Microsoft.AspNetCore.Routing`, `.Http.Features`,
`.Hosting.Server`, `.Hosting.Server.Features` and `Microsoft.Extensions.DependencyInjection`
are also available. These are **not** added to the default using list.

Use `Wire.CreateApplication()`, middleware, `UseRouting()` and `UseEndpoints` with
`MapGet`/`MapPost` **async HttpContext handlers**. Await
`Wire.ListenAsync(8080, app, "site")`, then return; the host retains the server.
Do not use blocking host Run/WaitForShutdown or infinite loops. For JSON, set
ContentType and `await context.Response.WriteAsync(JsonSerializer.Serialize(value))`;
.NET 11 RC2 WASM fails for the tested automatic object-result/WriteAsJsonAsync path.

Write a reusable .cs program with these explicit usings; run with
`await Dotnet.Run("Site.cs")`, or create a `Microsoft.NET.Sdk` executable csproj and
run its path. Do not add FrameworkReference or use Sdk.Web. Static `webRoot` and
handler working directory are captured relative to the registering program.
`Wire.Stop(port)` removes it before rerunning; reset/reload stops every site.

The user can interact through the host's Sites preview and download request-created
files. Lethetic also offers Run in Files and saves changes after each request.
Use classic JS and fetch to local paths, not ES modules, XHR or WebSockets. For
forms, preventDefault and fetch POST. No cookies, external preview networking or
parent UI/storage access. Outbound C# HTTP remains separately permissioned.

Read [the complete Wire guide](docs/wire.md) and the executable
[notebook sample](https://github.com/maxfridbe/dotnetdide/blob/master/src/DotnetDide.Browser/wwwroot/examples/site.cs) before generating a site. They cover .cs and
.csproj creation, compilation, static files, JSON, embedding and limits. In the
repository the executable sample is at `src/DotnetDide.Browser/wwwroot/examples/site.cs`.

## Playground virtual network debugging

The playground includes a Network panel implemented as a TypeScript/Snabbdom
component. Enable Interactive sites, run a Wire program, and Open / Reload its
virtual port. Requests from the iframe appear automatically. Headers, Payload,
Response and Timing show what reached the C# route and what it returned; error
filters help find 4xx/5xx responses. The panel does not observe outbound HttpClient
traffic or provide C# breakpoints. Use browser developer tools for external HTTP.
The host can export a bounded JSON capture for debugging. Known credential headers
and query parameters are redacted; request/response bodies can still contain user
data. See [the playground guide](docs/playground.md).

## JavaScript LoadNuget example

```js
import {loadDotnetdide} from './dotnetdide.js';
const DotnetDide = await loadDotnetdide({nugetAccess: true, networkAccess: false});
const loaded = await DotnetDide.LoadNuget("Newtonsoft.Json", "13.0.4");
console.log(loaded.name, loaded.version, loaded.packages);
// Use the package only in a subsequent submission, after loading completes.
const result = await DotnetDide.runCSharpAsync(
  'Newtonsoft.Json.JsonConvert.SerializeObject(new { answer = 42 })');
console.log(result.value); // {"answer":42}
// DotnetDide.terminate() when finished.
```

`DotnetDide` above is the JavaScript runspace variable, not a C# namespace method.
Inside C# submissions use `await DotnetDide.Nuget.Load("Newtonsoft.Json", "13.0.4")`
in step 1, then reference the package's types in step 2. The playground's **Load a
NuGet package** example demonstrates those two steps. Enable **NuGet downloads**
first; Run all checks skips this example while that permission is off. Downloads
use the browser and existing bounded Cache Storage, not localStorage. General
network access is independent. `LoadNuget` rejects when permission is disabled or
a package is incompatible; it does not silently grant permission. Already bundled
image/audio defaults remain available without downloading them.

### Diagnosing outbound HTTP failures

A target HTTP 403 means the destination rejected the request; it does not prove rate limiting or that Network access is off. A relay HTTP 502 means the relay could not complete forwarding. Preserve the bounded, sanitized relay explanation (DNS/connect/TLS/timeout/policy) instead of guessing about ad blockers or CORS. Tor exit addresses may be rejected, but do not claim that without evidence and never bypass Tor with direct HTTP. Relay requests omit browser cookies and do not execute website JavaScript; a challenge page requires a different authorized source/API. Save successful search results to session files before issuing more searches; repeated requests may be rejected.
