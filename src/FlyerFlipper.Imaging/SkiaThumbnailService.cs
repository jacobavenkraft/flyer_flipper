using FlyerFlipper.Core.Imaging;
using SkiaSharp;

namespace FlyerFlipper.Imaging;

public sealed class SkiaThumbnailService : IThumbnailService
{
    public ImageBuffer CreateThumbnail(ImageBuffer source, int maxEdge)
    {
        ArgumentNullException.ThrowIfNull(source);

        var (width, height) = ThumbnailSizing.Fit(source.Width, source.Height, maxEdge);

        using var wrapped = SkiaImageBuffers.WrapPinned(source, out var pin);
        using (pin)
        {
            if (width == source.Width && height == source.Height)
            {
                using var copy = wrapped.Copy();
                return SkiaImageBuffers.ToImageBuffer(copy);
            }

            using var resized = wrapped.Resize(SkiaImageBuffers.CreateInfo(width, height), SKFilterQuality.High)
                ?? throw new InvalidOperationException($"SkiaSharp failed to resize {source.Width}x{source.Height} to {width}x{height}.");
            return SkiaImageBuffers.ToImageBuffer(resized);
        }
    }
}
