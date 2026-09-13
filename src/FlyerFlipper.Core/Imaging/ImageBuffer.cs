namespace FlyerFlipper.Core.Imaging;

/// <summary>
/// A raw, library-agnostic pixel buffer. Deliberately free of Avalonia and SkiaSharp types so it
/// can cross the future native-plugin boundary unchanged.
/// </summary>
public sealed record ImageBuffer
{
    public ImageBuffer(ReadOnlyMemory<byte> pixels, int width, int height, int stride, ImagePixelFormat format)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var bytesPerPixel = GetBytesPerPixel(format);
        ArgumentOutOfRangeException.ThrowIfLessThan(stride, width * bytesPerPixel);
        if (stride % bytesPerPixel != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stride), stride, $"Stride must be a multiple of {bytesPerPixel} bytes per pixel.");
        }

        var requiredLength = ((long)stride * (height - 1)) + ((long)width * bytesPerPixel);
        if (pixels.Length < requiredLength)
        {
            throw new ArgumentException(
                $"Pixel buffer holds {pixels.Length} bytes but {requiredLength} are required for {width}x{height} (stride {stride}).",
                nameof(pixels));
        }

        Pixels = pixels;
        Width = width;
        Height = height;
        Stride = stride;
        Format = format;
    }

    public ReadOnlyMemory<byte> Pixels { get; }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Bytes per row, including any padding. Always a whole number of pixels.</summary>
    public int Stride { get; }

    public ImagePixelFormat Format { get; }

    public static int GetBytesPerPixel(ImagePixelFormat format) => format switch
    {
        ImagePixelFormat.Bgra8888Premultiplied => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };
}
