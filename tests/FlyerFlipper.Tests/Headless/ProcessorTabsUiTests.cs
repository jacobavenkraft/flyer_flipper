using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace FlyerFlipper.Tests.Headless;

public class ProcessorTabsUiTests
{
    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static NumericUpDown ShowResizeTab(AppHarness app, Window window, string inputName)
    {
        app.ProcessorTabs.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        return window.GetVisualDescendants().OfType<NumericUpDown>().Single(n => n.Name == inputName);
    }

    [AvaloniaFact]
    public void TabsAppearInProcessorOrder_BelowTheFolderInput()
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();

        var tabControl = window.GetVisualDescendants().OfType<TabControl>().Single();

        Assert.Equal(["Grayscale", "Resize"], app.ProcessorTabs.Tabs.Select(t => t.Header));
        Assert.Equal(2, tabControl.ItemCount);
        var folderBox = window.GetVisualDescendants().OfType<TextBox>().First(t => t.FindAncestorOfType<NumericUpDown>() is null);
        Assert.True(folderBox.TranslatePoint(default, window)!.Value.Y < tabControl.TranslatePoint(default, window)!.Value.Y);
    }

    [AvaloniaFact]
    public void GrayscaleCheckbox_AppliesImmediately()
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();
        var checkBox = window.GetVisualDescendants().OfType<CheckBox>().Single(c => Equals(c.Content, "Convert to grayscale"));

        checkBox.IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.True(app.GrayscaleSettings.Current.Enabled);
    }

    [AvaloniaFact]
    public void TypingInMaxWidth_DoesNotApplyUntilEnter()
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();
        var input = ShowResizeTab(app, window, "MaxWidthInput");
        var textBox = input.GetVisualDescendants().OfType<TextBox>().Single();
        textBox.Focus();
        textBox.SelectAll();
        Dispatcher.UIThread.RunJobs();

        window.KeyTextInput("500");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(800, app.ResizeSettings.Current.MaxWidth);

        Press(window, PhysicalKey.Enter);
        Assert.Equal(500, app.ResizeSettings.Current.MaxWidth);
    }

    [AvaloniaFact]
    public void TypingInMaxHeight_AppliesWhenFocusLeaves()
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();
        var input = ShowResizeTab(app, window, "MaxHeightInput");
        var textBox = input.GetVisualDescendants().OfType<TextBox>().Single();
        textBox.Focus();
        textBox.SelectAll();
        Dispatcher.UIThread.RunJobs();

        window.KeyTextInput("640");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(800, app.ResizeSettings.Current.MaxHeight);

        window.GetVisualDescendants().OfType<CheckBox>().Single(c => Equals(c.Content, "Shrink to fit")).Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(640, app.ResizeSettings.Current.MaxHeight);
    }

    [AvaloniaFact]
    public void SpinnerArrow_AppliesImmediately()
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();
        var input = ShowResizeTab(app, window, "MaxWidthInput");
        input.GetVisualDescendants().OfType<TextBox>().Single().Focus();
        Dispatcher.UIThread.RunJobs();

        Press(window, PhysicalKey.ArrowUp);

        Assert.Equal(810, app.ResizeSettings.Current.MaxWidth);
    }
}
