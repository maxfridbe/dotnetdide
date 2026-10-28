const MAX_BYTES = 8 * 1024 * 1024;
export function validateRelay(relay) {
  if (relay === undefined || relay === null) return null;
  if (typeof relay !== 'object' || typeof relay.endpoint !== 'string') throw new TypeError('relay.endpoint must be an HTTPS relay URL.');
  let url;
  try { url = new URL(relay.endpoint); } catch { throw new TypeError('Enter an HTTPS relay URL, not a Tor bridge line.'); }
  if (url.hostname === 'bridges.torproject.org') throw new TypeError('bridges.torproject.org provides Tor bridges; it is not a proxy. Configure a bridge in your Tor client, then connect the relay to its SOCKS5 port.');
  if (url.protocol.startsWith('socks')) throw new TypeError('Browsers cannot connect directly to SOCKS. Configure SOCKS5 on the native relay and enter its HTTPS /relay URL here.');
  if ((url.protocol !== 'https:' && !(url.protocol === 'http:' && ['localhost', '127.0.0.1', '[::1]'].includes(url.hostname))) || url.username || url.password || url.search || url.hash)
    throw new TypeError('Relay must use HTTPS (or HTTP on localhost) without URL credentials, query or fragment.');
  if (relay.token !== undefined && relay.token !== '' && (typeof relay.token !== 'string' || !/^[!-~]{24,1024}$/.test(relay.token))) throw new TypeError('A relay token of 24–1024 printable ASCII characters is required. Set it in Runtime & security.');
  return { endpoint: url.href, token: relay.token || '' };
}
function encode(bytes) {
  let value = '';
  for (let i = 0; i < bytes.length; i += 8192) value += String.fromCharCode(...bytes.subarray(i, i + 8192));
  return btoa(value);
}
async function readLimited(response) {
  const reader = response.body?.getReader();
  if (!reader) throw new Error('Relay returned an empty response.');
  const chunks = []; let length = 0;
  try {
    while (true) {
      const { value, done } = await reader.read(); if (done) break;
      length += value.byteLength;
      if (length > 12 * 1024 * 1024) throw new Error('Relay response exceeds 12 MiB.');
      chunks.push(value);
    }
  } catch (error) { await reader.cancel().catch(() => {}); throw error; }
  finally { reader.releaseLock(); }
  const bytes = new Uint8Array(length); let offset = 0;
  for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.length; }
  return JSON.parse(new TextDecoder().decode(bytes));
}
// Relay failures are separate from target HTTP status codes in successful envelopes.
// Never read an unbounded error page or expose request credentials in diagnostics.
async function relayFailure(response, relay, request) {
  const fallback = `HTTP relay failed (${response.status}). Check token, origin/target allowlists and upstream proxy. No direct fallback was attempted.`;
  const type = response.headers.get('content-type') || '';
  if (!/^(text\/plain|application\/(?:problem\+)?json)(?:;|$)/i.test(type)) {
    await response.body?.cancel().catch(() => {}); return new Error(fallback);
  }
  const reader = response.body?.getReader();
  if (!reader) return new Error(fallback);
  const chunks = []; let length = 0, truncated = false;
  const cancel = () => { void reader.cancel().catch(() => {}); };
  const timer = setTimeout(cancel, 5000);
  request.signal.addEventListener('abort', cancel, {once: true});
  try {
    while (length < 4096) {
      const {value, done} = await reader.read(); if (done) break;
      const part = value.subarray(0, 4096 - length); chunks.push(part); length += part.length;
      if (length === 4096) { truncated = true; break; }
    }
  } catch { return new Error(fallback); }
  finally { clearTimeout(timer); request.signal.removeEventListener('abort', cancel); cancel(); reader.releaseLock(); }
  request.signal.throwIfAborted();
  const bytes = new Uint8Array(length); let offset = 0;
  for (const part of chunks) { bytes.set(part, offset); offset += part.length; }
  let detail = new TextDecoder().decode(bytes);
  if (/json/i.test(type)) {
    try { const data = JSON.parse(detail); detail = typeof data.error === 'string' ? data.error : typeof data.message === 'string' ? data.message : ''; }
    catch { detail = ''; }
  }
  const url = new URL(request.url);
  const secrets = [relay.token, request.url, ...request.headers.values(), ...url.searchParams.values()];
  for (const secret of secrets.filter(Boolean).sort((a,b) => b.length - a.length)) detail = detail.split(secret).join('[REDACTED]');
  detail = detail.replace(/https?:\/\/[^\s<>"']+/gi, '[URL]')
    .replace(/(?:Bearer|Basic)\s+[^\s,;]+/gi, '[REDACTED]')
    .replace(/[\u0000-\u001f\u007f-\u009f\u202a-\u202e\u2066-\u2069]/g, ' ').trim().slice(0, 1024);
  // HTML is not a useful relay diagnostic, even if incorrectly labelled plain text.
  if (!detail || /<[^>]+>/.test(detail)) return new Error(fallback);
  return new Error(`HTTP relay failed (${response.status}): ${detail}${truncated ? ' [truncated]' : ''}. No direct fallback was attempted.`);
}
export async function relayFetch(fetch, relay, input, init) {
  if (!relay.token) throw new Error('Relay token is missing. Set it in Runtime & security; no direct fallback is permitted.');
  const request = new Request(input, init);
  const url = new URL(request.url);
  if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password || url.hash) throw new TypeError('Relay supports HTTP(S) URLs without embedded credentials or fragments.');
  if (request.mode === 'no-cors') throw new TypeError('no-cors produces unreadable opaque responses. Use cors with the authorized relay.');
  if (request.mode === 'same-origin' && url.origin !== globalThis.location.origin) throw new TypeError('same-origin requests cannot target another origin.');
  if (request.credentials === 'include') throw new TypeError('Relay does not forward browser cookies. Use credentials omit and explicit target authorization headers.');
  if (!['GET', 'HEAD', 'OPTIONS', 'POST', 'PUT', 'PATCH', 'DELETE'].includes(request.method)) throw new TypeError('Unsupported relay HTTP method.');
  request.signal.throwIfAborted();
  // Relay v1 deliberately buffers bounded bodies; no WebSocket, SSE or unbounded streaming tunnel.
  // Firefox can read arrayBuffer() while Request.body is unavailable. Do not
  // use the optional stream getter to decide whether a payload exists.
  const bytes = ['GET', 'HEAD'].includes(request.method) ? null : new Uint8Array(await request.arrayBuffer());
  if (bytes && bytes.length > MAX_BYTES) throw new Error('Relay request exceeds 8 MiB.');
  const response = await fetch(relay.endpoint, {
    method: 'POST', mode: 'cors', credentials: 'omit', redirect: 'error', cache: 'no-store', signal: request.signal,
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${relay.token}` },
    body: JSON.stringify({ url: url.href, method: request.method, headers: Object.fromEntries(request.headers), bodyBase64: bytes ? encode(bytes) : null }),
  });
  if (!response.ok) throw await relayFailure(response, relay, request);
  const envelope = await readLimited(response);
  request.signal.throwIfAborted();
  if (!Number.isInteger(envelope.status) || envelope.status < 200 || envelope.status > 599 || typeof envelope.bodyBase64 !== 'string' || envelope.bodyBase64.length > Math.ceil(MAX_BYTES / 3) * 4 || !envelope.headers || typeof envelope.headers !== 'object') throw new Error('Invalid relay response.');
  const body = Uint8Array.from(atob(envelope.bodyBase64), char => char.charCodeAt(0));
  if (body.length > MAX_BYTES) throw new Error('Relay response exceeds 8 MiB.');
  return new Response(request.method === 'HEAD' || [204, 205, 304].includes(envelope.status) ? null : body, { status: envelope.status, headers: envelope.headers });
}
