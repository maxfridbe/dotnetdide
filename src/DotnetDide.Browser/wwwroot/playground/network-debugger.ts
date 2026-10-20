import { h, download, fileSize } from "./vdom.js";
import type { VNode } from "./vdom.js";
import { VirtualNetworkLog } from "./network-log.js";
import type { BodyPreview, NetworkEntry } from "./network-log.js";
import type { WireRequest, WireResponse } from "../dotnetdide.js";

type DetailTab = "Headers" | "Payload" | "Response" | "Timing";
const elapsed = (value: number): string => value < 1000 ? `${value.toFixed(1)} ms` : `${(value / 1000).toFixed(2)} s`;

/** Reusable Snabbdom component: render view() with your patch loop and wrap
 * requestSite with trace(). No global fetch interception or runtime dependency.
 */
export class VirtualNetworkDebugger {
  readonly log: VirtualNetworkLog;
  open = true;
  dock: "bottom" | "right" = "bottom";
  height = 310;
  width = 540;
  filter = "";
  category = "all";
  selected: number | null = null;
  tab: DetailTab = "Headers";
  message = "";
  #resizeCleanup: (() => void) | null = null;
  constructor(private readonly changed: () => void) { this.log = new VirtualNetworkLog(changed); }
  get layoutDock(): "bottom" | "right" { return innerWidth < 800 ? "bottom" : this.dock; }
  trace(request: WireRequest, invoke: () => Promise<WireResponse>, scope?: string): Promise<WireResponse> {
    return this.log.trace(request, invoke, scope);
  }
  toggle(): void { this.open = !this.open; this.changed(); }
  dispose(): void { this.#resizeCleanup?.(); }
  private clear(): void { this.selected = null; this.log.clear(); this.message = ""; this.changed(); }
  private resize(event: PointerEvent): void {
    if (event.button !== 0) return;
    event.preventDefault(); this.#resizeCleanup?.();
    const move = (value: PointerEvent): void => {
      if (this.layoutDock === "bottom") this.height = Math.max(180, Math.min(innerHeight * .75, innerHeight - value.clientY));
      else this.width = Math.max(330, Math.min(innerWidth * .7, innerWidth - value.clientX));
      this.changed();
    };
    const stop = (): void => {
      document.removeEventListener("pointermove", move); document.removeEventListener("pointerup", stop);
      document.removeEventListener("pointercancel", stop); document.body.style.userSelect = "";
      this.#resizeCleanup = null;
    };
    this.#resizeCleanup = stop; document.body.style.userSelect = "none";
    document.addEventListener("pointermove", move); document.addEventListener("pointerup", stop); document.addEventListener("pointercancel", stop);
  }
  private resizeKey(event: KeyboardEvent): void {
    const delta = event.key === "ArrowUp" || event.key === "ArrowLeft" ? 24 : event.key === "ArrowDown" || event.key === "ArrowRight" ? -24 : 0;
    if (!delta) return;
    event.preventDefault();
    if (this.layoutDock === "bottom") this.height = Math.max(180, Math.min(innerHeight * .75, this.height + delta));
    else this.width = Math.max(330, Math.min(innerWidth * .7, this.width + delta));
    this.changed();
  }
  private body(value: BodyPreview | null): VNode {
    if (!value) return h("p.network-empty", "Waiting for the response…");
    return h("div", [
      h("p.network-body-info", `${fileSize(value.bytes)}${value.binary ? " · Binary · hex preview" : " · Text preview"}${value.truncated ? " · Preview truncated" : ""}`),
      h("pre.network-body", {attrs: {tabindex: "0"}}, value.text || "(empty body)"),
    ]);
  }
  private headerRows(values: Record<string, string[]>): VNode {
    return h("dl.network-headers", Object.entries(values).flatMap(([name, values]) =>
      [h("dt", name), h("dd", values.join("\n"))]));
  }
  private details(entry: NetworkEntry): VNode {
    const url = `wire://localhost:${entry.port}${entry.path}`;
    const button = (tab: DetailTab): VNode => h("button", {
      attrs: {type: "button", role: "tab", "aria-selected": String(this.tab === tab), "data-network-tab": tab},
      on: {click: () => { this.tab = tab; this.changed(); }},
    }, tab);
    let content: VNode;
    if (this.tab === "Payload") content = this.body(entry.requestBody);
    else if (this.tab === "Response") content = this.body(entry.responseBody);
    else if (this.tab === "Timing") content = h("div.network-timing", [
      h("h3", "Virtual request timing"),
      h("p", entry.duration === null ? "Request pending…" : `Total: ${elapsed(entry.duration)}`),
      h("div.network-timing-bar"),
      h("p.network-muted", "Includes the worker queue, .NET handler execution, and message transfer. Virtual ports have no DNS, TCP or TLS phase."),
      h("p.network-muted", `Initiator: ${entry.scope}`),
    ]);
    else content = h("div", [
      h("h3", "General"),
      this.headerRows({"Virtual URL": [url], "Request method": [entry.method],
        "Status": [entry.status === null ? entry.error ? "Failed" : "Pending" : String(entry.status)], "Initiator": [entry.scope]}),
      entry.error ? h("pre.network-error", entry.error) : null,
      h("h3", "Response headers"), this.headerRows(entry.responseHeaders),
      h("h3", "Request headers"), this.headerRows(entry.requestHeaders),
    ]);
    return h("section.network-details", {attrs: {"aria-label": "Request details"}}, [
      h("div.network-detail-tabs", {attrs: {role: "tablist", "aria-label": "Request details"}}, [
        ...(["Headers", "Payload", "Response", "Timing"] as DetailTab[]).map(button),
        h("button.network-copy", {attrs: {type: "button", title: "Copy request details"}, on: {click: () => {
          void Promise.resolve().then(() => navigator.clipboard.writeText(JSON.stringify(entry, null, 2))).then(() => { this.message = "Request copied."; this.changed(); },
            () => { this.message = "Clipboard unavailable. Use Export to download the log."; this.changed(); });
        }}}, "Copy"),
        h("button", {attrs: {type: "button", "aria-label": "Close request details"}, on: {click: () => { this.selected = null; this.changed(); }}}, "×"),
      ]),
      h("div.network-detail-content", {attrs: {role: "tabpanel"}}, [content]),
    ]);
  }
  view(): VNode {
    const needle = this.filter.toLowerCase();
    const entries = this.log.entries.filter(entry =>
      `${entry.method} :${entry.port}${entry.path} ${entry.status ?? ""} ${entry.scope}`.toLowerCase().includes(needle) &&
      (this.category === "all" || (this.category === "errors" ? !!entry.error || (entry.status ?? 0) >= 400 : entry.duration === null)));
    const selected = this.log.entries.find(entry => entry.id === this.selected);
    const start = this.log.entries[0]?.started ?? 0;
    const end = Math.max(start + 1, ...this.log.entries.map(entry => entry.started + (entry.duration ?? Math.max(1, performance.now() - entry.started))));
    const select = (entry: NetworkEntry): void => { this.selected = entry.id; this.changed(); };
    const keySelect = (event: KeyboardEvent, entry: NetworkEntry): void => {
      const direction = event.key === "ArrowDown" ? 1 : event.key === "ArrowUp" ? -1 : 0;
      if (!direction) return;
      event.preventDefault();
      const next = entries[Math.max(0, Math.min(entries.length - 1, entries.indexOf(entry) + direction))];
      if (next) { select(next); requestAnimationFrame(() => document.querySelector<HTMLButtonElement>(`[data-network-id="${next.id}"] button`)?.focus()); }
    };
    return h("section#virtual-debugger.virtual-debugger", {
      attrs: {"aria-label": "Virtual network debugger", hidden: !this.open, "data-dock": this.layoutDock},
      style: {height: this.layoutDock === "bottom" ? `${this.height}px` : "100%", width: this.layoutDock === "right" ? `${this.width}px` : "100%"},
    }, [
      h("div.network-resizer", {attrs: {role: "separator", tabindex: "0", "aria-label": "Resize virtual debugger",
        "aria-orientation": this.layoutDock === "bottom" ? "horizontal" : "vertical",
        "aria-valuemin": this.layoutDock === "bottom" ? "180" : "330", "aria-valuemax": String(this.layoutDock === "bottom" ? Math.floor(innerHeight * .75) : Math.floor(innerWidth * .7)),
        "aria-valuenow": String(Math.round(this.layoutDock === "bottom" ? this.height : this.width))},
        on: {pointerdown: event => this.resize(event as PointerEvent), keydown: event => this.resizeKey(event as KeyboardEvent)}}),
      h("header.network-titlebar", [
        h("strong", "Virtual DevTools"), h("span.network-active-tab", "Network"),
        h("span.network-title-note", "Wire · local HTTP"),
        h("button", {attrs: {type: "button", "data-network-action": "dock", "aria-label": "Toggle debugger dock", title: "Dock bottom / right", disabled: innerWidth < 800},
          on: {click: () => { this.dock = this.dock === "bottom" ? "right" : "bottom"; this.changed(); }}}, this.layoutDock === "bottom" ? "◨" : "⬒"),
        h("button", {attrs: {type: "button", "data-network-action": "close", "aria-label": "Close virtual debugger"}, on: {click: () => this.toggle()}}, "×"),
      ]),
      h("div.network-toolbar", [
        h("button.network-record", {class: {recording: this.log.recording}, attrs: {type: "button", "aria-label": "Record virtual requests", "aria-pressed": String(this.log.recording), "data-network-action": "record"},
          on: {click: () => { this.log.recording = !this.log.recording; this.changed(); }}}, "●"),
        h("button", {attrs: {type: "button", title: "Clear requests", "aria-label": "Clear requests", "data-network-action": "clear"}, on: {click: () => this.clear()}}, "⊘"),
        h("input.network-filter", {attrs: {type: "search", placeholder: "Filter URL, method, port…", "aria-label": "Filter network requests"},
          props: {value: this.filter}, on: {input: event => { this.filter = (event.target as HTMLInputElement).value; this.changed(); }}}),
        h("select", {attrs: {"aria-label": "Filter request status"}, props: {value: this.category},
          on: {change: event => { this.category = (event.target as HTMLSelectElement).value; this.changed(); }}},
        [h("option", {attrs: {value: "all"}}, "All"), h("option", {attrs: {value: "errors"}}, "Errors"), h("option", {attrs: {value: "pending"}}, "Pending")]),
        h("label", [h("input", {attrs: {type: "checkbox", "data-network-action": "preserve"}, props: {checked: this.log.preserve},
          on: {change: event => { this.log.preserve = (event.target as HTMLInputElement).checked; this.changed(); }}}), "Preserve log"]),
        h("button", {attrs: {type: "button", "data-network-action": "export", disabled: !this.log.entries.length},
          on: {click: () => download(JSON.stringify({version: 1, kind: "dotnetdide-virtual-network", requests: this.log.snapshot()}, null, 2), "dotnetdide-network.json")}}, "Export ↓"),
      ]),
      h("div.network-content", {class: {"has-selection": !!selected}}, [
        h("div.network-requests", [
          h("table.network-table", [
            h("thead", [h("tr", ["Name", "Method", "Status", "Port", "Size", "Time", "Waterfall"].map(text => h("th", {attrs: {scope: "col"}}, text)))]),
            h("tbody", entries.map(entry => h("tr", {key: entry.id, class: {selected: entry.id === this.selected, failed: !!entry.error || (entry.status ?? 0) >= 400},
              attrs: {"data-network-id": String(entry.id), "aria-selected": String(entry.id === this.selected)},
              on: {click: () => select(entry), keydown: event => keySelect(event as KeyboardEvent, entry)}}, [
              h("td.network-name", [h("button", {attrs: {type: "button", title: entry.path}}, entry.path)]),
              h("td", entry.method), h("td", entry.status === null ? entry.error ? "Failed" : "Pending" : String(entry.status)),
              h("td", `:${entry.port}`), h("td", entry.responseBody ? fileSize(entry.responseBody.bytes) : "—"),
              h("td", entry.duration === null ? "Pending" : elapsed(entry.duration)),
              h("td.network-waterfall", [h("span", {class: {pending: entry.duration === null},
                style: {marginLeft: `${Math.min(96, (entry.started - start) / (end - start) * 100)}%`,
                  width: `${Math.max(2, Math.min(100, (entry.duration ?? performance.now() - entry.started) / (end - start) * 100))}%`}})]),
            ]))),
          ]),
          !entries.length ? h("p.network-empty", this.log.entries.length ? "No matching requests." : "Listening for virtual HTTP requests. Run the Interactive site example, then Open / Reload.") : null,
        ]),
        selected ? this.details(selected) : null,
      ]),
      h("footer.network-footer", [
        h("span", `${entries.length} / ${this.log.entries.length} requests · ${fileSize(entries.reduce((sum, entry) => sum + (entry.responseBody?.bytes ?? 0), 0))} received${this.log.dropped ? ` · ${this.log.dropped} older requests evicted` : ""}`),
        h("span", {attrs: {role: "status"}}, this.message || "Memory only · Headers redacted · Body previews limited to 16 KiB"),
      ]),
    ]);
  }
}
