# Playground and virtual Network debugger

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

## Controls

- **Network** in the header toggles the debugger. **Alt+N** works throughout the
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

const runtime = await loadDotnetdide({siteAccess: true});
const send = runtime.requestSite.bind(runtime);
runtime.requestSite = (request, options) =>
  monitor.trace(request, () => send(request, options), "My application");
// Pass this runtime.requestSite into createWirePreview as usual.
// On host teardown: monitor.dispose(); runtime.terminate();
```

Load `network-debugger.css` in the host page. The host owns the patch loop and
placement; call your render function on window resize to update responsive docking.
Use `monitor.log.beginRunspace()` at reset/new-worker boundaries to honor Preserve
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
