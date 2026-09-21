using CommunityToolkit.Mvvm.ComponentModel;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;

namespace FlyerFlipper.UI.ViewModels.Processors;

public sealed partial class InvertSettingsViewModel : ObservableObject, IDisposable
{
    private readonly ProcessorSettings<InvertOptions> _settings;

    [ObservableProperty]
    private bool _enabled;

    public InvertSettingsViewModel(ProcessorSettings<InvertOptions> settings)
    {
        _settings = settings;
        _enabled = settings.Current.Enabled;
        _settings.Changed += OnSettingsChanged;
    }

    // Checkboxes apply immediately (decision 15b). Echoes from OnSettingsChanged are no-ops: equal options don't raise.
    partial void OnEnabledChanged(bool value) => _settings.Update(_settings.Current with { Enabled = value });

    /// <summary>Keeps the tab in sync when settings change elsewhere (e.g. restored from disk).</summary>
    private void OnSettingsChanged(object? sender, InvertOptions options) => Enabled = options.Enabled;

    public void Dispose() => _settings.Changed -= OnSettingsChanged;
}
