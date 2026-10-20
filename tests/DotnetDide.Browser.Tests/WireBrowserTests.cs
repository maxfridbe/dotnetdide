namespace DotnetDide.Browser.Tests;

public sealed class WireBrowserTests
{
    [Fact]
    public async Task AspNetServerRunsInWasmAndSandboxedPreviewCallsIt()
    {
        await using var browser = new BrowserHarness(); await browser.StartAsync();
        var result = await browser.EvaluateAsync("""
            globalThis.wire=await api.loadDotnetdide({siteAccess:true});
            const source=await (await fetch('./examples/site.cs')).text();
            const started=await wire.runCSharpAsync(source);if(!started.success)throw Error(JSON.stringify(started));
            const sites=await wire.listSites();if(sites.length!==1||sites[0].port!==8080)throw Error('No virtual site');
            const page=await wire.requestSite({port:8080,method:'GET',path:'/'});if(page.status!==200)throw Error(JSON.stringify(page));
            const json=await wire.requestSite({port:8080,method:'GET',path:'/api/hello'});if(json.status!==200||!atob(json.body).includes('IServer'))throw Error(JSON.stringify(json));
            const frame=document.createElement('iframe');frame.id='wire-preview';frame.style.cssText='position:fixed;inset:0;width:100vw;height:100vh;z-index:9999';document.body.append(frame);
            globalThis.wirePreview=await api.createWirePreview({iframe:frame,port:8080,request:value=>wire.requestSite(value)});
            // Opaque origin: a running site cannot read parent DOM or saved credentials.
            if(frame.getAttribute('sandbox')!=='allow-scripts allow-forms')throw Error('Preview sandbox changed');
            return true;
            """);
        Assert.True(result.GetBoolean());
        // Execute through the iframe's own isolated context via CDP (never enable same-origin access).
        var interact = await browser.EvaluateFrameAsync("wire-preview", """
            document.querySelector('#hello').click();
            for(let end=Date.now()+10000;Date.now()<end&&!document.querySelector('#answer').textContent.includes('IServer');)await new Promise(r=>setTimeout(r,25));
            if(!document.querySelector('#answer').textContent.includes('IServer'))throw Error('C# request did not reach iframe');
            document.querySelector('#note').requestSubmit();
            for(let end=Date.now()+10000;Date.now()<end&&!document.querySelector('#saved').textContent.includes('Saved');)await new Promise(r=>setTimeout(r,25));
            let blocked=false;try{parent.document.body}catch{blocked=true}
            let network=false;try{await fetch('https://example.org')}catch{network=true}
            return {blocked,network,saved:document.querySelector('#saved').textContent};
            """);
        Assert.True(interact.GetProperty("blocked").GetBoolean(), interact.ToString());
        Assert.True(interact.GetProperty("network").GetBoolean(), interact.ToString());
        Assert.Contains("Saved note.txt", interact.GetProperty("saved").GetString());
        Assert.Equal("Hello from the iframe", (await browser.EvaluateAsync("return await wire.FS.readFile('note.txt',{encoding:'utf8'});")).GetString());
        await browser.EvaluateAsync("wirePreview.destroy();await wire.setSiteAccess(false);if((await wire.listSites()).length)throw Error('Site survived revocation');wire.terminate();");
    }
    [Fact]
    public async Task SiteHttpClientObeysNetworkPermissionAfterRegistration()
    {
        await using var browser = new BrowserHarness(); await browser.StartAsync();
        await browser.EvaluateAsync("""
            globalThis.siteNet = await api.loadDotnetdide({siteAccess:true});
            // A different hostname exercises browser CORS, not just same-origin HTTP.
            const endpoint = new URL('/network-probe', location.origin);
            endpoint.hostname = 'localhost';
            const source = `
                using System.Net.Http;
                using DotnetDide;
                using Microsoft.AspNetCore.Builder;
                using Microsoft.AspNetCore.Http;
                var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                http.DefaultRequestHeaders.Add("User-Agent", "zip-weather-demo");
                var app = Wire.CreateApplication();
                app.UseRouting();
                app.UseEndpoints(routes => routes.MapGet("/api/weather", async context => {
                    try {
                        using var response = await http.GetAsync(${JSON.stringify(endpoint.href)}, context.RequestAborted);
                        await context.Response.WriteAsync(await response.Content.ReadAsStringAsync());
                    } catch (Exception error) {
                        context.Response.StatusCode = 502;
                        await context.Response.WriteAsync(error.ToString());
                    }
                }));
                await Wire.ListenAsync(8080, app);`;
            // Compile the file just as Lethetic's Files > Run does.
            await siteNet.FS.writeFile('weather.cs', source);
            const started = await siteNet.runCSharpAsync('var result = await Dotnet.Run("weather.cs"); if (!result.Success) throw new Exception(result.Error?.Message);');
            if (!started.success) throw Error(JSON.stringify(started));
            globalThis.weather = async () => {
                const response = await siteNet.requestSite({port:8080,method:'GET',path:'/api/weather'});
                return {status:response.status,body:atob(response.body)};
            };
            """);
        var blocked = await browser.EvaluateAsync("return await weather();");
        Assert.Equal(502, blocked.GetProperty("status").GetInt32());
        Assert.Contains("Network access is disabled", blocked.GetProperty("body").GetString());
        Assert.Equal(0, browser.NetworkRequests);
        await browser.EvaluateAsync("await siteNet.setNetworkAccess(true);");
        var allowed = await browser.EvaluateAsync("return await weather();");
        Assert.Equal(200, allowed.GetProperty("status").GetInt32());
        Assert.Equal("network-ok", allowed.GetProperty("body").GetString());
        Assert.Equal(1, browser.NetworkRequests);
        await browser.EvaluateAsync("await siteNet.setNetworkAccess(false);");
        var revoked = await browser.EvaluateAsync("return await weather();");
        Assert.Equal(502, revoked.GetProperty("status").GetInt32());
        Assert.Contains("Network access is disabled", revoked.GetProperty("body").GetString());
        Assert.Equal(1, browser.NetworkRequests);
        await browser.EvaluateAsync("siteNet.terminate();");
    }

