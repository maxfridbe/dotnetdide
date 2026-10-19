/** Load one isolated .NET 11 runspace. All compilation and file operations run in .NET. */
export async function loadDotnetdide({ signal, startupTimeoutMs = 120000, networkAccess = false } = {}) {
  if (typeof networkAccess !== 'boolean') throw new TypeError('networkAccess must be a boolean.');
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
  try { version = await request('initialize', { networkAccess }, { signal, timeoutMs: startupTimeoutMs }); }
  catch (error) { terminate(error); throw error; }
  return {
    version,
    runCSharpAsync(code, options) {
      if (typeof code !== 'string') return Promise.reject(new TypeError('code must be a string.'));
      return request('run', code, options);
    },
    setNetworkAccess(enabled) {
      if (typeof enabled !== 'boolean') return Promise.reject(new TypeError('networkAccess must be a boolean.'));
      return request('network', enabled);
    },
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
}
