import {h, download} from "./vdom.js";
import type {VNode} from "./vdom.js";
import type {DebugInfoStore} from "./debug-info.js";

export class AssembliesPanel {
  filter = "";
  constructor(private readonly store: DebugInfoStore, private readonly changed: () => void) {}
  view(): VNode[] {
    const rows = this.store.assemblies.filter(entry => `${entry.name} ${entry.version} ${entry.fullName}`.toLowerCase().includes(this.filter.toLowerCase()));
    return [
      h("div.network-toolbar", [
        h("input.network-filter", {attrs: {type: "search", placeholder: "Filter assembly name or version…", "aria-label": "Filter loaded assemblies"},
          props: {value: this.filter}, on: {input: event => { this.filter = (event.target as HTMLInputElement).value; this.changed(); }}}),
        h("span", this.store.assemblyScope || "No runspace"),
        h("button", {attrs: {type: "button", "data-assemblies-action": "export", disabled: !rows.length},
          on: {click: () => download(JSON.stringify({assemblyScope: this.store.assemblyScope, assemblies: rows}, null, 2), "dotnetdide-assemblies.json")}}, "Export ↓"),
      ]),
      h("div.assemblies-content", {attrs: {role: "region", "aria-label": "Loaded .NET assemblies", tabindex: "0"}}, [
        h("table.assemblies-table", [
          h("thead", [h("tr", ["Assembly", "Version", "Kind"].map(value => h("th", {attrs: {scope: "col"}}, value)))]),
          h("tbody", rows.map((row, index) => h("tr", {key: `${row.fullName}:${index}`, attrs: {"data-assembly-name": row.name}}, [
            h("td", [h("details", [h("summary", row.name), h("pre", row.fullName)])]),
            h("td", row.version), h("td", row.isDynamic ? "Dynamic" : "Managed"),
          ]))),
        ]),
        rows.length ? null : h("p.network-empty", this.store.assemblies.length ? "No matching assemblies." : "Run C# to inspect the loaded assemblies."),
      ]),
      h("footer.network-footer", [h("span", `${rows.length} / ${this.store.assemblies.length} loaded assemblies`),
        h("span", "Snapshot after runtime operations · Expand an assembly for its full identity")]),
    ];
  }
}
