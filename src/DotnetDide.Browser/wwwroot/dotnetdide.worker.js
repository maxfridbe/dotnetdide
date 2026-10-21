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
async function streamRequest(data) {
  const port = data.port; let cancelled = false, wake, credit = false;
  port.onmessage = ({data}) => {
    if (data.type === 'cancel') { cancelled = true; bridge?.CancelStream(); wake?.(); }
    else if (data.type === 'pull') { credit = true; wake?.(); }
  };
  try {
    if (!bridge) throw new Error('Runtime is not initialized.');
    const headers = JSON.parse(await bridge.BeginStream(JSON.stringify(data.payload.request), data.payload.maximumBytes));
    port.postMessage({type:'headers',value:headers});
    while (!cancelled) {
      if (!credit) await new Promise(resolve => { wake = resolve; });
      wake = null; if (cancelled) break; credit = false;
      const length = await bridge.ReadStream();
      if (!length) {
        const result = JSON.parse(await bridge.FinishStream());
        port.postMessage({type:'end',value:result});
        return;
      }
      const bytes = bridge.TakeStreamChunk();
      port.postMessage({type:'chunk',bytes}, [bytes.buffer]);
    }
  } catch (error) { port.postMessage({type:'error',error:String(error?.message ?? error)}); }
  finally {
    bridge?.CancelStream();
    try { await bridge?.FinishStream(); } catch { /* Already finished or cancelled. */ }
    port.postMessage({type:'closed'}); port.close();
  }
}
self.onmessage = ({ data }) => {
  // Reads/cancellation use the dedicated port; other operations wait for EOF.
  queue = queue.then(async () => {
    try {
      const value = data.operation === 'site-stream' ? await streamRequest(data) : await dispatch(data);
      const assemblies = bridge && ['initialize','run','reset','site-request','site-stream','assemblies'].includes(data.operation) ? JSON.parse(bridge.Assemblies()) : undefined;
      self.postMessage(data.operation === 'site-stream' ? {assemblies} : {id:data.id,value,assemblies});
    }
    catch (error) { self.postMessage({ id: data.id, error: String(error?.message ?? error) }); }
  });
};
