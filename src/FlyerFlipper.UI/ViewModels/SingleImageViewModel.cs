using System.Diagnostics;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlyerFlipper.Core.Imaging;
using FlyerFlipper.Core.Source;
using FlyerFlipper.Core.Viewport;
using FlyerFlipper.UI.Imaging;

namespace FlyerFlipper.UI.ViewModels;

/// <summary>
/// Full-size view of <see cref="IViewportModeService.CurrentImage"/>. Keeps the current image and its
/// immediate neighbours decoded so stepping through images feels instant.
/// </summary>
public sealed partial class SingleImageViewModel : ObservableObject, IDisposable
{
    private readonly IImageCatalog _catalog;
    private readonly IViewportModeService _viewport;
    private readonly IImageLoader _loader;

    // All members below are touched only on the UI thread.
    private readonly Dictionary<ImageReference, Task<Bitmap>> _cache = [];
    private CancellationTokenSource _cacheCts = new();
    private ImageReference? _displayedReference;
    private int _showVersion;

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

    public SingleImageViewModel(IImageCatalog catalog, IViewportModeService viewport, IImageLoader loader)
    {
        _catalog = catalog;
        _viewport = viewport;
        _loader = loader;
        _viewport.ModeChanged += OnModeChanged;
        _viewport.CurrentImageChanged += OnCurrentImageChanged;
    }

    public bool HasError => ErrorMessage is not null;

    public bool CanGoPrevious => _viewport.Mode == ViewportMode.Single && _viewport.CanMovePrevious;

    public bool CanGoNext => _viewport.Mode == ViewportMode.Single && _viewport.CanMoveNext;

    private bool IsActive => _viewport.Mode == ViewportMode.Single;

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void Previous() => _viewport.MovePrevious();

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next() => _viewport.MoveNext();

    [RelayCommand(CanExecute = nameof(IsActive))]
    private void BackToGrid() => _viewport.ShowGrid();

    private void OnModeChanged(object? sender, ViewportMode mode)
    {
        if (mode == ViewportMode.Single)
        {
            _ = ShowCurrentAsync();
        }
        else
        {
            // Full-size bitmaps are large; release them while the grid is showing.
            ClearDisplay();
            ResetCache();
        }

        RefreshNavigation();
        BackToGridCommand.NotifyCanExecuteChanged();
    }

    private void OnCurrentImageChanged(object? sender, EventArgs e)
    {
        if (IsActive)
        {
            _ = ShowCurrentAsync();
        }
        else
        {
            // The catalog may have been replaced; cached bitmaps could belong to the old folder.
            ResetCache();
        }

        RefreshNavigation();
    }

    private async Task ShowCurrentAsync()
    {
        var version = ++_showVersion;
        var reference = _viewport.CurrentImage;
        if (reference is null)
        {
            ClearDisplay();
            return;
        }

        FileName = reference.FileName;
        PositionText = $"{_viewport.CurrentIndex + 1} / {_viewport.ImageCount}";
        ErrorMessage = null;

        var index = _viewport.CurrentIndex;
        var neighbours = new List<ImageReference>(3) { reference };
        if (_viewport.CanMovePrevious)
        {
            neighbours.Add(ImageAt(index - 1));
        }

        if (_viewport.CanMoveNext)
        {
            neighbours.Add(ImageAt(index + 1));
        }

        if (_displayedReference is not null && !neighbours.Contains(_displayedReference))
        {
            ClearDisplay();
        }

        EvictAllExcept(neighbours);

        var load = GetOrStartLoad(reference);
        IsLoading = !load.IsCompleted;

        try
        {
            var bitmap = await load;
            if (version != _showVersion)
            {
                return;
            }

            Image = bitmap;
            _displayedReference = reference;
        }
        catch (Exception ex)
        {
            if (version != _showVersion || ex is OperationCanceledException)
            {
                return;
            }

            Trace.WriteLine($"Single view failed for '{reference.FullPath}': {ex}");
            ClearDisplay();
            ErrorMessage = ex is ImageLoadException or IOException or UnauthorizedAccessException
                ? ex.Message
                : "Could not load image.";
        }
        finally
        {
            if (version == _showVersion)
            {
                IsLoading = false;
            }
        }

        if (version == _showVersion)
        {
            // Pre-decode neighbours; failures surface if and when the user navigates to them.
            foreach (var neighbour in neighbours)
            {
                _ = GetOrStartLoad(neighbour);
            }
        }
    }

    private ImageReference ImageAt(int index) => _catalog.Images[index];

    private Task<Bitmap> GetOrStartLoad(ImageReference reference)
    {
        if (_cache.TryGetValue(reference, out var existing))
        {
            return existing;
        }

        var token = _cacheCts.Token;
        var task = Task.Run(
            () =>
            {
                var source = _loader.Load(reference, token);
                token.ThrowIfCancellationRequested();
                return ImageBufferBitmap.Create(source.Buffer);
            },
            token);

        _cache[reference] = task;
        return task;
    }

    private void EvictAllExcept(IReadOnlyCollection<ImageReference> keep)
    {
        foreach (var reference in _cache.Keys.Where(r => !keep.Contains(r)).ToList())
        {
            DisposeWhenDone(_cache[reference]);
            _cache.Remove(reference);
        }
    }

    private void ResetCache()
    {
        _showVersion++;
        _cacheCts.Cancel();
        _cacheCts.Dispose();
        _cacheCts = new CancellationTokenSource();

        foreach (var task in _cache.Values)
        {
            DisposeWhenDone(task);
        }

        _cache.Clear();
    }

    private static void DisposeWhenDone(Task<Bitmap> task)
        => task.ContinueWith(
            static t =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    t.Result.Dispose();
                }
                else
                {
                    _ = t.Exception; // observe, so an evicted failed pre-decode isn't reported as unobserved
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    /// <summary>
    /// Detaches the displayed bitmap. Disposal is left to the cache, which owns every bitmap.
    /// </summary>
    private void ClearDisplay()
    {
        Image = null;
        _displayedReference = null;
        IsLoading = false;
    }

    private void RefreshNavigation()
    {
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        _viewport.ModeChanged -= OnModeChanged;
        _viewport.CurrentImageChanged -= OnCurrentImageChanged;
        ClearDisplay();
        ResetCache();
        _cacheCts.Dispose();
    }
}
