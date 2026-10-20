# Embedding dotnetdide

Serve every file from the published `wwwroot` directory on the same origin over
HTTPS (or localhost). Import `dotnetdide.js` from that directory. No bundler is
needed. A strict host CSP must permit same-origin workers, runtime scripts,
WebAssembly compilation and same-origin asset fetches. Configure external network
access according to the code your host intends to run.

## Runtime lifecycle

```js
const runtime = await loadDotnetdide({ signal, startupTimeoutMs: 120000 });
const result = await runtime.runCSharpAsync(code, { signal, timeoutMs: 30000 });
```

Options are optional. Without `timeoutMs`, submissions have no execution deadline.
Calls queue in request order, including file operations and async submissions.
A completed `runCSharpAsync` resolves even for a compilation/runtime failure;
inspect `success`. Transport failures, invalid API calls, aborts and termination
reject the promise.

`result` contains `success`, `stdout`, `stderr`, `value`, `diagnostics`, and `error`.
Expression `value` is a string representation or null, not a live JavaScript proxy.
Diagnostics include ID, message, severity, and one-based line/column. Runtime
errors include the .NET exception type and message. Output is bounded to one
million characters per stream, followed by an explicit truncation marker.

Successful submissions extend the script chain. A compilation error changes
nothing. A runtime error does not add new declarations to the chain, but earlier
variables and filesystem side effects already performed by that code remain.
This is execution, not a transaction. `await` is supported; background tasks not
awaited by a submission are outside its output-capture lifetime.

`reset()` clears script variables/imports/types and keeps files. It does not reclaim
loaded assemblies. `terminate()` immediately kills the worker, rejects pending
calls and discards its variables and files. Aborting or timing out an active
request does the same, including any other queued requests. A pre-aborted request
is rejected without destroying a healthy worker. To resume after termination,
load a new worker and import a previously exported snapshot. Code is never replayed
automatically. There is no transparent cancellation-and-continue guarantee.

## Diagnostics and Console capture

Every loaded runspace owns a bounded diagnostics store. `runtime.GetDebugInfo()`
(or `runtime.getDebugInfo()`) returns a detached JSON-compatible object containing
`version`, `network`, `console`, `assemblies`, `assemblyScope`, and eviction counts in `dropped`. `runtime.listAssemblies()` explicitly refreshes the loaded assembly snapshot. Reading `GetDebugInfo()` is
synchronous and works after `terminate()`. `runtime.clearDebugInfo()` clears both
logs. Subscribe with `runtime.onDebugInfoChanged(callback)`; the returned function
unsubscribes. Observer failures do not affect the program.

`requestSite` records virtual HTTP automatically. `runCSharpAsync` records stdout,
stderr, compiler diagnostics and runtime errors. Site responses also include
`stdout` and `stderr` from their C# handler, including Wire's synchronous ILogger
provider. Connect hosted iframe logging explicitly:

```js
const preview = await createWirePreview({
  iframe, port: 8080, request: value => runtime.requestSite(value),
  onConsole: runtime.recordConsole,
});
const diagnostics = runtime.GetDebugInfo();
```

The preview sends bounded console/error messages over its private MessageChannel;
its origin remains opaque. It buffers early page logs, captures uncaught errors
and rejected promises, and stops forwarding on navigation/destruction. No parent
console monkey-patching or same-origin sandbox permission is required.

