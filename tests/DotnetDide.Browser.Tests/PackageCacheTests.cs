namespace DotnetDide.Browser.Tests;

public sealed class PackageCacheTests
{
    [Fact]
    public async Task CacheReusesExactVersionsBoundsStorageAndFallsBackWhenUnavailable()
    {
        await using var browser = new BrowserHarness(); await browser.StartAsync();
        var result = await browser.EvaluateAsync("""
            const {fetchNugetPackage,clearNugetCache}=await import('./package-cache.js');
            await clearNugetCache(); let downloads=0;
            const fetcher=async ()=>{downloads++;return new Response(new Uint8Array([1,2,3]));};
            const url='https://api.nuget.org/v3-flatcontainer/test/1.0.0/test.1.0.0.nupkg';
            await fetchNugetPackage(fetcher,url);
            const cached=await fetchNugetPackage(()=>{throw Error('Cache miss');},url);
            if((await cached.arrayBuffer()).byteLength!==3 || downloads!==1)throw Error('Cache did not preserve bytes');
            const {fetchNugetPackage:reload}=await import('./package-cache.js?reload=1');
            await reload(()=>{throw Error('Module reload lost cache');},url);
            let indexes=0; const indexFetch=async()=>{indexes++;return new Response('{}');};
            for(let i=0;i<2;i++)await fetchNugetPackage(indexFetch,'https://api.nuget.org/v3-flatcontainer/test/index.json');
            if(indexes!==2)throw Error('Version index was cached');
            for(let i=0;i<33;i++)await fetchNugetPackage(fetcher,`https://api.nuget.org/v3-flatcontainer/p${i}/1.0.0/p${i}.1.0.0.nupkg`);
            const cache=await caches.open('dotnetdide-nuget-v1');
            if((await cache.keys()).length!==32 || await cache.match(url))throw Error('Cache did not evict old packages');
            let rejected=false;
            try{await fetchNugetPackage(async()=>new Response('x',{headers:{'Content-Length':String(17*1024*1024)}}),url)}catch{rejected=true}
            if(!rejected)throw Error('Oversized archive was accepted');
            const descriptor=Object.getOwnPropertyDescriptor(globalThis,'caches');
            try {
              Object.defineProperty(globalThis,'caches',{configurable:true,value:{open:async()=>{throw Error('Storage blocked');}}});
              if((await (await fetchNugetPackage(fetcher,url)).arrayBuffer()).byteLength!==3)throw Error('No storage fallback');
            } finally { if(descriptor)Object.defineProperty(globalThis,'caches',descriptor);else delete globalThis.caches; }
            await clearNugetCache();
            return !(await caches.has('dotnetdide-nuget-v1'));
            """);
        Assert.True(result.GetBoolean());
    }
}
