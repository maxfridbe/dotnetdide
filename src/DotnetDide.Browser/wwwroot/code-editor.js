// Monaco's prebuilt browser distribution is pinned and served with this site.
// Keep the textarea usable while loading and if any editor asset is unavailable.
export function createCodeEditor({ container, fallback, host, message, run }) {
  let editor;
  const changed = () => {
    if (editor) globalThis.monaco.editor.setModelMarkers(editor.getModel(), 'roslyn', []);
  };
  const api = {
    getValue: () => editor ? editor.getValue() : fallback.value,
    setValue(value) {
      fallback.value = value;
      editor?.setValue(value);
      changed();
    },
    setDiagnostics(diagnostics, submittedCode) {
      if (!editor || editor.getValue() !== submittedCode) return;
      const model = editor.getModel(), monaco = globalThis.monaco;
      monaco.editor.setModelMarkers(model, 'roslyn', diagnostics.map(diagnostic => {
        const line = Math.max(1, Math.min(model.getLineCount(), diagnostic.line));
        const column = Math.max(1, Math.min(model.getLineMaxColumn(line), diagnostic.column));
        const word = model.getWordAtPosition({ lineNumber: line, column });
        return {
          code: diagnostic.id, message: diagnostic.message, source: 'Roslyn',
          severity: diagnostic.severity === 'Error' ? monaco.MarkerSeverity.Error : diagnostic.severity === 'Warning' ? monaco.MarkerSeverity.Warning : monaco.MarkerSeverity.Info,
          startLineNumber: line, startColumn: column, endLineNumber: line,
          endColumn: word?.endColumn > column ? word.endColumn : Math.min(model.getLineMaxColumn(line), column + 1),
        };
      }));
    },
  };
  fallback.addEventListener('keydown', event => {
    if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) { event.preventDefault(); run(); }
  });
  container.dataset.editor = 'loading';
  const base = new URL('./vendor/monaco/vs/', import.meta.url).href;
  const loader = document.createElement('script');
  loader.src = base + 'loader.js';
  let finished = false;
  function failed() {
    if (finished) return;
    finished = true; clearTimeout(timer);
    editor?.dispose(); editor = null; host.hidden = true; fallback.hidden = false;
    container.dataset.editor = 'fallback';
    message.textContent = 'Monaco could not load. The plain text editor is available; reload to retry.';
  }
  const timer = setTimeout(failed, 15000);
  loader.onerror = failed;
  loader.onload = () => {
    if (finished) return;
    globalThis.require.config({ paths: { vs: base.replace(/\/$/, '') } });
    globalThis.require(['vs/editor/editor.main'], monaco => {
      if (finished) return;
      try {
        host.hidden = false;
        const narrow = matchMedia('(max-width: 800px)');
        monaco.editor.defineTheme('dotnetdide', {
          base: 'vs-dark', inherit: true, rules: [],
          colors: { 'editor.background': '#09121e', 'editorLineNumber.foreground': '#64748b', 'editorCursor.foreground': '#8cf0d1' },
        });
        editor = monaco.editor.create(host, {
          value: fallback.value, language: 'csharp', theme: 'dotnetdide',
          ariaLabel: 'C# submission', automaticLayout: true, minimap: { enabled: false },
          fontSize: narrow.matches ? 16 : 14, lineHeight: 23, tabSize: 4, insertSpaces: true,
          scrollBeyondLastLine: false, padding: { top: 14, bottom: 14 },
          wordWrap: 'on', lineNumbersMinChars: 3, glyphMargin: false,
          bracketPairColorization: { enabled: true }, fixedOverflowWidgets: true,
          // Let Tab leave the editor for keyboard navigation. Monaco's standard
          // Ctrl+M toggle enables indentation with Tab; toolbar hints explain it.
          tabFocusMode: true,
        });
        editor.addAction({ id: 'dotnetdide.run', label: 'Run C# submission', keybindings: [monaco.KeyMod.CtrlCmd | monaco.KeyCode.Enter], run });
        editor.onDidChangeModelContent(() => { fallback.value = editor.getValue(); changed(); });
        narrow.addEventListener('change', event => editor?.updateOptions({ fontSize: event.matches ? 16 : 14 }));
        fallback.hidden = true;
        container.dataset.editor = 'monaco';
        message.textContent = 'C# highlighting · Ctrl/⌘ + Enter runs · Ctrl+M toggles Tab indentation';
        finished = true; clearTimeout(timer);
      } catch { failed(); }
    }, failed);
  };
  document.head.append(loader);
  return api;
}
