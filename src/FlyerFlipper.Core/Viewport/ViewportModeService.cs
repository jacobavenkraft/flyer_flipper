using FlyerFlipper.Core.Source;

namespace FlyerFlipper.Core.Viewport;

public sealed class ViewportModeService : IViewportModeService, IDisposable
{
    private readonly IImageCatalog _catalog;

    public ViewportModeService(IImageCatalog catalog)
    {
        _catalog = catalog;
        CurrentIndex = catalog.Images.Count > 0 ? 0 : -1;
        _catalog.ImagesChanged += OnImagesChanged;
    }

    public ViewportMode Mode { get; private set; } = ViewportMode.Grid;

    public int CurrentIndex { get; private set; }

    public ImageReference? CurrentImage => CurrentIndex >= 0 ? _catalog.Images[CurrentIndex] : null;

    public int ImageCount => _catalog.Images.Count;

    public bool CanMovePrevious => CurrentIndex > 0;

    public bool CanMoveNext => CurrentIndex >= 0 && CurrentIndex < ImageCount - 1;

    public event EventHandler<ViewportMode>? ModeChanged;

    public event EventHandler? CurrentImageChanged;

    public void ShowGrid() => SetMode(ViewportMode.Grid);

    public void Select(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, ImageCount);
        SetCurrentIndex(index);
    }

    public bool ShowSingle(int? index = null)
    {
        if (index is { } requested)
        {
            Select(requested);
        }

        if (CurrentIndex < 0)
        {
            return false;
        }

        SetMode(ViewportMode.Single);
        return true;
    }

    public bool ToggleMode()
    {
        if (Mode == ViewportMode.Single)
        {
            ShowGrid();
            return true;
        }

        return ShowSingle();
    }

    public bool MovePrevious()
    {
        if (!CanMovePrevious)
        {
            return false;
        }

        SetCurrentIndex(CurrentIndex - 1);
        return true;
    }

    public bool MoveNext()
    {
        if (!CanMoveNext)
        {
            return false;
        }

        SetCurrentIndex(CurrentIndex + 1);
        return true;
    }

    private void OnImagesChanged(object? sender, EventArgs e)
    {
        // A new folder starts at its first image; an empty one can only be shown as the (empty) grid.
        CurrentIndex = ImageCount > 0 ? 0 : -1;
        if (CurrentIndex < 0)
        {
            SetMode(ViewportMode.Grid);
        }

        CurrentImageChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetCurrentIndex(int index)
    {
        if (CurrentIndex == index)
        {
            return;
        }

        CurrentIndex = index;
        CurrentImageChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetMode(ViewportMode mode)
    {
        if (Mode == mode)
        {
            return;
        }

        Mode = mode;
        ModeChanged?.Invoke(this, mode);
    }

    public void Dispose()
    {
        _catalog.ImagesChanged -= OnImagesChanged;
    }
}
