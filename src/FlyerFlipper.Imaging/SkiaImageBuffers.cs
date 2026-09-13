using FlyerFlipper.Core.Imaging;
using SkiaSharp;

namespace FlyerFlipper.Imaging;

/// <summary>
/// Conversions between <see cref="ImageBuffer"/> and <see cref="SKBitmap"/>. The only place the
/// Core pixel format is mapped onto SkiaSharp's.
/// </summary>
internal static class SkiaImageBuffers
{
    public static SKImageInfo CreateInfo(int width, int height)
        => new(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);

    public static unsafe ImageBuffer ToImageBuffer(SKBitmap bitmap)
    {
        if (bitmap.ColorType != SKColorType.Bgra8888 || bitmap.AlphaType != SKAlphaType.Premul)
        {
            throw new ArgumentException($"Expected Bgra8888/Premul, got {bitmap.ColorType}/{bitmap.AlphaType}.", nameof(bitmap));
        }

        var stride = bitmap.RowBytes;
        var pixels = new byte[(long)stride * bitmap.Height];
        new ReadOnlySpan<byte>((void*)bitmap.GetPixels(), pixels.Length).CopyTo(pixels);

        return new ImageBuffer(pixels, bitmap.Width, bitmap.Height, stride, ImagePixelFormat.Bgra8888Premultiplied);
    }

    /// <summary>
    /// Wraps <paramref name="buffer"/> in an <see cref="SKBitmap"/> without copying. The returned
    /// bitmap is only valid while <paramref name="pin"/> is held.
    /// </summary>
    public static unsafe SKBitmap WrapPinned(ImageBuffer buffer, out System.Buffers.MemoryHandle pin)
    {
        if (buffer.Format != ImagePixelFormat.Bgra8888Premultiplied)
        {
            throw new NotSupportedException($"Unsupported pixel format {buffer.Format}.");
        }

        pin = buffer.Pixels.Pin();
        var bitmap = new SKBitmap();
        if (!bitmap.InstallPixels(CreateInfo(buffer.Width, buffer.Height), (nint)pin.Pointer, buffer.Stride))
        {
            bitmap.Dispose();
            pin.Dispose();
            throw new InvalidOperationException("SkiaSharp rejected the pixel buffer.");
        }

        return bitmap;
    }
}
