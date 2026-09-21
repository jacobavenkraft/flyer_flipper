using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlyerFlipper.Core.Source;
using FlyerFlipper.UI.Dialogs;

namespace FlyerFlipper.UI.ViewModels;

public sealed partial class ImageSourceViewModel : ObservableObject
{
    private readonly IImageCatalog _catalog;
    private readonly IFolderPicker _folderPicker;

    private CancellationTokenSource? _loadCts;

    [ObservableProperty]
    private string _folderPath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Supports JPG, PNG, BMP and WebP.";

    [ObservableProperty]
    private bool _hasError;

    public ImageSourceViewModel(IImageCatalog catalog, IFolderPicker folderPicker)
    {
        _catalog = catalog;
        _folderPicker = folderPicker;
    }

    /// <summary>
    /// Chooses a folder through the platform's picker, then loads it. Cancelling changes nothing —
    /// in particular it does not clear the path already in the box.
    /// </summary>
    [RelayCommand]
    private async Task BrowseAsync()
    {
        var picked = await _folderPicker.PickFolderAsync(NormalizePath(FolderPath));
        if (string.IsNullOrWhiteSpace(picked))
        {
            return;
        }

        FolderPath = picked;
        await LoadFolderAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task LoadFolderAsync()
    {
        var path = NormalizePath(FolderPath);
        if (path.Length == 0)
        {
            SetStatus("Enter a folder path.", isError: true);
            return;
        }

        _loadCts?.Cancel();
        using var cts = new CancellationTokenSource();
        _loadCts = cts;

        try
        {
            SetStatus("Loading…", isError: false);
            await _catalog.LoadAsync(new ImageSourceQuery(path), cts.Token);

            var count = _catalog.Images.Count;
            SetStatus(count == 1 ? "1 image" : $"{count} images", isError: false);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Superseded by a newer load; it owns the status message.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            SetStatus(ex.Message, isError: true);
        }
        finally
        {
            if (_loadCts == cts)
            {
                _loadCts = null;
            }
        }
    }

    /// <summary>Trims whitespace and the quotes Explorer's "Copy as path" adds.</summary>
    internal static string NormalizePath(string? path)
        => (path ?? string.Empty).Trim().Trim('"').Trim();

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        HasError = isError;
    }
}
