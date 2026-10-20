# Media libraries and browser NuGet packages

## Bundled defaults

These managed libraries ship with every runtime download and are already available
to scripts and `Dotnet.Build/Run`. No package or network permission is needed to use
them. Import their namespaces explicitly; the five default script imports are unchanged.

| Package | Version | Use |
| --- | --- | --- |
| StbImageSharp | 2.30.16 | Decode PNG/JPEG and other supported raster images into pixels |
| StbImageWriteSharp | 1.16.7 | Encode PNG/JPEG from pixels |
| NAudio.Core | 3.3.0 | WAV I/O, sample providers and managed audio processing |
| Concentus | 2.2.2 | Managed Opus encoding/decoding |
| Concentus.Oggfile | 1.0.7 | Package Opus packets into playable Ogg `.opus` files |

PNG/JPEG, 16-bit PCM WAV and Ogg Opus generation are tested in the real WASM
worker. Generated images and audio are also decoded by Chromium. Lethetic displays
them in its Files preview after the tool finishes; audio has native playback
controls. Ogg Opus support depends on the user's browser. Prefer WAV for broad
compatibility. Raw Opus packets are not playable files: call `Finish()` on the Ogg
writer before closing its stream. Desktop sound-device APIs are not supported.
`System.Drawing.Bitmap/Graphics`, ImageSharp and SkiaSharp are not bundled.

Both Stb namespaces define `ColorComponents`; qualify it to avoid ambiguity.
NAudio 3 sample providers use `Read(Span<float>)`. The examples use `using (...)`
blocks so they work as either interactive scripts or regular compiled programs.
Save them as `.cs`, then `await Dotnet.Run("images.cs")` or `await Dotnet.Run("audio.cs")`.
All output streams must be disposed before exporting/syncing files. Lethetic's
session limits still apply (1 MiB per file, 2 MiB total); keep images and clips small.

## PNG and JPEG example

Available in the release/site as `examples/images.cs`:

```csharp
using System;
using System.IO;
using StbImageSharp;
using StbImageWriteSharp;

Directory.CreateDirectory("media");
{
    const int width = 64, height = 32;
    var pixels = new byte[width * height * 4];
    for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var i = (y * width + x) * 4;
            pixels[i] = (byte)(x * 4); pixels[i + 1] = (byte)(y * 8);
            pixels[i + 2] = 128; pixels[i + 3] = 255;
        }
    var writer = new ImageWriter();
    using (var output = File.Create("media/gradient.png"))
        writer.WritePng(pixels, width, height, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, output);
    using (var output = File.Create("media/gradient.jpg"))
        writer.WriteJpg(pixels, width, height, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, output, 90);
    var decoded = ImageResult.FromMemory(File.ReadAllBytes("media/gradient.png"), StbImageSharp.ColorComponents.RedGreenBlueAlpha);
    if (decoded.Width != width || decoded.Height != height || !System.Linq.Enumerable.SequenceEqual(decoded.Data, pixels))
        throw new Exception("PNG round trip failed");
    var jpeg = ImageResult.FromMemory(File.ReadAllBytes("media/gradient.jpg"), StbImageSharp.ColorComponents.RedGreenBlueAlpha);
    if (jpeg.Width != width || jpeg.Height != height) throw new Exception("JPEG decode failed");
    Console.WriteLine("Created media/gradient.png and media/gradient.jpg (64 x 32)");
}
```

## WAV and Ogg Opus example

Available in the release/site as `examples/audio.cs`:

```csharp
using System;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Concentus;
using Concentus.Enums;
using Concentus.Oggfile;

Directory.CreateDirectory("media");
{
    const int sampleRate = 48000;
    var signal = new SignalGenerator(sampleRate, 1) { Type = SignalGeneratorType.Sin, Frequency = 440, Gain = 0.2 };
    var samples = new float[sampleRate / 4];
    signal.Read(samples.AsSpan());
    using (var wav = new WaveFileWriter("media/tone.wav", new WaveFormat(sampleRate, 16, 1)))
        wav.WriteSamples(samples, 0, samples.Length);
    using (var wav = new WaveFileReader("media/tone.wav"))
        if (wav.WaveFormat.SampleRate != sampleRate || wav.WaveFormat.Channels != 1 || wav.Length != samples.Length * 2)
            throw new Exception("WAV round trip failed");

    // Force the bundled managed codec: no native libopus is needed in WASM.
    OpusCodecFactory.AttemptToUseNativeLibrary = false;
    using (var encoder = OpusCodecFactory.CreateEncoder(sampleRate, 1, OpusApplication.OPUS_APPLICATION_AUDIO))
    using (var output = File.Create("media/tone.opus"))
    {
        var ogg = new OpusOggWriteStream(encoder, output);
        ogg.WriteSamples(samples, 0, samples.Length);
        ogg.Finish(); // Required: finish the Ogg container before preview/sync.
    }
    using (var decoder = OpusCodecFactory.CreateDecoder(sampleRate, 1))
    using (var input = File.OpenRead("media/tone.opus"))
    {
        var ogg = new OpusOggReadStream(decoder, input);
        var decoded = 0;
        while (ogg.HasNextPacket) decoded += ogg.DecodeNextPacket()?.Length ?? 0;
        if (decoded < samples.Length) throw new Exception("Opus round trip failed");
    }
    Console.WriteLine("Created media/tone.wav and media/tone.opus (440 Hz, 0.25 seconds)");
}
```

## Loading another managed package

The host must explicitly allow NuGet downloads. This is independent of general
network access and does not remove any bundled defaults:

