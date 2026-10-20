import {h, download} from "./vdom.js";
import type {VNode} from "./vdom.js";
import type {DebugInfoStore} from "./debug-info.js";

/** Console view over the same store returned by runtime.GetDebugInfo(). */
export class ConsolePanel {
  filter = "";
  level = "all";
  message = "";
  constructor(private readonly store: DebugInfoStore, private readonly changed: () => void) {}
  view(): VNode[] {
    const rows = this.store.console.filter(entry => (this.level === "all" || entry.level === this.level) &&
      `${entry.source} ${entry.message} ${entry.stack ?? ""}`.toLowerCase().includes(this.filter.toLowerCase()));
    return [
      h("div.network-toolbar", [
        h("button.network-record", {class: {recording: this.store.recording},
          attrs: {type: "button", "aria-label": "Record console messages", "aria-pressed": String(this.store.recording), "data-console-action": "record"},
          on: {click: () => { this.store.recording = !this.store.recording; this.changed(); }}}, "●"),
        h("button", {attrs: {type: "button", "aria-label": "Clear console", "data-console-action": "clear"},
          on: {click: () => { this.message = ""; this.store.clearConsole(); }}}, "⊘"),
        h("input.network-filter", {attrs: {type: "search", placeholder: "Filter text or source…", "aria-label": "Filter console messages"},
          props: {value: this.filter}, on: {input: event => { this.filter = (event.target as HTMLInputElement).value; this.changed(); }}}),
        h("select", {attrs: {"aria-label": "Filter console level"}, props: {value: this.level},
          on: {change: event => { this.level = (event.target as HTMLSelectElement).value; this.changed(); }}},
          ["all", "debug", "log", "info", "warn", "error"].map(level => h("option", {attrs: {value: level}}, level))),
        h("label", [h("input", {attrs: {type: "checkbox", "data-console-action": "preserve"}, props: {checked: this.store.preserve},
          on: {change: event => { this.store.preserve = (event.target as HTMLInputElement).checked; this.changed(); }}}), "Preserve log"]),
        h("button", {attrs: {type: "button", "data-console-action": "copy", disabled: !rows.length}, on: {click: () => {
          void Promise.resolve().then(() => navigator.clipboard.writeText(rows.map(row => `${row.timestamp} [${row.level}] ${row.source}: ${row.message}${row.stack ? "\n" + row.stack : ""}`).join("\n")))
            .then(() => { this.message = "Console copied."; this.changed(); }, () => { this.message = "Clipboard unavailable. Use Export debug info."; this.changed(); });
        }}}, "Copy"),
        h("button", {attrs: {type: "button", "data-console-action": "export"},
          on: {click: () => download(JSON.stringify(this.store.snapshot(), null, 2), "dotnetdide-debug.json")}}, "Export debug info ↓"),
      ]),
      h("div.console-content", {attrs: {role: "region", "aria-label": "Console messages", tabindex: "0"}}, rows.length ? rows.map(row =>
        h("article.console-row", {key: row.id, attrs: {"data-console-level": row.level}}, [
          h("div.console-meta", [h("time", {attrs: {datetime: row.timestamp}}, row.timestamp.slice(11, 23)), h("span", row.level), h("span", row.source)]),
          h("pre", row.message + (row.truncated ? "\n[truncated]" : "")),
          row.stack ? h("details", [h("summary", "Stack trace"), h("pre", row.stack)]) : null,
        ])) : [h("p.network-empty", this.store.console.length ? "No matching messages." : "Listening for C# output, compilation errors, and hosted iframe console messages.")]),
      h("footer.network-footer", [
        h("span", `${rows.length} / ${this.store.console.length} messages${this.store.dropped ? ` · ${this.store.dropped} older messages evicted` : ""}`),
        h("span", {attrs: {role: "status"}}, this.message || "Memory only · Runtime + hosted iframe · 16K characters per message"),
      ]),
    ];
  }
}
