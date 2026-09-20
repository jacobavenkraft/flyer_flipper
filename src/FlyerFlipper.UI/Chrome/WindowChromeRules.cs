using Avalonia.Controls;

namespace FlyerFlipper.UI.Chrome;

/// <summary>
/// Decisions behind the app-drawn window chrome (decision 20), kept free of any control so they can be tested.
/// </summary>
public static class WindowChromeRules
{
    /// <summary>
    /// The state the maximize/restore button — or a double-click on the title bar — should move the window to.
    /// </summary>
    /// <remarks>
    /// Full-screen counts as "already enlarged", so it restores down rather than staying put. Minimized restores
    /// to maximized, matching what the OS title bar does when a minimized window is re-activated and toggled.
    /// </remarks>
    public static WindowState ToggleMaximized(WindowState current)
        => current is WindowState.Maximized or WindowState.FullScreen
            ? WindowState.Normal
            : WindowState.Maximized;

    /// <summary>
    /// Whether the app should draw resize grips. A maximized or full-screen window is sized by the OS, and grips
    /// along its edges would let the user resize it without first restoring it down.
    /// </summary>
    public static bool ShowsResizeGrips(WindowState current, bool canResize)
        => canResize && current is not (WindowState.Maximized or WindowState.FullScreen);
}
