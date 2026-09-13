using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FlyerFlipper.UI.ViewModels;

namespace FlyerFlipper.UI.Views;

public partial class ThumbnailGridView : UserControl
{
    // Matches Avalonia's own per-notch step for non-logical scrolling.
    private const double WheelStep = 50;

    private ThumbnailGridViewModel? _viewModel;

    public ThumbnailGridView()
    {
        InitializeComponent();
        Scroller.AddHandler(PointerWheelChangedEvent, OnScrollerPointerWheelChanged, RoutingStrategies.Tunnel);
        Thumbnails.DoubleTapped += OnThumbnailsDoubleTapped;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.ScrollIntoViewRequested -= OnScrollIntoViewRequested;
        }

        _viewModel = DataContext as ThumbnailGridViewModel;
        if (_viewModel is not null)
        {
            _viewModel.ScrollIntoViewRequested += OnScrollIntoViewRequested;
        }

        base.OnDataContextChanged(e);
    }

    private void OnThumbnailsDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((e.Source as StyledElement)?.DataContext is ThumbnailItemViewModel item)
        {
            _viewModel?.OpenImageCommand.Execute(item);
            e.Handled = true;
        }
    }

    private void OnScrollIntoViewRequested(object? sender, int index)
    {
        // The grid is being made visible again; wait until it has been laid out.
        Dispatcher.UIThread.Post(
            () => Thumbnails.ContainerFromIndex(index)?.BringIntoView(),
            DispatcherPriority.Background);
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
