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
