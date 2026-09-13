namespace FlyerFlipper.Core.Settings;

public interface ISettingsStore
{
    /// <summary>
    /// Reads the saved settings. Returns defaults when nothing has been saved yet, or when the saved file is
    /// unreadable or corrupt (the bad file is set aside so it isn't lost).
    /// </summary>
    AppSettings Load();

    /// <summary>Writes <paramref name="settings"/>, replacing any previous save atomically.</summary>
    /// <exception cref="IOException">The settings could not be written.</exception>
    void Save(AppSettings settings);
}
