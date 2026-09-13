using FlyerFlipper.Core.Source;

namespace FlyerFlipper.Core.Viewport;

/// <summary>
/// Publishes whether the viewport shows the thumbnail grid or a single image, and which image of
/// the <see cref="IImageCatalog"/> is current.
/// </summary>
public interface IViewportModeService
{
    ViewportMode Mode { get; }

    /// <summary>Index into <see cref="IImageCatalog.Images"/>, or -1 when the catalog is empty.</summary>
    int CurrentIndex { get; }

    ImageReference? CurrentImage { get; }

    int ImageCount { get; }

    bool CanMovePrevious { get; }

    bool CanMoveNext { get; }

    event EventHandler<ViewportMode>? ModeChanged;

    /// <summary>
    /// Raised when <see cref="CurrentIndex"/> changes or the catalog is replaced (the current
    /// image may differ even if the index does not).
    /// </summary>
    event EventHandler? CurrentImageChanged;

    void ShowGrid();

    /// <summary>
    /// Switches to single view at <paramref name="index"/>, or at <see cref="CurrentIndex"/> when null.
    /// Returns <see langword="false"/> (and stays in the grid) when there are no images.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the catalog.</exception>
    bool ShowSingle(int? index = null);

    /// <summary>Grid ↔ single. Returns <see langword="false"/> if the mode did not change.</summary>
    bool ToggleMode();

    bool MovePrevious();

    bool MoveNext();
}
