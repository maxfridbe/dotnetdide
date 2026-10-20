namespace DotnetDide.Browser.Tests;

public sealed class VirtualNetworkTests
{
    [Fact]
    public async Task ComponentShowsPendingFailuresAndInertResponseMarkup()
    {
        await using var browser = new BrowserHarness(); await browser.StartAsync();
        Assert.True((await browser.EvaluateAsync("""
            const {VirtualNetworkDebugger}=await import('./playground/network-debugger.js');
            const {init,attributesModule,propsModule,eventListenersModule,classModule,styleModule}=await import('./playground/vdom.js');
            const patch=init([attributesModule,propsModule,eventListenersModule,classModule,styleModule]);
            let tree=document.body.appendChild(document.createElement('div'));
            const debug=new VirtualNetworkDebugger(()=>tree=patch(tree,debug.view()));
            tree=patch(tree,debug.view());
            const pane=()=>tree.elm,assert=(ok,message)=>{if(!ok)throw Error(message);};
            let resolve;
            const pending=debug.trace({port:9000,method:'GET',path:'/slow'},()=>new Promise(done=>resolve=done));
            assert(pane().querySelector('tbody').textContent.includes('Pending'),'Pending request missing');
            const category=pane().querySelector('select');category.value='pending';category.dispatchEvent(new Event('change'));
            assert(pane().querySelectorAll('tbody tr').length===1,'Pending filter');
            resolve({status:200,headers:{},body:btoa('<script>globalThis.responseExecuted=true</script>')});await pending;
            assert(!pane().querySelector('tbody tr'),'Completed request remains in Pending');
            category.value='all';category.dispatchEvent(new Event('change'));
            pane().querySelector('tbody button').click();pane().querySelector('[data-network-tab=Response]').click();
            assert(pane().querySelector('pre').textContent.includes('<script>') && !globalThis.responseExecuted && !pane().querySelector('script'),'Response HTML executed');
            try{await debug.trace({port:9000,method:'GET',path:'/failed'},()=>Promise.reject(Error('Worker stopped')));}catch{}
            assert(pane().querySelector('tbody').textContent.includes('Failed'),'Transport failure missing');
            [...pane().querySelectorAll('tbody tr')].find(row=>row.textContent.includes('Failed')).querySelector('button').click();
            assert(pane().querySelector('.network-detail-content').textContent.includes('No response received. Error: Worker stopped'),'Failure shown as still waiting');
            pane().querySelector('[data-network-action=clear]').click();
            assert(!pane().querySelector('tbody tr') && !pane().querySelector('.network-details'),'Clear retained stale selection');
            const before=pane().getBoundingClientRect().height;
            pane().querySelector('[role=separator]').dispatchEvent(new KeyboardEvent('keydown',{key:'ArrowUp',bubbles:true}));
            assert(pane().getBoundingClientRect().height>before,'Bottom resize');
            pane().querySelector('[role=separator]').dispatchEvent(new PointerEvent('pointerdown',{button:0,bubbles:true}));
            document.dispatchEvent(new PointerEvent('pointermove',{clientY:innerHeight-240,bubbles:true}));
            document.dispatchEvent(new PointerEvent('pointerup',{bubbles:true}));
            assert(Math.abs(pane().getBoundingClientRect().height-240)<1 && document.body.style.userSelect==='','Pointer resize or cleanup');
            debug.dispose();pane().remove();
            return true;
            """)).GetBoolean());
    }

    [Fact]
    public async Task LogIsBoundedRedactsSecretsAndDoesNotChangeRequestBehavior()
    {
        await using var browser = new BrowserHarness(); await browser.StartAsync();
        Assert.True((await browser.EvaluateAsync("""
            const {VirtualNetworkLog,bodyPreview}=await import('./playground/network-log.js');
            const assert=(ok,message)=>{if(!ok)throw Error(message);};
            const log=new VirtualNetworkLog(()=>{throw Error('observer failure');},2);
            const request={port:8080,method:'POST',path:'/api/save?token=secret&plain=yes',
                headers:{Authorization:['Bearer secret'],'X-Test':['visible']},body:btoa('{"answer":42}')};
            const response={status:201,headers:{'Set-Cookie':['secret'],'Content-Type':['application/json']},body:btoa('{"saved":true}')};
            let calls=0,resolve;
            const pending=log.trace(request,()=>{calls++;return new Promise(done=>resolve=done);});
            assert(log.entries[0].status===null && log.entries[0].duration===null,'Pending not visible');
            resolve(response);assert(await pending===response && calls===1,'Request behavior changed');
            const row=log.entries[0];
            assert(row.requestHeaders.Authorization[0]==='[redacted]' && row.responseHeaders['Set-Cookie'][0]==='[redacted]','Header secret leaked');
            assert(!row.path.includes('secret') && row.path.includes('plain=yes'),'Query redaction failed');
            assert(row.requestBody.text.includes('42') && row.responseBody.bytes===14 && row.duration>=0,'Payload or timing missing');
            const snapshot=log.snapshot();snapshot[0].path='changed';assert(log.entries[0].path!=='changed','Snapshot aliases log');
            const error=new DOMException('Worker stopped','AbortError');
            try{await log.trace(request,()=>Promise.reject(error));throw Error('Did not reject');}catch(actual){assert(actual===error,'Error identity changed');}
            assert(log.entries[1].error.includes('AbortError') && log.entries[1].duration!==null,'Failure not captured');
            await log.trace(request,async()=>response);assert(log.entries.length===2 && log.dropped===1,'Capacity not bounded');
            log.recording=false;await log.trace(request,async()=>response);assert(log.entries.length===2 && log.dropped===1,'Paused recording captured');
            log.preserve=true;log.beginRunspace();assert(log.entries.length===2,'Preserve ignored');
            log.preserve=false;log.beginRunspace();assert(!log.entries.length && !log.dropped,'Reset did not clear');
            log.recording=true;
            const late=log.trace(request,()=>new Promise(done=>resolve=done));log.clear();resolve(response);await late;
            assert(!log.entries.length,'Late response resurrected cleared request');
            const huge=bodyPreview(btoa('a'.repeat(20000)));assert(huge.truncated && huge.text.length===16384 && huge.bytes===20000,'Body limit');
            const binary=bodyPreview(btoa(String.fromCharCode(0,255,128)));assert(binary.binary && binary.text==='00 ff 80','Binary preview');
            assert(bodyPreview('!!!!').text.includes('Invalid base64'),'Invalid base64');
            return true;
            """)).GetBoolean());
    }

