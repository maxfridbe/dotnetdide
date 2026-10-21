namespace DotnetDide.Browser.Tests;

public sealed class WireStreamingTests
{
    [Fact]
    public async Task UncooperativeStreamIsTerminatedAndLogoIsVisible()
    {
        await using var browser = new BrowserHarness(); await browser.StartAsync();
        await browser.WaitForAsync("document.querySelector('.brand img')?.naturalWidth > 0");
        await browser.ScreenshotAsync("playground-logo.png");
        Assert.True((await browser.EvaluateAsync("""
            const runtime=await api.loadDotnetdide({siteAccess:true});
            const result=await runtime.runCSharpAsync('using Microsoft.AspNetCore.Builder; var app=DotnetDide.Wire.CreateApplication(); app.Run(context=>{while(true){} return Task.CompletedTask;}); await DotnetDide.Wire.ListenAsync(8080,app);');
            if(!result.success)throw Error(JSON.stringify(result));
            let failed=false;try{await runtime.requestSiteStream({port:8080,method:'GET',path:'/'},{timeoutMs:100})}catch{failed=true}
            if(!failed)throw Error('Uncooperative request did not time out');
            let stopped=false;try{await runtime.runCSharpAsync('42')}catch{stopped=true}
            runtime.terminate();return stopped;
            """)).GetBoolean());
    }

    [Fact]
    public async Task BinaryStreamsHandleLargeBodiesCancellationLimitsAndIframeFetch()
    {
        await using var browser = new BrowserHarness(); await browser.StartAsync();
        Assert.True((await browser.EvaluateAsync(""""
            const assert=(ok,message)=>{if(!ok)throw Error(message)};
            globalThis.streaming=await api.loadDotnetdide({siteAccess:true,siteResponseLimitBytes:6*1024*1024});
            const result=await streaming.runCSharpAsync(`
                using Microsoft.AspNetCore.Builder;
                using Microsoft.AspNetCore.Http;
                Directory.CreateDirectory("site");
                File.WriteAllText("site/index.html", """
                    <h1>Stream test</h1><output>waiting</output>
                    <script>fetch('/stream').then(async response=>{let size=0;const reader=response.body.getReader();while(true){const next=await reader.read();if(next.done)break;size+=next.value.length;document.querySelector('output').textContent=size;}document.querySelector('output').dataset.done='true';}).catch(error=>document.querySelector('output').textContent=error.message);</script>
                    """);
                var app=DotnetDide.Wire.CreateApplication();
                app.Run(async context=>{
                    if(context.Request.Path=="/"){context.Response.StatusCode=404;return;}
                    if(context.Request.Path=="/echo"){await context.Request.Body.CopyToAsync(context.Response.Body,context.RequestAborted);return;}
                    context.Response.ContentType="application/octet-stream";
                    var count=context.Request.Path=="/limit"?128:64;
                    var chunk=Enumerable.Repeat((byte)42,65536).ToArray();
                    for(var i=0;i<count;i++)await context.Response.Body.WriteAsync(chunk,context.RequestAborted);
                });
                await DotnetDide.Wire.ListenAsync(8080,app,"site");`);
            assert(result.success,JSON.stringify(result));
            const stream=await streaming.requestSiteStream({port:8080,method:'GET',path:'/stream'});
            assert(stream.body instanceof ReadableStream&&stream.status===200,'No stream headers');
            let queuedDone=false;const queued=streaming.runCSharpAsync('6*7').then(result=>{queuedDone=true;return result});
            await new Promise(resolve=>setTimeout(resolve,100));assert(!queuedDone,'Producer ignored backpressure');
            let total=0;const reader=stream.body.getReader();
            while(true){const {value,done}=await reader.read();if(done)break;assert(value.length<=65536&&value.every(b=>b===42),'Bad binary chunk');total+=value.length;}
            assert(total===4*1024*1024,'Large body truncated');await stream.completed;assert((await queued).value==='42','Queue did not resume');
            const entry=streaming.GetDebugInfo().network.find(row=>row.path==='/stream');
            assert(entry.responseBody.bytes===total&&entry.responseBody.truncated&&entry.responseBody.text.length<=16384,'Debugger buffered entire stream');
            const cancelled=await streaming.requestSiteStream({port:8080,method:'GET',path:'/stream'});
            const cancelReader=cancelled.body.getReader();await cancelReader.read();await cancelReader.cancel('test cancel');
            await cancelled.completed.then(()=>{throw Error('Cancel succeeded')},()=>{});
            assert((await streaming.runCSharpAsync('6*7')).value==='42','Cancellation lost worker');
            const limited=await streaming.requestSiteStream({port:8080,method:'GET',path:'/limit'});
            let failed=false;try{await new Response(limited.body).arrayBuffer()}catch{failed=true}
            assert(failed,'Response transfer cap ignored');
            const echoed=await streaming.requestSite({port:8080,method:'POST',path:'/echo',body:btoa('x'.repeat(3*1024*1024))});
            assert(echoed.status===200&&atob(echoed.body).length===3*1024*1024,'Buffered compatibility or upload still capped at 2 MiB');
            const idle=await streaming.requestSiteStream({port:8080,method:'GET',path:'/stream'},{timeoutMs:100});
            await idle.completed.then(()=>{throw Error('Idle reader did not expire')},()=>{});
            assert((await streaming.runCSharpAsync('42')).value==='42','Idle cancellation lost worker');
            const frame=document.createElement('iframe');frame.id='stream-test-frame';document.body.append(frame);
            globalThis.streamPreview=await api.createWirePreview({iframe:frame,port:8080,
                request:value=>streaming.requestSite(value),requestStream:value=>streaming.requestSiteStream(value)});
            return true;
            """")).GetBoolean());
        await browser.WaitForAsync("streaming.GetDebugInfo().network.filter(row=>row.path==='/stream'&&row.duration!==null&&!row.error).length>=2");
        Assert.True((await browser.EvaluateFrameAsync("stream-test-frame", "return document.querySelector('output').textContent==='4194304'&&document.querySelector('output').dataset.done==='true';")).GetBoolean());
        await browser.EvaluateAsync("streamPreview.destroy();streaming.terminate();");
    }
}