Pass `diagnostics: new DebugInfoStore()` to `loadDotnetdide` to share the store with
`new VirtualNetworkDebugger(changed, store)`. Do not additionally wrap requestSite
with `trace()` in that arrangement, or each request will be recorded twice.
`debugScope` labels a runtime's entries. The default store has 500 console entries
and 200 Wire requests; see [the LLM debugging guide](llm.md#debug-your-own-application)
for the schema, snapshot timing, limits and C# `Dotnet.GetDebugInfo()` usage.

## Shared filesystem

The `.NET browser-wasm` runtime provides the in-memory filesystem. `System.IO`
and the exported `FS` calls operate on the same files, not mirrored copies.
C# execution starts in `/home/dotnetdide`. Changing its current directory only
affects that submission; the next submission starts at the workspace root.

| API | Behavior |
| --- | --- |
| `FS.cwd()` | Workspace root path |
| `FS.mkdir(path)` | Create directory and missing parents |
| `FS.writeFile(path, stringOrUint8Array)` | Write UTF-8 text or exact bytes; parent must exist |
| `FS.readFile(path, {encoding: 'utf8'})` | Read text; omit options for `Uint8Array` |
| `FS.readdir(path = '.')` | Sorted names, excluding `.` and `..` |
| `FS.stat(path)` | `{name, isDirectory, size}`; byte size, zero for directories |
| `FS.rename(from, to)` | Move without overwriting an existing destination |
| `FS.unlink(path)` | Remove a file; directories rejected |
| `FS.rmdir(path, {recursive: false})` | Remove a directory; non-empty requires recursive |

The API rejects paths escaping the workspace and symbolic links. It does not
expose the entire runtime filesystem. These restrictions do not sandbox arbitrary
C# `System.IO` calls; each worker still owns its runtime filesystem.

## Session file snapshots

```json
{
  "version": 1,
  "directories": ["outputs", "outputs/empty"],
  "files": [{"path": "outputs/data.bin", "data": "AP+A"}]
}
```

`exportWorkspace()` returns this structure. `importWorkspace(snapshot)` replaces
the workspace. It validates version, canonical relative paths, duplicates,
file/directory conflicts, and base64 before replacing existing data. It stages
contents separately and preserves the old workspace on validation/write failure.
Limits are 4,096 entries and 64 MiB decoded file bytes per snapshot. Directories
are preserved, including empty ones. Runtime file writes themselves are not
quota-managed. Snapshots contain files only, never C# variables or host credentials.

The embedding host decides whether to save snapshots to IndexedDB, a session
record, or downloads. Refreshing an unsaved runspace discards it. Keep the complete
published asset tree together when upgrading the SDK/runtime.

## .NET API

`DotnetDide.Core` exposes `Runspace`, `WorkspaceFileSystem`, `RunResult`, and
`WorkspaceSnapshot`. Supply Roslyn metadata references when creating a runspace.
The browser bridge resolves these from the runtime's real DLL assets, following
WasmSharp's approach. Native code may use trusted platform assemblies as the tests
do. `RunAsync` serializes execution because `Console` and current directory are
runtime-global. File operations, reset and disposal should be issued while idle
from native callers; the browser worker enforces this queue automatically.

The .NET cancellation token supports waiting/compilation cancellation; it cannot
interrupt arbitrary running IL. Browser termination is the hard-stop mechanism.

## Workspace compilation from C#

The C# submission globals expose `Dotnet`. From JavaScript, use the normal ordered
submission API; no extra worker or host process is needed:

```js
await runtime.FS.writeFile('hello.cs', 'Console.WriteLine(args[0]);');
const submission = await runtime.runCSharpAsync(`
    var build = Dotnet.Build("hello.cs");
    var run = await Dotnet.Run("hello.cs", new[] { "browser" });
    run.Success
`);
console.log(submission.stdout); // build logs and program output
console.log(submission.value);  // "True" when the program exits successfully
const dll = await runtime.FS.readFile('bin/hello.dll');
const symbols = await runtime.FS.readFile('bin/hello.pdb');
```

`Run` returns a task and must be awaited. It rebuilds each time and runs the source
entry point directly, including async Main, without blocking the browser thread.
`Build` is synchronous and writes artifacts without executing them. `Run` returns
a program result with `Success`, `Build`, `Stdout`, `Stderr`, `ExitCode` and `Error`.
A nonzero exit or compiler/runtime failure makes that result unsuccessful; the
containing script can still succeed. Inspect the inner result. Build diagnostics
also expose `FilePath` alongside one-based line/column and compiler ID.

No-path calls discover a single project in the current directory, or compile the
directory's C# files if no project exists. Explicit `.cs` paths build that file
alone; explicit `.csproj` paths use the supported project subset. Executables
run in their source/project directory and restore the caller's directory and
console afterward. Ordinary compiled programs do not share interactive variables
or imports. See [llm.md](llm.md) for the exact imports, project properties/items,
artifacts, default behavior and browser limitations. The published site includes
`llm.md` and both runtime inventories for embedding hosts and code-generating agents.

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
Before adding custom request headers, read the
[header errata](docs/networking.md#errata-custom-http-request-headers), including
Firefox preflight failures caused by User-Agent and API-specific header approval.
`bridges.torproject.org` supplies Tor bridge addresses; it is not a proxy endpoint.
Raw `System.Net.Sockets` and `HttpClientHandler.Proxy` remain unsupported in WASM;
`Net.OpenReadAsync` exposes HTTP as an async `System.IO.Stream`.
Native .NET runspace hosts must supply their own network policy.

This switch is a capability control, **not a hostile-code security sandbox**:
arbitrary JS interop/dynamic module imports can reach browser APIs outside these
wrappers. Run trusted code, keep credentials out of runspaces, and enforce network
isolation at the embedding origin/CSP layer when executing untrusted code.

## Media defaults and additional NuGet packages

The runtime bundles StbImageSharp/StbImageWriteSharp, NAudio.Core, and
Concentus/Oggfile. These are available offline after the runtime loads, without
NuGet permission. Downloadable examples are in `wwwroot/examples/`.

Enable optional package downloads separately from general networking:

```javascript
import { loadDotnetdide, clearNugetCache } from './dotnetdide.js';
const runtime = await loadDotnetdide({ nugetAccess: true, networkAccess: false });
await runtime.runCSharpAsync('await DotnetDide.Nuget.Load("Newtonsoft.Json", "13.0.4");');
// Use loaded types in the next submission, or in subsequent Dotnet.Build/Run calls.
await runtime.runCSharpAsync('Newtonsoft.Json.JsonConvert.SerializeObject(new { answer = 42 })');
await runtime.setNugetAccess(false); // Keep variables/files; deny additional loads.
await clearNugetCache(); // Clear persistent archives, not already loaded assemblies.
```

NuGet requests go directly to NuGet.org without browser cookies or relay credentials;
allow `https://api.nuget.org` in the embedding CSP's `connect-src`. Exact-version
archives are stored in bounded browser Cache Storage (32 archives / 64 MiB), with
network fallback if caching is unavailable. They are not included in session snapshots.

See [media and package guide](docs/media-and-packages.md) and [llm.md](llm.md) for
complete code examples, result types, version/dependency resolution, unsupported
packages and permission behavior. Serve the bundled guides alongside the SDK.

## Interactive ASP.NET sites

Pass `siteAccess: true` to `loadDotnetdide` to enable virtual servers. The default
is false. `setSiteAccess(false)` stops them, `listSites()` lists ports, and
`requestSite({port, method, path, headers, body}, {signal, timeoutMs})` dispatches
buffered HTTP to C# (body is base64). Calls share the runtime queue.

Use `createWirePreview({iframe, port, path, request})` for interactive HTML. Keep
`wire-preview.js` and `wire-preview-host.html` beside the runtime. The helper
uses an opaque sandbox with script/form events; the parent must restrict iframe
navigation with `frame-src 'self'`. Its separate document CSP allows generated
code without relaxing the parent script policy. Export session files after mutating
requests. See [Wire guide](docs/wire.md) for the complete runnable example, lifecycle,
request limits and supported browser features.

## Optional virtual Network inspector

The playground ships a reusable **TypeScript/Snabbdom** component at
`playground/network-debugger.js`, with type declarations and
`network-debugger.css`. Wrap `runtime.requestSite` with `monitor.trace` to observe
real Wire traffic without changing responses or replaying requests. Pass the
wrapped method to `createWirePreview` to include iframe requests. The host owns
the patch loop, layout and runtime permissions. See [component setup and log
limits](docs/playground.md#reuse-the-snabbdom-component). No Node.js is needed
when embedding the published modules.

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
