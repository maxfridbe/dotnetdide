using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

// Download the official native TypeScript compiler directly; no Node/npm runtime.
if (args.Length != 2) throw new ArgumentException("Usage: WebBuild <browser project directory> <output directory>");
var project = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1], project);
var root = Path.GetFullPath("../..", project);
using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "build/typescript-native.json")));
var version = manifest.RootElement.GetProperty("version").GetString()!;
var os = OperatingSystem.IsWindows() ? "win32" : OperatingSystem.IsMacOS() ? "darwin" : OperatingSystem.IsLinux() ? "linux" : "unsupported";
var architecture = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
var platform = $"{os}-{architecture}";
if (!manifest.RootElement.GetProperty("packages").TryGetProperty(platform, out var package))
    throw new PlatformNotSupportedException($"Native TypeScript is not pinned for {platform}.");
var cache = Path.Combine(root, ".toolchain", "typescript", version, platform);
var executable = Path.Combine(cache, "lib", os == "win32" ? "tsgo.exe" : "tsgo");
var integrity = package.GetProperty("integrity").GetString()!;
var marker = Path.Combine(cache, ".verified");
if (!File.Exists(executable) || !File.Exists(marker) || await File.ReadAllTextAsync(marker) != integrity)
{
    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
    var bytes = await http.GetByteArrayAsync(package.GetProperty("url").GetString()!);
    if ("sha512-" + Convert.ToBase64String(SHA512.HashData(bytes)) != integrity)
        throw new InvalidDataException("Native TypeScript archive checksum mismatch.");
    var temporary = cache + "." + Guid.NewGuid().ToString("N");
    Directory.CreateDirectory(temporary);
    try
    {
        using var gzip = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress);
        using var tar = new TarReader(gzip);
        long total = 0;
        while (await tar.GetNextEntryAsync() is { } entry)
        {
            var parts = entry.Name.Split('/');
            if (parts[0] != "package" || parts.Skip(1).Any(part => part is ".." or "." || part.Contains('\\')))
                throw new InvalidDataException("Unsafe compiler archive path.");
            if (entry.EntryType == TarEntryType.Directory) continue;
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                throw new InvalidDataException("Unexpected compiler archive link.");
            total += entry.Length;
            if (total > 100 * 1024 * 1024) throw new InvalidDataException("Compiler archive is too large.");
            var path = Path.Combine(temporary, Path.Combine(parts.Skip(1).ToArray()));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var file = File.Create(path);
            if (entry.DataStream is { } source) await source.CopyToAsync(file);
        }
        var binary = Path.Combine(temporary, "lib", os == "win32" ? "tsgo.exe" : "tsgo");
        if (!File.Exists(binary)) throw new InvalidDataException("Compiler archive is missing tsgo.");
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(binary, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        await File.WriteAllTextAsync(Path.Combine(temporary, ".verified"), integrity);
        if (Directory.Exists(cache)) Directory.Delete(cache, true);
        Directory.Move(temporary, cache);
    }
    finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
}
Directory.CreateDirectory(output);
var start = new ProcessStartInfo(executable) { WorkingDirectory = project, UseShellExecute = false };
foreach (var argument in new[] { "--project", "tsconfig.json", "--outDir", output, "--pretty", "false" }) start.ArgumentList.Add(argument);
using var compiler = Process.Start(start) ?? throw new InvalidOperationException("Could not start native TypeScript.");
await compiler.WaitForExitAsync();
return compiler.ExitCode;
