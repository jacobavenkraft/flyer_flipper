using FlyerFlipper.Core.Imaging;

namespace FlyerFlipper.Core.Store;

/// <summary>
/// Turns a pixel buffer into whatever the UI displays (e.g. an Avalonia bitmap), so the store holds a
/// single display-ready copy of each image. Implemented by the UI layer.
/// </summary>
public interface IDisplayImageFactory<out TImage>
    where TImage : class
{
    /// <summary>
    /// Copies <paramref name="buffer"/> into a new display image. Called on background threads.
    /// If <typeparamref name="TImage"/> is <see cref="IDisposable"/>, the store disposes it when released.
    /// </summary>
    TImage Create(ImageBuffer buffer);
}
