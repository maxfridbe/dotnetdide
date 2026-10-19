Monaco Editor 0.57.0 — prebuilt min/ browser distribution, unmodified.
Upstream: https://github.com/microsoft/monaco-editor/tree/v0.57.0
Archive: https://registry.npmjs.org/monaco-editor/-/monaco-editor-0.57.0.tgz
Integrity: sha512-5BkI9KGoqrNvBGUe15/QlZq3OooZ8WLg1AxTpaqHRCP3HNpzPPZKE2EDz8M7c+VRmCeUw1Brp4cx/PWm3kI/5A==

The archive SHA-512 was verified against the publisher registry metadata before
extracting min/. LICENSE and ThirdPartyNotices.txt accompany these assets.
The full min/ tree is retained so lazy modules, workers and CSS resources resolve.

This uses the deprecated but still shipped AMD distribution to serve the editor
without adding Node.js, npm or a JavaScript bundler to the .NET build. Pin updates
must rerun the browser tests. The runspace SDK does not depend on Monaco; only the
playground loads it. All files are served locally, including worker resources.
