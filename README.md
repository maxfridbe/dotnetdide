# dotnetdide

**[Try it now — C# playground](https://maxfridbe.github.io/dotnetdide/)** ·
Editable examples, downloadable files and real browser runtime checks.

[![dotnetdide browser playground](docs/images/playground.png)](https://maxfridbe.github.io/dotnetdide/)

A .NET 11 browser runspace with persistent C# submissions and a shared virtual
filesystem, inspired by [WasmSharp](https://github.com/JakeYallop/WasmSharp).

**No npm, Node.js, TypeScript build, Comlink, or Playwright dependencies.** The
compiler, runspace, filesystem and tests are .NET. Plain browser JavaScript only
loads the runtime, carries worker messages and presents the small demo.

## What works

- Compile and execute C# inside the browser with Roslyn on .NET 11 WebAssembly.
- Keep variables, imports, types and functions between submissions; await async code.
- Capture stdout, stderr, expression values, compiler diagnostics and runtime errors.
- Read/write text and binary files from either `System.IO` or the browser `FS` API.
- List, inspect, rename and remove files; preserve empty directories in snapshots.
- Export/import JSON workspace snapshots for host-managed persistence.
- Isolate each runspace in its own worker. Terminate a worker even during an infinite loop.
- Reset variables while retaining files, or start a fresh worker for a full reset.

## Available .NET libraries

See the [full runtime assembly list](docs/runtime-assemblies.md) for the libraries
included in the pinned .NET 11 build: **185 assembly references**, including
**164 `System.*` assemblies**. They are already referenced by the C# compiler;
use their namespaces directly in submissions without a separate loading step.

The inventory includes collections, LINQ, JSON, regular expressions, XML,
compression, HTTP and `System.IO`. File operations use the same virtual filesystem
as the browser `FS` API. Assembly presence does not guarantee browser support for
every API; native OS facilities may throw `PlatformNotSupportedException`.
Additional NuGet packages cannot currently be installed at runtime.

## Browser playground

The [GitHub Pages testing page](https://maxfridbe.github.io/dotnetdide/) includes
examples for persistent variables, LINQ, JSON, System.IO, ZIP archives, async code,
compiler diagnostics and runtime exceptions. Select an example, edit it and click
**Run C#** (or Ctrl/⌘ + Enter). The variables example has two steps to run in order.
**Run all checks** verifies every example in a separate workspace without changing
your interactive session. Expected compiler/runtime errors count as passing checks.

The editor uses Monaco with C# syntax highlighting, line numbers, bracket matching,
find/replace, undo/redo and Roslyn diagnostics after each run. Ctrl/⌘ + Enter runs
the current code; Ctrl+M toggles whether Tab indents or moves focus. C# semantic
IntelliSense and automatic formatting are not provided by this integration.
Monaco 0.57.0 is bundled as prebuilt browser assets (including its workers), so the
site has no editor CDN dependency and still needs no Node.js/npm build step.
If Monaco fails to load, a plain text editor remains available.

The first run downloads approximately 46 MiB of runtime/compiler assets. Files and
variables are in memory; export a files snapshot before refreshing. You can restore
files from that snapshot, but variable state is not serialized.

Pushes to `master` build and test the actual WebAssembly site before deploying it
to GitHub Pages. In repository **Settings → Pages → Build and deployment**, select
**GitHub Actions** as the source. The same page can be served locally below.

## Build and test

Install the SDK pinned in `global.json`: **11.0.100-rc.2.26504.105**. This is a
.NET 11 release candidate; the pin is intentional. Official downloads:
[.NET 11](https://dotnet.microsoft.com/en-us/download/dotnet/11.0).

```sh
dotnet workload install wasm-tools
dotnet restore DotnetDide.slnx --locked-mode
dotnet test tests/DotnetDide.Core.Tests --no-restore
dotnet publish src/DotnetDide.Browser -c Release -o artifacts/site --no-restore
CHROME_BIN=/path/to/chrome dotnet test tests/DotnetDide.Browser.Tests --no-restore
dotnet run --project tools/DotnetDide.Server -- artifacts/site/wwwroot 5080
```

Open **http://localhost:5080** to try the C# runspace. Any static HTTPS host can
serve `artifacts/site/wwwroot`; no application server is needed for execution.
Subdirectory hosting is covered by browser tests. The included .NET server is
only a local static-file development server.

Browser tests launch an installed Chrome/Chromium and speak DevTools Protocol
using .NET `ClientWebSocket`. They fail if the browser or published files are
missing; they never silently substitute a mock runtime.

## Embed

```js
import { loadDotnetdide } from './dotnetdide.js';

const runtime = await loadDotnetdide();
await runtime.runCSharpAsync('int answer = 40;');
const result = await runtime.runCSharpAsync('answer + 2');
console.log(result.value); // "42"

await runtime.FS.mkdir('data');
await runtime.FS.writeFile('data/input.txt', 'hello');
await runtime.runCSharpAsync(`
    File.WriteAllText("data/output.txt", File.ReadAllText("data/input.txt").ToUpperInvariant());
`);
console.log(await runtime.FS.readFile('data/output.txt', { encoding: 'utf8' }));

const snapshot = await runtime.exportWorkspace();
// Store snapshot with your session (IndexedDB, a downloaded file, etc.).
await runtime.reset(); // clears C# variables, keeps files
runtime.terminate();  // discards the entire worker

const resumed = await loadDotnetdide();
await resumed.importWorkspace(snapshot); // files restored; variables are new
```

All browser `FS` calls are asynchronous because their implementation is in .NET
inside the worker. Paths are relative to `/home/dotnetdide` (the C# working
directory), or absolute within that directory. Text uses UTF-8; binary reads
return `Uint8Array`.

See [integration.md](integration.md) for the API and failure semantics, and
[docs/tdd.md](docs/tdd.md) for the tests-first development record.

## Foundation scope

This is an initial runspace SDK, not a replacement for all Pyodide features.
There is no runtime NuGet installer, language server, debugger, stdin bridge or
automatic IndexedDB persistence yet. Native processes and platform-specific
.NET APIs are unavailable in browser WebAssembly. Only files are serializable;
a running C# object graph is not a session snapshot.

The runtime is untrimmed and interpreted so dynamically compiled programs can
use standard library APIs. The initial static site is about 46 MiB before HTTP
compression; size optimization comes after compatibility tests. Loaded submission
assemblies cannot be unloaded individually: replace the worker to reclaim them.

Workspace paths are bounded by the file API, but arbitrary C# can access other
files in its browser runtime and use browser-available network APIs. The worker
is an isolation/lifecycle boundary, not a security boundary for mutually
untrusted code. The native core test library executes trusted test code only.

## Layout and provenance

- `src/DotnetDide.Core`: Roslyn submission chain, results, System.IO workspace.
- `src/DotnetDide.Browser`: .NET WebAssembly exports and browser entry points.
- `tests/DotnetDide.Core.Tests`: native semantic and filesystem tests.
- `tests/DotnetDide.Browser.Tests`: real Chromium/WASM acceptance, driven by .NET.
- `tools/DotnetDide.Server`: optional .NET static development server.

WasmSharp's runtime-assembly metadata and in-memory compiler approach provided
the starting point; this repository supplies the persistent runspace and filesystem
layer. See [NOTICE](NOTICE) for the upstream commit and attribution. Apache-2.0.
