import { dotnet } from './_framework/dotnet.js';

let bridge;
let queue = Promise.resolve();
async function dispatch({ operation, payload }) {
  if (operation === 'initialize') {
    const runtime = await dotnet.withConfig({ debugLevel: 0 }).create();
    const config = runtime.getConfig();
    const exports = await runtime.getAssemblyExports(config.mainAssemblyName);
    bridge = exports.DotnetDide.Browser.RuntimeBridge;
    const resources = config.resources;
    const assets = [...resources.coreAssembly, ...resources.assembly];
    const urls = assets.map(asset => new URL(asset.resolvedUrl ?? asset.name, new URL('./_framework/', import.meta.url)).href);
    return await bridge.Initialize(urls);
  }
  if (!bridge) throw new Error('Runtime is not initialized.');
  if (operation === 'run') return JSON.parse(await bridge.Run(payload));
  if (operation === 'reset') { bridge.Reset(); return; }
  if (operation === 'files') return JSON.parse(bridge.Files(JSON.stringify(payload)));
  throw new Error('Unknown runspace operation.');
}
self.onmessage = ({ data }) => {
  // Files and submissions share a single ordered queue, including during async C# calls.
  queue = queue.then(async () => {
    try { self.postMessage({ id: data.id, value: await dispatch(data) }); }
    catch (error) { self.postMessage({ id: data.id, error: String(error?.message ?? error) }); }
  });
};
