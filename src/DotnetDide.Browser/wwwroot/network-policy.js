import { fetchNugetPackage } from './package-cache.js';
import { relayFetch, validateRelay } from './relay-fetch.js';

// Install before .NET captures fetch. This is a capability control for cooperative
// C# code, not an isolation boundary for hostile JS interop or dynamically imported code.
export function installNetworkPolicy(scope = globalThis) {
  let allowed = true; // Runtime bootstrap must load its own assemblies before user code exists.
  let relay = null;
  let nugetAllowed = false;
  const sockets = new Set();
  const denied = () => new DOMException('Network access is disabled by the host.', 'NotAllowedError');
  const originalFetch = scope.fetch.bind(scope);
  Object.defineProperty(scope, 'fetch', { configurable: false, writable: false,
    value: async (input, init = {}) => {
      const url = new URL(typeof input === 'string' || input instanceof URL ? input : input.url, scope.location?.href);
      const method = (init.method ?? (typeof input === 'object' ? input.method : null) ?? 'GET').toUpperCase();
      if (nugetAllowed && method === 'GET' && url.origin === 'https://api.nuget.org' && !url.search &&
          /^\/v3-flatcontainer\/[a-z0-9_.-]+\/(?:index\.json|[a-z0-9.+-]+\/[a-z0-9_.+-]+\.nupkg)$/.test(url.pathname)) {
        // Package permission is direct NuGet.org access only, independent of the general HTTP route.
        return fetchNugetPackage(originalFetch, input, { ...init, credentials: 'omit', redirect: 'error' });
      }
      if (!allowed) throw denied();
      // Preserve fetch's signal through body consumption, not only until headers arrive.
      return relay ? relayFetch(originalFetch, relay, input, init) : originalFetch(input, init);
    } });
  for (const name of ['WebSocket', 'EventSource', 'WebTransport', 'XMLHttpRequest']) {
    const Original = scope[name];
    if (!Original) continue;
    Object.defineProperty(scope, name, { configurable: false, writable: false,
      value: new Proxy(Original, { construct(target, args) {
        if (!allowed) throw denied();
        if (relay) throw new DOMException('The HTTP relay does not support sockets or XMLHttpRequest. Use HttpClient/fetch.', 'NotSupportedError');
        const socket = Reflect.construct(target, args);
        sockets.add(socket);
        if (name === 'XMLHttpRequest') {
          const send = socket.send.bind(socket);
          socket.send = (...values) => { if (!allowed) throw denied(); if (relay) throw new DOMException('Use HTTP fetch with the relay.', 'NotSupportedError'); return send(...values); };
        }
        socket.addEventListener?.('close', () => sockets.delete(socket), { once: true });
        socket.addEventListener?.('loadend', () => sockets.delete(socket), { once: true });
        return socket;
      } }) });
  }
  const configure = (enabled, configuration) => {
    if (typeof enabled !== 'boolean') throw new TypeError('networkAccess must be a boolean.');
    if (configuration !== undefined) relay = validateRelay(configuration);
    allowed = enabled;
    if (!allowed || relay) {
      for (const socket of sockets) { socket.close?.(); socket.abort?.(); }
      sockets.clear();
    }
  };
  configure.setNugetAccess = enabled => {
    if (typeof enabled !== 'boolean') throw new TypeError('nugetAccess must be a boolean.');
    nugetAllowed = enabled;
  };
  return configure;
}
