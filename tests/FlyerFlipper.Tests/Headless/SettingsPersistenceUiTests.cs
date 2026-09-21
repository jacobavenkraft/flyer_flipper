using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlyerFlipper.Core.Layout;
using FlyerFlipper.Core.Processors;
using FlyerFlipper.Core.Settings;
using FlyerFlipper.Core.Viewport;
using FlyerFlipper.Infrastructure.Settings;
using FlyerFlipper.Tests.TestSupport;
using FlyerFlipper.UI.Settings;
using FlyerFlipper.UI.Views;

namespace FlyerFlipper.Tests.Headless;

public class SettingsPersistenceUiTests
{
    // ---- Window placement ----------------------------------------------------------------------

    [AvaloniaFact]
    public void Tracker_AppliesSavedSizePositionAndMaximizedState()
    {
        using var app = new AppHarness();
        var window = new MainWindow(app.MainViewModel);
        var tracker = new WindowPlacementTracker();

        tracker.Attach(window, new WindowPlacement(120, 90, 1100, 750, IsMaximized: true));
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(WindowStartupLocation.Manual, window.WindowStartupLocation);
        Assert.Equal(new PixelPoint(120, 90), window.Position);
        Assert.Equal((1100d, 750d), (window.Width, window.Height));
        Assert.Equal(WindowState.Maximized, window.WindowState);
        Assert.Equal(new WindowPlacement(120, 90, 1100, 750, true), tracker.Current);
        window.Close();
    }

    [AvaloniaFact]
    public void Tracker_HidesTheWindowWhileRestoringPosition()
    {
        // A window manager that ignores the pre-show position shows the window in the wrong place first; hiding it
        // until the position has been re-applied is what keeps that from being a visible jump.
        using var app = new AppHarness();
        var window = new MainWindow(app.MainViewModel);

        new WindowPlacementTracker().Attach(window, new WindowPlacement(120, 90, 1100, 750, IsMaximized: false));

        Assert.Equal(0d, window.Opacity);
    }

    [AvaloniaFact]
    public void Tracker_NothingToRestore_LeavesTheWindowVisible()
    {
        using var app = new AppHarness();
        var window = new MainWindow(app.MainViewModel);

        new WindowPlacementTracker().Attach(window, null);

        Assert.Equal(1d, window.Opacity);
    }

    /// <summary>Runs scheduled restore steps immediately, so the restore completes without a real clock.</summary>
    private static readonly Action<TimeSpan, Action> RunImmediately = static (_, action) => action();

    [AvaloniaFact]
    public void Tracker_AlwaysRevealsTheWindowAfterRestoring()
    {
        // The window is hidden to restore it, so failing to reveal it would leave the app invisible.
        using var app = new AppHarness();
        var window = new MainWindow(app.MainViewModel);
        var tracker = new WindowPlacementTracker(RunImmediately);

        tracker.Attach(window, new WindowPlacement(120, 90, 1100, 750, IsMaximized: false));
        Assert.Equal(0d, window.Opacity);

        window.Show();

        Assert.Equal(1d, window.Opacity);
        Assert.Equal(new PixelPoint(120, 90), window.Position);
        Assert.Equal(new WindowPlacement(120, 90, 1100, 750, false), tracker.Current);
        window.Close();
    }

    [AvaloniaFact]
    public void Tracker_RevealsTheWindowEvenWhenThePositionNeverSticks()
    {
        // A window manager that refuses to honour the position must not leave the window hidden forever.
        using var app = new AppHarness();
        var window = new MainWindow(app.MainViewModel);
        var stubborn = new WindowPlacementTracker((_, action) =>
        {
            window.Position = new PixelPoint(7, 9); // whatever we ask for, the window sits here
            action();
        });

        stubborn.Attach(window, new WindowPlacement(120, 90, 1100, 750, IsMaximized: false));
        window.Show();

        Assert.Equal(1d, window.Opacity);
        window.Close();
    }

    [AvaloniaFact]
    public void Tracker_PlacementOffEveryScreen_IsCenteredInstead()
    {
        using var app = new AppHarness();
        var window = new MainWindow(app.MainViewModel);

        new WindowPlacementTracker().Attach(window, new WindowPlacement(-5000, 4000, 1100, 750, IsMaximized: false));

        Assert.Equal(WindowStartupLocation.CenterScreen, window.WindowStartupLocation);
        Assert.Equal((1100d, 750d), (window.Width, window.Height));
    }

    [AvaloniaFact]
    public void Tracker_TinySavedSize_IsRaisedToMinimum()
    {
        using var app = new AppHarness();
        var window = new MainWindow(app.MainViewModel);

        new WindowPlacementTracker().Attach(window, new WindowPlacement(100, 100, 50, 20, IsMaximized: false));

        Assert.Equal((WindowPlacementRules.MinWidth, WindowPlacementRules.MinHeight), (window.Width, window.Height));
    }

