using System.Collections.Concurrent;
using Avalonia.Threading;
using FlyerFlipper.Core.Application;
using FlyerFlipper.Core.Imaging;
using FlyerFlipper.Core.Layout;
using FlyerFlipper.Core.Source;
using Avalonia.Media.Imaging;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;
using FlyerFlipper.Core.Store;
using FlyerFlipper.Core.Viewport;
using FlyerFlipper.Imaging;
using FlyerFlipper.Imaging.Processors;
using FlyerFlipper.UI.Imaging;
using FlyerFlipper.UI.Processors;
using FlyerFlipper.UI.ViewModels;
using FlyerFlipper.UI.Views;
using Moq;
using FlyerFlipper.Tests.TestSupport;

namespace FlyerFlipper.Tests.Headless;

/// <summary>
/// Composes the real services and view models (as <c>Program.cs</c> does) over a fake image source
/// and loader, so headless tests exercise the actual bindings and wiring.
/// </summary>
internal sealed class AppHarness : IDisposable
{
    public static readonly string Folder = TestPaths.Folder("flyers");

    private readonly Mock<IImageSource> _source = new();
    private IReadOnlyList<ImageReference> _images = [];

    public AppHarness()
    {
        _source.Setup(s => s.Enumerate(It.IsAny<ImageSourceQuery>(), It.IsAny<CancellationToken>()))
            .Returns(() => _images);

        Catalog = new ImageCatalog(_source.Object);
        Layout = new LayoutModeService();
        Viewport = new ViewportModeService(Catalog);
        Grayscale = new GrayscaleProcessor(GrayscaleSettings);
        Resize = new ResizeProcessor(ResizeSettings);
        Pipeline = new ImageProcessingPipeline([Grayscale, Resize]);
        Store = new ImageStore<Bitmap>(Catalog, Viewport, Loader, Pipeline, new SkiaThumbnailService(), new AvaloniaBitmapFactory());
        ImageSource = new ImageSourceViewModel(Catalog);
        Grid = new ThumbnailGridViewModel(Store, Layout, Viewport);
        Single = new SingleImageViewModel(Viewport, Store);
        ProcessorTabs = new ProcessorTabHostViewModel([new ResizeControlProvider(ResizeSettings), new GrayscaleControlProvider(GrayscaleSettings)]);
        MainViewModel = new MainWindowViewModel(Layout, Viewport, Mock.Of<IApplicationShutdown>(), ImageSource, Grid, Single, ProcessorTabs);
    }

    public ProcessorSettings<GrayscaleOptions> GrayscaleSettings { get; } = new(new GrayscaleOptions());

    public ProcessorSettings<ResizeOptions> ResizeSettings { get; } = new(new ResizeOptions());

    public GrayscaleProcessor Grayscale { get; }

    public ResizeProcessor Resize { get; }

    public ImageProcessingPipeline Pipeline { get; }

    public ProcessorTabHostViewModel ProcessorTabs { get; }

    public ImageCatalog Catalog { get; }

    public LayoutModeService Layout { get; }

    public ViewportModeService Viewport { get; }

    public ImageStore<Bitmap> Store { get; }

    public FakeImageLoader Loader { get; } = new();

    public ImageSourceViewModel ImageSource { get; }

    public ThumbnailGridViewModel Grid { get; }

    public SingleImageViewModel Single { get; }

    public MainWindowViewModel MainViewModel { get; }

    public MainWindow? Window { get; private set; }

    public MainWindow ShowWindow()
    {
        Window = new MainWindow(MainViewModel);
        Window.Show();
        Dispatcher.UIThread.RunJobs();
        return Window;
    }

    /// <summary>Sets what the fake source returns for any folder, without loading it.</summary>
    public void SetImages(int imageCount)
        => _images = Enumerable.Range(0, imageCount)
            .Select(i => new ImageReference(Path.Combine(Folder, $"image{i:D2}.png")))
            .ToArray();

    public async Task LoadFolderAsync(int imageCount)
    {
        SetImages(imageCount);
        ImageSource.FolderPath = Folder;
        await ImageSource.LoadFolderCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Pumps the dispatcher until <paramref name="condition"/> holds (background loads finish).</summary>
    public static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (!condition())
        {
            if (Environment.TickCount64 > deadline)
            {
                throw new TimeoutException("Condition not met in time.");
            }

            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
    }

    public void Dispose()
    {
        Window?.Close();
        MainViewModel.Dispose();
        Single.Dispose();
        Grid.Dispose();
        Store.Dispose();
        Viewport.Dispose();
    }
}

/// <summary>Returns a small solid image per path and records how often each path was decoded.</summary>
internal sealed class FakeImageLoader : IImageLoader
{
    private readonly ConcurrentDictionary<string, int> _loads = new(StringComparer.OrdinalIgnoreCase);

    public ConcurrentDictionary<string, bool> Failing { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int LoadCount(ImageReference reference) => _loads.GetValueOrDefault(reference.FullPath);

    public int TotalLoads => _loads.Values.Sum();

    public SourceImage Load(ImageReference reference, CancellationToken cancellationToken = default)
    {
        _loads.AddOrUpdate(reference.FullPath, 1, static (_, n) => n + 1);
        cancellationToken.ThrowIfCancellationRequested();

        if (Failing.ContainsKey(reference.FullPath))
        {
            throw new ImageLoadException($"Corrupt image '{reference.FileName}'.");
        }

        // Portrait "flyer" with colored horizontal bands (varying per file), so processing is visible in screenshots.
        const int width = 60;
        const int height = 80;
        var seed = (uint)StringComparer.OrdinalIgnoreCase.GetHashCode(reference.FullPath);
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            var band = (uint)(y / 20) + seed;
            byte r = (byte)(band * 97), g = (byte)(band * 57), b = (byte)(band * 23);
            for (var x = 0; x < width; x++)
            {
                var i = ((y * width) + x) * 4;
                pixels[i] = b;
                pixels[i + 1] = g;
                pixels[i + 2] = r;
                pixels[i + 3] = 0xFF;
            }
        }

        return new SourceImage(reference, new ImageBuffer(pixels, width, height, width * 4, ImagePixelFormat.Bgra8888Premultiplied));
    }
}
