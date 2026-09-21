using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;

namespace FlyerFlipper.UI.ViewModels.Processors;

public sealed partial class RotateSettingsViewModel : ObservableObject, IDisposable
{
    private readonly ProcessorSettings<RotateOptions> _settings;

    [ObservableProperty]
    private bool _enabled;

    [ObservableProperty]
    private RotationAngle _angle;

    public RotateSettingsViewModel(ProcessorSettings<RotateOptions> settings)
    {
        _settings = settings;
        var current = settings.Current;
        _enabled = current.Enabled;
        _angle = current.Angle;
        _settings.Changed += OnSettingsChanged;
    }

    /// <summary>
    /// The radio buttons bind to these rather than to <see cref="Angle"/> directly: Avalonia's
    /// <c>IsChecked</c> is a bool, and a one-way binding per angle keeps the group in step when the
    /// angle is restored from disk.
    /// </summary>
    public bool IsNone => Angle == RotationAngle.None;

    public bool Is90 => Angle == RotationAngle.Clockwise90;

    public bool Is180 => Angle == RotationAngle.Clockwise180;

    public bool Is270 => Angle == RotationAngle.Clockwise270;

    /// <summary>Selects an angle. Each radio button invokes this with its own angle.</summary>
    [RelayCommand]
    private void SetAngle(RotationAngle angle) => Angle = angle;

    // Checkboxes and radio buttons apply immediately (decision 15b). RotateOptions validates its angle,
    // so its properties are get-only and a new instance is built rather than `with`-ed.
    partial void OnEnabledChanged(bool value)
        => _settings.Update(new RotateOptions(value, _settings.Current.Angle));

    partial void OnAngleChanged(RotationAngle value)
    {
        _settings.Update(new RotateOptions(_settings.Current.Enabled, value));
        OnPropertyChanged(nameof(IsNone));
        OnPropertyChanged(nameof(Is90));
        OnPropertyChanged(nameof(Is180));
        OnPropertyChanged(nameof(Is270));
    }

    /// <summary>Keeps the tab in sync when settings change elsewhere (e.g. restored from disk).</summary>
    private void OnSettingsChanged(object? sender, RotateOptions options)
    {
        Enabled = options.Enabled;
        Angle = options.Angle;
    }

    public void Dispose() => _settings.Changed -= OnSettingsChanged;
}
