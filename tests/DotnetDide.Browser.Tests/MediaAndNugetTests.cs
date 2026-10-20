using System.Text.Json;
namespace DotnetDide.Browser.Tests;

public sealed class MediaAndNugetTests
{
    [Fact]
    public async Task BundledMediaRunsOfflineAndProducesBrowserDecodableImagesAndAudio()
    {
        await using var browser = new BrowserHarness(); await browser.StartAsync();
        var result = await browser.EvaluateAsync("""
            globalThis.mediaRuntime = await api.loadDotnetdide();
            try {
              for (const name of ['images','audio']) {
                const code = await (await fetch('./examples/' + name + '.cs')).text();
                const script = await mediaRuntime.runCSharpAsync(code);
                if (!script.success) throw Error(JSON.stringify(script));
                await mediaRuntime.FS.writeFile('Program.cs', code);
                const compiled = await mediaRuntime.runCSharpAsync('var program = await Dotnet.Run("Program.cs"); if (!program.Success) throw new Exception(program.Build.Output + program.Error);');
                if (!compiled.success) throw Error(JSON.stringify(compiled));
              }
              const dimensions = [];
              for (const name of ['gradient.png','gradient.jpg']) {
                const bytes = await mediaRuntime.FS.readFile('media/' + name);
                const image = await createImageBitmap(new Blob([bytes]));
                dimensions.push([image.width, image.height]); image.close();
              }
              const audio = new AudioContext(); const durations = [];
              try {
                for (const name of ['tone.wav','tone.opus']) {
                  const bytes = await mediaRuntime.FS.readFile('media/' + name);
                  const decoded = await audio.decodeAudioData(bytes.buffer);
                  durations.push(decoded.duration);
                  if (decoded.numberOfChannels !== 1) throw Error('Wrong channel count');
                }
              } finally { await audio.close(); }
              return {dimensions,durations};
            } finally { mediaRuntime.terminate(); }
            """);
        Assert.Equal("[[64,32],[64,32]]", result.GetProperty("dimensions").GetRawText());
        foreach (var duration in result.GetProperty("durations").EnumerateArray()) Assert.InRange(duration.GetDouble(), 0.24, 0.30);
    }

    [Fact]
    public async Task BrowserDownloadsNugetWithSeparatePermissionAndUsesItInScriptsAndBuilds()
    {
        await using var browser = new BrowserHarness(); await browser.StartAsync();
        var result = await browser.EvaluateAsync("""
            globalThis.packages = await api.loadDotnetdide();
            try {
              const denied = await packages.runCSharpAsync('await DotnetDide.Nuget.Load("Newtonsoft.Json", "13.0.4")');
              if (denied.success || !denied.error?.message.includes('disabled')) throw Error('NuGet was not denied');
              await packages.runCSharpAsync('int kept = 42; File.WriteAllText("kept.txt", "saved");');
              await packages.setNugetAccess(true);
              const loaded = await packages.runCSharpAsync('await DotnetDide.Nuget.Load("Newtonsoft.Json", "13.0.4")');
              if (!loaded.success) throw Error(JSON.stringify(loaded));
              const serialized = await packages.runCSharpAsync('Newtonsoft.Json.JsonConvert.SerializeObject(new { answer = kept })');
              if (!serialized.success || serialized.value !== '{"answer":42}') throw Error(JSON.stringify(serialized));
              await packages.FS.writeFile('Program.cs', 'using Newtonsoft.Json; Console.Write(JsonConvert.SerializeObject(new { answer = 42 }));');
              const compiled = await packages.runCSharpAsync('var p = await Dotnet.Run("Program.cs"); if (!p.Success) throw new Exception(p.Build.Output + p.Error);');
              if (!compiled.success || !compiled.stdout.includes('{"answer":42}')) throw Error(JSON.stringify(compiled));
              const network = await packages.runCSharpAsync('await new System.Net.Http.HttpClient().GetStringAsync(' + JSON.stringify(location.origin + '/network-probe') + ')');
              if (network.success) throw Error('NuGet permission enabled general network');
              await packages.setNugetAccess(false);
              const revoked = await packages.runCSharpAsync('await DotnetDide.Nuget.Load("Newtonsoft.Json", "13.0.4")');
              if (revoked.success) throw Error('NuGet permission was not revoked');
              return {kept:(await packages.runCSharpAsync('kept')).value,file:await packages.FS.readFile('kept.txt',{encoding:'utf8'})};
            } finally { packages.terminate(); }
            """);
        Assert.Equal("42", result.GetProperty("kept").GetString()); Assert.Equal("saved", result.GetProperty("file").GetString());
        Assert.Equal(0, browser.NetworkRequests);
    }
}