```javascript
import { loadDotnetdide, clearNugetCache } from './dotnetdide.js';
const runtime = await loadDotnetdide({ nugetAccess: true, networkAccess: false });
const loaded = await runtime.runCSharpAsync(
  'await DotnetDide.Nuget.Load("Newtonsoft.Json", "13.0.4")');
if (!loaded.success) throw new Error(JSON.stringify(loaded));
// A second submission is required: its compiler now knows the loaded types.
const result = await runtime.runCSharpAsync(
  'Newtonsoft.Json.JsonConvert.SerializeObject(new { answer = 42 })');
await runtime.setNugetAccess(false); // Idle only. Blocks future loads, including cached loads.
await clearNugetCache(); // Optional: remove persisted package archives.
```

Inside the runspace, use the exact capitalization `DotnetDide.Nuget`:

```csharp
var package = await DotnetDide.Nuget.Load("Newtonsoft.Json", "13.0.4");
Console.WriteLine($"Loaded {package.Name} {package.Version}");
foreach (var dependency in package.Packages)
    Console.WriteLine($"{dependency.Name} {dependency.Version}");
```

`Load("name")` reuses an already loaded version or selects the latest stable
version from NuGet.org. Use an exact version for reproducibility. Compatible
managed dependencies are downloaded recursively, selecting the lowest stable
version satisfying each dependency range. Conflicts fail rather than silently
changing an assembly already in use; restart the worker to change versions.
The resolver does not backtrack dependency choices like a full NuGet restore.

Load in one submission; import/use new types in the **next** submission. The
whole submission is compiled before execution. Future `Dotnet.Build/Run` calls
also see loaded assemblies, without `PackageReference` items. Regular `.cs`
programs still need their own imports and cannot access script variables.

## Permission, compatibility and limits

- `nugetAccess` defaults to false. The playground has a package-download checkbox;
  Lethetic exposes it in Connections → Runtime & security and saves it per session.
- Downloads go directly to `https://api.nuget.org/v3-flatcontainer/`, without
  cookies or redirects. They do not use a configured general HTTP relay or its
  credentials. NuGet.org CORS and the embedding page's CSP must allow these requests.
  Allow `https://api.nuget.org` in `connect-src` for embeds that restrict origins.
- The NuGet permission authorizes downloads from that endpoint independently of
  general network access. It does not authorize arbitrary HTTP requests.
- Only compatible managed `lib/<framework>/*.dll` assemblies are loaded. Native
  binaries, `runtimes/` assets, build targets, analyzers/source generators and
  incompatible framework groups are rejected. Package content and resources are
  not copied into the filesystem; satellite/resource-dependent packages may not
  work. Package references in `.csproj` still are not restored. No shell or MSBuild.
- Limits: 16 MiB per archive/entry; 64 MiB downloaded/expanded per load; 64 MiB of
  dynamically loaded assemblies per worker; 32 packages including bundled defaults;
  at most 2048 ZIP entries per package. Archives are inspected without extracting
  paths to disk. Validation completes before compiler references are added.
- Runtime packages/assemblies are outside workspace snapshots and session file
  quotas. Loaded packages survive `reset()` in the standalone SDK; variables are cleared.
  Worker termination/reload loses both. Reload packages in a new worker.
  Lethetic permission/mode changes restart the worker while keeping session files.
- Revocation blocks new loads; it cannot unload assemblies or undo previously run
  code. Terminate the worker to discard loaded assemblies. As with existing
  network controls, this is a capability control for trusted code, not a security
  boundary against hostile reflection/JavaScript interop. NuGet package code runs
  with the same capabilities as other user code; only load packages you trust.

## Persistent browser cache

Exact-version `.nupkg` archives are cached in **Cache Storage**, not localStorage.
They survive page/worker reloads in the same origin, subject to browser eviction.
Version indexes are fetched fresh. Cache limits are 32 archives and 64 MiB total,
with oldest entries evicted first. A new worker still validates the archive and
loads managed assemblies into memory; the cache saves repeat downloads.

Permission is checked before using the cache. Private mode, denied storage or
quota errors fall back to downloading. `await clearNugetCache()` deletes the cache
but does not unload active assemblies; terminate the worker for that. Cache
Storage is shared by same-origin embeds, so use a separate origin if they need
separate package storage. Browser site-data controls also clear it.

## JavaScript LoadNuget example

```js
import {loadDotnetdide} from './dotnetdide.js';
const DotnetDide = await loadDotnetdide({nugetAccess: true, networkAccess: false});
const loaded = await DotnetDide.LoadNuget("Newtonsoft.Json", "13.0.4");
console.log(loaded.name, loaded.version, loaded.packages);
// Use the package only in a subsequent submission, after loading completes.
const result = await DotnetDide.runCSharpAsync(
  'Newtonsoft.Json.JsonConvert.SerializeObject(new { answer = 42 })');
console.log(result.value); // {"answer":42}
// DotnetDide.terminate() when finished.
```

`DotnetDide` above is the JavaScript runspace variable, not a C# namespace method.
Inside C# submissions use `await DotnetDide.Nuget.Load("Newtonsoft.Json", "13.0.4")`
in step 1, then reference the package's types in step 2. The playground's **Load a
NuGet package** example demonstrates those two steps. Enable **NuGet downloads**
first; Run all checks skips this example while that permission is off. Downloads
use the browser and existing bounded Cache Storage, not localStorage. General
network access is independent. `LoadNuget` rejects when permission is disabled or
a package is incompatible; it does not silently grant permission. Already bundled
image/audio defaults remain available without downloading them.
