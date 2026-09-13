namespace FlyerFlipper.Core.Source;

/// <summary>
/// Identifies a single image produced by an <see cref="IImageSource"/>.
/// </summary>
/// <param name="FullPath">Absolute path of the image file.</param>
public sealed record ImageReference(string FullPath)
{
    public string FileName => Path.GetFileName(FullPath);
}
