namespace DotnetDide.Browser.Tests;

public sealed class NetworkTests
{
    [Fact]
    public async Task RelayPreservesPayloadsWhenRequestBodyStreamIsUnavailable()
    {
        await using var browser = new BrowserHarness(); await browser.StartAsync();
        var result = await browser.EvaluateAsync("""
            const {relayFetch}=await import('./relay-fetch.js');
            const descriptor=Object.getOwnPropertyDescriptor(Request.prototype,'body');
            let calls=0;
            try {
              // Firefox exposes arrayBuffer/text but not Request.body in this environment.
              Object.defineProperty(Request.prototype,'body',{configurable:true,get:()=>undefined});
              const relay={endpoint:location.origin+'/relay',token:'regression-test-token-123456789'};
              for(const method of ['GET','HEAD','OPTIONS','DELETE','POST','PUT','PATCH']) {
                const payload=['POST','PUT','PATCH'].includes(method)?new Uint8Array([0,255,128]):undefined;
                const response=await relayFetch(async (url,init)=>{
                  calls++; const envelope=JSON.parse(init.body);
                  if(envelope.method!==method || envelope.bodyBase64!==(payload?'AP+A':['GET','HEAD'].includes(method)?null:''))throw Error('Relay lost request body: '+method);
                  if(init.headers.Authorization!=='Bearer '+relay.token || envelope.headers.authorization!=='Bearer target-key')throw Error('Relay and target credentials mixed');
                  return new Response(JSON.stringify({status:200,headers:{'Content-Type':'application/octet-stream'},bodyBase64:envelope.bodyBase64??''}));
                },relay,'https://target.example/api',{method,headers:{Authorization:'Bearer target-key'},body:payload});
                if((await response.arrayBuffer()).byteLength!==(payload?3:0))throw Error('Relay response lost bytes');
              }
              return calls;
            } finally {
              if(descriptor)Object.defineProperty(Request.prototype,'body',descriptor);else delete Request.prototype.body;
            }
            """);
        Assert.Equal(7, result.GetInt32());
    }

    [Fact]
    public async Task HttpMethodsBrowserOptionsStreamsAndDownloadsWorkInWasm()
    {
        await using var browser = new BrowserHarness(); await browser.StartAsync();
        await browser.EvaluateAsync("globalThis.networkRuntime = await api.loadDotnetdide({networkAccess:true}); globalThis.target=location.origin;");
        var result = await browser.EvaluateAsync("""
            return await networkRuntime.runCSharpAsync(`
                using System.Net.Http;
                using Dotnetdide;
                var options = new BrowserRequestOptions { Credentials = "omit", Mode = "cors", Redirect = "error" };
                foreach (var method in new[] { "GET", "HEAD", "OPTIONS", "DELETE", "POST", "PUT", "PATCH" }) {
                    using (var req = new HttpRequestMessage(new HttpMethod(method), ${JSON.stringify(target + '/network-method')})) {
                        req.Headers.Add("X-Test", "present");
                        if (method is "POST" or "PUT" or "PATCH") req.Content = new ByteArrayContent(new byte[] {0,255,128});
                        using (var response = await Net.SendAsync(req, options)) {
                            Console.Write(method + ":" + (int)response.StatusCode + ":" + response.Headers.GetValues("X-Method").Single() + ":" + response.Headers.GetValues("X-Test").Single() + ":" + Convert.ToBase64String(await response.Content.ReadAsByteArrayAsync()) + ";");
                        }
                    }
                }
                using (var stream = await Net.OpenReadAsync(${JSON.stringify(target + '/network-probe')})) {
                    using (var reader = new StreamReader(stream)) { Console.Write(await reader.ReadToEndAsync()); }
                }
                await Net.DownloadFileAsync(${JSON.stringify(target + '/network-probe')}, "download.txt");
                Console.Write(File.ReadAllText("download.txt"));
                try { await Net.DownloadFileAsync(${JSON.stringify(target + '/missing')}, "download.txt"); } catch (HttpRequestException) { }
                if (File.ReadAllText("download.txt") != "network-ok" || Directory.GetFiles(".", "*.download").Length != 0) throw new Exception("Failed download destroyed the existing file");
                using (var form = await Net.PostFormAsync(${JSON.stringify(target + '/network-method')}, new Dictionary<string,string> { ["x"] = "hello world" })) { Console.Write(await form.Content.ReadAsStringAsync()); }
            `);
            """);
        Assert.True(result.GetProperty("success").GetBoolean(), result.ToString());
        var output = result.GetProperty("stdout").GetString();
        Assert.Contains("DELETE:409:DELETE:present:;", output);
        foreach (var method in new[] {"POST", "PUT", "PATCH"}) Assert.Contains(method + ":200:" + method + ":present:AP+A;", output);
        Assert.Contains("HEAD:200:HEAD:present:;", output);
        Assert.Contains("network-oknetwork-okx=hello+world", output);
        await browser.EvaluateAsync("networkRuntime.terminate();");
    }
}
