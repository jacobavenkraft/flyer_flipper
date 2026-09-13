using FlyerFlipper.Core.Source;

namespace FlyerFlipper.Core.Store;

/// <summary>
/// The single source of display images for all views (PLAN.md decision 14).
/// <list type="bullet">
/// <item>Thumbnails for every image in the <see cref="IImageCatalog"/>, generated in the background and always retained.</item>
/// <item>Full-size images only for the current image ± 1, and only while the viewport is in single-image mode.</item>
/// </list>
/// The store is UI-thread-affine: members must be used, and events are raised, on the thread that
/// drives the catalog and viewport. Change events are raised <em>before</em> a replaced image is
/// disposed, so observers can drop their reference first.
/// </summary>
public interface IImageStore<TImage>
    where TImage : class
{
    /// <summary>The catalog images the store currently holds slots for (same order and indices).</summary>
    IReadOnlyList<ImageReference> Images { get; }

    ImageSlot<TImage> GetThumbnail(int index);

    ImageSlot<TImage> GetFullImage(int index);

    /// <summary>Raised after the catalog was replaced; all previous slots are gone.</summary>
    event EventHandler? ImagesReset;

    /// <summary>Raised with the image index whenever its thumbnail slot changes.</summary>
    event EventHandler<int>? ThumbnailChanged;

    /// <summary>Raised with the image index whenever its full-size slot changes (including release).</summary>
    event EventHandler<int>? FullImageChanged;
}
