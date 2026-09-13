namespace FlyerFlipper.Core.Source;

public sealed class ImageCatalog : IImageCatalog
{
    private readonly IImageSource _source;

    public ImageCatalog(IImageSource source)
    {
        _source = source;
    }

    public ImageSourceQuery? Query { get; private set; }

    public IReadOnlyList<ImageReference> Images { get; private set; } = [];

    public event EventHandler? ImagesChanged;

    public async Task LoadAsync(ImageSourceQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var images = await Task.Run(() => _source.Enumerate(query, cancellationToken), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        Query = query;
        Images = images;
        ImagesChanged?.Invoke(this, EventArgs.Empty);
    }
}
