using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace FlyerFlipper.UI.Views;

public partial class ThumbnailGridView : UserControl
{
    // Matches Avalonia's own per-notch step for non-logical scrolling.
    private const double WheelStep = 50;

    public ThumbnailGridView()
    {
        InitializeComponent();
        Scroller.AddHandler(PointerWheelChangedEvent, OnScrollerPointerWheelChanged, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Avalonia only scrolls horizontally on Shift+wheel. When the grid is horizontal-only, map a
    /// plain vertical wheel onto horizontal scrolling so mouse users can move through it.
    /// </summary>
    private void OnScrollerPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (Scroller.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled
            || e.Delta.X != 0
            || e.Delta.Y == 0)
        {
            return;
        }

        var maxX = Math.Max(0, Scroller.Extent.Width - Scroller.Viewport.Width);
        var x = Math.Clamp(Scroller.Offset.X - (e.Delta.Y * WheelStep), 0, maxX);
        Scroller.Offset = new Vector(x, Scroller.Offset.Y);
        e.Handled = true;
    }
}
