using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using FlyerFlipper.Core.Layout;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Settings;
using FlyerFlipper.Core.Source;
using FlyerFlipper.Core.Viewport;
using FlyerFlipper.UI.ViewModels;

namespace FlyerFlipper.UI.Settings;

/// <summary>
/// Restores saved settings at startup and saves them shortly after anything changes (PLAN.md decision 16).
/// Lives on the UI thread. Startup is two-phase: <see cref="LoadAndApplyStartupState"/> before the window is
/// shown, then <see cref="RestoreImagesAsync"/> once it is open.
/// </summary>
public sealed class SettingsCoordinator : IDisposable
{
    /// <summary>How long after the last change the settings are written.</summary>
    public static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(500);

    private readonly ISettingsStore _store;
    private readonly IImageCatalog _catalog;
    private readonly ILayoutModeService _layout;
    private readonly IViewportModeService _viewport;
    private readonly IReadOnlyList<IConfigurableImageProcessor> _processors;
    private readonly ImageSourceViewModel _imageSource;
    private readonly ProcessorTabHostViewModel _tabs;
    private readonly IWindowPlacementSource _windowPlacement;
    private readonly TimeProvider _timeProvider;

    private AppSettings _loaded = new();
    private bool _started;
    private bool _restoring = true;
    private bool _dirty;
    private CancellationTokenSource? _pendingSave;

