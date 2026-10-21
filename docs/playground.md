# Playground and virtual DevTools

The playground uses **TypeScript and Snabbdom**, with Monaco as an editor component.
Open [the live playground](https://maxfridbe.github.io/dotnetdide/), enable
**Interactive sites**, select **Host an interactive site**, run it, and choose
**Open / Reload**. Click the site's buttons to send GET and POST requests to C#.

The **Network** panel records these real virtual HTTP exchanges. It also records
the isolated site's request during **Run all checks**, labeled `Checks` in the
request details. It does not record runtime downloads or outbound `HttpClient`
requests; use your browser's own developer tools for external network traffic.
Virtual ports use messages, not OS sockets.

[Desktop capture](images/network-debugger.png) · [Phone capture](images/network-debugger-mobile.png)

## Console, assemblies and application debugging

Select **Console** beside **Network** in Virtual DevTools. It shows C# stdout and
stderr, compiler/runtime failures, Wire ILogger output, and hosted iframe console
messages, uncaught errors and rejected promises. Filter by level or text/source,
expand stack traces, pause capture, clear, preserve across resets, copy matching
entries, or **Export debug info** to download both network and console data.

[Desktop Console](images/console-debugger.png) · [Phone Console](images/console-debugger-mobile.png) · [Assemblies](images/assemblies-debugger.png)

The **Assemblies** tab lists real loaded .NET assemblies with names, versions, full
identities, filtering and export. It updates after runtime operations, including
NuGet loads; the snapshot remains available after stopping a worker.

The **Debug your application** example demonstrates `Dotnet.GetDebugInfo()` in a
later C# submission and saves `debug-info.json` in workspace files. The JavaScript
host can read live snapshots using `runtime.GetDebugInfo()` / `getDebugInfo()`.
Both APIs read the same store used by this UI. Step 2 prints console messages,
stack traces, virtual requests and an assembly count to the **Output** terminal.
Click **Dump logs** beside Output to print a complete JSON snapshot without
running C#, changing the editor, or starting/resetting a worker. It works while
running and after Stop, includes all captured entries regardless of panel filters,
and does not record its own dump back into the log. Raw result / diagnostics shows
the same JSON; **Console → Export debug info** downloads it. A web page cannot
write to a native OS terminal; C# uses `Console.WriteLine` for captured output. See [llm.md](../llm.md#debug-your-own-application)
for a complete self-debugging recipe and schema. Console capture is diagnostic
output, not a JavaScript REPL or access to native browser developer tools.

## Controls

- **DevTools** in the header toggles the debugger. **Alt+N** works throughout the
  page. F12 also toggles it when the browser delivers that key to the page; some
  browsers reserve F12 for their native tools.
- Select a request to inspect **Headers**, **Payload**, **Response**, and **Timing**.
  Arrow keys move between request rows. Response previews are inert text; HTML
  and scripts are never executed inside the debugger.
- Filter by URL, method, port, status, or initiator; choose **Errors** or **Pending**.
- The red recording button pauses capture without blocking actual requests.
  **Clear** removes the log. **Preserve log** keeps entries across runspace creation
  and variable resets. Stopping a worker keeps captured entries for inspection.
- Dock at the bottom or right using the title-bar toggle. Drag the pane edge to
  resize, or focus the divider and use arrow keys. Small screens use bottom docking.
- **Copy** copies the selected request. **Export** downloads the captured requests
  as JSON, including response previews and timing.

Timing covers the requestSite call: worker queuing, handler execution and message
transfer. There are no synthetic DNS, TCP or TLS phases. Size means the decoded
response-body size, not a compressed transfer size.

The log is memory-only and limited to 200 requests. Each body preview is limited
to 16 KiB; binary bodies have a short hex preview. Known credential headers and
sensitive query parameters are redacted. Bodies and arbitrary custom headers can
still contain private data; inspect an export before sharing it. Refreshing loses
the log, variables and workspace files. Export files separately to preserve them.

## Reuse the Snabbdom component

Published sites and versioned ZIPs include compiled ES modules, type declarations,
source maps, vendored Snabbdom and `network-debugger.css`. The debugger has no
global fetch interception and does not create or grant permissions to a runspace.

```js
import {h, init, attributesModule, propsModule, eventListenersModule,
  classModule, styleModule} from "./playground/vdom.js";
import {VirtualNetworkDebugger} from "./playground/network-debugger.js";
import {loadDotnetdide} from "./dotnetdide.js";

const patch = init([attributesModule, propsModule, eventListenersModule,
  classModule, styleModule]);
let tree = document.querySelector("#debugger");
const monitor = new VirtualNetworkDebugger(() => {
  tree = patch(tree, monitor.view());
});
tree = patch(tree, monitor.view());

const runtime = await loadDotnetdide({siteAccess: true,
  diagnostics: monitor.diagnostics, debugScope: "My application"});
// createWirePreview({iframe, port: 8080,
//   request: value => runtime.requestSite(value), onConsole: runtime.recordConsole});
const snapshot = runtime.GetDebugInfo(); // Same network and console entries as the UI.
// On host teardown: monitor.dispose(); runtime.terminate();
```

Load `network-debugger.css` in the host page. The host owns the patch loop and
placement; call your render function on window resize to update responsive docking.
Use `monitor.diagnostics.beginRunspace()` at reset/new-worker boundaries to honor Preserve
log. Recording returns the original response and rethrows transport failures.
For a custom UI, import `VirtualNetworkLog` from `playground/network-log.js`.

## Build without Node.js

`dotnet publish src/DotnetDide.Browser -c Release -o artifacts/site` builds
`tools/DotnetDide.WebBuild` and compiles the playground's TypeScript with the
official **native TypeScript compiler** (`tsgo`). No Node.js, npm install, JS
bundler or package-manager dependency tree is required.

`build/typescript-native.json` pins the compiler version and SHA-512 archive
integrity for Linux, macOS and Windows (x64/arm64). The first build downloads that
platform's archive; verified binaries are cached in `.toolchain/typescript`.
Subsequent builds work without another compiler download. NuGet/runtime restore
has its normal separate requirements.

Snabbdom's prebuilt ES modules and MIT license are checked in under
`wwwroot/vendor/snabbdom`; provenance is recorded in `build/snabbdom.json`.
Monaco is also vendored. Strict type checking must pass before publishing.
Generated playground modules go through `obj/` into `wwwroot/playground` in the
published output; source TypeScript lives in the corresponding source directory.

Run the native and real-browser tests using the commands in the README.
`VirtualNetworkTests` covers recording and privacy limits, failure propagation,
actual iframe GET/POST traffic, details, filters, docking, resize, export, and
mobile layout. Existing tests continue to exercise Monaco, all examples and stop/reset.
