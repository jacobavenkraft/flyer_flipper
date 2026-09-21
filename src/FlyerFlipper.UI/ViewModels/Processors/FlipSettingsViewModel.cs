using CommunityToolkit.Mvvm.ComponentModel;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;

namespace FlyerFlipper.UI.ViewModels.Processors;

public sealed partial class FlipSettingsViewModel : ObservableObject, IDisposable
{
    private readonly ProcessorSettings<FlipOptions> _settings;

    [ObservableProperty]
    private bool _enabled;

    [ObservableProperty]
    private bool _mirrorHorizontally;

    [ObservableProperty]
    private bool _mirrorVertically;

    public FlipSettingsViewModel(ProcessorSettings<FlipOptions> settings)
    {
        _settings = settings;
        var current = settings.Current;
        _enabled = current.Enabled;
        _mirrorHorizontally = current.MirrorHorizontally;
        _mirrorVertically = current.MirrorVertically;
        _settings.Changed += OnSettingsChanged;
    }

    // Checkboxes apply immediately (decision 15b). Echoes from OnSettingsChanged are no-ops: equal options don't raise.
    partial void OnEnabledChanged(bool value) => _settings.Update(_settings.Current with { Enabled = value });

    partial void OnMirrorHorizontallyChanged(bool value)
        => _settings.Update(_settings.Current with { MirrorHorizontally = value });

    partial void OnMirrorVerticallyChanged(bool value)
        => _settings.Update(_settings.Current with { MirrorVertically = value });

    /// <summary>Keeps the tab in sync when settings change elsewhere (e.g. restored from disk).</summary>
    private void OnSettingsChanged(object? sender, FlipOptions options)
    {
        Enabled = options.Enabled;
        MirrorHorizontally = options.MirrorHorizontally;
        MirrorVertically = options.MirrorVertically;
    }

    public void Dispose() => _settings.Changed -= OnSettingsChanged;
}
