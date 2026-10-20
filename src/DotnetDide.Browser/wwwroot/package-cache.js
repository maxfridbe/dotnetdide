// Cache immutable, exact-version NuGet archives. Permission is checked by the
// caller before this runs; cached bytes never grant permission by themselves.
const CACHE = 'dotnetdide-nuget-v1';
const MAX_PACKAGE = 16 * 1024 * 1024, MAX_TOTAL = 64 * 1024 * 1024, MAX_ENTRIES = 32;
const packageUrl = url => url.origin === 'https://api.nuget.org' && !url.search &&
  /^\/v3-flatcontainer\/[a-z0-9_.-]+\/[a-z0-9.+-]+\/[a-z0-9_.+-]+\.nupkg$/.test(url.pathname);
let pendingWrite = Promise.resolve();

/** Clear downloaded NuGet archives. Already loaded assemblies remain in workers. */
export async function clearNugetCache() {
  try { return await globalThis.caches?.delete(CACHE) ?? false; } catch { return false; }
}

export async function fetchNugetPackage(fetch, input, init = {}) {
  const url = new URL(typeof input === 'string' || input instanceof URL ? input : input.url);
  if (!packageUrl(url)) return fetch(input, { ...init, cache: 'no-store' }); // Never cache version indexes.
  let cache;
  try {
    cache = await globalThis.caches?.open(CACHE);
    const hit = await cache?.match(url.href);
    if (hit && Number(hit.headers.get('content-length')) > 0 && Number(hit.headers.get('content-length')) <= MAX_PACKAGE) return hit;
  } catch { /* Private mode/storage restrictions must not prevent package loading. */ }
  const response = await fetch(input, init);
  if (!response.ok) return response;
  if (Number(response.headers.get('content-length')) > MAX_PACKAGE) { await response.body?.cancel(); throw new Error('NuGet package exceeds 16 MiB.'); }
  const reader = response.body?.getReader();
  if (!reader) throw new Error('NuGet package response has no body.');
  const chunks = []; let size = 0;
  try {
    while (true) {
      const {value, done} = await reader.read(); if (done) break;
      size += value.byteLength;
      if (size > MAX_PACKAGE) { await reader.cancel(); throw new Error('NuGet package exceeds 16 MiB.'); }
      chunks.push(value);
    }
  } finally { reader.releaseLock(); }
  const bytes = new Uint8Array(size); let offset = 0;
  for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.byteLength; }
  const stored = new Response(bytes, { headers: { 'Content-Type': 'application/octet-stream', 'Content-Length': String(size) } });
  if (cache) {
    // Serialize writes in this context. Browser locks also bound concurrent workers.
    const write = async () => {
      const keys = await cache.keys(); let total = 0; const entries = [];
      for (const key of keys) {
        const item = await cache.match(key); const length = Number(item?.headers.get('content-length')) || MAX_TOTAL;
        total += length; entries.push({key, length});
      }
      while (entries.length && (entries.length >= MAX_ENTRIES || total + size > MAX_TOTAL)) {
        const oldest = entries.shift(); await cache.delete(oldest.key); total -= oldest.length;
      }
      await cache.put(url.href, stored.clone());
    };
    pendingWrite = pendingWrite.catch(() => {}).then(() => globalThis.navigator?.locks
      ? navigator.locks.request(CACHE, write) : write());
    try { await pendingWrite; } catch { /* Full/disabled storage: keep the downloaded package usable. */ }
  }
  return stored;
}
