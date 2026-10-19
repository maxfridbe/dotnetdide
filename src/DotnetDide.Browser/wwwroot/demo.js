import { loadDotnetdide } from './dotnetdide.js';
import { examples } from './examples.js';

const field = id => document.getElementById(id);
let runtime, job, selected = examples[0];
const download = (data, name, type) => {
  const url = URL.createObjectURL(new Blob([data], { type }));
  const link = document.createElement('a'); link.href = url; link.download = name; link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
};
function status(message, state = 'ready') {
  field('status').textContent = message;
  field('status').dataset.state = state;
}
function buttons() {
  for (const id of ['run', 'import', 'check']) field(id).disabled = !!job;
  for (const id of ['reset', 'save']) field(id).disabled = !!job || !runtime;
  field('stop').disabled = !job && !runtime;
}
function loadStep(index) {
  field('code').value = selected.steps[index];
  for (const [i, button] of [...field('steps').children].entries()) button.setAttribute('aria-pressed', i === index);
}
function selectExample(example) {
  selected = example;
  field('example-title').textContent = example.title;
  field('description').textContent = example.description;
  field('expected').textContent = example.expected;
  for (const button of field('examples').children) button.setAttribute('aria-pressed', button.dataset.example === example.id);
  field('steps').replaceChildren();
  if (example.steps.length > 1) example.steps.forEach((_, index) => {
    const button = document.createElement('button'); button.textContent = `Step ${index + 1}`;
    button.onclick = () => loadStep(index); field('steps').append(button);
  });
  loadStep(0);
}
for (const example of examples) {
  const button = document.createElement('button'), category = document.createElement('small');
  category.textContent = example.category; button.append(category, document.createTextNode(example.title));
  button.dataset.example = example.id; button.onclick = () => selectExample(example);
  field('examples').append(button);
}
selectExample(selected);

