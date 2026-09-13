namespace FlyerFlipper.Core.Source;

public interface IImageSource
{
    /// <summary>
    /// Enumerates the images matching <paramref name="query"/>, ordered by path.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException"><see cref="ImageSourceQuery.RootPath"/> does not exist.</exception>
    IReadOnlyList<ImageReference> Enumerate(ImageSourceQuery query, CancellationToken cancellationToken = default);
}
