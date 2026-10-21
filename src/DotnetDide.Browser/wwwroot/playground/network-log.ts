import type { WireRequest, WireResponse } from "../dotnetdide.js";

export interface BodyPreview { bytes: number; text: string; truncated: boolean; binary: boolean }
export interface NetworkEntry {
  id: number; scope: string; method: string; port: number; path: string; started: number; duration: number | null;
  status: number | null; error: string | null; requestHeaders: Record<string, string[]>;
  responseHeaders: Record<string, string[]>; requestBody: BodyPreview; responseBody: BodyPreview | null;
}
const BODY_LIMIT = 16 * 1024;
const SECRET = /^(authorization|proxy-authorization|cookie|set-cookie|x-api-key|api[-_]?key|access[-_]?token|token|password|secret)$/iu;
function headers(values: Record<string, string[]> = {}): Record<string, string[]> {
  return Object.fromEntries(Object.entries(values).slice(0, 64).map(([key, value]) =>
    [key.slice(0, 256), SECRET.test(key) ? ["[redacted]"] : value.slice(0, 16).map(part => part.slice(0, 2048))]));
}
function safePath(path: string): string {
  try {
    const url = new URL(path, "http://virtual.invalid");
    for (const key of [...url.searchParams.keys()]) if (SECRET.test(key)) url.searchParams.set(key, "[redacted]");
    return (url.pathname + url.search).slice(0, 8192);
  } catch { return "[invalid path]"; }
}
export function bodyPreview(base64?: string | null): BodyPreview {
  if (!base64) return {bytes: 0, text: "", truncated: false, binary: false};
  const size = Math.max(0, Math.floor(base64.length / 4) * 3 - (base64.endsWith("==") ? 2 : base64.endsWith("=") ? 1 : 0));
  try {
    const data = Uint8Array.from(atob(base64.slice(0, Math.ceil(BODY_LIMIT / 3) * 4)), c => c.charCodeAt(0)).slice(0, BODY_LIMIT);
    let text: string, binary = false;
    try {
      text = new TextDecoder("utf-8", {fatal: true}).decode(data, {stream: size > BODY_LIMIT});
      if (/[\u0000-\u0008\u000e-\u001f]/u.test(text)) throw Error("Binary data");
    } catch {
      binary = true;
      text = [...data.slice(0, 64)].map(value => value.toString(16).padStart(2, "0")).join(" ");
    }
    if (!binary && size <= BODY_LIMIT) {
      try { text = JSON.stringify(JSON.parse(text), null, 2); } catch { /* Plain text. */ }
    }
    return {bytes: size, text: text.slice(0, BODY_LIMIT), binary, truncated: size > (binary ? 64 : BODY_LIMIT) || text.length > BODY_LIMIT};
  } catch { return {bytes: size, text: "[Invalid base64 body]", binary: true, truncated: false}; }
}

/** Bounded, in-memory observation of virtual Wire requests. It never replays a request. */
export class VirtualNetworkLog {
  readonly entries: NetworkEntry[] = [];
  recording = true;
  preserve = false;
  dropped = 0;
  #sequence = 0;
  constructor(private readonly changed: () => void = () => {}, readonly capacity = 200) {
    if (!Number.isInteger(capacity) || capacity < 1 || capacity > 1000) throw Error("Network log capacity must be 1–1000.");
  }
  private emit(): void { try { this.changed(); } catch { /* Observers must not change HTTP behavior. */ } }
  clear(): void { this.entries.length = 0; this.dropped = 0; this.emit(); }
  beginRunspace(): void { if (!this.preserve) this.clear(); }
  snapshot(): NetworkEntry[] { return structuredClone(this.entries); }
  observeStream(request: WireRequest, scope = "Playground") {
    const entry: NetworkEntry = { id: ++this.#sequence, scope, method: request.method, port: request.port, path: safePath(request.path),
      started: performance.now(), duration: null, status: null, error: null, requestHeaders: headers(request.headers), responseHeaders: {},
      requestBody: bodyPreview(request.body), responseBody: null };
    const active = this.recording; let count = 0, prefix = new Uint8Array(0), finished = false;
    if (active) { this.entries.push(entry); while (this.entries.length > this.capacity) { this.entries.shift(); this.dropped++; } this.emit(); }
    return {
      response: (response: Omit<WireResponse, "body">) => {
        entry.status = response.status; entry.error = response.error ?? null; entry.responseHeaders = headers(response.headers);
        if (active && this.entries.includes(entry)) this.emit();
      },
      chunk: (chunk: Uint8Array) => {
        count += chunk.length;
        if (active && prefix.length < BODY_LIMIT) { const next = new Uint8Array(Math.min(BODY_LIMIT, prefix.length + chunk.length)); next.set(prefix); next.set(chunk.subarray(0, next.length - prefix.length), prefix.length); prefix = next; }
      },
      finish: (error?: unknown) => {
        if (finished) return; finished = true;
        if (error !== undefined) entry.error = error instanceof Error ? error.message : String(error);
        let binary = ""; for (const byte of prefix) binary += String.fromCharCode(byte);
        const preview = bodyPreview(btoa(binary));
        entry.responseBody = {...preview, bytes:count, truncated:preview.truncated || count > prefix.length};
        entry.duration = Math.max(0, performance.now() - entry.started);
        if (active && this.entries.includes(entry)) this.emit();
      },
    };
  }
  async trace(request: WireRequest, invoke: () => Promise<WireResponse>, scope = "Playground"): Promise<WireResponse> {
    if (!this.recording) return invoke();
    const entry: NetworkEntry = {
      id: ++this.#sequence, scope, method: request.method, port: request.port, path: safePath(request.path),
      started: performance.now(), duration: null, status: null, error: null,
      requestHeaders: headers(request.headers), responseHeaders: {},
      requestBody: bodyPreview(request.body), responseBody: null,
    };
    this.entries.push(entry);
    while (this.entries.length > this.capacity) { this.entries.shift(); this.dropped++; }
    this.emit();
    try {
      const response = await invoke();
      entry.status = response.status; entry.error = response.error ?? null;
      entry.responseHeaders = headers(response.headers); entry.responseBody = bodyPreview(response.body);
      return response;
    } catch (error) {
      entry.error = error instanceof Error ? `${error.name}: ${error.message}` : String(error);
      throw error;
    } finally {
      entry.duration = Math.max(0, performance.now() - entry.started);
      if (this.entries.includes(entry)) this.emit();
    }
  }
}
