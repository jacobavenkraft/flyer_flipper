using SkiaSharp;

namespace FlyerFlipper.Tests.Imaging;

internal static class SkiaTestImages
{
    public static readonly SKColor[] Palette =
    [
        new(255, 0, 0), new(0, 255, 0), new(0, 0, 255),
        new(255, 255, 0), new(0, 255, 255), new(255, 0, 255),
    ];

    /// <summary>A 3x2 opaque bitmap where every pixel has a distinct color.</summary>
    public static SKBitmap CreateDistinct3x2()
    {
        var bitmap = new SKBitmap(new SKImageInfo(3, 2, SKColorType.Bgra8888, SKAlphaType.Premul));
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                bitmap.SetPixel(x, y, Palette[(y * 3) + x]);
            }
        }

        return bitmap;
    }

    public static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format, int quality = 100)
    {
        using var data = bitmap.Encode(format, quality)
            ?? throw new InvalidOperationException($"SkiaSharp cannot encode {format}.");
        return data.ToArray();
    }

    /// <summary>Hand-built 24-bit BMP (SkiaSharp has no BMP encoder).</summary>
    public static byte[] CreateBmp24(SKColor[,] pixels)
    {
        var width = pixels.GetLength(1);
        var height = pixels.GetLength(0);
        var rowSize = ((width * 3) + 3) & ~3;
        var imageSize = rowSize * height;
        const int headerSize = 14 + 40;

        var bytes = new byte[headerSize + imageSize];
        using var writer = new BinaryWriter(new MemoryStream(bytes));
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(bytes.Length);
        writer.Write(0);
        writer.Write(headerSize);
        writer.Write(40);
        writer.Write(width);
        writer.Write(height); // positive = bottom-up rows
        writer.Write((short)1);
        writer.Write((short)24);
        writer.Write(0); // BI_RGB
        writer.Write(imageSize);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);

        for (var y = height - 1; y >= 0; y--)
        {
            for (var x = 0; x < width; x++)
            {
                writer.Write(pixels[y, x].Blue);
                writer.Write(pixels[y, x].Green);
                writer.Write(pixels[y, x].Red);
            }

            for (var pad = width * 3; pad < rowSize; pad++)
            {
                writer.Write((byte)0);
            }
        }

        return bytes;
    }
}
