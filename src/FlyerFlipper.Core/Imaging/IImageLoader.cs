using FlyerFlipper.Core.Source;

namespace FlyerFlipper.Core.Imaging;

public interface IImageLoader
{
    /// <summary>
    /// Decodes <paramref name="reference"/> into memory. CPU- and IO-bound; call off the UI thread.
    /// </summary>
    /// <exception cref="ImageLoadException">The file could not be decoded.</exception>
    /// <exception cref="IOException">The file could not be read.</exception>
    SourceImage Load(ImageReference reference, CancellationToken cancellationToken = default);
}
