using FlyerFlipper.Core.Imaging;
using FlyerFlipper.Core.Source;
using SkiaSharp;

namespace FlyerFlipper.Imaging;

/// <summary>
/// Decodes JPG, PNG, BMP and WebP via SkiaSharp's built-in codecs and applies EXIF orientation.
/// </summary>
public sealed class SkiaImageLoader : IImageLoader
{
    public SourceImage Load(ImageReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        cancellationToken.ThrowIfCancellationRequested();

        using var stream = File.OpenRead(reference.FullPath);
        using var codec = SKCodec.Create(stream, out var createResult)
            ?? throw new ImageLoadException($"Unsupported or corrupt image '{reference.FileName}' ({createResult}).");

        var info = SkiaImageBuffers.CreateInfo(codec.Info.Width, codec.Info.Height);
        using var decoded = new SKBitmap(info);
        var result = codec.GetPixels(info, decoded.GetPixels());

        // IncompleteInput still yields a usable (partially filled) image, as in browsers/viewers.
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            throw new ImageLoadException($"Failed to decode '{reference.FileName}' ({result}).");
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var oriented = SkiaOrientation.Apply(decoded, codec.EncodedOrigin);
        return new SourceImage(reference, SkiaImageBuffers.ToImageBuffer(oriented ?? decoded));
    }
}
