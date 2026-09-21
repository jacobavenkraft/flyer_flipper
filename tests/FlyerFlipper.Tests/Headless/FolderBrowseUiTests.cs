using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace FlyerFlipper.Tests.Headless;

public class FolderBrowseUiTests
{
    [AvaloniaFact]
    public void BrowseButton_SitsBesideTheFolderBox_AndInvokesThePicker()
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();
        app.SetImages(2);
        app.FolderPicker.NextPickedFolder = AppHarness.Folder;

        // A real click: the button is bound through Command, which RaiseEvent(ClickEvent) does not invoke.
        var browse = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "BrowseButton");
        var centre = browse.TranslatePoint(new Point(browse.Bounds.Width / 2, browse.Bounds.Height / 2), window)!.Value;
        window.MouseDown(centre, MouseButton.Left);
        window.MouseUp(centre, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, app.FolderPicker.Calls);
        Assert.Equal(AppHarness.Folder, app.ImageSource.FolderPath);
    }

    [AvaloniaFact]
    public void BrowseButton_IsNextToTheFolderInput_AndBeforeLoad()
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();

        var folderBox = window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "FolderPathInput");
        var browse = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "BrowseButton");
        var load = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "LoadButton");

        var boxX = folderBox.TranslatePoint(default, window)!.Value.X;
        var browseX = browse.TranslatePoint(default, window)!.Value.X;
        var loadX = load.TranslatePoint(default, window)!.Value.X;

        Assert.True(boxX < browseX, "browse should follow the folder box");
        Assert.True(browseX < loadX, "browse should come before load");
    }
}
