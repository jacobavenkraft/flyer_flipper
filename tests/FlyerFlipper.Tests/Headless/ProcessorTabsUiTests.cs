using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlyerFlipper.Core.Processors;

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
        Assert.True(app.ProcessorTabs.SelectTab("Resize"));
        Dispatcher.UIThread.RunJobs();
        return window.GetVisualDescendants().OfType<NumericUpDown>().Single(n => n.Name == inputName);
    }

    [AvaloniaFact]
    public void TabsAppearInProcessorOrder_BelowTheFolderInput()
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();

        var tabControl = window.GetVisualDescendants().OfType<TabControl>().Single();

        // Order comes from ProcessorOrder, not registration order. This is the shipped default;
        // reordering the pipeline is a planned enhancement.
        Assert.Equal(["Channels", "Invert", "Grayscale", "Flip", "Rotate", "Resize"], app.ProcessorTabs.Tabs.Select(t => t.Header));
        Assert.Equal(6, tabControl.ItemCount);
        var folderBox = window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "FolderPathInput");
        Assert.True(folderBox.TranslatePoint(default, window)!.Value.Y < tabControl.TranslatePoint(default, window)!.Value.Y);
    }

    [AvaloniaFact]
    public void GrayscaleCheckbox_AppliesImmediately()
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();
        Assert.True(app.ProcessorTabs.SelectTab("Grayscale"));
        Dispatcher.UIThread.RunJobs();
        var checkBox = window.GetVisualDescendants().OfType<CheckBox>().Single(c => Equals(c.Content, "Convert to grayscale"));

        checkBox.IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.True(app.GrayscaleSettings.Current.Enabled);
    }

    [AvaloniaFact]
    public void InvertCheckbox_AppliesImmediately()
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();
        Assert.True(app.ProcessorTabs.SelectTab("Invert"));
        Dispatcher.UIThread.RunJobs();

        window.GetVisualDescendants().OfType<CheckBox>().Single(c => Equals(c.Content, "Invert colours")).IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.True(app.InvertSettings.Current.Enabled);
    }

    [AvaloniaFact]
    public void ChannelCombos_ApplyImmediately_AndFollowRestoredSettings()
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();
        Assert.True(app.ProcessorTabs.SelectTab("Channels"));
        Dispatcher.UIThread.RunJobs();

        window.GetVisualDescendants().OfType<CheckBox>().Single(c => Equals(c.Content, "Remap colour channels")).IsChecked = true;
        var red = window.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "RedSource");
        red.SelectedItem = ColorChannel.Blue;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new ChannelMapOptions(enabled: true, red: ColorChannel.Blue), app.ChannelMapSettings.Current);

        // Settings restored from disk have to move the selections.
        app.ChannelMapSettings.Update(new ChannelMapOptions(enabled: true, blue: ColorChannel.Green));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ColorChannel.Red, red.SelectedItem);
        Assert.Equal(
            ColorChannel.Green,
            window.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "BlueSource").SelectedItem);
    }

    [AvaloniaFact]
    public void FlipCheckboxes_ApplyImmediately()
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();
        Assert.True(app.ProcessorTabs.SelectTab("Flip"));
        Dispatcher.UIThread.RunJobs();

        var checkBoxes = window.GetVisualDescendants().OfType<CheckBox>().ToList();
        Check(checkBoxes, "Flip image");
        Check(checkBoxes, "Mirror top ↕ bottom");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new FlipOptions(Enabled: true, MirrorVertically: true), app.FlipSettings.Current);

        static void Check(IEnumerable<CheckBox> boxes, string content)
            => boxes.Single(c => Equals(c.Content, content)).IsChecked = true;
    }

    [AvaloniaFact]
    public void RotateRadioButtons_ApplyImmediately_AndFollowRestoredSettings()
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();
        Assert.True(app.ProcessorTabs.SelectTab("Rotate"));
        Dispatcher.UIThread.RunJobs();

        window.GetVisualDescendants().OfType<CheckBox>().Single(c => Equals(c.Content, "Rotate image")).IsChecked = true;
        var oneEighty = window.GetVisualDescendants().OfType<RadioButton>().Single(r => Equals(r.Content, "180°"));
        oneEighty.Command!.Execute(oneEighty.CommandParameter);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new RotateOptions(enabled: true, angle: RotationAngle.Clockwise180), app.RotateSettings.Current);

        // An angle restored from disk has to move the selection, which is why IsChecked is bound one-way.
        app.RotateSettings.Update(new RotateOptions(enabled: true, angle: RotationAngle.Clockwise90));
        Dispatcher.UIThread.RunJobs();

        Assert.False(oneEighty.IsChecked);
        Assert.True(window.GetVisualDescendants().OfType<RadioButton>()
            .Single(r => Equals(r.Content, "90° clockwise")).IsChecked);
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
