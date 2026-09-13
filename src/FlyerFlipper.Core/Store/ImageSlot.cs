namespace FlyerFlipper.Core.Store;

/// <summary>
/// A snapshot of one stored image (a thumbnail or a full-size image).
/// </summary>
/// <param name="State">Load state.</param>
/// <param name="Image">
/// The display image when <see cref="ImageLoadState.Ready"/>. While <see cref="ImageLoadState.Loading"/> it may
/// hold the previous (stale) result, kept visible while processing settings are re-applied. Otherwise null.
/// Owned by the store.
/// </param>
/// <param name="Error">User-facing message when <see cref="ImageLoadState.Failed"/>; otherwise null.</param>
public readonly record struct ImageSlot<TImage>(ImageLoadState State, TImage? Image, string? Error)
    where TImage : class
{
    public static ImageSlot<TImage> NotLoaded => default;

    public static ImageSlot<TImage> Loading => new(ImageLoadState.Loading, null, null);

    /// <summary>Loading, keeping <paramref name="stale"/> (if any) on screen until the new result arrives.</summary>
    public static ImageSlot<TImage> Refreshing(TImage? stale) => new(ImageLoadState.Loading, stale, null);

    public static ImageSlot<TImage> Ready(TImage image) => new(ImageLoadState.Ready, image, null);

    public static ImageSlot<TImage> Failed(string error) => new(ImageLoadState.Failed, null, error);
}
