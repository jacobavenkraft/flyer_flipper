using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using FlyerFlipper.Core.Source;

namespace FlyerFlipper.UI.ViewModels;

public sealed partial class ThumbnailItemViewModel : ObservableObject, IDisposable
{
    [ObservableProperty]
    private Bitmap? _thumbnail;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    /// <summary>Whether this is the viewport's current image (last one opened / navigated to).</summary>
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

    public void SetThumbnail(Bitmap bitmap)
    {
        var previous = Thumbnail;
        Thumbnail = bitmap;
        IsLoading = false;
        ErrorMessage = null;
        previous?.Dispose();
    }

    public void SetError(string message)
    {
        IsLoading = false;
        ErrorMessage = message;
    }

    public void Dispose()
    {
        var thumbnail = Thumbnail;
        Thumbnail = null;
        thumbnail?.Dispose();
    }
}
