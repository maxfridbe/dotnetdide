# Writing C# for dotnetdide

dotnetdide runs .NET 11 (currently RC2) and Roslyn inside a browser worker. Generate
C# programs that solve the user's task and save useful source files and outputs in
the workspace. The user can download those files from the web interface. Code is
compiled to .NET IL and executed by the browser runtime; this is not a native OS
shell, a full installed .NET SDK, or a WebAssembly AOT compiler.

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
registry access and native filesystem watchers are unavailable. There is no
runtime NuGet installer, ASP.NET server host, or desktop UI framework.

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

This switch is a capability control, **not a hostile-code security sandbox**:
arbitrary JS interop/dynamic module imports can reach browser APIs outside these
wrappers. Run trusted code, keep credentials out of runspaces, and enforce network
isolation at the embedding origin/CSP layer when executing untrusted code.

For disposable resources in script submissions, use `using (...) { ... }` blocks.
Top-level `using var` declarations are not accepted by the script parser; regular
compiled `.cs` programs support them.
