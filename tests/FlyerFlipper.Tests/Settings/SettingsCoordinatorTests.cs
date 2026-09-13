using System.Text.Json;
using Avalonia.Controls;
using FlyerFlipper.Core.Layout;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;
using FlyerFlipper.Core.Settings;
using FlyerFlipper.Core.Source;
using FlyerFlipper.Core.Viewport;
using FlyerFlipper.Imaging.Processors;
using FlyerFlipper.Tests.TestSupport;
using FlyerFlipper.UI.Processors;
using FlyerFlipper.UI.Settings;
using FlyerFlipper.UI.ViewModels;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace FlyerFlipper.Tests.Settings;

public class SettingsCoordinatorTests
{
    private sealed class InMemorySettingsStore(AppSettings initial) : ISettingsStore
    {
        public AppSettings Stored { get; private set; } = initial;

        public List<AppSettings> Saves { get; } = [];

        public AppSettings Load() => Stored;

        public void Save(AppSettings settings)
        {
            Saves.Add(settings);
            Stored = settings;
        }
    }

    private sealed class FakeWindowPlacement : IWindowPlacementSource
    {
        public WindowPlacement? Current { get; private set; }

        public event EventHandler? Changed;

        public void MoveTo(WindowPlacement placement)
        {
            Current = placement;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class HeaderOnlyProvider(string header, int order) : IProcessorControlProvider
    {
        public string Header => header;

        public int Order => order;

        public Control CreateControl() => throw new NotSupportedException("Not needed without a window.");
    }

    private sealed class Fixture : IDisposable
    {
        private readonly Mock<IImageSource> _source = new();
        private readonly Dictionary<string, ImageReference[]> _folders = new(StringComparer.OrdinalIgnoreCase);

        public Fixture(AppSettings saved)
        {
            _source.Setup(s => s.Enumerate(It.IsAny<ImageSourceQuery>(), It.IsAny<CancellationToken>()))
                .Returns((ImageSourceQuery q, CancellationToken _) => _folders.TryGetValue(q.RootPath, out var images)
                    ? images
                    : throw new DirectoryNotFoundException($"Folder not found: {q.RootPath}"));

            Store = new InMemorySettingsStore(saved);
            Catalog = new ImageCatalog(_source.Object);
            Viewport = new ViewportModeService(Catalog);
            Grayscale = new GrayscaleProcessor(GrayscaleSettings);
            Resize = new ResizeProcessor(ResizeSettings);
            ImageSource = new ImageSourceViewModel(Catalog);
            Tabs = new ProcessorTabHostViewModel([new HeaderOnlyProvider("Grayscale", 100), new HeaderOnlyProvider("Resize", 200)]);
            Coordinator = new SettingsCoordinator(
                Store, Catalog, Layout, Viewport, [Grayscale, Resize, new DiagnosticLoggingProcessorStub()], ImageSource, Tabs, Window, Time);
        }

        public InMemorySettingsStore Store { get; }

        public ImageCatalog Catalog { get; }

        public LayoutModeService Layout { get; } = new();

        public ViewportModeService Viewport { get; }

        public ProcessorSettings<GrayscaleOptions> GrayscaleSettings { get; } = new(new GrayscaleOptions());

        public ProcessorSettings<ResizeOptions> ResizeSettings { get; } = new(new ResizeOptions());

        public GrayscaleProcessor Grayscale { get; }

        public ResizeProcessor Resize { get; }

        public ImageSourceViewModel ImageSource { get; }

        public ProcessorTabHostViewModel Tabs { get; }

        public FakeWindowPlacement Window { get; } = new();

        public FakeTimeProvider Time { get; } = new();

        public SettingsCoordinator Coordinator { get; }

        public void AddFolder(string path, params string[] fileNames)
            => _folders[path] = fileNames.Select(n => new ImageReference(Path.Combine(path, n))).ToArray();

        public async Task StartAsync()
        {
            Coordinator.LoadAndApplyStartupState();
            await Coordinator.RestoreImagesAsync();
        }

        /// <summary>Advances the fake clock past the save delay and lets the save continuation run.</summary>
        public async Task ElapseSaveDelayAsync()
        {
            Time.Advance(SettingsCoordinator.SaveDelay);
            await SingleThreadedContext.WaitUntilAsync(() => true);
            await Task.Delay(20);
        }

        public void Dispose()
        {
            Coordinator.Dispose();
            Grayscale.Dispose();
            Resize.Dispose();
            Viewport.Dispose();
        }
    }

    /// <summary>A non-configurable processor, which the coordinator must ignore.</summary>
    private sealed class DiagnosticLoggingProcessorStub : IImageProcessor
    {
        public int Order => int.MaxValue;

        public ProcessedImage Process(ProcessedImage input, CancellationToken cancellationToken) => input;
    }

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [Fact]
    public void Startup_AppliesLayoutScalingTabAndProcessorSnippets() => SingleThreadedContext.Run(async () =>
    {
        using var f = new Fixture(new AppSettings
        {
            Orientation = LayoutOrientation.Horizontal,
            ScaleMode = ViewportScaleMode.StretchToFill,
            ActiveProcessorTab = "Resize",
            Processors = new Dictionary<string, JsonElement>
            {
                ["flyerflipper.grayscale"] = Json("""{"enabled":true}"""),
                ["flyerflipper.resize"] = Json("""{"enabled":true,"maxWidth":320,"maxHeight":240}"""),
            },
        });

        await f.StartAsync();

        Assert.Equal(LayoutOrientation.Horizontal, f.Layout.Orientation);
        Assert.Equal(ViewportScaleMode.StretchToFill, f.Viewport.ScaleMode);
        Assert.Equal("Resize", f.Tabs.SelectedHeader);
        Assert.True(f.GrayscaleSettings.Current.Enabled);
        Assert.Equal(new ResizeOptions(true, 320, 240), f.ResizeSettings.Current);
        Assert.Empty(f.Store.Saves); // restoring alone doesn't need a save
    });

    [Fact]
    public void Startup_InvalidProcessorSnippetOrUnknownTab_KeepsDefaults() => SingleThreadedContext.Run(async () =>
    {
        using var f = new Fixture(new AppSettings
        {
            ActiveProcessorTab = "Sepia",
            Processors = new Dictionary<string, JsonElement> { ["flyerflipper.resize"] = Json("""{"maxWidth":-5}""") },
        });

        await f.StartAsync();

        Assert.Equal("Grayscale", f.Tabs.SelectedHeader);
        Assert.Equal(new ResizeOptions(), f.ResizeSettings.Current);
    });

    [Fact]
    public void Restore_ReopensFolder_SelectsImageByFileName_AndReturnsToSingleView() => SingleThreadedContext.Run(async () =>
    {
        using var f = new Fixture(new AppSettings
        {
            LastFolder = @"D:\flyers",
            ViewedImageFileName = "Boise_ID.jpg",
            ViewportMode = ViewportMode.Single,
        });
        f.AddFolder(@"D:\flyers", "Atlanta_GA.jpg", "Austin_TX.jpg", "Boise_ID.jpg", "Denver_CO.jpg");

        await f.StartAsync();

        Assert.Equal(@"D:\flyers", f.ImageSource.FolderPath);
        Assert.Equal(4, f.Catalog.Images.Count);
        Assert.Equal("Boise_ID.jpg", f.Viewport.CurrentImage?.FileName);
        Assert.Equal(ViewportMode.Single, f.Viewport.Mode);
    });

    [Fact]
    public void Restore_ViewedFileGone_SelectsFirstImage() => SingleThreadedContext.Run(async () =>
    {
        using var f = new Fixture(new AppSettings
        {
            LastFolder = @"D:\flyers",
            ViewedImageFileName = "Deleted.jpg",
            ViewportMode = ViewportMode.Single,
        });
        f.AddFolder(@"D:\flyers", "Atlanta_GA.jpg", "Austin_TX.jpg");

        await f.StartAsync();

        Assert.Equal(0, f.Viewport.CurrentIndex);
        Assert.Equal(ViewportMode.Single, f.Viewport.Mode);
    });

    [Fact]
    public void Restore_MissingFolder_ShowsError_AndKeepsSavedFolderAndImage() => SingleThreadedContext.Run(async () =>
    {
        using var f = new Fixture(new AppSettings
        {
            LastFolder = @"E:\unplugged",
            ViewedImageFileName = "Boise_ID.jpg",
            ViewportMode = ViewportMode.Single,
        });

        await f.StartAsync();
        f.Layout.Toggle(); // any change triggers a save
        await f.ElapseSaveDelayAsync();

        Assert.Equal(@"E:\unplugged", f.ImageSource.FolderPath);
        Assert.True(f.ImageSource.HasError);
        Assert.Contains("Folder not found", f.ImageSource.StatusMessage);
        Assert.Empty(f.Catalog.Images);

        var saved = Assert.Single(f.Store.Saves);
        Assert.Equal(@"E:\unplugged", saved.LastFolder);
        Assert.Equal("Boise_ID.jpg", saved.ViewedImageFileName);
        Assert.Equal(ViewportMode.Single, saved.ViewportMode);
    });

    [Fact]
    public void Changes_AreSavedOnceAfterTheDebounceDelay() => SingleThreadedContext.Run(async () =>
    {
        using var f = new Fixture(new AppSettings());
        f.AddFolder(@"D:\flyers", "a.jpg", "b.jpg", "c.jpg");
        await f.StartAsync();

        f.Layout.Toggle();
        f.Viewport.SetScaleMode(ViewportScaleMode.ActualSize);
        f.GrayscaleSettings.Update(new GrayscaleOptions(true));
        f.Time.Advance(SettingsCoordinator.SaveDelay / 2);
        await Task.Delay(20);
        Assert.Empty(f.Store.Saves);

        f.Tabs.SelectedIndex = 1; // restarts the countdown
        f.Time.Advance(SettingsCoordinator.SaveDelay / 2);
        await Task.Delay(20);
        Assert.Empty(f.Store.Saves);

        await f.ElapseSaveDelayAsync();
        var saved = Assert.Single(f.Store.Saves);
        Assert.Equal(LayoutOrientation.Horizontal, saved.Orientation);
        Assert.Equal(ViewportScaleMode.ActualSize, saved.ScaleMode);
        Assert.Equal("Resize", saved.ActiveProcessorTab);
        Assert.True(saved.Processors["flyerflipper.grayscale"].GetProperty("enabled").GetBoolean());
    });

    [Fact]
    public void SavedState_CapturesFolderImageModeAndWindow() => SingleThreadedContext.Run(async () =>
    {
        using var f = new Fixture(new AppSettings());
        f.AddFolder(@"D:\flyers", "a.jpg", "b.jpg", "c.jpg");
        await f.StartAsync();

        f.ImageSource.FolderPath = @"D:\flyers";
        await f.ImageSource.LoadFolderCommand.ExecuteAsync(null);
        f.Viewport.ShowSingle(2);
        f.Window.MoveTo(new WindowPlacement(40, 50, 1200, 800, IsMaximized: true));
        await f.ElapseSaveDelayAsync();

        var saved = f.Store.Saves.Last();
        Assert.Equal(@"D:\flyers", saved.LastFolder);
        Assert.Equal("c.jpg", saved.ViewedImageFileName);
        Assert.Equal(ViewportMode.Single, saved.ViewportMode);
        Assert.Equal(new WindowPlacement(40, 50, 1200, 800, true), saved.Window);
    });

    [Fact]
    public void Flush_SavesPendingChangesImmediately_AndOnlyWhenDirty() => SingleThreadedContext.Run(async () =>
    {
        using var f = new Fixture(new AppSettings());
        await f.StartAsync();

        f.Coordinator.Flush();
        Assert.Empty(f.Store.Saves);

        f.Layout.Toggle();
        f.Coordinator.Flush();
        Assert.Single(f.Store.Saves);

        await f.ElapseSaveDelayAsync(); // the cancelled debounce must not save again
        Assert.Single(f.Store.Saves);
    });

    [Fact]
    public void ProcessorEntriesForUnregisteredProcessors_ArePreserved() => SingleThreadedContext.Run(async () =>
    {
        using var f = new Fixture(new AppSettings
        {
            Processors = new Dictionary<string, JsonElement>
            {
                ["someplugin.sepia"] = Json("""{"strength":0.4}"""),
                ["flyerflipper.grayscale"] = Json("""{"enabled":false}"""),
            },
        });
        await f.StartAsync();

        f.GrayscaleSettings.Update(new GrayscaleOptions(true));
        await f.ElapseSaveDelayAsync();

        var saved = Assert.Single(f.Store.Saves);
        Assert.Equal(0.4, saved.Processors["someplugin.sepia"].GetProperty("strength").GetDouble());
        Assert.True(saved.Processors["flyerflipper.grayscale"].GetProperty("enabled").GetBoolean());
        Assert.True(saved.Processors.ContainsKey("flyerflipper.resize"));
        Assert.Equal(3, saved.Processors.Count); // non-configurable processors contribute nothing
    });

    [Fact]
    public void ChangesDuringRestore_AreSavedOnceRestoreCompletes() => SingleThreadedContext.Run(async () =>
    {
        using var f = new Fixture(new AppSettings { LastFolder = @"D:\flyers" });
        f.AddFolder(@"D:\flyers", "a.jpg");
        f.Coordinator.LoadAndApplyStartupState();

        f.Window.MoveTo(new WindowPlacement(1, 2, 900, 600, false)); // window shown while restoring
        f.Time.Advance(SettingsCoordinator.SaveDelay * 2);
        await Task.Delay(20);
        Assert.Empty(f.Store.Saves);

        await f.Coordinator.RestoreImagesAsync();
        await f.ElapseSaveDelayAsync();

        var saved = Assert.Single(f.Store.Saves);
        Assert.Equal(new WindowPlacement(1, 2, 900, 600, false), saved.Window);
        Assert.Equal(@"D:\flyers", saved.LastFolder);
    });
}
