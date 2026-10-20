export interface Diagnostic { id: string; severity: string; message: string; line: number; column: number }
export interface SubmissionResult {
  success: boolean; stdout: string; stderr: string; value: string | null;
  diagnostics: Diagnostic[]; error: {type: string; message: string} | null;
}
export interface Workspace { version: number; directories: string[]; files: {path: string; data: string}[] }
export interface WireRequest { port: number; method: string; path: string; headers?: Record<string, string[]>; body?: string | null }
export interface WireResponse { status: number; headers: Record<string, string[]>; body: string; error?: string | null }
export interface WireSite { port: number; webRoot: string | null }
export interface RequestOptions { signal?: AbortSignal; timeoutMs?: number }
export interface Runspace {
  version: string;
  runCSharpAsync(code: string, options?: RequestOptions): Promise<SubmissionResult>;
  setNetworkAccess(enabled: boolean): Promise<void>;
  setNugetAccess(enabled: boolean): Promise<void>;
  setSiteAccess(enabled: boolean): Promise<void>;
  listSites(): Promise<WireSite[]>;
  requestSite(value: WireRequest, options?: RequestOptions): Promise<WireResponse>;
  reset(): Promise<void>; terminate(): void;
  exportWorkspace(): Promise<Workspace>; importWorkspace(value: Workspace): Promise<void>;
  FS: {
    cwd(): Promise<string>; mkdir(path: string): Promise<void>; readdir(path?: string): Promise<string[]>;
    stat(path: string): Promise<unknown>; writeFile(path: string, data: string | Uint8Array): Promise<void>;
    readFile(path: string, options: {encoding: "utf8"}): Promise<string>;
    readFile(path: string): Promise<Uint8Array>;
    unlink(path: string): Promise<void>; rmdir(path: string, options?: {recursive?: boolean}): Promise<void>;
    rename(path: string, destination: string): Promise<void>;
  };
}
export function loadDotnetdide(options?: {
  signal?: AbortSignal; startupTimeoutMs?: number; networkAccess?: boolean;
  nugetAccess?: boolean; siteAccess?: boolean; relay?: {endpoint: string; token?: string} | null;
}): Promise<Runspace>;
export interface WirePreview { navigate(path: string): Promise<void>; destroy(): void }
export function createWirePreview(options: {
  iframe: HTMLIFrameElement; port: number; path?: string;
  onNavigate?: (path: string) => void; request: (value: WireRequest) => Promise<WireResponse>;
}): Promise<WirePreview>;
export function clearNugetCache(): Promise<void>;
