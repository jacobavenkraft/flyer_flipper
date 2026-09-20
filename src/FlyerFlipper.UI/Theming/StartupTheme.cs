namespace FlyerFlipper.UI.Theming;

/// <summary>What the app should do about its theme at startup.</summary>
public enum StartupThemeDecision
{
    /// <summary>The OS has a light/dark preference worth following.</summary>
    FollowOperatingSystem,

    /// <summary>The OS exposes no preference, so pick dark rather than letting Avalonia fall back to light.</summary>
    ForceDark,
}

/// <summary>
/// Decides the startup theme variant.
/// </summary>
/// <remarks>
/// Avalonia's <c>RequestedThemeVariant="Default"</c> means "ask the OS". Windows and macOS always answer. On Linux
/// the answer comes from the XDG desktop portal (<c>org.freedesktop.appearance</c> / <c>color-scheme</c>), and when
/// no portal is running — as under WSLg — Avalonia silently falls back to <em>light</em>. That fallback is what made
/// the app light on WSL and dark on Windows. Where nothing can be asked, prefer dark.
/// </remarks>
public static class StartupTheme
{
    private const string PortalServiceFile = "dbus-1/services/org.freedesktop.portal.Desktop.service";

    public static StartupThemeDecision Decide(bool operatingSystemReportsPreference)
        => operatingSystemReportsPreference ? StartupThemeDecision.FollowOperatingSystem : StartupThemeDecision.ForceDark;

    /// <summary>True when the running OS can be asked for a light/dark preference.</summary>
    public static bool OperatingSystemReportsPreference()
        => OperatingSystem.IsWindows()
            || OperatingSystem.IsMacOS()
            || DesktopPortalAvailable(XdgDataDirectories(), File.Exists);

    /// <summary>
    /// True when an XDG desktop portal is installed, i.e. something can answer a colour-scheme query.
    /// </summary>
    /// <remarks>
    /// Checked by looking for the portal's D-Bus service file rather than by calling D-Bus, so startup stays
    /// synchronous and AOT-friendly. A desktop with portals installed but not running still counts as "can be
    /// asked" — it is D-Bus activated on demand.
    /// </remarks>
    internal static bool DesktopPortalAvailable(IEnumerable<string> dataDirectories, Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(dataDirectories);
        ArgumentNullException.ThrowIfNull(fileExists);

        return dataDirectories.Any(directory =>
            !string.IsNullOrWhiteSpace(directory) && fileExists(Path.Combine(directory, PortalServiceFile)));
    }

    /// <summary>The directories a freedesktop system searches for D-Bus service files, per the XDG base-dir spec.</summary>
    internal static IEnumerable<string> XdgDataDirectories()
    {
        var home = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(home))
        {
            yield return home;
        }
        else
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile))
            {
                yield return Path.Combine(userProfile, ".local", "share");
            }
        }

        var dirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
        var candidates = string.IsNullOrWhiteSpace(dirs)
            ? ["/usr/local/share", "/usr/share"]
            : dirs.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var directory in candidates)
        {
            yield return directory;
        }
    }
}
