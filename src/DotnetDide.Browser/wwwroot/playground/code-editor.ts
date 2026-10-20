import type { Diagnostic } from "../dotnetdide.js";
interface Model {
  getLineCount(): number; getLineMaxColumn(line: number): number;
  getWordAtPosition(position: {lineNumber: number; column: number}): {endColumn: number} | null;
  dispose(): void;
}
interface Editor {
  getValue(): string; setValue(value: string): void; getModel(): Model;
  dispose(): void; updateOptions(options: Record<string, unknown>): void;
  addAction(action: Record<string, unknown>): void;
  onDidChangeModelContent(callback: () => void): {dispose(): void};
}
interface Monaco {
  editor: {
    create(element: HTMLElement, options: Record<string, unknown>): Editor;
    defineTheme(name: string, options: Record<string, unknown>): void;
    setModelMarkers(model: Model, owner: string, markers: Record<string, unknown>[]): void;
  };
  KeyMod: {CtrlCmd: number}; KeyCode: {Enter: number};
  MarkerSeverity: {Error: number; Warning: number; Info: number};
}
interface AmdRequire {
  (modules: string[], ready: (monaco: Monaco) => void, failed: () => void): void;
  config(options: Record<string, unknown>): void;
}
declare global { var monaco: Monaco; var require: AmdRequire }
export interface CodeEditor {
  getValue(): string; setValue(value: string): void; setDiagnostics(diagnostics: Diagnostic[], code: string): void; dispose(): void;
}

/** An opaque Snabbdom island: Monaco owns only the descendants of its mount. */
export function createCodeEditor(container: HTMLElement, value: string, run: () => void): CodeEditor {
  const host = document.createElement("div"); host.id = "monaco"; host.hidden = true;
  const fallback = document.createElement("textarea"); fallback.id = "code"; fallback.value = value;
  fallback.spellcheck = false; fallback.autocapitalize = "off"; fallback.autocomplete = "off";
  fallback.setAttribute("autocorrect", "off"); fallback.setAttribute("aria-label", "C# submission");
  fallback.setAttribute("aria-describedby", "editor-help");
  const message = document.createElement("p"); message.id = "editor-state"; message.className = "hint";
  message.role = "status"; message.textContent = "Loading Monaco… The plain text editor is ready.";
  container.append(host, fallback, message);
  let editor: Editor | undefined, finished = false, disposed = false;
  const narrow = matchMedia("(max-width: 800px)");
  const changed = (): void => { if (editor) monaco.editor.setModelMarkers(editor.getModel(), "roslyn", []); };
  const keydown = (event: KeyboardEvent): void => {
    if (event.key === "Enter" && (event.ctrlKey || event.metaKey)) { event.preventDefault(); run(); }
  };
  const resize = (): void => editor?.updateOptions({fontSize: narrow.matches ? 16 : 14});
  fallback.addEventListener("keydown", keydown);
  container.dataset.editor = "loading";
  const base = new URL("../vendor/monaco/vs/", import.meta.url).href;
  const loader = document.createElement("script"); loader.src = base + "loader.js";
  const failed = (): void => {
    if (finished || disposed) return;
    finished = true; clearTimeout(timer); editor?.dispose(); editor = undefined;
    host.hidden = true; fallback.hidden = false; container.dataset.editor = "fallback";
    message.textContent = "Monaco could not load. The plain text editor is available; reload to retry.";
  };
  const timer = setTimeout(failed, 15000);
  loader.onerror = failed;
  loader.onload = (): void => {
    if (finished || disposed) return;
    globalThis.require.config({paths: {vs: base.replace(/\/$/u, "")}});
    globalThis.require(["vs/editor/editor.main"], monaco => {
      if (finished || disposed) return;
      try {
        host.hidden = false;
        monaco.editor.defineTheme("dotnetdide", {base: "vs-dark", inherit: true, rules: [],
          colors: {"editor.background": "#151820", "editorLineNumber.foreground": "#657086", "editorCursor.foreground": "#93c5fd"}});
        editor = monaco.editor.create(host, {value: fallback.value, language: "csharp", theme: "dotnetdide",
          ariaLabel: "C# submission", automaticLayout: true, minimap: {enabled: false},
          fontSize: narrow.matches ? 16 : 14, lineHeight: 23, tabSize: 4, insertSpaces: true,
          scrollBeyondLastLine: false, padding: {top: 14, bottom: 14}, wordWrap: "on", lineNumbersMinChars: 3,
          glyphMargin: false, bracketPairColorization: {enabled: true}, fixedOverflowWidgets: true, tabFocusMode: true});
        editor.addAction({id: "dotnetdide.run", label: "Run C# submission", keybindings: [monaco.KeyMod.CtrlCmd | monaco.KeyCode.Enter], run});
        editor.onDidChangeModelContent(() => { fallback.value = editor!.getValue(); changed(); });
        narrow.addEventListener("change", resize);
        fallback.hidden = true; container.dataset.editor = "monaco";
        message.textContent = "C# highlighting · Ctrl/⌘ + Enter runs · Ctrl+M toggles Tab indentation";
        finished = true; clearTimeout(timer);
      } catch { failed(); }
    }, failed);
  };
  document.head.append(loader);
  return {
    getValue: () => editor?.getValue() ?? fallback.value,
    setValue(text): void { fallback.value = text; editor?.setValue(text); changed(); },
    setDiagnostics(diagnostics, submittedCode): void {
      if (!editor || editor.getValue() !== submittedCode) return;
      const model = editor.getModel();
      monaco.editor.setModelMarkers(model, "roslyn", diagnostics.map(diagnostic => {
        const line = Math.max(1, Math.min(model.getLineCount(), diagnostic.line));
        const column = Math.max(1, Math.min(model.getLineMaxColumn(line), diagnostic.column));
        const word = model.getWordAtPosition({lineNumber: line, column});
        return {code: diagnostic.id, message: diagnostic.message, source: "Roslyn",
          severity: diagnostic.severity === "Error" ? monaco.MarkerSeverity.Error : diagnostic.severity === "Warning" ? monaco.MarkerSeverity.Warning : monaco.MarkerSeverity.Info,
          startLineNumber: line, startColumn: column, endLineNumber: line,
          endColumn: word && word.endColumn > column ? word.endColumn : Math.min(model.getLineMaxColumn(line), column + 1)};
      }));
    },
    dispose(): void {
      disposed = true; clearTimeout(timer); narrow.removeEventListener("change", resize);
      fallback.removeEventListener("keydown", keydown); loader.remove();
      const model = editor?.getModel(); editor?.dispose(); model?.dispose();
    },
  };
}
