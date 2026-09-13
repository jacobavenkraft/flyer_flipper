using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FlyerFlipper.Core.Imaging;

namespace FlyerFlipper.UI.Imaging;

internal static class ImageBufferBitmap
{
    /// <summary>
    /// Copies <paramref name="buffer"/> into a new Avalonia <see cref="Bitmap"/>.
    /// </summary>
    public static unsafe Bitmap Create(ImageBuffer buffer)
    {
        var (format, alpha) = buffer.Format switch
        {
            ImagePixelFormat.Bgra8888Premultiplied => (PixelFormat.Bgra8888, AlphaFormat.Premul),
            _ => throw new NotSupportedException($"Unsupported pixel format {buffer.Format}."),
        };

        using var pin = buffer.Pixels.Pin();
        return new Bitmap(
            format,
            alpha,
            (nint)pin.Pointer,
            new PixelSize(buffer.Width, buffer.Height),
            new Vector(96, 96),
            buffer.Stride);
    }
}
