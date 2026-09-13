using Avalonia;
using Avalonia.Controls;
using FlyerFlipper.Core.Settings;

namespace FlyerFlipper.UI.Settings;

/// <summary>
/// Restores the main window's saved size, position, and maximized state, then tracks changes so they can be saved.
/// Remembers the <em>normal</em> (restore-down) bounds even while the window is maximized or minimized.
/// </summary>
public sealed class WindowPlacementTracker : IWindowPlacementSource
{
    private Window? _window;
    private PixelPoint _normalPosition;
    private Size _normalSize;
    private bool _maximized;

    public WindowPlacement? Current { get; private set; }

    public event EventHandler? Changed;

    /// <summary>
    /// Applies <paramref name="saved"/> (if any) to <paramref name="window"/> — call before the window is shown —
    /// and starts tracking it.
    /// </summary>
    public void Attach(Window window, WindowPlacement? saved)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (_window is not null)
        {
            throw new InvalidOperationException("A window is already attached.");
        }

        if (saved is not null)
        {
            Apply(window, saved);
        }

        _window = window;
        _normalPosition = window.Position;
        _normalSize = new Size(window.Width, window.Height);
        _maximized = window.WindowState == WindowState.Maximized;

        window.PositionChanged += (_, _) => Update();
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == TopLevel.ClientSizeProperty || e.Property == Window.WindowStateProperty)
            {
                Update();
            }
        };

        Update();
    }

    private static void Apply(Window window, WindowPlacement saved)
    {
        var width = Math.Max(saved.Width, Math.Max(window.MinWidth, WindowPlacementRules.MinWidth));
        var height = Math.Max(saved.Height, Math.Max(window.MinHeight, WindowPlacementRules.MinHeight));
        var placement = saved with { Width = width, Height = height };

        var screens = window.Screens?.All
            .Select(static s => new ScreenArea(s.WorkingArea.X, s.WorkingArea.Y, s.WorkingArea.Width, s.WorkingArea.Height, s.Scaling))
            .ToList() ?? [];

        if (screens.Count > 0 && WindowPlacementRules.IsReachable(placement, screens))
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Position = new PixelPoint(placement.X, placement.Y);
        }
        else
        {
            // The saved spot is off every current screen (e.g. a monitor was unplugged): keep the size, center it.
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        window.Width = placement.Width;
        window.Height = placement.Height;
        if (placement.IsMaximized)
        {
            window.WindowState = WindowState.Maximized;
        }
    }

    private void Update()
    {
        if (_window is null)
        {
            return;
        }

        switch (_window.WindowState)
        {
            case WindowState.Normal:
                _normalPosition = _window.Position;
                _normalSize = _window.ClientSize;
                _maximized = false;
                break;
            case WindowState.Maximized:
            case WindowState.FullScreen:
                _maximized = true;
                break;
            // Minimized: keep the last normal bounds and maximized flag — never restore minimized.
        }

        var placement = new WindowPlacement(_normalPosition.X, _normalPosition.Y, _normalSize.Width, _normalSize.Height, _maximized);
        if (placement != Current)
        {
            Current = placement;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
