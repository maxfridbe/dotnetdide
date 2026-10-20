import {loadDotnetdide, createWirePreview} from "../dotnetdide.js";
import type {Runspace, SubmissionResult, Workspace, WireSite, WirePreview} from "../dotnetdide.js";
import {h, init, attributesModule, propsModule, eventListenersModule, classModule, styleModule, download, fileSize} from "./vdom.js";
import type {VNode} from "./vdom.js";
import {examples} from "./examples.js";
import type {Example} from "./examples.js";
import {createCodeEditor} from "./code-editor.js";
import type {CodeEditor} from "./code-editor.js";
import {VirtualNetworkDebugger} from "./network-debugger.js";

interface Job {controller: AbortController; runtime: Runspace | null}
interface Check {title: string; state: "pending" | "running" | "pass" | "fail" | "stopped"; details?: string}
const patch = init([attributesModule, propsModule, eventListenersModule, classModule, styleModule]);
let tree: VNode | Element = document.querySelector("#app")!;
let queued = false;
function changed(): void {
  if (queued) return;
  queued = true;
  queueMicrotask(() => { queued = false; tree = patch(tree, view()); });
}
const network = new VirtualNetworkDebugger(changed);
let editor: CodeEditor;
let runtime: Runspace | null = null, job: Job | null = null;
let selected = examples[0]!, step = 0;
let state = "ready", statusText = "Ready. First run downloads about 40 MiB of .NET and Roslyn.";
let resultText = "Choose an example, edit it, and press Run C#.", rawResult = "No submissions yet.", timing = "";
let files: Workspace["files"] = [], sites: WireSite[] = [], sitePort = "", sitePath = "/";
let siteAccess = false, nugetAccess = false, networkAccess = false;
let checkSummary = `${examples.length} examples · real .NET execution`, checks: Check[] = [];
let preview: WirePreview | null = null, previewGeneration = 0, previewVisible = false, wireBusy = 0;
let siteStatus = "Enable Interactive sites, then run the site example.";
let siteQueue: Promise<unknown> = Promise.resolve();
const busy = (): boolean => !!job || wireBusy > 0;
const status = (text: string, next = "ready"): void => { statusText = text; state = next; changed(); };
function selectExample(example: Example): void { selected = example; loadStep(0); }
function loadStep(index: number): void { step = index; editor.setValue(selected.steps[index]!); changed(); }
function renderResult(result: SubmissionResult): void {
  rawResult = JSON.stringify(result, null, 2);
  const parts: string[] = [];
  if (result.stdout) parts.push(result.stdout.trimEnd());
  if (result.stderr) parts.push("stderr:\n" + result.stderr.trimEnd());
  if (result.value !== null) parts.push("→ " + result.value);
  for (const diagnostic of result.diagnostics) parts.push(`${diagnostic.id} · line ${diagnostic.line}, column ${diagnostic.column}\n${diagnostic.message}`);
  if (result.error) parts.push(`${result.error.type}: ${result.error.message}`);
  resultText = parts.join("\n\n") || (result.success ? "Completed without a return value." : "Submission failed.");
  changed();
}
async function refreshFiles(active: Runspace): Promise<void> {
  const snapshot = await active.exportWorkspace();
  if (runtime === active) { files = snapshot.files; changed(); }
}
function clearPreview(): void {
  previewGeneration++; preview?.destroy(); preview = null; previewVisible = false; changed();
}
async function refreshSites(active: Runspace): Promise<void> {
  const values = await active.listSites();
  if (runtime !== active) return;
  sites = values;
  if (!sites.some(site => String(site.port) === sitePort)) sitePort = String(sites[0]?.port ?? "");
  if (!sites.length) {
    clearPreview();
    siteStatus = siteAccess ? "No site running. Run the site example." : "Interactive sites are disabled.";
  }
  changed();
}
async function action(work: (active: Runspace, signal: AbortSignal) => Promise<string | void>, isolated = false): Promise<void> {
  if (busy()) return;
  const current: Job = {controller: new AbortController(), runtime: null};
  job = current; changed();
  try {
    if (isolated || !runtime) {
      if (!isolated) network.diagnostics.beginRunspace();
      status("Loading .NET 11 and Roslyn… First load may take a minute.", "busy");
      const loaded = await loadDotnetdide({signal: current.controller.signal, nugetAccess, networkAccess, siteAccess: isolated || siteAccess,
        diagnostics: network.diagnostics, debugScope: isolated ? "Checks" : "Playground"});
      current.runtime = loaded;
      if (job !== current) { loaded.terminate(); return; }
      if (!isolated) runtime = loaded;
    } else current.runtime = runtime;
    const message = await work(current.runtime, current.controller.signal);
    if (job !== current) return;
    if (!isolated) { await refreshFiles(current.runtime); await refreshSites(current.runtime); }
    if (job === current) status(message || `.NET ${current.runtime.version} · Ready`);
  } catch (error) {
    if (job === current) status(String(error), "error");
  } finally {
    if (isolated) { current.runtime?.terminate(); if (runtime) void runtime.listAssemblies().catch(() => {}); }
    if (job === current) { job = null; changed(); }
  }
}
function run(): void {
  const code = editor.getValue();
  void action(async (active, signal) => {
    status("Compiling and running…", "busy");
    const start = performance.now();
    const result = await active.runCSharpAsync(code, {signal});
    if (signal.aborted) return;
    renderResult(result); editor.setDiagnostics(result.diagnostics, code);
    timing = `${((performance.now() - start) / 1000).toFixed(2)} s · .NET ${active.version}`;
    return result.success ? undefined : "Submission failed. See the diagnostics below. Ready for another run.";
  });
}
function stop(): void {
  clearPreview(); sites = []; sitePort = ""; siteStatus = "Worker stopped.";
  const current = job; job = null;
  current?.controller.abort(); current?.runtime?.terminate(); runtime?.terminate(); runtime = null;
  if (checks.some(row => row.state === "running")) {
    checkSummary = "Checks stopped; unfinished checks have not passed.";
    for (const row of checks) if (row.state === "pending" || row.state === "running") row.state = "stopped";
  }
  files = [];
  status("Worker stopped; variables and files discarded. Run again to start fresh.");
}
function checkAll(): void {
  if (busy()) return;
  const runnable = examples.filter(example => !example.requiresNuget || nugetAccess);
  const rows: Check[] = runnable.map(example => ({title: example.title, state: "pending"}));
  checks = rows; checkSummary = "Loading test workspace…";
  void action(async (active, signal) => {
    let passed = 0;
    for (const [index, example] of runnable.entries()) {
      signal.throwIfAborted();
      const row = rows[index]!; row.state = "running";
      status(`Checking ${index + 1}/${runnable.length}: ${example.title}…`, "busy");
      await active.reset(); await active.importWorkspace({version: 1, directories: [], files: []});
      const results: SubmissionResult[] = [];
      for (const code of example.steps) results.push(await active.runCSharpAsync(code, {signal, timeoutMs: 15000}));
      signal.throwIfAborted();
      const success = (example.expectsFailure || results.every(result => result.success)) && await example.verify(results, active);
      signal.throwIfAborted();
      row.state = success ? "pass" : "fail";
      if (success) passed++; else row.details = JSON.stringify(results, null, 2);
      checkSummary = `${passed}/${index + 1} checks passed`; changed();
    }
    checkSummary = `${passed}/${runnable.length} checks passed`;
    return `${passed}/${runnable.length} checks passed on .NET ${active.version}. Your workspace is unchanged.`;
  }, true).finally(() => {
    for (const row of rows) if (row.state === "running" || row.state === "pending") { row.state = "fail"; row.details = "Not completed; see status."; }
    if (rows.some(row => row.state === "fail")) checkSummary = `${rows.filter(row => row.state === "pass").length}/${runnable.length} checks passed; see failures and status.`;
    changed();
  });
}
async function openSite(): Promise<void> {
  if (busy() || !runtime || !sitePort) return;
  clearPreview();
  const active = runtime, generation = previewGeneration;
  try {
    const created = await createWirePreview({
      iframe: document.querySelector<HTMLIFrameElement>("#site-frame")!, port: Number(sitePort), path: sitePath,
      onConsole: entry => { if (runtime === active && previewGeneration === generation) active.recordConsole(entry); },
      onNavigate: path => { if (previewGeneration === generation) { sitePath = path; changed(); } },
      request: request => {
        const next = siteQueue.catch(() => {}).then(async () => {
          if (runtime !== active || generation !== previewGeneration) throw new Error("Preview stopped.");
          if (job) throw new Error("Wait for the current C# submission.");
          wireBusy++; changed();
          try {
            const response = await active.requestSite(request);
            if (generation === previewGeneration) {
              await refreshFiles(active);
              siteStatus = `${request.method} ${request.path} → ${response.status}${response.error ? " · " + response.error : ""}`;
            }
            return response;
          } finally { wireBusy--; changed(); }
        });
        siteQueue = next; return next;
      },
    });
    if (generation !== previewGeneration) created.destroy();
    else { preview = created; previewVisible = true; changed(); }
  } catch (error) {
    if (generation === previewGeneration) { clearPreview(); siteStatus = String(error); changed(); }
  }
}
function permission(id: string, label: string, enabled: boolean, set: (value: boolean, active: Runspace) => Promise<void>): VNode {
  return h("label.permission", [
    h(`input#${id}`, {attrs: {type: "checkbox", disabled: busy()}, props: {checked: enabled}, on: {change: event => {
      const value = (event.target as HTMLInputElement).checked;
      if (id === "site-access") { siteAccess = value; clearPreview(); }
      else if (id === "nuget-access") nugetAccess = value;
      else networkAccess = value;
      void action(async active => { await set(value, active); return `${label} ${value ? "enabled" : "disabled"}.`; });
    }}}), label,
  ]);
}
function button(id: string, text: string, disabled: boolean, click: () => void): VNode {
  return h(`button#${id}`, {attrs: {type: "button", disabled}, on: {click}}, text);
}
function view(): VNode {
  return h("div#app.app", {class: {"debugger-right": network.open && network.layoutDock === "right"}}, [
    h("header.app-header", [
      h("a.brand", {attrs: {href: "./"}}, ["dotnet", h("span", "dide")]),
      h("span.app-subtitle", "C# playground"), h("span.badge", ".NET 11 · WASM"),
      h("nav", {attrs: {"aria-label": "Playground tools"}}, [
        h("button#network-toggle", {attrs: {type: "button", "aria-pressed": String(network.open), "aria-controls": "virtual-debugger", title: "Toggle virtual debugger (Alt+N)"},
          on: {click: () => network.toggle()}}, "⌘ DevTools"),
        h("a", {attrs: {href: "docs/playground.md"}}, "Guide"),
        h("a", {attrs: {href: "https://github.com/maxfridbe/dotnetdide"}}, "Source ↗"),
      ]),
    ]),
    h("main.workspace", [
      h("aside.sidebar", {attrs: {"aria-labelledby": "examples-title"}}, [
        h("h2#examples-title", "EXAMPLES"),
        h("nav#examples", {attrs: {"aria-label": "Code examples"}}, examples.map(example =>
          h("button", {key: example.id, attrs: {type: "button", "data-example": example.id, "aria-pressed": String(example === selected)},
            on: {click: () => selectExample(example)}}, [h("small", example.category), example.title]))),
        h("section.checks-panel", [
          h("h2", "Runtime checks"), h("p.hint", "Verify examples in a separate workspace. The NuGet example runs only when NuGet downloads is enabled. Your editor and files stay intact."),
          button("check", "▷ Run all checks", busy(), checkAll),
          h("p#check-summary", {attrs: {role: "status"}}, checkSummary),
          h("ol#checks", {attrs: {"aria-label": "Check results"}}, checks.map((row, index) =>
            h("li", {key: index, attrs: {"data-state": row.state}}, [
              `${({pending: "○", running: "◌", pass: "✓", fail: "✗", stopped: "■"})[row.state]} ${row.title}${row.state === "stopped" ? " · Stopped" : ""}`,
              row.details ? h("details", [h("summary", "Failure details"), h("pre", row.details)]) : null,
            ]))),
        ]),
      ]),
      h("div.lab", [
        h("div.code-column", [
          h("section.editor-panel.panel", {attrs: {"aria-labelledby": "example-title"}}, [
            h("div.section-heading", [h("h1#example-title", selected.title), h("span.badge", "Editable C#")]),
            h("p#description", selected.description), h("p#expected.expected", selected.expected),
            h("div#steps", {attrs: {role: "group", "aria-label": "Example steps"}}, selected.steps.length > 1 ? selected.steps.map((_, index) =>
              h("button", {key: index, attrs: {type: "button", "aria-pressed": String(step === index)}, on: {click: () => loadStep(index)}}, `Step ${index + 1}`)) : []),
            h("div.toolbar", [
              button("run", "▶ Run C#", busy(), run),
              button("reset", "Reset variables", busy() || !runtime, () => { void action(async active => {
                clearPreview(); network.diagnostics.beginRunspace(); await active.reset(); return "Variables reset; workspace files retained. Ready.";
              }); }),
              button("stop", "■ Stop worker", !job && !runtime, stop),
            ]),
            h("div.permissions", [
              permission("site-access", "Interactive sites", siteAccess, (value, active) => active.setSiteAccess(value)),
              permission("network-access", "Network access", networkAccess, (value, active) => active.setNetworkAccess(value)),
              permission("nuget-access", "NuGet downloads", nugetAccess, (value, active) => active.setNugetAccess(value)),
            ]),
            h("div#code-editor", {key: "editor", hook: {
              insert: vnode => { editor = createCodeEditor(vnode.elm as HTMLElement, selected.steps[step]!, run); },
              destroy: () => editor?.dispose(),
            }}),
            h("p#editor-help.hint", "Ctrl/⌘ + Enter to run. Reset retains files; Stop discards the worker."),
            h("p#status", {attrs: {role: "status", "data-state": state}}, statusText),
          ]),
          h("section.result-panel.panel", {attrs: {"aria-labelledby": "result-title"}}, [
            h("div.section-heading", [h("h2#result-title", "Output"), h("span#timing.hint", timing)]),
            h("pre#result", {attrs: {tabindex: "0"}}, resultText),
            h("details", [h("summary", "Raw result / diagnostics"), h("pre#output", {attrs: {tabindex: "0"}}, rawResult)]),
          ]),
        ]),
        h("div.preview-column", [
          h("section.site-panel.panel", {attrs: {"aria-labelledby": "site-title"}}, [
            h("div.section-heading", [h("h2#site-title", "Interactive sites"), h("span.badge", "Wire / IServer")]),
            h("p#site-status", {attrs: {role: "status"}}, siteStatus),
            h("div.site-address", [
              h("label", ["Virtual port", h("select#site-port", {props: {value: sitePort}, on: {change: event => { sitePort = (event.target as HTMLSelectElement).value; changed(); }}},
                sites.map(site => h("option", {key: site.port, attrs: {value: String(site.port)}}, `:${site.port} · ${site.webRoot ?? "Routes"}`)))]),
              h("label", ["Path", h("input#site-path", {attrs: {type: "text"}, props: {value: sitePath},
                on: {input: event => { sitePath = (event.target as HTMLInputElement).value; }, keydown: event => { if ((event as KeyboardEvent).key === "Enter") void openSite(); }}})]),
            ]),
            h("div.toolbar", [
              button("site-open", "↻ Open / Reload", busy() || !sitePort, () => { void openSite(); }),
              button("site-stop", "Stop site", busy() || !sitePort, () => {
                const port = Number(sitePort); clearPreview();
                void action(async active => { await active.runCSharpAsync(`DotnetDide.Wire.Stop(${port});`); return `Stopped virtual port ${port}.`; });
              }),
            ]),
            h("iframe#site-frame", {key: "site-frame", attrs: {title: "Interactive C# site", sandbox: "allow-scripts allow-forms", referrerpolicy: "no-referrer", hidden: !previewVisible}}),
            !previewVisible ? h("div.preview-placeholder", [h("span", "⌘"), h("p", "Your C# site runs here"), h("small", "Enable Interactive sites → run the site example → Open / Reload")]) : null,
            h("p.hint", ["An isolated iframe calls your local C# routes. Requests and console output appear in DevTools. ",
              h("a", {attrs: {href: "docs/wire.md"}}, "Wire guide"), " · ", h("a", {attrs: {href: "examples/site.cs"}}, "Sample C#")]),
          ]),
          h("section.files-panel.panel", {attrs: {"aria-labelledby": "files-title"}}, [
            h("div.section-heading", [h("h2#files-title", ["Workspace files ", h("span#file-count", `(${files.length})`)]),
              button("save", "Export snapshot", busy() || !runtime, () => { void action(async active => { download(JSON.stringify(await active.exportWorkspace(), null, 2), "dotnetdide-files.json"); }); })]),
            h("ul#files", files.length ? files.map(file => h("li", {key: file.path}, [
              h("button", {attrs: {type: "button"}, on: {click: () => download(Uint8Array.from(atob(file.data), value => value.charCodeAt(0)),
                file.path.split("/").at(-1)!, "application/octet-stream")}}, [h("span", file.path), h("span", `${fileSize(atob(file.data).length)} · Download ↓`)]),
            ])) : [h("li.hint", "No files in this workspace.")]),
            h("label.upload", ["Restore a files snapshot", h("input#import", {attrs: {type: "file", accept: "application/json,.json", disabled: busy()},
              on: {change: event => {
                const input = event.target as HTMLInputElement, file = input.files?.[0]; input.value = "";
                if (file) void action(async active => { await active.importWorkspace(JSON.parse(await file.text()) as Workspace); });
              }}})]),
          ]),
          h("footer.workspace-footer", [
            h("p", "Files and variables live in memory. Export a snapshot before refreshing. Snapshots contain files, not C# variables."),
            h("p", [h("a", {attrs: {href: "llm.md"}}, "LLM guide"), " · ",
              h("a", {attrs: {href: "docs/runtime-assemblies.md"}}, "Libraries"), " · ",
              h("a", {attrs: {href: "docs/media-and-packages.md"}}, "Media & NuGet")]),
            h("p", "TypeScript + Snabbdom · Built with .NET · No Node.js"),
          ]),
        ]),
      ]),
    ]),
    network.view(),
  ]);
}
tree = patch(tree, view());
window.addEventListener("resize", changed);
window.addEventListener("keydown", event => {
  if (event.key === "F12" || (event.altKey && event.key.toLowerCase() === "n")) {
    event.preventDefault(); network.toggle();
  }
});
window.addEventListener("pagehide", event => {
  if (!event.persisted) { stop(); editor.dispose(); network.dispose(); }
});
