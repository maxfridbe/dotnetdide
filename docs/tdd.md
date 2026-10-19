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