    [Fact]
    public async Task PlaygroundDebuggerInspectsRealIframeRequestsAndKeepsMonacoAlive()
    {
        await using var browser = new BrowserHarness(); await browser.StartAsync();
        await browser.WaitForAsync("document.querySelector('#code-editor')?.dataset.editor==='monaco'");
        await browser.EvaluateAsync("""
            globalThis.originalModel=monaco.editor.getModels()[0];
            document.querySelector('#site-access').click();
            """);
        await browser.WaitForAsync("!document.querySelector('#run').disabled");
        await browser.EvaluateAsync("document.querySelector('[data-example=site]').click();document.querySelector('#run').click();");
        await browser.WaitForAsync("document.querySelector('#site-port').value==='8080' && !document.querySelector('#run').disabled");
        await browser.EvaluateAsync("document.querySelector('#site-open').click();document.querySelector('.site-panel').scrollIntoView();");
        await browser.WaitForAsync("!document.querySelector('#site-frame').hidden");
        await browser.EvaluateFrameAsync("site-frame", """
            for(let end=Date.now()+10000;Date.now()<end&&!document.querySelector('#note');)await new Promise(r=>setTimeout(r,25));
            document.querySelector('#note').requestSubmit();
            for(let end=Date.now()+10000;Date.now()<end&&!document.querySelector('#saved').textContent.includes('Saved');)await new Promise(r=>setTimeout(r,25));
            if(!document.querySelector('#saved').textContent.includes('Saved'))throw Error('Site did not save');
            await fetch('/does-not-exist?token=keep-private');
            return true;
            """);
        await browser.WaitForAsync("document.querySelector('.network-table').textContent.includes('404') && !document.querySelector('#run').disabled");
        Assert.True((await browser.EvaluateAsync("""
            const rows=[...document.querySelectorAll('[data-network-id]')];
            const post=rows.find(row=>row.textContent.includes('POST'));
            if(!post)throw Error('POST missing from Network');
            post.querySelector('button').click();
            return monaco.editor.getModels()[0]===originalModel && document.querySelector('#files').textContent.includes('note.txt');
            """)).GetBoolean());
        await browser.EvaluateAsync("document.querySelector('[data-network-tab=Payload]').click();");
        Assert.Contains("Hello from the iframe", (await browser.EvaluateAsync("return document.querySelector('.network-body').textContent;")).GetString());
        await browser.EvaluateAsync("document.querySelector('[data-network-tab=Response]').click();");
        Assert.Contains("Saved", (await browser.EvaluateAsync("return document.querySelector('.network-body').textContent;")).GetString());
        await browser.ScreenshotAsync("playground-network-desktop.png");
        await browser.EvaluateAsync("document.querySelector('[data-network-tab=Timing]').click();");
        Assert.Contains("Total:", (await browser.EvaluateAsync("return document.querySelector('.network-detail-content').textContent;")).GetString());
        await browser.EvaluateAsync("""
            const filter=document.querySelector('[aria-label="Filter network requests"]');filter.value='does-not-exist';filter.dispatchEvent(new Event('input'));
            """);
        Assert.Single((await browser.EvaluateAsync("return [...document.querySelectorAll('[data-network-id]')].map(row=>row.textContent);")).EnumerateArray());
        Assert.DoesNotContain("keep-private", (await browser.EvaluateAsync("return document.querySelector('.network-table').textContent;")).GetString());
        await browser.EvaluateAsync("""
            const filter=document.querySelector('[aria-label="Filter network requests"]');filter.value='';filter.dispatchEvent(new Event('input'));
            const category=document.querySelector('[aria-label="Filter request status"]');category.value='errors';category.dispatchEvent(new Event('change'));
            """);
        Assert.Single((await browser.EvaluateAsync("return [...document.querySelectorAll('[data-network-id]')].map(row=>row.textContent);")).EnumerateArray());
        await browser.EvaluateAsync("""
            const category=document.querySelector('[aria-label="Filter request status"]');category.value='all';category.dispatchEvent(new Event('change'));
            document.querySelector('[data-network-action=dock]').click();
            """);
        Assert.Equal("right", (await browser.EvaluateAsync("return document.querySelector('#virtual-debugger').dataset.dock;")).GetString());
        Assert.True((await browser.EvaluateAsync("""
            const pane=document.querySelector('#virtual-debugger'),before=pane.getBoundingClientRect().width;
            pane.querySelector('[role=separator]').dispatchEvent(new KeyboardEvent('keydown',{key:'ArrowLeft',bubbles:true}));
            await new Promise(resolve=>requestAnimationFrame(resolve));
            return pane.getBoundingClientRect().width>before;
            """)).GetBoolean());
        await browser.EvaluateAsync("""
            globalThis.logBlob=null;globalThis.createUrl=URL.createObjectURL;globalThis.anchorClick=HTMLAnchorElement.prototype.click;
            URL.createObjectURL=blob=>{logBlob=blob;return createUrl(blob);};HTMLAnchorElement.prototype.click=function(){};
            document.querySelector('[data-network-action=export]').click();
            URL.createObjectURL=createUrl;HTMLAnchorElement.prototype.click=anchorClick;
            """);
        var exported = await browser.EvaluateAsync("return JSON.parse(await logBlob.text());");
        Assert.Equal("dotnetdide-virtual-network", exported.GetProperty("kind").GetString());
        Assert.Contains(exported.GetProperty("requests").EnumerateArray(), row => row.GetProperty("method").GetString() == "POST");
        await browser.EvaluateAsync("document.querySelector('[data-network-action=record]').click();globalThis.recordedCount=document.querySelectorAll('[data-network-id]').length;");
        Assert.Equal(200, (await browser.EvaluateFrameAsync("site-frame", "return (await fetch('/api/hello')).status;")).GetInt32());
        Assert.True((await browser.EvaluateAsync("return document.querySelectorAll('[data-network-id]').length===recordedCount;")).GetBoolean());
        await browser.EvaluateAsync("document.querySelector('[data-network-action=record]').click();document.querySelector('[data-network-action=preserve]').click();");
        await browser.SetViewportAsync(390, 844);
        await browser.WaitForAsync("document.querySelector('#virtual-debugger').dataset.dock==='bottom'");
        Assert.True((await browser.EvaluateAsync("return document.documentElement.scrollWidth<=innerWidth && monaco.editor.getModels()[0]===originalModel;")).GetBoolean());
        await browser.ScreenshotAsync("playground-network-mobile.png");
        await browser.EvaluateFrameAsync("site-frame", "console.info('Live iframe console', {answer:42}); console.warn('Example warning'); console.error('Example error', new Error('Example stack')); return true;");
        await browser.EvaluateAsync("document.querySelector('[data-debugger-tab=Console]').click();");
        await browser.WaitForAsync("document.querySelector('.console-content')?.textContent.includes('Live iframe console')");
        await browser.EvaluateAsync("const pane=document.querySelector('.console-content');pane.scrollTop=pane.scrollHeight;");
        await browser.ScreenshotAsync("playground-console-mobile.png");
        await browser.SetViewportAsync(1280, 1000);
        await browser.EvaluateAsync("document.querySelector('[data-network-action=dock]').click();");
        await browser.ScreenshotAsync("playground-console-desktop.png");
        await browser.EvaluateAsync("document.querySelector('[data-debugger-tab=Assemblies]').click();");
        await browser.WaitForAsync("!!document.querySelector('[data-assembly-name=\"DotnetDide.Core\"]')");
        await browser.ScreenshotAsync("playground-assemblies.png");
        await browser.EvaluateAsync("document.querySelector('[data-debugger-tab=Network]').click();");
        await browser.EvaluateAsync("document.querySelector('#reset').click();");
        await browser.WaitForAsync("!document.querySelector('#run').disabled");
        Assert.True((await browser.EvaluateAsync("return document.querySelectorAll('[data-network-id]').length===recordedCount;")).GetBoolean());
        await browser.EvaluateAsync("document.querySelector('[data-network-action=preserve]').click();document.querySelector('#reset').click();");
        await browser.WaitForAsync("!document.querySelector('#run').disabled");
        Assert.Empty((await browser.EvaluateAsync("return [...document.querySelectorAll('[data-network-id]')];")).EnumerateArray());
        await browser.EvaluateAsync("document.querySelector('[data-network-action=close]').click();");
        Assert.True((await browser.EvaluateAsync("return document.querySelector('#virtual-debugger').hidden;")).GetBoolean());
        await browser.PressKeyAsync("n", 78, 1);
        Assert.False((await browser.EvaluateAsync("return document.querySelector('#virtual-debugger').hidden;")).GetBoolean());
        await browser.EvaluateAsync("document.querySelector('#stop').click();");
    }
}
