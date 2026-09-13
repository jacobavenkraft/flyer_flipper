namespace FlyerFlipper.Core.Settings;

/// <summary>
/// The main window's restorable placement.
/// </summary>
/// <param name="X">Left edge of the normal (non-maximized) window, in screen pixels.</param>
/// <param name="Y">Top edge of the normal window, in screen pixels.</param>
/// <param name="Width">Client width of the normal window, in device-independent pixels.</param>
/// <param name="Height">Client height of the normal window, in device-independent pixels.</param>
/// <param name="IsMaximized">Whether the window was maximized (X/Y/Width/Height are its restore-down bounds).</param>
public sealed record WindowPlacement(int X, int Y, double Width, double Height, bool IsMaximized);

/// <summary>A screen's working area in screen pixels, with its DPI scaling factor.</summary>
public readonly record struct ScreenArea(int X, int Y, int Width, int Height, double Scaling = 1.0);

public static class WindowPlacementRules
{
    /// <summary>Minimum size a restored window may have, in device-independent pixels.</summary>
    public const double MinWidth = 600;

    public const double MinHeight = 400;

    /// <summary>How much of the title bar must land on a screen for the window to count as reachable.</summary>
    private const int GrabWidthPixels = 120;

    private const int GrabHeightPixels = 24;

    /// <summary>
    /// True when a strip of the window's title bar would lie on one of <paramref name="screens"/>, so the user can
    /// see and drag it. Guards against restoring onto a monitor that has since been disconnected.
    /// </summary>
    public static bool IsReachable(WindowPlacement placement, IEnumerable<ScreenArea> screens)
    {
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(screens);

        if (!double.IsFinite(placement.Width) || !double.IsFinite(placement.Height)
            || placement.Width <= 0 || placement.Height <= 0)
        {
            return false;
        }

        foreach (var screen in screens)
        {
            var widthPixels = (int)Math.Round(placement.Width * screen.Scaling);
            var grabWidth = Math.Min(GrabWidthPixels, widthPixels);

            var overlapX = Math.Min(placement.X + widthPixels, screen.X + screen.Width) - Math.Max(placement.X, screen.X);
            var overlapY = Math.Min(placement.Y + GrabHeightPixels, screen.Y + screen.Height) - Math.Max(placement.Y, screen.Y);
            if (overlapX >= grabWidth && overlapY >= GrabHeightPixels)
            {
                return true;
            }
        }

        return false;
    }
}
