using Avalonia;
using Avalonia.Controls;
using Shapes = Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlyerFlipper.UI.Views;

namespace FlyerFlipper.Tests.Headless;

/// <summary>
/// The app draws its own title bar (decision 20), so everything the OS used to provide is now the app's to get
/// right — and to keep working.
/// </summary>
public class CaptionBarTests
{
    private static T Find<T>(Visual root, string name)
        where T : Control
        => root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static MainWindow ShowWindow(AppHarness app)
    {
        var window = new MainWindow(app.MainViewModel);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Window_HasNoSystemDecorations()
    {
        using var app = new AppHarness();
        var window = ShowWindow(app);

        Assert.Equal(SystemDecorations.None, window.SystemDecorations);
        window.Close();
    }

    [AvaloniaFact]
    public void CaptionBar_ShowsTheWindowTitle_AndTracksChanges()
    {
        using var app = new AppHarness();
        var window = ShowWindow(app);
        var title = Find<TextBlock>(window, "TitleText");

        Assert.Equal("Flyer Flipper", title.Text);

        window.Title = "Flyer Flipper — renamed";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Flyer Flipper — renamed", title.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void MaximizeButton_TogglesWindowState_AndSwapsItsGlyph()
    {
        using var app = new AppHarness();
        var window = ShowWindow(app);
        var maximize = Find<Button>(window, "MaximizeButton");
        var maximizeGlyph = Find<Shapes.Path>(window, "MaximizeGlyph");
        var restoreGlyph = Find<Shapes.Path>(window, "RestoreGlyph");

        Assert.True(maximizeGlyph.IsVisible);
        Assert.False(restoreGlyph.IsVisible);

        Click(maximize);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(WindowState.Maximized, window.WindowState);
        Assert.False(maximizeGlyph.IsVisible);
        Assert.True(restoreGlyph.IsVisible);

        Click(maximize);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(WindowState.Normal, window.WindowState);
        Assert.True(maximizeGlyph.IsVisible);
        Assert.False(restoreGlyph.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void MinimizeButton_MinimizesTheWindow()
    {
        using var app = new AppHarness();
        var window = ShowWindow(app);

        Click(Find<Button>(window, "MinimizeButton"));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(WindowState.Minimized, window.WindowState);
        window.WindowState = WindowState.Normal;
        window.Close();
    }

    [AvaloniaFact]
    public void CloseButton_ClosesTheWindow()
    {
        using var app = new AppHarness();
        var window = ShowWindow(app);
        var closed = false;
        window.Closed += (_, _) => closed = true;

        Click(Find<Button>(window, "CloseButton"));
        Dispatcher.UIThread.RunJobs();

        Assert.True(closed);
    }

    [AvaloniaFact]
    public void ResizeGrips_AreHiddenWhileMaximized()
    {
        // Grips along a maximized window's edges would resize it without restoring it down first.
        using var app = new AppHarness();
        var window = ShowWindow(app);
        var grips = Find<Grid>(window, "ResizeGrips");

        Assert.True(grips.IsVisible);

        window.WindowState = WindowState.Maximized;
        Dispatcher.UIThread.RunJobs();
        Assert.False(grips.IsVisible);

        window.WindowState = WindowState.Normal;
        Dispatcher.UIThread.RunJobs();
        Assert.True(grips.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void ResizeGrips_CoverEveryEdgeAndCorner()
    {
        using var app = new AppHarness();
        var window = ShowWindow(app);
        var grips = Find<Grid>(window, "ResizeGrips");

        var edges = grips.GetVisualDescendants()
            .OfType<Border>()
            .Select(b => b.Tag)
            .OfType<WindowEdge>()
            .ToHashSet();

        Assert.Equal(
            [
                WindowEdge.North, WindowEdge.NorthEast, WindowEdge.East, WindowEdge.SouthEast,
                WindowEdge.South, WindowEdge.SouthWest, WindowEdge.West, WindowEdge.NorthWest,
            ],
            edges);
        window.Close();
    }
}
