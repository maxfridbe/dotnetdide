import {VirtualNetworkLog} from "./network-log.js";
import type {NetworkEntry} from "./network-log.js";

export type ConsoleLevel = "debug" | "log" | "info" | "warn" | "error";
export interface ConsoleMessage {level: ConsoleLevel; message: string; source: string; stack?: string; truncated?: boolean}
export interface ConsoleEntry extends ConsoleMessage {id: number; timestamp: string; truncated: boolean}
export interface LoadedAssembly {name: string; version: string; fullName: string; isDynamic: boolean}
export interface DebugInfo {
  version: 1; assemblies: LoadedAssembly[]; assemblyScope: string; network: NetworkEntry[]; console: ConsoleEntry[];
  dropped: {network: number; console: number};
}

/** Shared, bounded diagnostics used by the SDK and the Snabbdom debugger. */
export class DebugInfoStore {
  readonly network: VirtualNetworkLog;
  assemblies: LoadedAssembly[] = [];
  assemblyScope = "";
  readonly console: ConsoleEntry[] = [];
  recording = true;
  preserve = false;
  dropped = 0;
  #sequence = 0;
  #listeners = new Set<() => void>();
  constructor(readonly capacity = 500) {
    if (!Number.isInteger(capacity) || capacity < 1 || capacity > 2000) throw Error("Console capacity must be 1–2000.");
    this.network = new VirtualNetworkLog(() => this.emit());
  }
  subscribe(listener: () => void): () => void {
    this.#listeners.add(listener); return () => { this.#listeners.delete(listener); };
  }
  private emit(): void {
    for (const listener of this.#listeners) { try { listener(); } catch { /* Observers cannot affect the application. */ } }
  }
  recordConsole(value: ConsoleMessage): void {
    if (!this.recording || !value || typeof value.message !== "string") return;
    const level = ["debug", "log", "info", "warn", "error"].includes(value.level) ? value.level : "log";
    const stack = typeof value.stack === "string" ? value.stack : "";
    this.console.push({id: ++this.#sequence, timestamp: new Date().toISOString(), level,
      source: String(value.source ?? "host").slice(0, 512), message: value.message.slice(0, 16384),
      ...(stack ? {stack: stack.slice(0, 8192)} : {}), truncated: value.truncated === true || value.message.length > 16384 || stack.length > 8192});
    while (this.console.length > this.capacity) { this.console.shift(); this.dropped++; }
    this.emit();
  }
  setAssemblies(values: LoadedAssembly[], scope: string): void {
    this.assemblies = structuredClone(values); this.assemblyScope = scope; this.emit();
  }
  clearConsole(): void { this.console.length = 0; this.dropped = 0; this.emit(); }
  clear(): void { this.network.clear(); this.clearConsole(); }
  beginRunspace(): void { this.setAssemblies([], ""); this.network.beginRunspace(); if (!this.preserve) this.clearConsole(); }
  snapshot(): DebugInfo {
    return {version: 1, assemblies: structuredClone(this.assemblies), assemblyScope: this.assemblyScope, network: this.network.snapshot(), console: structuredClone(this.console),
      dropped: {network: this.network.dropped, console: this.dropped}};
  }
}
