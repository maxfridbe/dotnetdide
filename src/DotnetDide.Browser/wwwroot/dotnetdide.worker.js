import { dotnet } from './_framework/dotnet.js';

import { installNetworkPolicy } from './network-policy.js';
const setNetworkAccess = installNetworkPolicy();
let bridge;
let queue = Promise.resolve();
async function dispatch({ operation, payload }) {
  if (operation === 'initialize') {
    const runtime = await dotnet.withModuleConfig({
      print: message => { console.log(message); self.postMessage({type: 'console', entry: {level: 'log', message: String(message)}}); },
      printErr: message => { console.error(message); self.postMessage({type: 'console', entry: {level: 'error', message: String(message)}}); },
    }).withConfig({ debugLevel: 0 }).create();
    const config = runtime.getConfig();
    const exports = await runtime.getAssemblyExports(config.mainAssemblyName);
    bridge = exports.DotnetDide.Browser.RuntimeBridge;
    const resources = config.resources;
    const assets = [...resources.coreAssembly, ...resources.assembly];
    const urls = assets.map(asset => new URL(asset.resolvedUrl ?? asset.name, new URL('./_framework/', import.meta.url)).href);
    const version = await bridge.Initialize(urls, payload.nugetAccess, payload.siteAccess);
    setNetworkAccess(payload.networkAccess, payload.relay);
    setNetworkAccess.setNugetAccess(payload.nugetAccess);
    return version;
  }
  if (!bridge) throw new Error('Runtime is not initialized.');
  if (operation === 'network') { setNetworkAccess(payload); return; }
  if (operation === 'nuget') { bridge.SetNugetAccess(payload); setNetworkAccess.setNugetAccess(payload); return; }
  if (operation === 'site-access') { bridge.SetSiteAccess(payload); return; }
  if (operation === 'assemblies') return JSON.parse(bridge.Assemblies());
  if (operation === 'sites') return JSON.parse(bridge.Sites());
  if (operation === 'site-request') return JSON.parse(await bridge.Request(JSON.stringify(payload)));
  if (operation === 'run') { bridge.SetDebugInfo(payload.debugInfo); return JSON.parse(await bridge.Run(payload.code)); }
  if (operation === 'reset') { bridge.Reset(); return; }
  if (operation === 'files') return JSON.parse(bridge.Files(JSON.stringify(payload)));
  throw new Error('Unknown runspace operation.');
}
self.onmessage = ({ data }) => {
  // Files and submissions share a single ordered queue, including during async C# calls.
  queue = queue.then(async () => {
    try {
      const value = await dispatch(data);
      const assemblies = bridge && ['initialize','run','reset','site-request','assemblies'].includes(data.operation) ? JSON.parse(bridge.Assemblies()) : undefined;
      self.postMessage({ id: data.id, value, assemblies });
    }
    catch (error) { self.postMessage({ id: data.id, error: String(error?.message ?? error) }); }
  });
};
