namespace DotnetDide.Browser.Tests;

public sealed class DebugInfoTests
{
    [Fact]
    public async Task StoreAndConsolePanelAreBoundedFilterableAndSafe()
    {
        await using var browser = new BrowserHarness(); await browser.StartAsync();
        Assert.True((await browser.EvaluateAsync("""
            const {DebugInfoStore}=await import('./playground/debug-info.js');
            const {VirtualNetworkDebugger}=await import('./playground/network-debugger.js');
            const {init,attributesModule,propsModule,eventListenersModule,classModule,styleModule}=await import('./playground/vdom.js');
            const patch=init([attributesModule,propsModule,eventListenersModule,classModule,styleModule]);
            const assert=(ok,message)=>{if(!ok)throw Error(message)};
            const store=new DebugInfoStore(2);store.subscribe(()=>{throw Error('observer failed')});
            store.recordConsole({level:'log',message:'first',source:'test'});
            store.recordConsole({level:'warn',message:'second',source:'test'});
            store.recordConsole({level:'error',message:'x'.repeat(20000),stack:'y'.repeat(10000),source:'test'});
            assert(store.console.length===2&&store.dropped===1,'Capacity');
            assert(store.console[1].truncated&&store.console[1].message.length===16384&&store.console[1].stack.length===8192,'Text bounds');
            const snapshot=store.snapshot();snapshot.console[0].message='changed';assert(store.console[0].message==='second','Snapshot isolation');
            store.preserve=true;store.beginRunspace();assert(store.console.length===2,'Preserve');
            store.preserve=false;store.beginRunspace();assert(!store.console.length&&!store.dropped,'Reset');
            let tree=document.body.appendChild(document.createElement('div'));
            const debug=new VirtualNetworkDebugger(()=>tree=patch(tree,debug.view()),store);debug.panel='Console';tree=patch(tree,debug.view());
            store.recordConsole({level:'warn',message:'<script>globalThis.consoleExecuted=true</script>',source:'iframe'});
            store.recordConsole({level:'error',message:'route failed',source:'C#',stack:'at Route.cs:12'});
            const pane=()=>tree.elm;
            assert(pane().querySelectorAll('.console-row').length===2&&!pane().querySelector('script')&&!globalThis.consoleExecuted,'Console text executed');
            const level=pane().querySelector('[aria-label="Filter console level"]');level.value='error';level.dispatchEvent(new Event('change'));
            assert(pane().querySelectorAll('.console-row').length===1&&pane().textContent.includes('Route.cs:12'),'Level filter/stack');
            const filter=pane().querySelector('input[type=search]');filter.value='missing';filter.dispatchEvent(new Event('input'));
            assert(!pane().querySelector('.console-row'),'Text filter');
            filter.value='';filter.dispatchEvent(new Event('input'));
            pane().querySelector('[data-console-action=record]').click();store.recordConsole({level:'log',source:'test',message:'paused'});
            assert(store.console.length===2,'Pause');
            globalThis.debugBlob=null;const create=URL.createObjectURL,click=HTMLAnchorElement.prototype.click;
            try{URL.createObjectURL=blob=>{debugBlob=blob;return create(blob)};HTMLAnchorElement.prototype.click=function(){};
              pane().querySelector('[data-console-action=export]').click();
            }finally{URL.createObjectURL=create;HTMLAnchorElement.prototype.click=click;}
            const exported=JSON.parse(await debugBlob.text());assert(exported.console.length===2&&Array.isArray(exported.network),'Shared export');
            pane().querySelector('[data-console-action=clear]').click();assert(!store.console.length,'Clear');
            store.setAssemblies([{name:'Demo.Assembly',version:'1.2.3.4',fullName:'Demo.Assembly, Version=1.2.3.4',isDynamic:false}], 'Test runtime');
            pane().querySelector('[data-debugger-tab=Assemblies]').click();
            assert(pane().querySelector('[data-assembly-name="Demo.Assembly"]')&&pane().textContent.includes('1.2.3.4'),'Assembly table');
            const assemblyFilter=pane().querySelector('[aria-label="Filter loaded assemblies"]');assemblyFilter.value='missing';assemblyFilter.dispatchEvent(new Event('input'));
            assert(!pane().querySelector('tbody tr'),'Assembly filter');
            const assemblySnapshot=store.snapshot();assemblySnapshot.assemblies[0].name='changed';assert(store.assemblies[0].name==='Demo.Assembly','Assembly snapshot isolation');
            debug.dispose();pane().remove();return true;
            """)).GetBoolean());
    }

    [Fact]
    public async Task PlaygroundDumpsDebugDataWithoutRecordingItsOwnOutput()
    {
        await using var browser = new BrowserHarness(); await browser.StartAsync();
        await browser.EvaluateAsync("document.querySelector('#run').click();");
        await browser.WaitForAsync("!document.querySelector('#run').disabled && document.querySelector('#status').textContent.includes('Ready')");
        Assert.True((await browser.EvaluateAsync("""
            const assert=(ok,message)=>{if(!ok)throw Error(message)};
            document.querySelector('#dump-logs').click(); await Promise.resolve();
            const first=document.querySelector('#result').textContent;
            const snapshot=JSON.parse(first);
            assert(snapshot.console.some(row=>row.message.includes('Saved outputs/answer.txt')),'Missing C# output');
            assert(snapshot.assemblies.length>0&&Array.isArray(snapshot.network),'Incomplete snapshot');
            document.querySelector('#dump-logs').click(); await Promise.resolve();
            assert(document.querySelector('#result').textContent===first,'Dump recorded itself');
            document.querySelector('#stop').click();document.querySelector('#dump-logs').click(); await Promise.resolve();
            assert(document.querySelector('#result').textContent===first,'Stop lost snapshot');
            assert(document.querySelector('#output').textContent===first,'Raw snapshot differs');
            return true;
            """)).GetBoolean());
    }