    [Fact]
    public async Task PlaygroundOffersSiteExamplePermissionPreviewAndStop()
    {
        await using var browser = new BrowserHarness(); await browser.StartAsync();
        await browser.WaitForAsync("!!document.querySelector('[data-example=site]')");
        Assert.False((await browser.EvaluateAsync("return document.querySelector('#site-access').checked;")).GetBoolean());
        await browser.EvaluateAsync("document.querySelector('#site-access').click();");
        await browser.WaitForAsync("!document.querySelector('#run').disabled");
        await browser.EvaluateAsync("document.querySelector('[data-example=site]').click();document.querySelector('#run').click();");
        await browser.WaitForAsync("document.querySelector('#site-port').value==='8080' && !document.querySelector('#run').disabled");
        await browser.EvaluateAsync("document.querySelector('#site-open').click();document.querySelector('.site-panel').scrollIntoView();");
        await browser.WaitForAsync("!document.querySelector('#site-frame').hidden");
        var answer = await browser.EvaluateFrameAsync("site-frame", """
            for(let end=Date.now()+10000;Date.now()<end&&!document.querySelector('#hello');) await new Promise(r=>setTimeout(r,50));
            document.querySelector('#hello').click();
            for(let end=Date.now()+10000;Date.now()<end&&!document.querySelector('#answer').textContent.includes('IServer');)await new Promise(r=>setTimeout(r,50));
            return document.querySelector('#answer').textContent;
            """);
        Assert.Contains("IServer", answer.GetString());
        await browser.ScreenshotAsync("playground-wire.png");
        await browser.EvaluateAsync("document.querySelector('#site-stop').click();");
        await browser.WaitForAsync("!document.querySelector('#site-port').value && !document.querySelector('#run').disabled");
        Assert.True((await browser.EvaluateAsync("return document.querySelector('#files').textContent.includes('site/index.html');")).GetBoolean());
    }

}
