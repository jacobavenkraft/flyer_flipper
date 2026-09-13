using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using FlyerFlipper.Core.Source;
using FlyerFlipper.Core.Store;

namespace FlyerFlipper.UI.ViewModels;

public sealed partial class ThumbnailItemViewModel : ObservableObject
{
    /// <summary>Owned by the image store; never disposed here.</summary>
    [ObservableProperty]
    private Bitmap? _thumbnail;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    /// <summary>
    /// Whether this is the selected (current) image: clicked in the grid, or last shown in single view.
    /// Toggling to single view opens it.
    /// </summary>
    [ObservableProperty]
    private bool _isCurrent;

    public ThumbnailItemViewModel(ImageReference reference, int index)
    {
        Reference = reference;
        Index = index;
    }

    public ImageReference Reference { get; }

    /// <summary>Position in the image catalog.</summary>
    public int Index { get; }

    public string FileName => Reference.FileName;

    public bool HasError => ErrorMessage is not null;

    public void Apply(ImageSlot<Bitmap> slot)
    {
        Thumbnail = slot.Image;
        IsLoading = slot.State is ImageLoadState.NotLoaded or ImageLoadState.Loading;
        ErrorMessage = slot.State == ImageLoadState.Failed ? slot.Error : null;
    }
}
