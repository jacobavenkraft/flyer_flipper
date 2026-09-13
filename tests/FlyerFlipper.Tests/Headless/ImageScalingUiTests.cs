using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlyerFlipper.Core.Viewport;

namespace FlyerFlipper.Tests.Headless;

public class ImageScalingUiTests
{
    public static TheoryData<ViewportScaleMode, Stretch, StretchDirection, ScrollBarVisibility> Modes => new()
    {
        { ViewportScaleMode.FitToWindow, Stretch.Uniform, StretchDirection.Both, ScrollBarVisibility.Disabled },
        { ViewportScaleMode.FitWithoutEnlarging, Stretch.Uniform, StretchDirection.DownOnly, ScrollBarVisibility.Disabled },
        { ViewportScaleMode.StretchToFill, Stretch.Fill, StretchDirection.Both, ScrollBarVisibility.Disabled },
        { ViewportScaleMode.ActualSize, Stretch.None, StretchDirection.Both, ScrollBarVisibility.Auto },
    };

    [AvaloniaTheory]
    [MemberData(nameof(Modes))]
    public async Task ScaleMode_DrivesSingleViewImageAndScrolling(
        ViewportScaleMode mode, Stretch stretch, StretchDirection direction, ScrollBarVisibility scrollBars)
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();
        await app.LoadFolderAsync(2);
        app.Viewport.ShowSingle(0);

        app.MainViewModel.SetScaleModeCommand.Execute(mode);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(mode, app.Viewport.ScaleMode);
        var image = window.GetVisualDescendants().OfType<Image>().Single(i => i.Name == "DisplayedImage");
        var scroller = window.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "ImageScroller");
        Assert.Equal(stretch, image.Stretch);
        Assert.Equal(direction, image.StretchDirection);
        Assert.Equal(scrollBars, scroller.HorizontalScrollBarVisibility);
        Assert.Equal(scrollBars, scroller.VerticalScrollBarVisibility);
    }

    [AvaloniaFact]
    public void ImageScalingMenu_ChecksExactlyTheActiveMode()
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();
        var menu = window.GetLogicalDescendantsOfName("ImageScalingMenu");
        var items = menu.Items.OfType<MenuItem>().ToList();

        Assert.Equal(["_Fit to Window", "Fit _without Enlarging", "_Stretch to Fill", "_Actual Size"], items.Select(i => i.Header as string));
        Assert.Equal([true, false, false, false], items.Select(i => i.IsChecked));

        app.MainViewModel.SetScaleModeCommand.Execute(ViewportScaleMode.ActualSize);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([false, false, false, true], items.Select(i => i.IsChecked));
    }
}

internal static class LogicalTreeTestExtensions
{
    public static MenuItem GetLogicalDescendantsOfName(this Window window, string name)
        => Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(window).OfType<MenuItem>().Single(m => m.Name == name);
}
