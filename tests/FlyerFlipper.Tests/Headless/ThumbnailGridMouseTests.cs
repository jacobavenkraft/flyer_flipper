using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlyerFlipper.Core.Viewport;
using FlyerFlipper.UI.Views;

namespace FlyerFlipper.Tests.Headless;

public class ThumbnailGridMouseTests
{
    private static Point CenterOfThumbnail(Window window, int index)
    {
        var grid = window.GetVisualDescendants().OfType<ThumbnailGridView>().Single();
        var items = grid.GetVisualDescendants().OfType<ItemsControl>().Single();
        var container = items.ContainerFromIndex(index)!;
        return container.TranslatePoint(new Point(container.Bounds.Width / 2, container.Bounds.Height / 2), window)!.Value;
    }

    private static void Click(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task SingleClick_SelectsThumbnail_WithoutLeavingGrid()
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();
        await app.LoadFolderAsync(4);

        Click(window, CenterOfThumbnail(window, 2));

        Assert.Equal(ViewportMode.Grid, app.Viewport.Mode);
        Assert.Equal(2, app.Viewport.CurrentIndex);
        Assert.Equal([false, false, true, false], app.Grid.Items.Select(i => i.IsCurrent));
    }

    [AvaloniaFact]
    public async Task SelectThenToggleViewportMode_OpensSelectedImage()
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();
        await app.LoadFolderAsync(4);

        Click(window, CenterOfThumbnail(window, 3));
        window.KeyPressQwerty(PhysicalKey.V, RawInputModifiers.Control | RawInputModifiers.Shift);
        window.KeyReleaseQwerty(PhysicalKey.V, RawInputModifiers.Control | RawInputModifiers.Shift);
        await AppHarness.WaitUntilAsync(() => app.Single.Image is not null);

        Assert.Equal(ViewportMode.Single, app.Viewport.Mode);
        Assert.Equal("image03.png", app.Single.FileName);
    }

    [AvaloniaFact]
    public async Task DoubleClick_OpensThumbnailInSingleView()
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();
        await app.LoadFolderAsync(4);
        var point = CenterOfThumbnail(window, 1);

        Click(window, point);
        Click(window, point);

        Assert.Equal(ViewportMode.Single, app.Viewport.Mode);
        Assert.Equal(1, app.Viewport.CurrentIndex);
    }
}
