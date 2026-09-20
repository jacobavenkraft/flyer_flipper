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
    /// Largest read-back discrepancy we will correct for. A bigger gap means the window manager put the window
    /// somewhere of its own choosing rather than applying a fixed origin offset, so correcting would only make
    /// it worse.
    /// </summary>
    public const int MaxOriginCorrectionPixels = 200;

    /// <summary>
    /// How far to nudge a window whose reported position disagrees with the position that was just set.
    /// </summary>
    /// <remarks>
    /// Some platforms report <c>Window.Position</c> in a different origin than the setter uses — WSLg/XWayland
    /// reports it 32px short on both axes. Saving that read-back and re-applying it on the next launch walks the
    /// window across the screen, and once it walks off the top-left <see cref="IsReachable"/> discards it
    /// altogether. Re-applying the target plus this correction lands the window where the saved coordinates
    /// actually mean, and keeps the value stable across launches.
    /// </remarks>
    /// <param name="targetX">X the window should end up reporting.</param>
    /// <param name="targetY">Y the window should end up reporting.</param>
    /// <param name="assignedX">X most recently assigned to the window — <em>not</em> necessarily the target.</param>
    /// <param name="assignedY">Y most recently assigned to the window.</param>
    /// <param name="reportedX">X the window reported once the window manager settled.</param>
    /// <param name="reportedY">Y the window reported once the window manager settled.</param>
    /// <returns>
    /// The position to assign next, or <see langword="null"/> when no correction should be made — either the
    /// platform reported the assignment back exactly (Windows) or the gap is too large to be an origin offset.
    /// </returns>
    /// <remarks>
    /// The offset is measured against what was <em>assigned</em>, not against the target. Measuring against the
    /// target instead makes the correction oscillate: each assignment moves the window, so the next reading is
    /// relative to that assignment, and the correction never converges.
    /// </remarks>
    public static (int X, int Y)? OriginCorrection(
        int targetX, int targetY, int assignedX, int assignedY, int reportedX, int reportedY)
    {
        var offsetX = assignedX - reportedX;
        var offsetY = assignedY - reportedY;

        if (offsetX == 0 && offsetY == 0)
        {
            return null;
        }

        if (Math.Abs(offsetX) > MaxOriginCorrectionPixels || Math.Abs(offsetY) > MaxOriginCorrectionPixels)
        {
            return null;
        }

        return (targetX + offsetX, targetY + offsetY);
    }

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
