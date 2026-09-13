using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlyerFlipper.Core.Store;
using FlyerFlipper.Core.Viewport;

namespace FlyerFlipper.UI.ViewModels;

/// <summary>
/// Full-size view of <see cref="IViewportModeService.CurrentImage"/>. The image store keeps the current
/// image and its neighbours decoded while in single mode; this view model only mirrors the current slot.
/// </summary>
public sealed partial class SingleImageViewModel : ObservableObject, IDisposable
{
    private readonly IViewportModeService _viewport;
    private readonly IImageStore<Bitmap> _store;

    /// <summary>Index whose slot <see cref="Image"/> came from, or -1.</summary>
    private int _displayedIndex = -1;

    /// <summary>Owned by the image store; never disposed here.</summary>
    [ObservableProperty]
    private Bitmap? _image;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _positionText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public SingleImageViewModel(IViewportModeService viewport, IImageStore<Bitmap> store)
    {
        _viewport = viewport;
        _store = store;
        _viewport.ModeChanged += OnViewportModeChanged;
        _viewport.CurrentImageChanged += OnViewportChanged;
        _store.ImagesReset += OnViewportChanged;
        _store.FullImageChanged += OnStoreFullImageChanged;
        _viewport.ScaleModeChanged += OnScaleModeChanged;
        Refresh();
    }

    public bool HasError => ErrorMessage is not null;

    /// <summary>Image scaling for <see cref="IViewportModeService.ScaleMode"/> (display only).</summary>
    public Stretch ImageStretch => _viewport.ScaleMode switch
    {
        ViewportScaleMode.StretchToFill => Stretch.Fill,
        ViewportScaleMode.ActualSize => Stretch.None,
        _ => Stretch.Uniform,
    };

    public StretchDirection ImageStretchDirection => _viewport.ScaleMode == ViewportScaleMode.FitWithoutEnlarging
        ? StretchDirection.DownOnly
        : StretchDirection.Both;

    /// <summary>Only Actual Size scrolls; the other modes constrain the image to the viewport.</summary>
    public ScrollBarVisibility ScrollBarVisibility => _viewport.ScaleMode == ViewportScaleMode.ActualSize
        ? ScrollBarVisibility.Auto
        : ScrollBarVisibility.Disabled;

    private void OnScaleModeChanged(object? sender, ViewportScaleMode e)
    {
        OnPropertyChanged(nameof(ImageStretch));
        OnPropertyChanged(nameof(ImageStretchDirection));
        OnPropertyChanged(nameof(ScrollBarVisibility));
    }

    public bool CanGoPrevious => IsActive && _viewport.CanMovePrevious;

    public bool CanGoNext => IsActive && _viewport.CanMoveNext;

    private bool IsActive => _viewport.Mode == ViewportMode.Single;

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void Previous() => _viewport.MovePrevious();

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next() => _viewport.MoveNext();

    [RelayCommand(CanExecute = nameof(IsActive))]
    private void BackToGrid() => _viewport.ShowGrid();

    private void OnViewportModeChanged(object? sender, ViewportMode mode)
    {
        Refresh();
        BackToGridCommand.NotifyCanExecuteChanged();
    }

    private void OnViewportChanged(object? sender, EventArgs e) => Refresh();

    private void OnStoreFullImageChanged(object? sender, int index)
    {
        // Also refresh for the displayed index: the store may release it (and then dispose it)
        // before our viewport handler has moved us to the new current image.
        if (index == _viewport.CurrentIndex || index == _displayedIndex)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        var index = _viewport.CurrentIndex;
        if (IsActive && (uint)index < (uint)_store.Images.Count)
        {
            var slot = _store.GetFullImage(index);
            FileName = _store.Images[index].FileName;
            PositionText = $"{index + 1} / {_store.Images.Count}";
            Image = slot.Image;
            _displayedIndex = slot.Image is null ? -1 : index;
            IsLoading = slot.State is ImageLoadState.NotLoaded or ImageLoadState.Loading;
            ErrorMessage = slot.State == ImageLoadState.Failed ? slot.Error : null;
        }
        else
        {
            Image = null;
            _displayedIndex = -1;
            IsLoading = false;
            ErrorMessage = null;
        }

        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        _viewport.ModeChanged -= OnViewportModeChanged;
        _viewport.CurrentImageChanged -= OnViewportChanged;
        _store.ImagesReset -= OnViewportChanged;
        _store.FullImageChanged -= OnStoreFullImageChanged;
        _viewport.ScaleModeChanged -= OnScaleModeChanged;
    }
}