    [Fact]
    public async Task RuntimeIframeAndCSharpShareDebugSnapshots()
    {
        await using var browser = new BrowserHarness(); await browser.StartAsync();
        Assert.True((await browser.EvaluateAsync(""""
            const assert=(ok,message)=>{if(!ok)throw Error(message)};
            globalThis.debugRuntime=await api.loadDotnetdide({siteAccess:true});
            const code=String.raw`
                using Microsoft.AspNetCore.Builder;
                using Microsoft.AspNetCore.Http;
                Console.WriteLine("submission stdout"); Console.Error.WriteLine("submission stderr");
                Directory.CreateDirectory("site");
                File.WriteAllText("site/index.html", """
                    <h1>Console test</h1><script>
                    console.log('startup before channel', {answer:42});
                    console.assert(false,'iframe assertion');console.log('z'.repeat(20000));
                    const cycle={};cycle.self=cycle;console.info('cycle',cycle);
                    const getter={get value(){throw Error('getter executed')}};console.log(getter);
                    setTimeout(()=>{throw Error('iframe thrown error')},20);
                    setTimeout(()=>Promise.reject(Error('iframe rejected promise')),30);
                    </script>
                    """);
                var app=DotnetDide.Wire.CreateApplication();
                app.UseRouting();
                app.UseEndpoints(routes=>routes.MapGet("/api/log", async ctx=>{
                    Console.WriteLine("handler stdout");Console.Error.WriteLine("handler stderr");
                    var factory=(Microsoft.Extensions.Logging.ILoggerFactory)app.ApplicationServices.GetService(typeof(Microsoft.Extensions.Logging.ILoggerFactory))!;
                    Microsoft.Extensions.Logging.LoggerExtensions.LogWarning(factory.CreateLogger("demo"), "application logger warning");
                    await ctx.Response.WriteAsync("ok");}));
                await DotnetDide.Wire.ListenAsync(8080, app, "site");`;
            const result=await debugRuntime.runCSharpAsync(code);assert(result.success,JSON.stringify(result));
            const frame=document.createElement('iframe');frame.id='console-test-frame';document.body.append(frame);
            globalThis.debugPreview=await api.createWirePreview({iframe:frame,port:8080,
                request:value=>debugRuntime.requestSite(value),onConsole:debugRuntime.recordConsole});
            return true;
            """")).GetBoolean());
        await browser.WaitForAsync("debugRuntime.getDebugInfo().console.some(row=>row.message.includes('iframe rejected promise'))");
        Assert.True((await browser.EvaluateFrameAsync("console-test-frame", "return (await fetch('/api/log')).status===200;")).GetBoolean());
        Assert.True((await browser.EvaluateAsync("""
            const assert=(ok,message)=>{if(!ok)throw Error(message)};
            const loadedAssemblies=await debugRuntime.listAssemblies();
            assert(loadedAssemblies.some(item=>item.name==='DotnetDide.Core'),'Loaded runtime assembly');
            const info=debugRuntime.GetDebugInfo();
            assert(info.assemblies.some(item=>item.name==='System.Private.CoreLib'&&item.version),'Assembly snapshot');
            for(const message of ['submission stdout','submission stderr','startup before channel','iframe thrown error','iframe rejected promise','handler stdout','handler stderr','application logger warning','iframe assertion','[Circular]','[Getter]'])
                assert(info.console.some(row=>row.message.includes(message)),'Missing: '+message);
            assert(info.console.some(row=>row.source.startsWith('iframe')&&row.stack?.includes('iframe thrown error')),'Error stack/source');
            assert(info.console.some(row=>row.truncated&&row.message.startsWith('zzzz')),'Iframe truncation flag');
            assert(info.network.some(row=>row.path==='/api/log'&&row.status===200),'Network snapshot');
            const result=await debugRuntime.runCSharpAsync('Console.WriteLine(Dotnet.GetDebugInfo());');
            assert(result.success,JSON.stringify(result));const csharp=JSON.parse(result.stdout);
            assert(csharp.assemblies.some(item=>item.name==='DotnetDide.Core'),'C# assembly snapshot');
            assert(csharp.console.some(row=>row.message.includes('handler stdout'))&&csharp.network.some(row=>row.path==='/api/log'),'C# snapshot');
            const guide=await (await fetch('./llm.md')).text();
            const recipe=guide.split('```csharp\n')[1].split('```')[0];
            const documented=await debugRuntime.runCSharpAsync(recipe);assert(documented.success,JSON.stringify(documented));
            const compile=await debugRuntime.runCSharpAsync('int invalid = ;');assert(!compile.success,'Compile should fail');
            assert(debugRuntime.getDebugInfo().console.some(row=>row.source.includes('compiler')&&row.level==='error'),'Compiler diagnostics');
            debugPreview.destroy();debugRuntime.terminate();
            assert(debugRuntime.getDebugInfo().console.length>0,'Terminate discarded diagnostics');
            debugRuntime.clearDebugInfo();assert(!debugRuntime.getDebugInfo().console.length&&!debugRuntime.getDebugInfo().network.length,'Clear diagnostics');
            return true;
            """)).GetBoolean());
    }
}
