using System.Diagnostics;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using CommunityToolkit.Mvvm.ComponentModel;
using FlyerFlipper.Core.Imaging;
using FlyerFlipper.Core.Layout;
using FlyerFlipper.Core.Source;
using FlyerFlipper.UI.Imaging;

namespace FlyerFlipper.UI.ViewModels;

public sealed partial class ThumbnailGridViewModel : ObservableObject, IDisposable
{
    /// <summary>Longest edge, in pixels, of generated thumbnails. Oversized vs. the cell for high-DPI displays.</summary>
    public const int ThumbnailPixelSize = 256;

    private static readonly int MaxConcurrentLoads = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);

    private readonly IImageCatalog _catalog;
    private readonly IImageLoader _loader;
    private readonly IThumbnailService _thumbnails;
    private readonly ILayoutModeService _layoutMode;

    private CancellationTokenSource? _loadCts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private IReadOnlyList<ThumbnailItemViewModel> _items = [];

    [ObservableProperty]
    private string _emptyMessage = "Enter a folder path to see its images.";

    [ObservableProperty]
    private Orientation _itemsFlow;

    [ObservableProperty]
    private ScrollBarVisibility _horizontalScrollBarVisibility;

    [ObservableProperty]
    private ScrollBarVisibility _verticalScrollBarVisibility;

    public ThumbnailGridViewModel(
        IImageCatalog catalog,
        IImageLoader loader,
        IThumbnailService thumbnails,
        ILayoutModeService layoutMode)
    {
        _catalog = catalog;
        _loader = loader;
        _thumbnails = thumbnails;
        _layoutMode = layoutMode;

        ApplyOrientation(layoutMode.Orientation);
        _layoutMode.OrientationChanged += OnOrientationChanged;
        _catalog.ImagesChanged += OnImagesChanged;
    }

    public bool IsEmpty => Items.Count == 0;

    private void OnOrientationChanged(object? sender, LayoutOrientation e) => ApplyOrientation(e);

    private void ApplyOrientation(LayoutOrientation orientation)
    {
        if (orientation == LayoutOrientation.Vertical)
        {
            // Wide viewport: fill rows left-to-right, scroll down.
            ItemsFlow = Orientation.Horizontal;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        }
        else
        {
            // Tall viewport: fill columns top-to-bottom, scroll right.
            ItemsFlow = Orientation.Vertical;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        }
    }

    // Raised on the UI thread (the catalog is loaded from UI commands), so the async
    // continuations below also resume on the UI thread.
    private void OnImagesChanged(object? sender, EventArgs e)
    {
        _loadCts?.Cancel();

        var previous = Items;
        var items = _catalog.Images.Select(static r => new ThumbnailItemViewModel(r)).ToList();
        Items = items;
        EmptyMessage = "No images found in this folder.";

        foreach (var item in previous)
        {
            item.Dispose();
        }

        if (items.Count > 0)
        {
            var cts = new CancellationTokenSource();
            _loadCts = cts;
            _ = LoadThumbnailsAsync(items, cts);
        }
    }

    private async Task LoadThumbnailsAsync(IReadOnlyList<ThumbnailItemViewModel> items, CancellationTokenSource cts)
    {
        using var gate = new SemaphoreSlim(MaxConcurrentLoads);
        try
        {
            await Task.WhenAll(items.Select(item => LoadThumbnailAsync(item, gate, cts.Token)));
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Superseded by a newer folder load.
        }
        finally
        {
            if (_loadCts == cts)
            {
                _loadCts = null;
            }

            cts.Dispose();
        }
    }

    private async Task LoadThumbnailAsync(ThumbnailItemViewModel item, SemaphoreSlim gate, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var thumbnail = await Task.Run(
                () =>
                {
                    var source = _loader.Load(item.Reference, cancellationToken);
                    return _thumbnails.CreateThumbnail(source.Buffer, ThumbnailPixelSize);
                },
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            item.SetThumbnail(ImageBufferBitmap.Create(thumbnail));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Thumbnail failed for '{item.Reference.FullPath}': {ex}");
            item.SetError(ex is ImageLoadException or IOException or UnauthorizedAccessException
                ? ex.Message
                : "Could not load image.");
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
        _layoutMode.OrientationChanged -= OnOrientationChanged;
        _catalog.ImagesChanged -= OnImagesChanged;
        _loadCts?.Cancel();

        foreach (var item in Items)
        {
            item.Dispose();
        }
    }
}
