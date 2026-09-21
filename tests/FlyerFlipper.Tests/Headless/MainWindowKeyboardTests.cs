using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlyerFlipper.Core.Layout;
using FlyerFlipper.Core.Viewport;

namespace FlyerFlipper.Tests.Headless;

public class MainWindowKeyboardTests
{
    private static void Press(Avalonia.Controls.Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void CtrlShiftO_TogglesOrientation()
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();

        Press(window, PhysicalKey.O, RawInputModifiers.Control | RawInputModifiers.Shift);

        Assert.Equal(LayoutOrientation.Horizontal, app.Layout.Orientation);
    }

    [AvaloniaFact]
    public async Task CtrlShiftV_TogglesViewportMode_WhenImagesLoaded()
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();
        await app.LoadFolderAsync(3);

        Press(window, PhysicalKey.V, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Equal(ViewportMode.Single, app.Viewport.Mode);
        Assert.True(app.MainViewModel.IsSingleMode);

        Press(window, PhysicalKey.V, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Equal(ViewportMode.Grid, app.Viewport.Mode);
    }

    [AvaloniaFact]
    public void CtrlShiftV_DoesNothing_WithoutImages()
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();

        Press(window, PhysicalKey.V, RawInputModifiers.Control | RawInputModifiers.Shift);

        Assert.Equal(ViewportMode.Grid, app.Viewport.Mode);
        Assert.False(app.MainViewModel.ToggleViewportModeCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task ArrowsNavigate_AndEscapeReturnsToGrid_InSingleMode()
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();
        await app.LoadFolderAsync(3);
        app.Viewport.ShowSingle(0);
        Dispatcher.UIThread.RunJobs();

        Press(window, PhysicalKey.ArrowRight);
        Assert.Equal(1, app.Viewport.CurrentIndex);

        Press(window, PhysicalKey.ArrowRight);
        Press(window, PhysicalKey.ArrowRight); // already at the last image
        Assert.Equal(2, app.Viewport.CurrentIndex);

        Press(window, PhysicalKey.ArrowLeft);
        Assert.Equal(1, app.Viewport.CurrentIndex);

        Press(window, PhysicalKey.Escape);
        Assert.Equal(ViewportMode.Grid, app.Viewport.Mode);
        Assert.Equal(1, app.Viewport.CurrentIndex);
    }

    [AvaloniaFact]
    public async Task Arrows_DoNotNavigate_InGridMode()
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();
        await app.LoadFolderAsync(3);

        Press(window, PhysicalKey.ArrowRight);

        Assert.Equal(ViewportMode.Grid, app.Viewport.Mode);
        Assert.Equal(0, app.Viewport.CurrentIndex);
    }

    [AvaloniaFact]
    public async Task Arrows_StayWithFolderTextBox_WhenItHasFocus()
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();
        await app.LoadFolderAsync(3);
        app.Viewport.ShowSingle(1);
        var textBox = window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "FolderPathInput");
        textBox.Focus();
        textBox.CaretIndex = 3;
        Dispatcher.UIThread.RunJobs();

        Press(window, PhysicalKey.ArrowRight);

        Assert.Equal(1, app.Viewport.CurrentIndex);
        Assert.Equal(4, textBox.CaretIndex);
    }

    [AvaloniaFact]
    public async Task Arrows_Navigate_WhenLoadButtonHasFocus()
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();
        await app.LoadFolderAsync(3);
        app.Viewport.ShowSingle(0);
        var loadButton = window.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Load"));
        loadButton.Focus();
        Dispatcher.UIThread.RunJobs();

        Press(window, PhysicalKey.ArrowRight);

        Assert.Equal(1, app.Viewport.CurrentIndex);
    }
}
