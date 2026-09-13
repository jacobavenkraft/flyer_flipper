namespace FlyerFlipper.Core.Settings;

/// <summary>Reports the main window's current placement (implemented by the UI layer).</summary>
public interface IWindowPlacementSource
{
    /// <summary>The latest placement, or null before a window is attached.</summary>
    WindowPlacement? Current { get; }

    /// <summary>Raised on the UI thread when the window moves, resizes, or is maximized/restored.</summary>
    event EventHandler? Changed;
}
