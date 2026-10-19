import { loadDotnetdide } from './dotnetdide.js';
const field = id => document.getElementById(id);
let runtime, busy = false;
const download = (data, name, type) => {
  const url = URL.createObjectURL(new Blob([data], { type }));
  const link = document.createElement('a'); link.href = url; link.download = name; link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
};
async function ensureRuntime() {
  if (!runtime) { field('status').textContent = 'Loading .NET 11 and Roslyn…'; runtime = await loadDotnetdide(); }
  return runtime;
}
function buttons() {
  field('run').disabled = busy; field('import').disabled = busy;
  field('reset').disabled = busy || !runtime; field('save').disabled = busy || !runtime;
  field('stop').disabled = !runtime;
}
async function refreshFiles() {
  const snapshot = await runtime.exportWorkspace(); field('files').replaceChildren();
  for (const file of snapshot.files) {
    const item = document.createElement('li'), button = document.createElement('button');
    button.textContent = file.path + ' · Download';
    button.onclick = async () => download(await runtime.FS.readFile(file.path), file.path.split('/').at(-1), 'application/octet-stream');
    item.append(button); field('files').append(item);
  }
}
async function action(work) {
  busy = true; buttons();
  try { await ensureRuntime(); buttons(); await work(); await refreshFiles(); field('status').textContent = `.NET ${runtime.version} · Ready`; }
  catch (error) { field('status').textContent = String(error); }
  finally { busy = false; buttons(); }
}
field('run').onclick = () => action(async () => { field('status').textContent = 'Running…'; field('output').textContent = JSON.stringify(await runtime.runCSharpAsync(field('code').value), null, 2); });
field('reset').onclick = () => action(() => runtime.reset());
field('stop').onclick = () => { runtime?.terminate(); runtime = null; field('files').replaceChildren(); field('status').textContent = 'Worker stopped. Run again to start a fresh runspace.'; buttons(); };
field('save').onclick = () => action(async () => download(JSON.stringify(await runtime.exportWorkspace(), null, 2), 'dotnetdide-files.json', 'application/json'));
field('import').onchange = () => action(async () => { const file=field('import').files[0]; if(file) await runtime.importWorkspace(JSON.parse(await file.text())); field('import').value=''; });