function renderResult(result) {
  field('output').textContent = JSON.stringify(result, null, 2);
  const sections = [];
  if (result.stdout) sections.push(result.stdout.trimEnd());
  if (result.stderr) sections.push('stderr:\n' + result.stderr.trimEnd());
  if (result.value !== null) sections.push('→ ' + result.value);
  for (const diagnostic of result.diagnostics) sections.push(`${diagnostic.id} · line ${diagnostic.line}, column ${diagnostic.column}\n${diagnostic.message}`);
  if (result.error) sections.push(`${result.error.type}: ${result.error.message}`);
  field('result').textContent = sections.join('\n\n') || (result.success ? 'Completed without a return value.' : 'Submission failed.');
}
function fileSize(bytes) {
  return bytes < 1024 ? `${bytes} B` : bytes < 1048576 ? `${(bytes / 1024).toFixed(1)} KiB` : `${(bytes / 1048576).toFixed(1)} MiB`;
}
async function refreshFiles(active) {
  const snapshot = await active.exportWorkspace();
  if (runtime !== active) return;
  field('files').replaceChildren(); field('file-count').textContent = `(${snapshot.files.length})`;
  for (const file of snapshot.files) {
    const item = document.createElement('li'), button = document.createElement('button');
    const name = document.createElement('span'), action = document.createElement('span');
    name.textContent = file.path;
    action.textContent = `${fileSize(atob(file.data).length)} · Download ↓`;
    button.append(name, action);
    // Download the displayed snapshot, even if a subsequent run replaces the file.
    button.onclick = () => download(Uint8Array.from(atob(file.data), c => c.charCodeAt(0)), file.path.split('/').at(-1), 'application/octet-stream');
    item.append(button); field('files').append(item);
  }
  if (!snapshot.files.length) { const item = document.createElement('li'); item.className = 'hint'; item.textContent = 'No files in this workspace.'; field('files').append(item); }
}
async function action(work, { isolated = false } = {}) {
  if (job) return;
  const current = { controller: new AbortController(), runtime: null };
  job = current; buttons();
  try {
    if (isolated || !runtime) {
      status('Loading .NET 11 and Roslyn… First load may take a minute.', 'busy');
      current.runtime = await loadDotnetdide({ signal: current.controller.signal });
      if (!isolated) runtime = current.runtime;
    } else current.runtime = runtime;
    const message = await work(current.runtime, current.controller.signal);
    if (!isolated) await refreshFiles(current.runtime);
    if (job === current) status(message || `.NET ${current.runtime.version} · Ready`);
  } catch (error) {
    if (job === current) status(String(error), 'error');
  } finally {
    if (isolated) current.runtime?.terminate();
    if (job === current) { job = null; buttons(); }
  }
}
field('run').onclick = () => {
  const code = field('code').value;
  return action(async active => {
    status('Compiling and running…', 'busy'); const start = performance.now();
    const result = await active.runCSharpAsync(code);
    renderResult(result);
    field('timing').textContent = `${((performance.now() - start) / 1000).toFixed(2)} s · .NET ${active.version}`;
    return result.success ? undefined : 'Submission failed. See the diagnostics below. Ready for another run.';
  });
};
field('code').addEventListener('keydown', event => {
  if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) { event.preventDefault(); field('run').click(); }
});
field('reset').onclick = () => action(async active => { await active.reset(); return 'Variables reset; workspace files retained. Ready.'; });
field('stop').onclick = () => {
  const current = job; job = null;
  current?.controller.abort(); current?.runtime?.terminate();
  runtime?.terminate(); runtime = null;
  if (field('checks').querySelector('[data-state="running"]')) {
    field('check-summary').textContent = 'Checks stopped; unfinished checks have not passed.';
    for (const item of field('checks').children) if (item.dataset.state === 'running' || item.dataset.state === 'pending') { item.dataset.state = 'stopped'; item.textContent += ' · Stopped'; }
  }
  field('files').replaceChildren(); field('file-count').textContent = '(0)';
  status('Worker stopped; variables and files discarded. Run again to start fresh.'); buttons();
};
field('save').onclick = () => action(async active => download(JSON.stringify(await active.exportWorkspace(), null, 2), 'dotnetdide-files.json', 'application/json'));
field('import').onchange = () => {
  const file = field('import').files[0]; field('import').value = '';
  if (file) return action(async active => active.importWorkspace(JSON.parse(await file.text())));
};
field('check').onclick = () => {
  field('checks').replaceChildren();
  const rows = examples.map(example => {
    const row = document.createElement('li'); row.dataset.state = 'pending'; row.textContent = `○ ${example.title}`;
    field('checks').append(row); return row;
  });
  field('check-summary').textContent = 'Loading test workspace…';
  return action(async (active, signal) => {
    let passed = 0;
    for (const [index, example] of examples.entries()) {
      signal.throwIfAborted();
      const row = rows[index]; row.dataset.state = 'running'; row.textContent = `◌ ${example.title}`;
      status(`Checking ${index + 1}/${examples.length}: ${example.title}…`, 'busy');
      await active.reset(); await active.importWorkspace({ version: 1, directories: [], files: [] });
      const results = [];
      for (const code of example.steps) results.push(await active.runCSharpAsync(code, { signal, timeoutMs: 15000 }));
      const success = (example.expectsFailure || results.every(r => r.success)) && await example.verify(results, active);
      row.dataset.state = success ? 'pass' : 'fail';
      row.textContent = `${success ? '✓' : '✗'} ${example.title}`;
      if (success) passed++;
      else { const details = document.createElement('details'), summary = document.createElement('summary'), output = document.createElement('pre'); summary.textContent = 'Failure details'; output.textContent = JSON.stringify(results, null, 2); details.append(summary, output); row.append(details); }
      field('check-summary').textContent = `${passed}/${index + 1} checks passed`;
    }
    field('check-summary').textContent = `${passed}/${examples.length} checks passed`;
    return `${passed}/${examples.length} checks passed on .NET ${active.version}. Your workspace is unchanged.`;
  }, { isolated: true }).finally(() => {
    for (const row of rows) if (row.dataset.state === 'running' || row.dataset.state === 'pending') { row.dataset.state = 'fail'; row.textContent += ' · Not completed'; }
    if (rows.some(row => row.dataset.state === 'fail')) field('check-summary').textContent = `${rows.filter(row => row.dataset.state === 'pass').length}/${examples.length} checks passed; see failures and status.`;
  });
};
