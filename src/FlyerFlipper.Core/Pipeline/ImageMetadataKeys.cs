namespace FlyerFlipper.Core.Pipeline;

/// <summary>Well-known <see cref="ProcessedImage.Metadata"/> keys.</summary>
public static class ImageMetadataKeys
{
    /// <summary>Absolute path of the file the image was decoded from.</summary>
    public const string SourcePath = "source.path";

    /// <summary>File name of the source image.</summary>
    public const string SourceFileName = "source.fileName";
}
