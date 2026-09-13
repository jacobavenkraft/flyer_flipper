using CommunityToolkit.Mvvm.ComponentModel;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;

namespace FlyerFlipper.UI.ViewModels.Processors;

public sealed partial class ResizeSettingsViewModel : ObservableObject, IDisposable
{
    private readonly ProcessorSettings<ResizeOptions> _settings;

    /// <summary>True while copying incoming settings into the properties, so they aren't re-applied piecemeal.</summary>
    private bool _syncing;

    [ObservableProperty]
    private bool _enabled;

    /// <summary>Bound to a NumericUpDown that commits on Enter, focus loss, or spin (decision 15b).</summary>
    [ObservableProperty]
    private decimal? _maxWidth;

    [ObservableProperty]
    private decimal? _maxHeight;

    public ResizeSettingsViewModel(ProcessorSettings<ResizeOptions> settings)
    {
        _settings = settings;
        CopyFrom(settings.Current);
        _settings.Changed += OnSettingsChanged;
    }

    public decimal Minimum => ResizeOptions.MinDimension;

    public decimal Maximum => ResizeOptions.MaxDimension;

    partial void OnEnabledChanged(bool value) => Apply();

    partial void OnMaxWidthChanged(decimal? value) => Apply();

    partial void OnMaxHeightChanged(decimal? value) => Apply();

    private void Apply()
    {
        if (_syncing)
        {
            return;
        }

        var current = _settings.Current;

        // A cleared field keeps the last valid value rather than applying nothing or throwing.
        var width = MaxWidth is { } w ? ClampDimension(w) : current.MaxWidth;
        var height = MaxHeight is { } h ? ClampDimension(h) : current.MaxHeight;
        _settings.Update(new ResizeOptions(Enabled, width, height));
    }

    /// <summary>Keeps the tab in sync when settings change elsewhere (e.g. restored from disk).</summary>
    private void OnSettingsChanged(object? sender, ResizeOptions options) => CopyFrom(options);

    private void CopyFrom(ResizeOptions options)
    {
        _syncing = true;
        try
        {
            Enabled = options.Enabled;
            MaxWidth = options.MaxWidth;
            MaxHeight = options.MaxHeight;
        }
        finally
        {
            _syncing = false;
        }
    }

    private static int ClampDimension(decimal value)
        => (int)Math.Clamp(Math.Round(value), ResizeOptions.MinDimension, ResizeOptions.MaxDimension);

    public void Dispose() => _settings.Changed -= OnSettingsChanged;
}