    [AvaloniaFact]
    public void Tracker_ReportsMovesAndResizes_AndKeepsNormalBoundsWhileMaximized()
    {
        using var app = new AppHarness();
        var window = new MainWindow(app.MainViewModel);
        var tracker = new WindowPlacementTracker();
        tracker.Attach(window, null);
        window.Show();
        var changes = 0;
        tracker.Changed += (_, _) => changes++;

        window.Position = new PixelPoint(33, 44);
        window.Width = 900;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(new WindowPlacement(33, 44, 900, 700, false), tracker.Current);

        window.WindowState = WindowState.Maximized;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(new WindowPlacement(33, 44, 900, 700, true), tracker.Current);

        window.WindowState = WindowState.Minimized; // never saved as minimized
        Dispatcher.UIThread.RunJobs();
        Assert.True(tracker.Current!.IsMaximized);
        Assert.True(changes >= 3);
        window.Close();
    }

    // ---- Tabs ------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void TabSelectedByName_BeforeWindowOpens_IsShownSelected()
    {
        using var app = new AppHarness();
        Assert.True(app.ProcessorTabs.SelectTab("Resize"));

        var window = app.ShowWindow();

        Assert.Equal(app.ProcessorTabs.SelectedIndex, window.GetVisualDescendants().OfType<TabControl>().Single().SelectedIndex);
        Assert.Equal("Resize", app.ProcessorTabs.SelectedHeader);
    }

    // ---- Relaunch --------------------------------------------------------------------------------

    /// <summary>One app "session": the harness plus the startup wiring App.axaml.cs performs, over a real settings file.</summary>
    private sealed class Session : IDisposable
    {
        public Session(string settingsPath, int imageCount)
        {
            App = new AppHarness();
            App.SetImages(imageCount);
            Store = new JsonSettingsStore(settingsPath);
            Tracker = new WindowPlacementTracker();
            Coordinator = new SettingsCoordinator(
                Store, App.Catalog, App.Layout, App.Viewport, [App.ChannelMap, App.Invert, App.Grayscale, App.Flip, App.Rotate, App.Resize], App.ImageSource, App.ProcessorTabs, Tracker, TimeProvider.System);
        }

        public AppHarness App { get; }

        public JsonSettingsStore Store { get; }

        public WindowPlacementTracker Tracker { get; }

        public SettingsCoordinator Coordinator { get; }

        public MainWindow? Window { get; private set; }

        public async Task StartAsync()
        {
            var saved = Coordinator.LoadAndApplyStartupState();
            Window = new MainWindow(App.MainViewModel);
            Tracker.Attach(Window, saved.Window);
            Window.Show();
            Dispatcher.UIThread.RunJobs();
            await Coordinator.RestoreImagesAsync();
            Dispatcher.UIThread.RunJobs();
        }

        public void Exit()
        {
            Window?.Close();
            Coordinator.Flush();
        }

        public void Dispose()
        {
            Coordinator.Dispose();
            App.Dispose();
        }
    }

    [AvaloniaFact]
    public async Task Relaunch_RestoresEverythingFromTheSettingsFile()
    {
        using var temp = new TempDirectory();
        var settingsPath = Path.Combine(temp.Path, "settings.json");

        using (var first = new Session(settingsPath, imageCount: 6))
        {
            await first.StartAsync();
            await first.App.LoadFolderAsync(6);
            first.App.Layout.Toggle();
            first.App.Viewport.SetScaleMode(ViewportScaleMode.FitWithoutEnlarging);
            first.App.GrayscaleSettings.Update(new GrayscaleOptions(true));
            first.App.ResizeSettings.Update(new ResizeOptions(true, 500, 400));
            Assert.True(first.App.ProcessorTabs.SelectTab("Resize"));
            first.App.Viewport.ShowSingle(4);
            first.Window!.Position = new PixelPoint(70, 60);
            first.Window.Width = 1111;
            Dispatcher.UIThread.RunJobs();
            first.Exit();
        }

        Assert.True(File.Exists(settingsPath));

        using var second = new Session(settingsPath, imageCount: 6);
        await second.StartAsync();

        Assert.Equal(LayoutOrientation.Horizontal, second.App.Layout.Orientation);
        Assert.Equal(ViewportScaleMode.FitWithoutEnlarging, second.App.Viewport.ScaleMode);
        Assert.True(second.App.GrayscaleSettings.Current.Enabled);
        Assert.Equal(new ResizeOptions(true, 500, 400), second.App.ResizeSettings.Current);
        Assert.Equal("Resize", second.App.ProcessorTabs.SelectedHeader);
        Assert.Equal(AppHarness.Folder, second.App.ImageSource.FolderPath);
        Assert.Equal(ViewportMode.Single, second.App.Viewport.Mode);
        Assert.Equal("image04.png", second.App.Viewport.CurrentImage?.FileName);
        Assert.Equal(new PixelPoint(70, 60), second.Window!.Position);
        Assert.Equal(1111, second.Window.Width);

        // The restored tab controls show the restored settings too.
        var resizeCheck = second.Window.GetVisualDescendants().OfType<CheckBox>().Single(c => Equals(c.Content, "Shrink to fit"));
        Assert.True(resizeCheck.IsChecked);
        second.Exit();
    }
}
