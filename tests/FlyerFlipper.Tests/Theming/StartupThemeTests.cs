using FlyerFlipper.UI.Theming;

namespace FlyerFlipper.Tests.Theming;

public class StartupThemeTests
{
    private const string PortalService = "dbus-1/services/org.freedesktop.portal.Desktop.service";

    [Theory]
    [InlineData(true, StartupThemeDecision.FollowOperatingSystem)]
    [InlineData(false, StartupThemeDecision.ForceDark)]
    public void Decide_FallsBackToDarkOnlyWhenTheOsHasNoPreference(bool reports, StartupThemeDecision expected)
    {
        Assert.Equal(expected, StartupTheme.Decide(reports));
    }

    [Fact]
    public void DesktopPortalAvailable_FindsThePortalServiceFile()
    {
        var present = Path.Combine("/usr/share", PortalService);

        Assert.True(StartupTheme.DesktopPortalAvailable(
            ["/usr/local/share", "/usr/share"],
            path => path == present));
    }

    [Fact]
    public void DesktopPortalAvailable_NoPortalInstalled_IsFalse()
    {
        // WSLg: a session bus exists, but nothing provides the portal, so no colour scheme can be read.
        Assert.False(StartupTheme.DesktopPortalAvailable(
            ["/usr/local/share", "/usr/share", "/var/lib/snapd/desktop"],
            static _ => false));
    }

    [Fact]
    public void DesktopPortalAvailable_NoDirectories_IsFalse()
    {
        Assert.False(StartupTheme.DesktopPortalAvailable([], static _ => true));
    }

    [Fact]
    public void DesktopPortalAvailable_IgnoresBlankDirectoryEntries()
    {
        var probed = new List<string>();

        Assert.False(StartupTheme.DesktopPortalAvailable(
            ["", "   "],
            path =>
            {
                probed.Add(path);
                return true;
            }));

        Assert.Empty(probed);
    }

    [Fact]
    public void XdgDataDirectories_DefaultsToTheSpecifiedSystemDirectories()
    {
        var previousDirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
        var previousHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_DATA_DIRS", null);
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", "/home/someone/.local/share");

            Assert.Equal(
                ["/home/someone/.local/share", "/usr/local/share", "/usr/share"],
                StartupTheme.XdgDataDirectories());
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_DATA_DIRS", previousDirs);
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", previousHome);
        }
    }

    [Fact]
    public void XdgDataDirectories_SplitsAndTrimsTheEnvironmentValue()
    {
        var previousDirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
        var previousHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", "/data/home");
            Environment.SetEnvironmentVariable("XDG_DATA_DIRS", "/usr/local/share: /usr/share ::/var/lib/snapd/desktop");

            Assert.Equal(
                ["/data/home", "/usr/local/share", "/usr/share", "/var/lib/snapd/desktop"],
                StartupTheme.XdgDataDirectories());
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_DATA_DIRS", previousDirs);
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", previousHome);
        }
    }

    [Fact]
    public void OperatingSystemReportsPreference_IsTrueOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.True(StartupTheme.OperatingSystemReportsPreference());
    }
}
