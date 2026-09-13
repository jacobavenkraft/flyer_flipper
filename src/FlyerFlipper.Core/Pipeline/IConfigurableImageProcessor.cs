namespace FlyerFlipper.Core.Pipeline;

/// <summary>
/// A processor whose output depends on user-changeable settings. The pipeline forwards
/// <see cref="SettingsChanged"/> so already-processed images can be regenerated.
/// </summary>
public interface IConfigurableImageProcessor : IImageProcessor
{
    /// <summary>Raised on the UI thread when a setting that affects <see cref="IImageProcessor.Process"/> changes.</summary>
    event EventHandler? SettingsChanged;
}
