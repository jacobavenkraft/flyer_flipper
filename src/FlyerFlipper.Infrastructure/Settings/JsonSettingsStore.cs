using System.Diagnostics;
using System.Text.Json;
using FlyerFlipper.Core.Settings;

namespace FlyerFlipper.Infrastructure.Settings;

/// <summary>
/// Stores <see cref="AppSettings"/> as indented JSON. Default location: <c>%APPDATA%\FlyerFlipper\settings.json</c>
/// on Windows, <c>$XDG_CONFIG_HOME/FlyerFlipper/settings.json</c> (usually <c>~/.config</c>) on Linux.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    public const string BadFileSuffix = ".bad";

    public JsonSettingsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = Path.GetFullPath(filePath);
    }

    public string FilePath { get; }

    /// <summary>The OS-appropriate per-user settings file path.</summary>
    public static string DefaultFilePath
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlyerFlipper", "settings.json");

    public AppSettings Load()
    {
        if (!File.Exists(FilePath))
        {
            return new AppSettings();
        }

        try
        {
            using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return JsonSerializer.Deserialize(stream, AppSettingsJsonContext.Default.AppSettings)
                ?? throw new JsonException("The settings file contains null.");
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            Trace.WriteLine($"Settings file '{FilePath}' is unreadable; starting with defaults. {ex}");
            SetAsideBadFile();
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

        // Write beside the target, then swap in, so a crash mid-write never leaves a truncated settings file.
        var temporaryPath = FilePath + ".tmp";
        using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, settings, AppSettingsJsonContext.Default.AppSettings);
        }

        File.Move(temporaryPath, FilePath, overwrite: true);
    }

    private void SetAsideBadFile()
    {
        try
        {
            File.Move(FilePath, FilePath + BadFileSuffix, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Couldn't rename (e.g. still locked). Defaults are used either way; the next save replaces the file.
            Trace.WriteLine($"Could not set aside bad settings file '{FilePath}': {ex.Message}");
        }
    }
}
