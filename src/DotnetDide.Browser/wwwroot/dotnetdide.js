export { createWirePreview } from './wire-preview.js';
export { clearNugetCache } from './package-cache.js';
export { DebugInfoStore } from './playground/debug-info.js';
import { DebugInfoStore } from './playground/debug-info.js';
import { validateRelay } from './relay-fetch.js';
import {openSiteStream, collectSiteStream} from './site-stream.js';

/** Load one isolated .NET 11 runspace. All compilation and file operations run in .NET. */
export async function loadDotnetdide({ signal, startupTimeoutMs = 120000, networkAccess = false, nugetAccess = false, siteAccess = false, relay = null, diagnostics = new DebugInfoStore(), debugScope = "Runspace", siteResponseLimitBytes = 64 * 1024 * 1024 } = {}) {
  if (typeof networkAccess !== 'boolean') throw new TypeError('networkAccess must be a boolean.');
  if (typeof nugetAccess !== 'boolean') throw new TypeError('nugetAccess must be a boolean.');
  if (typeof siteAccess !== 'boolean') throw new TypeError('siteAccess must be a boolean.');
  if (!Number.isSafeInteger(siteResponseLimitBytes) || siteResponseLimitBytes < 1 || siteResponseLimitBytes > 512 * 1024 * 1024) throw new TypeError('siteResponseLimitBytes must be 1–512 MiB in bytes.');
  relay = validateRelay(relay);
  if (!(diagnostics instanceof DebugInfoStore)) throw new TypeError('diagnostics must be a DebugInfoStore.');
  const recordConsole = value => diagnostics.recordConsole(value);
  const output = (result, source) => {
    if (result.stdout) recordConsole({level: 'log', message: result.stdout, source});
    if (result.stderr) recordConsole({level: 'error', message: result.stderr, source});
  };
  const worker = new Worker(new URL('./dotnetdide.worker.js', import.meta.url), { type: 'module', name: 'dotnetdide' });
  const pending = new Map();
  let sequence = 0;
  let closed = false;
  function terminate(reason = new DOMException('Runspace terminated.', 'AbortError')) {
    if (closed) return;
    closed = true;
    worker.terminate();
    for (const item of pending.values()) { item.cleanup(); item.reject(reason); }
    pending.clear();
  }
  worker.onerror = event => { event.preventDefault(); terminate(new Error(event.message || 'Runspace worker failed.')); };
  worker.onmessageerror = () => terminate(new Error('Runspace response could not be decoded.'));
  worker.onmessage = ({ data }) => {
    if (closed) return;
    if (data.type === 'console') { recordConsole({...data.entry, source: `${debugScope} · runtime`}); return; }
    if (data.assemblies) diagnostics.setAssemblies(data.assemblies, debugScope);
    const item = pending.get(data.id);
    if (!item) return;
    pending.delete(data.id); item.cleanup();
    if (data.error) item.reject(new Error(data.error)); else item.resolve(data.value);
  };
  function request(operation, payload, { signal, timeoutMs } = {}) {
    if (closed) return Promise.reject(new Error('Runspace is terminated. Create a new runspace to continue.'));
    if (signal?.aborted) return Promise.reject(signal.reason ?? new DOMException('Aborted.', 'AbortError'));
    if (timeoutMs !== undefined && (!Number.isFinite(timeoutMs) || timeoutMs <= 0)) return Promise.reject(new TypeError('timeoutMs must be positive.'));
    return new Promise((resolve, reject) => {
      const id = ++sequence;
      const abort = () => terminate(signal?.reason ?? new DOMException('Aborted.', 'AbortError'));
      const timer = timeoutMs === undefined ? null : setTimeout(() => terminate(new DOMException('Runspace timed out.', 'TimeoutError')), timeoutMs);
      const cleanup = () => { if (timer !== null) clearTimeout(timer); signal?.removeEventListener('abort', abort); };
      pending.set(id, { resolve, reject, cleanup });
      signal?.addEventListener('abort', abort, { once: true });
      try { worker.postMessage({ id, operation, payload }); }
      catch (error) { pending.delete(id); cleanup(); reject(error); }
    });
  }
  const files = (op, values = {}) => request('files', { op, ...values });
  let version;
  try { version = await request('initialize', { networkAccess, nugetAccess, siteAccess, relay }, { signal, timeoutMs: startupTimeoutMs }); }
  catch (error) { terminate(error); throw error; }
  const runspace = {
    version,
    runCSharpAsync(code, options) {
      if (typeof code !== 'string') return Promise.reject(new TypeError('code must be a string.'));
      return request('run', {code, debugInfo: JSON.stringify(diagnostics.snapshot())}, options).then(result => {
        output(result, `${debugScope} · C#`);
        for (const item of result.diagnostics ?? []) recordConsole({level: item.severity.toLowerCase() === 'error' ? 'error' : 'warn',
          source: `${debugScope} · compiler`, message: `${item.id} (${item.line},${item.column}): ${item.message}`});
        if (result.error) recordConsole({level: 'error', source: `${debugScope} · C#`, message: `${result.error.type}: ${result.error.message}`});
        return result;
      });
    },
    async LoadNuget(name, version = null) {
      if (typeof name !== 'string' || (version !== null && typeof version !== 'string')) throw new TypeError('Package name and version must be strings.');
      const code = `System.Text.Json.JsonSerializer.Serialize(await DotnetDide.Nuget.Load(${JSON.stringify(name)}, ${JSON.stringify(version)}), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))`;
      const result = await runspace.runCSharpAsync(code);
      if (!result.success) throw new Error(result.error?.message ?? result.diagnostics.map(item => item.message).join('\n'));
      return JSON.parse(result.value);
    },
    setNetworkAccess(enabled) {
      if (typeof enabled !== 'boolean') return Promise.reject(new TypeError('networkAccess must be a boolean.'));
      return request('network', enabled);
    },
    setNugetAccess(enabled) {
      if (typeof enabled !== 'boolean') return Promise.reject(new TypeError('nugetAccess must be a boolean.'));
      return request('nuget', enabled);
    },
    setSiteAccess(enabled) {
      if (typeof enabled !== 'boolean') return Promise.reject(new TypeError('siteAccess must be a boolean.'));
      return request('site-access', enabled);
    },
    listSites: () => request('sites'),
    listAssemblies: () => request('assemblies'),
    requestSiteStream(value, options = {}) {
      if (closed) return Promise.reject(new Error('Runspace is terminated.'));
      return openSiteStream(worker, ++sequence, value, siteResponseLimitBytes, options, pending,
        diagnostics.network.observeStream(value, debugScope), result => output(result, `${debugScope} · site :${value.port}`), terminate);
    },
    async requestSite(value, options = {}) { return collectSiteStream(await runspace.requestSiteStream(value, options)); },
    getDebugInfo: () => diagnostics.snapshot(),
    GetDebugInfo: () => diagnostics.snapshot(),
    clearDebugInfo: () => diagnostics.clear(),
    onDebugInfoChanged: listener => diagnostics.subscribe(listener),
    recordConsole,
    reset: () => request('reset'),
    terminate,
    exportWorkspace: () => files('export'),
    importWorkspace: snapshot => files('import', { snapshot }),
    FS: {
      cwd: () => files('cwd'),
      mkdir: path => files('mkdir', { path }),
      readdir: (path = '.') => files('list', { path }),
      stat: path => files('stat', { path }),
      async writeFile(path, value) {
        const bytes = typeof value === 'string' ? new TextEncoder().encode(value) : value;
        if (!(bytes instanceof Uint8Array)) throw new TypeError('File contents must be a string or Uint8Array.');
        let binary = '';
        for (let offset = 0; offset < bytes.length; offset += 8192) binary += String.fromCharCode(...bytes.subarray(offset, offset + 8192));
        await files('write', { path, data: btoa(binary) });
      },
      async readFile(path, { encoding } = {}) {
        if (encoding !== undefined && encoding !== 'utf8') throw new TypeError('Only utf8 text encoding is supported.');
        const bytes = Uint8Array.from(atob(await files('read', { path })), character => character.charCodeAt(0));
        return encoding === 'utf8' ? new TextDecoder().decode(bytes) : bytes;
      },
      unlink: path => files('unlink', { path }),
      rmdir: (path, { recursive = false } = {}) => files('rmdir', { path, recursive }),
      rename: (path, destination) => files('rename', { path, destination }),
    },
  };
  return runspace;
}
