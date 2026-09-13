namespace FlyerFlipper.Core.Source;

/// <summary>
/// Holds the set of images the app is currently working with. Views (thumbnail grid, single view)
/// observe it; inputs (folder path box, restored settings) load into it.
/// </summary>
public interface IImageCatalog
{
    ImageSourceQuery? Query { get; }

    IReadOnlyList<ImageReference> Images { get; }

    /// <summary>
    /// Raised after <see cref="Images"/> is replaced, on the context that awaited <see cref="LoadAsync"/>.
    /// </summary>
    event EventHandler? ImagesChanged;

    /// <summary>
    /// Enumerates <paramref name="query"/> off the calling thread and replaces <see cref="Images"/>.
    /// If enumeration fails, the current images are left untouched.
    /// </summary>
    Task LoadAsync(ImageSourceQuery query, CancellationToken cancellationToken = default);
}
