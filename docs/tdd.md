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
