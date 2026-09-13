namespace FlyerFlipper.Core.Store;

/// <summary>
/// A snapshot of one stored image (a thumbnail or a full-size image).
/// </summary>
/// <param name="State">Load state.</param>
/// <param name="Image">The display image when <see cref="ImageLoadState.Ready"/>; otherwise null. Owned by the store.</param>
/// <param name="Error">User-facing message when <see cref="ImageLoadState.Failed"/>; otherwise null.</param>
public readonly record struct ImageSlot<TImage>(ImageLoadState State, TImage? Image, string? Error)
    where TImage : class
{
    public static ImageSlot<TImage> NotLoaded => default;

    public static ImageSlot<TImage> Loading => new(ImageLoadState.Loading, null, null);

    public static ImageSlot<TImage> Ready(TImage image) => new(ImageLoadState.Ready, image, null);

    public static ImageSlot<TImage> Failed(string error) => new(ImageLoadState.Failed, null, error);
}
