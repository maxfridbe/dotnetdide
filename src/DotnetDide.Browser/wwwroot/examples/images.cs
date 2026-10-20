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