    public SettingsCoordinator(
        ISettingsStore store,
        IImageCatalog catalog,
        ILayoutModeService layout,
        IViewportModeService viewport,
        IEnumerable<IImageProcessor> processors,
        ImageSourceViewModel imageSource,
        ProcessorTabHostViewModel tabs,
        IWindowPlacementSource windowPlacement,
        TimeProvider timeProvider)
    {
        _store = store;
        _catalog = catalog;
        _layout = layout;
        _viewport = viewport;
        _processors = processors.OfType<IConfigurableImageProcessor>().ToArray();
        _imageSource = imageSource;
        _tabs = tabs;
        _windowPlacement = windowPlacement;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Phase 1 (before the window is shown): loads the settings file and applies everything that doesn't need
    /// images — layout, scaling mode, processor settings, active tab — then starts watching for changes.
    /// Returns the loaded settings so the caller can restore the window placement.
    /// </summary>
    public AppSettings LoadAndApplyStartupState()
    {
        if (_started)
        {
            throw new InvalidOperationException("Startup state was already applied.");
        }

        _started = true;
        _loaded = _store.Load();

        if (Enum.IsDefined(_loaded.Orientation))
        {
            _layout.SetOrientation(_loaded.Orientation);
        }

        if (Enum.IsDefined(_loaded.ScaleMode))
        {
            _viewport.SetScaleMode(_loaded.ScaleMode);
        }

        // Hand each processor its own snippet; the coordinator never interprets processor settings.
        foreach (var processor in _processors)
        {
            if (_loaded.Processors.TryGetValue(processor.SettingsId, out var json)
                && !processor.TryApplySettingsJson(json.GetRawText()))
            {
                Trace.WriteLine($"Ignoring invalid saved settings for '{processor.SettingsId}'.");
            }
        }

        if (_loaded.ActiveProcessorTab is { } tab)
        {
            _tabs.SelectTab(tab);
        }

        Subscribe();
        return _loaded;
    }

    /// <summary>
    /// Phase 2 (window open): reloads the last folder, reselects the viewed image by file name (first image if it
    /// is gone), and returns to single view if that's where the user left off. Saving resumes afterwards.
    /// </summary>
    public async Task RestoreImagesAsync()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_loaded.LastFolder))
            {
                _imageSource.FolderPath = _loaded.LastFolder;

                // A missing folder shows the usual error in the folder panel and leaves the grid empty.
                await _imageSource.LoadFolderCommand.ExecuteAsync(null);

                if (_catalog.Query is not null && _catalog.Images.Count > 0)
                {
                    _viewport.Select(IndexOfFile(_loaded.ViewedImageFileName) ?? 0);
                    if (_loaded.ViewportMode == ViewportMode.Single)
                    {
                        _viewport.ShowSingle();
                    }
                }
            }
        }
        finally
        {
            _restoring = false;
            if (_dirty)
            {
                ScheduleSave();
            }
        }
    }

    /// <summary>Writes any pending changes immediately (call on exit).</summary>
    public void Flush()
    {
        _pendingSave?.Cancel();
        _pendingSave = null;
        if (_dirty)
        {
            SaveNow();
        }
    }

    /// <summary>The settings as they would be saved right now.</summary>
    public AppSettings Capture()
    {
        // Until a folder has loaded this session (e.g. the saved one is missing), keep the saved image state
        // so it can still be restored once the folder is back.
        var noFolderLoaded = _catalog.Query is null;

        var processors = new Dictionary<string, JsonElement>(_loaded.Processors); // keeps entries of absent processors
        foreach (var processor in _processors)
        {
            try
            {
                using var document = JsonDocument.Parse(processor.GetSettingsJson());
                processors[processor.SettingsId] = document.RootElement.Clone();
            }
            catch (JsonException ex)
            {
                Trace.WriteLine($"Processor '{processor.SettingsId}' produced invalid settings JSON: {ex.Message}");
            }
        }

        return new AppSettings
        {
            LastFolder = _catalog.Query?.RootPath ?? _loaded.LastFolder,
            Orientation = _layout.Orientation,
            ViewportMode = noFolderLoaded ? _loaded.ViewportMode : _viewport.Mode,
            ViewedImageFileName = noFolderLoaded ? _loaded.ViewedImageFileName : _viewport.CurrentImage?.FileName,
            ScaleMode = _viewport.ScaleMode,
            ActiveProcessorTab = _tabs.SelectedHeader ?? _loaded.ActiveProcessorTab,
            Window = _windowPlacement.Current ?? _loaded.Window,
            Processors = processors,
        };
    }

    private int? IndexOfFile(string? fileName)
    {
        if (fileName is null)
        {
            return null;
        }

        for (var i = 0; i < _catalog.Images.Count; i++)
        {
            if (string.Equals(_catalog.Images[i].FileName, fileName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return null;
    }

    private void Subscribe()
    {
        _catalog.ImagesChanged += OnChanged;
        _layout.OrientationChanged += OnOrientationChanged;
        _viewport.ModeChanged += OnViewportModeChanged;
        _viewport.CurrentImageChanged += OnChanged;
        _viewport.ScaleModeChanged += OnScaleModeChanged;
        _windowPlacement.Changed += OnChanged;
        _tabs.PropertyChanged += OnTabsPropertyChanged;
        foreach (var processor in _processors)
        {
            processor.SettingsChanged += OnChanged;
        }
    }

    private void OnChanged(object? sender, EventArgs e) => ScheduleSave();

    private void OnOrientationChanged(object? sender, LayoutOrientation e) => ScheduleSave();

    private void OnViewportModeChanged(object? sender, ViewportMode e) => ScheduleSave();

    private void OnScaleModeChanged(object? sender, ViewportScaleMode e) => ScheduleSave();

    private void OnTabsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProcessorTabHostViewModel.SelectedIndex))
        {
            ScheduleSave();
        }
    }

    /// <summary>Debounces saves: each change restarts the <see cref="SaveDelay"/> countdown.</summary>
    private void ScheduleSave()
    {
        _dirty = true;
        if (_restoring)
        {
            return; // restore's own changes; saved once restore completes
        }

        _pendingSave?.Cancel();
        var cts = new CancellationTokenSource();
        _pendingSave = cts;
        _ = SaveAfterDelayAsync(cts);
    }

    private async Task SaveAfterDelayAsync(CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(SaveDelay, _timeProvider, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            cts.Dispose();
        }

        if (_pendingSave == cts)
        {
            _pendingSave = null;
            SaveNow();
        }
    }

    private void SaveNow()
    {
        _dirty = false;
        try
        {
            _store.Save(Capture());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not fatal: the next change (or exit) tries again.
            _dirty = true;
            Trace.WriteLine($"Saving settings failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _pendingSave?.Cancel();
        if (!_started)
        {
            return;
        }

        _catalog.ImagesChanged -= OnChanged;
        _layout.OrientationChanged -= OnOrientationChanged;
        _viewport.ModeChanged -= OnViewportModeChanged;
        _viewport.CurrentImageChanged -= OnChanged;
        _viewport.ScaleModeChanged -= OnScaleModeChanged;
        _windowPlacement.Changed -= OnChanged;
        _tabs.PropertyChanged -= OnTabsPropertyChanged;
        foreach (var processor in _processors)
        {
            processor.SettingsChanged -= OnChanged;
        }
    }
}
