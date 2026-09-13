using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlyerFlipper.Core.Layout;
using FlyerFlipper.Core.Store;
using FlyerFlipper.Core.Viewport;

namespace FlyerFlipper.UI.ViewModels;

/// <summary>
/// Thumbnail grid. Thumbnails come from <see cref="IImageStore{TImage}"/>; this view model only mirrors
/// the store's slots and the viewport's selection.
/// </summary>
public sealed partial class ThumbnailGridViewModel : ObservableObject, IDisposable
{
    private readonly IImageStore<Bitmap> _store;
    private readonly ILayoutModeService _layoutMode;
    private readonly IViewportModeService _viewport;

    private ThumbnailItemViewModel? _currentItem;

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
        IImageStore<Bitmap> store,
        ILayoutModeService layoutMode,
        IViewportModeService viewport)
    {
        _store = store;
        _layoutMode = layoutMode;
        _viewport = viewport;

        ApplyOrientation(layoutMode.Orientation);
        _layoutMode.OrientationChanged += OnOrientationChanged;
        _store.ImagesReset += OnStoreImagesReset;
        _store.ThumbnailChanged += OnStoreThumbnailChanged;
        _viewport.CurrentImageChanged += OnCurrentImageChanged;
        _viewport.ModeChanged += OnViewportModeChanged;
    }

    public bool IsEmpty => Items.Count == 0;

    /// <summary>
    /// Asks the view to scroll the item at the given index into view (raised when returning to the grid).
    /// </summary>
    public event EventHandler<int>? ScrollIntoViewRequested;

    [RelayCommand]
    private void SelectImage(ThumbnailItemViewModel? item)
    {
        if (item is not null && item.Index < _viewport.ImageCount)
        {
            _viewport.Select(item.Index);
        }
    }

    [RelayCommand]
    private void OpenImage(ThumbnailItemViewModel? item)
    {
        if (item is not null && item.Index < _viewport.ImageCount)
        {
            _viewport.ShowSingle(item.Index);
        }
    }

    // The store raises this before disposing the previous folder's thumbnails, so the old items
    // (which reference them) are dropped first.
    private void OnStoreImagesReset(object? sender, EventArgs e)
    {
        var items = new List<ThumbnailItemViewModel>(_store.Images.Count);
        for (var i = 0; i < _store.Images.Count; i++)
        {
            var item = new ThumbnailItemViewModel(_store.Images[i], i);
            item.Apply(_store.GetThumbnail(i));
            items.Add(item);
        }

        _currentItem = null;
        Items = items;
        EmptyMessage = "No images found in this folder.";
        UpdateCurrentItem();
    }

    private void OnStoreThumbnailChanged(object? sender, int index)
    {
        if ((uint)index < (uint)Items.Count)
        {
            Items[index].Apply(_store.GetThumbnail(index));
        }
    }

    private void OnViewportModeChanged(object? sender, ViewportMode mode)
    {
        if (mode == ViewportMode.Grid && _viewport.CurrentIndex >= 0)
        {
            ScrollIntoViewRequested?.Invoke(this, _viewport.CurrentIndex);
        }
    }

    private void OnCurrentImageChanged(object? sender, EventArgs e) => UpdateCurrentItem();

    // Converges regardless of whether the store or viewport handler runs first on a folder change.
    private void UpdateCurrentItem()
    {
        var index = _viewport.CurrentIndex;
        var item = index >= 0 && index < Items.Count ? Items[index] : null;
        if (ReferenceEquals(item, _currentItem))
        {
            return;
        }

        if (_currentItem is not null)
        {
            _currentItem.IsCurrent = false;
        }

        _currentItem = item;
        if (item is not null)
        {
            item.IsCurrent = true;
        }
    }

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

    public void Dispose()
    {
        _layoutMode.OrientationChanged -= OnOrientationChanged;
        _store.ImagesReset -= OnStoreImagesReset;
        _store.ThumbnailChanged -= OnStoreThumbnailChanged;
        _viewport.CurrentImageChanged -= OnCurrentImageChanged;
        _viewport.ModeChanged -= OnViewportModeChanged;
    }
}
