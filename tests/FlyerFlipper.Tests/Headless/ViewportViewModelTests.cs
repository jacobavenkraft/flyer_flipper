using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlyerFlipper.Core.Viewport;

namespace FlyerFlipper.Tests.Headless;

/// <summary>
/// Single-image and grid view-model behaviour. Headless because both create Avalonia bitmaps.
/// </summary>
public class ViewportViewModelTests
{
    [AvaloniaFact]
    public async Task OpenImage_FromGrid_ShowsThatImageInSingleView()
    {
        using var app = new AppHarness();
        await app.LoadFolderAsync(5);
        var item = app.Grid.Items[3];

        app.Grid.OpenImageCommand.Execute(item);
        await AppHarness.WaitUntilAsync(() => app.Single.Image is not null);

        Assert.Equal(ViewportMode.Single, app.Viewport.Mode);
        Assert.Equal(3, app.Viewport.CurrentIndex);
        Assert.Equal("image03.png", app.Single.FileName);
        Assert.Equal("4 / 5", app.Single.PositionText);
        Assert.False(app.Single.IsLoading);
    }

    [AvaloniaFact]
    public async Task SingleView_PreDecodesNeighbours_AndReusesThemWhenNavigating()
    {
        using var app = new AppHarness();
        await app.LoadFolderAsync(5);
        // Let the grid's thumbnail loads finish so load counts below are single-view only.
        await AppHarness.WaitUntilAsync(() => app.Grid.Items.All(i => !i.IsLoading));
        var baseline = app.Grid.Items.ToDictionary(i => i.Reference, i => app.Loader.LoadCount(i.Reference));
        int SingleLoads(int index) => app.Loader.LoadCount(app.Catalog.Images[index]) - baseline[app.Catalog.Images[index]];

        app.Viewport.ShowSingle(2);
        await AppHarness.WaitUntilAsync(() => SingleLoads(1) == 1 && SingleLoads(3) == 1);

        app.Viewport.MoveNext();
        await AppHarness.WaitUntilAsync(() => app.Single.Image is not null && SingleLoads(4) == 1);

        Assert.Equal(1, SingleLoads(3)); // shown from the pre-decoded neighbour, not decoded again
        Assert.Equal(0, SingleLoads(0));
        Assert.Equal("image03.png", app.Single.FileName);
    }

    [AvaloniaFact]
    public async Task SingleView_DecodeFailure_ShowsError_AndNavigationStillWorks()
    {
        using var app = new AppHarness();
        app.Loader.Failing[Path.Combine(AppHarness.Folder, "image01.png")] = true;
        await app.LoadFolderAsync(3);

        app.Viewport.ShowSingle(1);
        await AppHarness.WaitUntilAsync(() => app.Single.HasError);

        Assert.Null(app.Single.Image);
        Assert.Contains("image01.png", app.Single.ErrorMessage);

        app.Viewport.MoveNext();
        await AppHarness.WaitUntilAsync(() => app.Single.Image is not null);
        Assert.False(app.Single.HasError);
    }

    [AvaloniaFact]
    public async Task ReturningToGrid_ReleasesSingleViewImage()
    {
        using var app = new AppHarness();
        await app.LoadFolderAsync(3);
        app.Viewport.ShowSingle(0);
        await AppHarness.WaitUntilAsync(() => app.Single.Image is not null);

        app.Viewport.ShowGrid();

        Assert.Null(app.Single.Image);
        Assert.False(app.Single.BackToGridCommand.CanExecute(null));
        Assert.False(app.Single.NextCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task NavigationCommands_ReflectPosition()
    {
        using var app = new AppHarness();
        await app.LoadFolderAsync(2);

        app.Viewport.ShowSingle(0);
        Dispatcher.UIThread.RunJobs();
        Assert.False(app.Single.CanGoPrevious);
        Assert.True(app.Single.CanGoNext);

        app.Single.NextCommand.Execute(null);
        Assert.True(app.Single.CanGoPrevious);
        Assert.False(app.Single.CanGoNext);
        Assert.False(app.Single.NextCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task LoadingNewFolder_WhileInSingleView_ShowsItsFirstImage()
    {
        using var app = new AppHarness();
        await app.LoadFolderAsync(5);
        app.Viewport.ShowSingle(4);
        await AppHarness.WaitUntilAsync(() => app.Single.Image is not null);

        await app.LoadFolderAsync(2);
        await AppHarness.WaitUntilAsync(() => app.Single.Image is not null && app.Single.PositionText == "1 / 2");

        Assert.Equal(ViewportMode.Single, app.Viewport.Mode);
        Assert.Equal("image00.png", app.Single.FileName);
    }

    [AvaloniaFact]
    public async Task Grid_TracksCurrentItem_AcrossNavigationAndFolderChanges()
    {
        using var app = new AppHarness();
        await app.LoadFolderAsync(4);
        Assert.True(app.Grid.Items[0].IsCurrent);

        app.Viewport.ShowSingle(2);
        app.Viewport.MoveNext();

        Assert.Equal([false, false, false, true], app.Grid.Items.Select(i => i.IsCurrent));

        await app.LoadFolderAsync(3);
        Assert.Equal([true, false, false], app.Grid.Items.Select(i => i.IsCurrent));
    }

    [AvaloniaFact]
    public async Task ReturningToGrid_RequestsScrollToCurrentImage()
    {
        using var app = new AppHarness();
        await app.LoadFolderAsync(4);
        int? requested = null;
        app.Grid.ScrollIntoViewRequested += (_, index) => requested = index;

        app.Viewport.ShowSingle(3);
        app.Viewport.ShowGrid();

        Assert.Equal(3, requested);
    }
}
