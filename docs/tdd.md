# Tests-first record

1. Wrote 18 xUnit contract cases for runspaces and workspace files against throwing
   stubs. `dotnet test tests/DotnetDide.Core.Tests` failed all 18 tests before implementation.
2. Implemented the Roslyn script chain and System.IO workspace. The same 18 tests passed.
3. Wrote the Chromium acceptance test against a published WebAssembly host with an
   unimplemented loader. It failed with `Runspace bridge not implemented`.
4. Implemented the .NET exports and worker bridge. The acceptance test passed against
   the real .NET 11 runtime in Chromium, including persistent variables, async code,
   diagnostics, stdout/stderr, shared binary files, two-worker isolation, snapshot
   restoration, malformed snapshot rejection, reset, and non-cooperative termination.
5. Added regression cases for more than 16 submissions, user-defined types, bounded
   output, disposal, and conflicting snapshots. The output case caught an empty-string return value for a statement, which
   was corrected to the documented null value.

Run the commands in README.md to reproduce the suite. The browser test is driven
by C# over Chrome DevTools Protocol; no JavaScript test runner or mocked compiler
is involved. Each run uses temporary workspace/browser-profile directories.

## GitHub Pages playground

Added browser acceptance coverage before replacing the original demo: the first
run failed because there were no example buttons. The finished page runs eight
examples with assertions in an isolated worker, including expected compiler and
runtime errors. Real browser execution caught unsupported top-level `using var`
script declarations in the JSON and ZIP examples; scoped `using` blocks fixed them.

The passing acceptance test also verifies that checks preserve interactive files
and variables, example steps share state, diagnostics are visible, a 390px viewport
has no horizontal page overflow, and stopping an infinite loop allows a fresh run.
Desktop and mobile screenshots are saved as CI artifacts. Together with the original
runspace acceptance test, the browser suite has two passing tests; the core suite
continues to pass all 22 tests. No JavaScript package manager is used.

## Monaco playground editor

The updated acceptance test first failed waiting for Monaco on the original
textarea page. It now passes with the actual locally served Monaco 0.57.0 assets.
The four browser tests cover C# models, example switching, execution of edited
code, Roslyn error markers and their removal after edits, phone-width layout,
selection replacement, undo/redo and Ctrl+Enter via real browser key events.
Blocking all Monaco asset requests also passes: the plain text editor remains
editable and its run shortcut executes C# successfully. The published site and
README screenshot were regenerated; no Node.js or npm build step was added.

## Workspace programs and versioned distribution

Eleven new tests first failed because `Dotnet` was not defined. The globals helper
now compiles `.cs` files, discovers source directories and reads a strict subset of
SDK-style `.csproj` files. Tests cover DLL/portable-PDB output without execution,
async entry points and arguments, source locations in diagnostics, rebuilds,
runtime exceptions, project-relative files, compile globs and library output,
unsupported project features, default imports and warning policy.

All 35 core tests and five real browser tests pass. Browser coverage includes both
new playground examples, explicit async Main executing without a blocking wrapper,
DLL/PDB signatures, compile failures and the published LLM/namespace documents.
The namespace inventory is derived from public type definitions/forwarders in the
published runtime DLL metadata.

Release packaging checks verify ZIP integrity, SHA-256, required assets/documents
and byte equality with the tested runtime DLL. Version selection was checked for
new-day reset, two-digit leading zeroes, unrelated tags and sequence exhaustion.
GitHub publishing is gated on the successful test job and uses that run's artifact.

Network policy regression: the real browser test first failed because ordinary
HttpClient reached the server with the default policy. It now checks default denial,
helper and HttpClient opt-in, revocation, request counts and retained script state.

## Media defaults and optional browser NuGet loading

Media tests first failed with missing Stb/NAudio/Concentus namespaces, then passed
after adding pinned managed packages. The same source examples run as scripts
and compiled programs. Real browser checks decode generated PNG/JPEG and WAV/Ogg
Opus files. NuGet tests cover default denial, invalid IDs, dependency resolution,
version reuse/conflicts, revocation and rejecting native/build/traversal archives.
A real WASM test downloads Newtonsoft.Json directly from NuGet.org and uses it in
submissions and compiled files while general networking stays disabled. Cache
tests cover exact-version reuse across module reloads, fresh version indexes,
eviction, size limits, clearing and disabled-storage fallback.
