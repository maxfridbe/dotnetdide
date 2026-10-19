// Install before .NET captures fetch. This is a capability control for cooperative
// C# code, not an isolation boundary for hostile JS interop or dynamically imported code.
export function installNetworkPolicy(scope = globalThis) {
  let allowed = true; // Runtime bootstrap must load its own assemblies before user code exists.
  const sockets = new Set();
  const denied = () => new DOMException('Network access is disabled by the host.', 'NotAllowedError');
  const originalFetch = scope.fetch.bind(scope);
  Object.defineProperty(scope, 'fetch', { configurable: false, writable: false,
    value: async (input, init = {}) => {
      if (!allowed) throw denied();
      // Preserve fetch's signal through body consumption, not only until headers arrive.
      return originalFetch(input, init);
    } });
  for (const name of ['WebSocket', 'EventSource', 'WebTransport', 'XMLHttpRequest']) {
    const Original = scope[name];
    if (!Original) continue;
    Object.defineProperty(scope, name, { configurable: false, writable: false,
      value: new Proxy(Original, { construct(target, args) {
        if (!allowed) throw denied();
        const socket = Reflect.construct(target, args);
        sockets.add(socket);
        if (name === 'XMLHttpRequest') {
          const send = socket.send.bind(socket);
          socket.send = (...values) => { if (!allowed) throw denied(); return send(...values); };
        }
        socket.addEventListener?.('close', () => sockets.delete(socket), { once: true });
        socket.addEventListener?.('loadend', () => sockets.delete(socket), { once: true });
        return socket;
      } }) });
  }
  return enabled => {
    if (typeof enabled !== 'boolean') throw new TypeError('networkAccess must be a boolean.');
    allowed = enabled;
    if (!allowed) {
      for (const socket of sockets) { socket.close?.(); socket.abort?.(); }
      sockets.clear();
    }
  };
}
