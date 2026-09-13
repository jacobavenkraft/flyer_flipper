using FlyerFlipper.Core.Imaging;

namespace FlyerFlipper.Core.Pipeline;

/// <summary>
/// The value flowing through the <see cref="IImageProcessingPipeline"/>: pixels plus string metadata.
/// Deliberately made of plain, ABI-friendly parts (no Avalonia or SkiaSharp types) so it can cross the
/// future native-plugin boundary.
/// </summary>
public sealed record ProcessedImage
{
    public ProcessedImage(ImageBuffer buffer, IReadOnlyDictionary<string, string> metadata)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(metadata);
        Buffer = buffer;
        Metadata = metadata;
    }

    public ImageBuffer Buffer { get; init; }

    public IReadOnlyDictionary<string, string> Metadata { get; init; }

    /// <summary>Starts a pipeline run from a decoded image, recording where it came from.</summary>
    public static ProcessedImage FromSource(SourceImage source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new ProcessedImage(
            source.Buffer,
            new Dictionary<string, string>
            {
                [ImageMetadataKeys.SourcePath] = source.Reference.FullPath,
                [ImageMetadataKeys.SourceFileName] = source.Reference.FileName,
            });
    }

    /// <summary>Returns a copy with <paramref name="key"/> set to <paramref name="value"/>.</summary>
    public ProcessedImage WithMetadata(string key, string value)
    {
        var metadata = new Dictionary<string, string>(Metadata) { [key] = value };
        return this with { Metadata = metadata };
    }
}
