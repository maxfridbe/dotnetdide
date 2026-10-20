namespace DotnetDide.Browser.Tests;

public sealed class BrowserTests
{
    [Fact]
    public async Task NetworkHelpersSupportBinaryJsonAndCustomRequests()
    {
        await using var browser = new BrowserHarness(); await browser.StartAsync();
        await browser.EvaluateAsync("globalThis.httpRuntime = await api.loadDotnetdide({networkAccess:true}); globalThis.originUrl=location.origin;");
        var bytes = await browser.EvaluateAsync("return await httpRuntime.runCSharpAsync('System.Text.Encoding.UTF8.GetString(await Dotnetdide.Net.GetBytesAsync(' + JSON.stringify(originUrl + '/network-probe') + '))');");
        Assert.True(bytes.GetProperty("success").GetBoolean(), bytes.ToString()); Assert.Equal("network-ok", bytes.GetProperty("value").GetString());
        var json = await browser.EvaluateAsync("return await httpRuntime.runCSharpAsync('await Dotnetdide.Net.PostJsonAsync(' + JSON.stringify(originUrl + '/network-echo') + ', new { answer = 42 })');");
        Assert.True(json.GetProperty("success").GetBoolean(), json.ToString()); Assert.Equal("{\"answer\":42}", json.GetProperty("value").GetString());
        var custom = await browser.EvaluateAsync("return await httpRuntime.runCSharpAsync('using System.Net.Http; using (var req = new HttpRequestMessage(HttpMethod.Get, ' + JSON.stringify(originUrl + '/network-probe') + ')) { using (var response = await Dotnetdide.Net.SendAsync(req)) { Console.Write(await response.Content.ReadAsStringAsync()); } }');");
        Assert.True(custom.GetProperty("success").GetBoolean(), custom.ToString()); Assert.Equal("network-ok", custom.GetProperty("stdout").GetString());
        var cancelled = await browser.EvaluateAsync("return await httpRuntime.runCSharpAsync('using (var cts = new System.Threading.CancellationTokenSource(100)) { try { await Dotnetdide.Net.GetStringAsync(' + JSON.stringify(originUrl + '/network-slow') + ', cts.Token); Console.Write(\"unexpected\"); } catch (OperationCanceledException) { Console.Write(\"cancelled\"); } }');");
        Assert.True(cancelled.GetProperty("success").GetBoolean(), cancelled.ToString()); Assert.Equal("cancelled", cancelled.GetProperty("stdout").GetString());
        await browser.EvaluateAsync("httpRuntime.terminate();");
    }
    [Fact]
    public async Task NetworkPermissionCoversHelperAndOrdinaryHttpClientWithoutLosingState()
    {
        await using var browser = new BrowserHarness(); await browser.StartAsync();
        await browser.EvaluateAsync("globalThis.net = await api.loadDotnetdide(); globalThis.url = new URL('/network-probe', location.href).href; await net.runCSharpAsync('int kept = 42;');");
        var blocked = await browser.EvaluateAsync("return await net.runCSharpAsync('await new System.Net.Http.HttpClient().GetStringAsync(' + JSON.stringify(url) + ')');");
        Assert.False(blocked.GetProperty("success").GetBoolean()); Assert.Equal(0, browser.NetworkRequests);
        await browser.EvaluateAsync("await net.setNetworkAccess(true);");
        var helper = await browser.EvaluateAsync("return await net.runCSharpAsync('await Dotnetdide.Net.GetStringAsync(' + JSON.stringify(url) + ')');");
        Assert.True(helper.GetProperty("success").GetBoolean(), helper.ToString()); Assert.Equal("network-ok", helper.GetProperty("value").GetString());
        var normal = await browser.EvaluateAsync("return await net.runCSharpAsync('await new System.Net.Http.HttpClient().GetStringAsync(' + JSON.stringify(url) + ')');");
        Assert.True(normal.GetProperty("success").GetBoolean()); Assert.Equal(2, browser.NetworkRequests);
        await browser.EvaluateAsync("await net.setNetworkAccess(false);");
        var revoked = await browser.EvaluateAsync("return await net.runCSharpAsync('await Dotnetdide.Net.GetStringAsync(' + JSON.stringify(url) + ')');");
        Assert.False(revoked.GetProperty("success").GetBoolean()); Assert.Equal(2, browser.NetworkRequests);
        Assert.Equal("42", (await browser.EvaluateAsync("return (await net.runCSharpAsync('kept')).value;")).GetString());
        await browser.EvaluateAsync("net.terminate();");
    }
    [Fact]
    public async Task RealDotnet11RunspaceAndFilesystemWorkInsideBrowserWorkers()
    {
        await using var browser = new BrowserHarness(); await browser.StartAsync();
        var version = await browser.EvaluateAsync("globalThis.r = await api.loadDotnetdide(); return r.version;");
        Assert.StartsWith("11.", version.GetString());
        var state = await browser.EvaluateAsync("await r.runCSharpAsync('int answer = 40;'); return await r.runCSharpAsync('await Task.Delay(1); Console.Write(answer + 2); answer + 2');");
        Assert.True(state.GetProperty("success").GetBoolean()); Assert.Equal("42", state.GetProperty("value").GetString()); Assert.Equal("42", state.GetProperty("stdout").GetString());
        var fs = await browser.EvaluateAsync("await r.FS.mkdir('inputs'); await r.FS.writeFile('inputs/raw.bin', new Uint8Array([0,255,128])); await r.runCSharpAsync('Directory.CreateDirectory(\"outputs\"); File.WriteAllBytes(\"outputs/copy.bin\",File.ReadAllBytes(\"inputs/raw.bin\"));'); return [...await r.FS.readFile('outputs/copy.bin')];");
        Assert.Equal("[0,255,128]", fs.GetRawText());
        var error = await browser.EvaluateAsync("return await r.runCSharpAsync('unknownVariable');");
        Assert.False(error.GetProperty("success").GetBoolean()); Assert.Contains(error.GetProperty("diagnostics").EnumerateArray(), d => d.GetProperty("id").GetString() == "CS0103");
        var runtimeError = await browser.EvaluateAsync("return await r.runCSharpAsync('Console.Error.Write(\"before\"); throw new Exception(\"boom\");');");
        Assert.Equal("boom", runtimeError.GetProperty("error").GetProperty("message").GetString()); Assert.Equal("before", runtimeError.GetProperty("stderr").GetString());
        var isolation = await browser.EvaluateAsync("globalThis.second=await api.loadDotnetdide(); const result=await second.runCSharpAsync('answer'); return {success:result.success,files:await second.FS.readdir('.')};");
        Assert.False(isolation.GetProperty("success").GetBoolean()); Assert.Empty(isolation.GetProperty("files").EnumerateArray());
        var restored = await browser.EvaluateAsync("await r.FS.mkdir('empty'); globalThis.snapshot=await r.exportWorkspace(); await second.importWorkspace(snapshot); return {bytes:[...await second.FS.readFile('outputs/copy.bin')],empty:await second.FS.readdir('empty')};");
        Assert.Equal("[0,255,128]", restored.GetProperty("bytes").GetRawText()); Assert.Empty(restored.GetProperty("empty").EnumerateArray());
        var invalid = await browser.EvaluateAsync("try { await second.importWorkspace({version:1,directories:[],files:[{path:'../escape',data:'AA=='}]});return false;}catch{return (await second.FS.readFile('outputs/copy.bin'))[1]===255;}");
        Assert.True(invalid.GetBoolean());
        var reset = await browser.EvaluateAsync("await r.reset(); return {variable:await r.runCSharpAsync('answer'),file:[...await r.FS.readFile('outputs/copy.bin')]};");
        Assert.False(reset.GetProperty("variable").GetProperty("success").GetBoolean()); Assert.Equal("[0,255,128]", reset.GetProperty("file").GetRawText());
        var cancelled = await browser.EvaluateAsync("const start=performance.now();const pending=r.runCSharpAsync('while(true) {}').then(()=>false,()=>true); await new Promise(resolve=>setTimeout(resolve,100));r.terminate();return {rejected:await pending,elapsed:performance.now()-start,other:(await second.runCSharpAsync('6*7')).value};");
        Assert.True(cancelled.GetProperty("rejected").GetBoolean()); Assert.True(cancelled.GetProperty("elapsed").GetDouble() < 5000); Assert.Equal("42", cancelled.GetProperty("other").GetString());
        await browser.EvaluateAsync("second.terminate();");
        await browser.EvaluateAsync("document.querySelector('#run').click();");
        await browser.WaitForAsync("document.querySelector('#status').textContent.includes('Ready') && !!document.querySelector('#files button')");
        var demo = await browser.EvaluateAsync("return JSON.parse(document.querySelector('#output').textContent);");
        Assert.True(demo.GetProperty("success").GetBoolean()); Assert.Equal("42", demo.GetProperty("value").GetString());
        await browser.ScreenshotAsync();
        await browser.EvaluateAsync("document.querySelector('#stop').click();");

    }
    [Fact]
    public async Task PlaygroundExamplesPassWithoutChangingTheInteractiveWorkspace()
    {
        await using var browser = new BrowserHarness(); await browser.StartAsync();
        await browser.WaitForAsync("document.querySelector('#code-editor')?.dataset.editor === 'monaco'");
        Assert.Equal("csharp", (await browser.EvaluateAsync("return monaco.editor.getModels()[0].getLanguageId();")).GetString());
        Assert.True((await browser.EvaluateAsync("return document.querySelector('#code').hidden && !!document.querySelector('#monaco .view-lines');")).GetBoolean());
        Assert.Equal(13, (await browser.EvaluateAsync("return document.querySelectorAll('#examples button').length;")).GetInt32());
        await browser.EvaluateAsync("document.querySelector('#run').click();");
        await browser.WaitForAsync("!document.querySelector('#run').disabled && !!document.querySelector('#files button')");
        await browser.EvaluateAsync("document.querySelector('#check').click();");
        await browser.WaitForAsync("!document.querySelector('#check').disabled");
        Assert.True((await browser.EvaluateAsync("return document.querySelector('#check-summary').textContent === '12/12 checks passed';")).GetBoolean(), (await browser.EvaluateAsync("return document.querySelector('#checks').textContent + document.querySelector('#status').textContent;")).GetString());
        Assert.Equal(12, (await browser.EvaluateAsync("return document.querySelectorAll('#checks [data-state=pass]').length;")).GetInt32());
        Assert.Contains("outputs/answer.txt", (await browser.EvaluateAsync("return document.querySelector('#files').textContent;")).GetString());
        await browser.EvaluateAsync("monaco.editor.getModels()[0].setValue('answer + 3'); document.querySelector('#run').click();");
        await browser.WaitForAsync("!document.querySelector('#run').disabled");
        Assert.Equal("43", (await browser.EvaluateAsync("return JSON.parse(document.querySelector('#output').textContent).value;")).GetString());
        await browser.EvaluateAsync("document.querySelector('[data-example=state]').click(); document.querySelector('#run').click();");
        await browser.WaitForAsync("!document.querySelector('#run').disabled");
        await browser.EvaluateAsync("document.querySelectorAll('#steps button')[1].click(); document.querySelector('#run').click();");
        await browser.WaitForAsync("!document.querySelector('#run').disabled");
        Assert.Equal("42", (await browser.EvaluateAsync("return JSON.parse(document.querySelector('#output').textContent).value;")).GetString());
        await browser.EvaluateAsync("document.querySelector('[data-example=diagnostics]').click(); document.querySelector('#run').click();");
        await browser.WaitForAsync("!document.querySelector('#run').disabled");
        Assert.Contains("CS0103", (await browser.EvaluateAsync("return document.querySelector('#result').textContent;")).GetString());
        Assert.True((await browser.EvaluateAsync("return monaco.editor.getModelMarkers({owner:'roslyn'}).some(m => m.code === 'CS0103' && m.startLineNumber === 1);")).GetBoolean());
        await browser.EvaluateAsync("document.querySelector('[data-example=files]').click(); document.querySelector('#run').click();");
        await browser.WaitForAsync("!document.querySelector('#run').disabled");
        Assert.Empty((await browser.EvaluateAsync("return monaco.editor.getModelMarkers({owner:'roslyn'});")).EnumerateArray());
        await browser.ScreenshotAsync("playground-desktop.png");
        await browser.SetViewportAsync(390, 844);
        await browser.WaitForAsync("document.querySelector('#monaco .monaco-editor').getBoundingClientRect().width <= 360");
        Assert.True((await browser.EvaluateAsync("return document.documentElement.scrollWidth <= innerWidth;")).GetBoolean());
        await browser.ScreenshotAsync("playground-mobile.png");
        await browser.EvaluateAsync("monaco.editor.getModels()[0].setValue('while(true) {}'); document.querySelector('#run').click();");
        await browser.WaitForAsync("document.querySelector('#status').textContent.includes('Compiling')");
        await browser.EvaluateAsync("document.querySelector('#stop').click();");
        Assert.False((await browser.EvaluateAsync("return document.querySelector('#run').disabled;")).GetBoolean());
        await browser.EvaluateAsync("monaco.editor.getModels()[0].setValue('6 * 7'); document.querySelector('#run').click();");
        await browser.WaitForAsync("!document.querySelector('#run').disabled");
        Assert.Equal("42", (await browser.EvaluateAsync("return JSON.parse(document.querySelector('#output').textContent).value;")).GetString());
        await browser.EvaluateAsync("document.querySelector('#stop').click();");
    }
    [Fact]
    public async Task MonacoSelectionUndoAndRunShortcutUseTheEditedCode()
    {
        await using var browser = new BrowserHarness(); await browser.StartAsync();
        await browser.WaitForAsync("document.querySelector('#code-editor').dataset.editor === 'monaco'");
        await browser.EvaluateAsync("globalThis.editor=monaco.editor.getEditors()[0]; editor.setValue('4 * 7'); editor.setSelection(new monaco.Range(1,1,1,2)); editor.focus();");
        await browser.InsertTextAsync("6");
        Assert.Equal("6 * 7", (await browser.EvaluateAsync("return editor.getValue();")).GetString());
        await browser.PressKeyAsync("z", 90, 2);
        Assert.Equal("4 * 7", (await browser.EvaluateAsync("return editor.getValue();")).GetString());
        await browser.PressKeyAsync("y", 89, 2);
        Assert.Equal("6 * 7", (await browser.EvaluateAsync("return editor.getValue();")).GetString());
        await browser.PressKeyAsync("Enter", 13, 2);
        await browser.WaitForAsync("!document.querySelector('#run').disabled && document.querySelector('#result').textContent.includes('42')");
        Assert.Equal("42", (await browser.EvaluateAsync("return JSON.parse(document.querySelector('#output').textContent).value;")).GetString());
        await browser.EvaluateAsync("document.querySelector('#stop').click();");
    }
    [Fact]
    public async Task PlainTextEditorStillRunsCodeWhenMonacoCannotLoad()
    {
        await using var browser = new BrowserHarness(); await browser.StartAsync(blockMonaco: true);
        await browser.WaitForAsync("document.querySelector('#code-editor').dataset.editor === 'fallback'");
        Assert.True((await browser.EvaluateAsync("return !document.querySelector('#code').hidden && document.querySelector('#monaco').hidden;")).GetBoolean());
        await browser.EvaluateAsync("document.querySelector('[data-example=linq]').click(); document.querySelector('#code').value='7 * 8'; document.querySelector('#code').focus();");
        await browser.PressKeyAsync("Enter", 13, 2);
        await browser.WaitForAsync("!document.querySelector('#run').disabled && document.querySelector('#result').textContent.includes('56')");
        Assert.Equal("56", (await browser.EvaluateAsync("return JSON.parse(document.querySelector('#output').textContent).value;")).GetString());
        await browser.EvaluateAsync("document.querySelector('#stop').click();");
    }
    [Fact]
    public async Task WorkspaceProgramsBuildAndAsyncMainRunsInsideWasm()
    {
        await using var browser = new BrowserHarness(); await browser.StartAsync();
        var result = await browser.EvaluateAsync("globalThis.programRuntime = await api.loadDotnetdide(); await programRuntime.FS.writeFile('Program.cs', 'class Program { static async Task<int> Main(string[] args) { await Task.Delay(1); Console.Write(args[0]); return 3; } }'); return await programRuntime.runCSharpAsync('var program = await Dotnet.Run(\"Program.cs\", new[] { \"wasm\" }); program.ExitCode');");
        Assert.True(result.GetProperty("success").GetBoolean()); Assert.Equal("3", result.GetProperty("value").GetString());
        Assert.Contains("Build succeeded", result.GetProperty("stdout").GetString()); Assert.Contains("wasm", result.GetProperty("stdout").GetString());
        Assert.Equal("[77,90]", (await browser.EvaluateAsync("return [...(await programRuntime.FS.readFile('bin/Program.dll')).slice(0,2)];")).GetRawText());
        Assert.Equal("[66,83,74,66]", (await browser.EvaluateAsync("return [...(await programRuntime.FS.readFile('bin/Program.pdb')).slice(0,4)];")).GetRawText());
        var failed = await browser.EvaluateAsync("await programRuntime.FS.writeFile('Program.cs', 'missingName();'); return await programRuntime.runCSharpAsync('var failed = await Dotnet.Run(\"Program.cs\"); failed.Success');");
        Assert.True(failed.GetProperty("success").GetBoolean()); Assert.Equal("False", failed.GetProperty("value").GetString());
        Assert.Contains("Program.cs(1,1)", failed.GetProperty("stdout").GetString()); Assert.Contains("CS0103", failed.GetProperty("stdout").GetString());
        await browser.EvaluateAsync("programRuntime.terminate();");
        Assert.True((await browser.EvaluateAsync("const guide=await fetch('llm.md'); return guide.ok && (await guide.text()).includes('using System.Threading.Tasks;');")).GetBoolean());
        Assert.True((await browser.EvaluateAsync("const inventory=await fetch('docs/runtime-namespaces.md'); return inventory.ok && (await inventory.text()).includes('System.Text.Json');")).GetBoolean());
    }
}
