using CommunityToolkit.Mvvm.ComponentModel;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;

namespace FlyerFlipper.UI.ViewModels.Processors;

public sealed partial class ChannelMapSettingsViewModel : ObservableObject, IDisposable
{
    private readonly ProcessorSettings<ChannelMapOptions> _settings;
    private bool _applying;

    [ObservableProperty]
    private bool _enabled;

    [ObservableProperty]
    private ColorChannel _red;

    [ObservableProperty]
    private ColorChannel _green;

    [ObservableProperty]
    private ColorChannel _blue;

    public ChannelMapSettingsViewModel(ProcessorSettings<ChannelMapOptions> settings)
    {
        _settings = settings;
        var current = settings.Current;
        _enabled = current.Enabled;
        _red = current.Red;
        _green = current.Green;
        _blue = current.Blue;
        _settings.Changed += OnSettingsChanged;
    }

    /// <summary>The source channels offered for each output, in a fixed order. Bound as the combo boxes' items.</summary>
    public IReadOnlyList<ColorChannel> Channels { get; } =
        [ColorChannel.Red, ColorChannel.Green, ColorChannel.Blue];

    // Selections apply immediately (decision 15b).
    partial void OnEnabledChanged(bool value) => Apply();

    partial void OnRedChanged(ColorChannel value) => Apply();

    partial void OnGreenChanged(ColorChannel value) => Apply();

    partial void OnBlueChanged(ColorChannel value) => Apply();

    /// <summary>
    /// Pushes all four values at once. ChannelMapOptions validates its channels, so its properties are
    /// get-only and a new instance is built rather than `with`-ed.
    /// </summary>
    private void Apply()
    {
        if (_applying)
        {
            return;
        }

        _settings.Update(new ChannelMapOptions(Enabled, Red, Green, Blue));
    }

    /// <summary>Keeps the tab in sync when settings change elsewhere (e.g. restored from disk).</summary>
    private void OnSettingsChanged(object? sender, ChannelMapOptions options)
    {
        // Setting four properties would otherwise push four half-updated option sets back at the store.
        _applying = true;
        try
        {
            Enabled = options.Enabled;
            Red = options.Red;
            Green = options.Green;
            Blue = options.Blue;
        }
        finally
        {
            _applying = false;
        }
    }

    public void Dispose() => _settings.Changed -= OnSettingsChanged;
}
