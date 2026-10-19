namespace DotnetDide.Browser.Tests;

public sealed class BrowserTests
{
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
}
