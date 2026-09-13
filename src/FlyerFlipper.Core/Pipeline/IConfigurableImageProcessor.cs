namespace FlyerFlipper.Core.Pipeline;

/// <summary>
/// A processor whose output depends on user-changeable settings. The pipeline forwards
/// <see cref="SettingsChanged"/> so already-processed images can be regenerated, and the settings are
/// persisted generically as a JSON snippet (PLAN.md decision 16) — the settings store never needs to know
/// a processor's option types. JSON strings also cross the future native-plugin boundary unchanged.
/// </summary>
public interface IConfigurableImageProcessor : IImageProcessor
{
    /// <summary>
    /// Stable identifier under which the settings are saved (e.g. <c>flyerflipper.grayscale</c>).
    /// Must not change between releases, or saved settings will no longer be found.
    /// </summary>
    string SettingsId { get; }

    /// <summary>Raised on the UI thread when a setting that affects <see cref="IImageProcessor.Process"/> changes.</summary>
    event EventHandler? SettingsChanged;

    /// <summary>The current settings as a JSON object.</summary>
    string GetSettingsJson();

    /// <summary>
    /// Replaces the current settings with those in <paramref name="json"/>. Returns false, leaving the settings
    /// unchanged, when the JSON is malformed or its values are invalid. Called on the UI thread.
    /// </summary>
    bool TryApplySettingsJson(string json);
}
