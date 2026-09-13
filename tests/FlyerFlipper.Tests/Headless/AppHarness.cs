using System.Collections.Concurrent;
using Avalonia.Threading;
using FlyerFlipper.Core.Application;
using FlyerFlipper.Core.Imaging;
using FlyerFlipper.Core.Layout;
using FlyerFlipper.Core.Source;
using Avalonia.Media.Imaging;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Store;
using FlyerFlipper.Core.Viewport;
using FlyerFlipper.Imaging;
using FlyerFlipper.UI.Imaging;
using FlyerFlipper.UI.ViewModels;
using FlyerFlipper.UI.Views;
using Moq;

namespace FlyerFlipper.Tests.Headless;

/// <summary>
/// Composes the real services and view models (as <c>Program.cs</c> does) over a fake image source
/// and loader, so headless tests exercise the actual bindings and wiring.
/// </summary>
internal sealed class AppHarness : IDisposable
{
    public const string Folder = @"C:\flyers";

    private readonly Mock<IImageSource> _source = new();
    private IReadOnlyList<ImageReference> _images = [];

    public AppHarness()
    {
        _source.Setup(s => s.Enumerate(It.IsAny<ImageSourceQuery>(), It.IsAny<CancellationToken>()))
            .Returns(() => _images);

        Catalog = new ImageCatalog(_source.Object);
        Layout = new LayoutModeService();
        Viewport = new ViewportModeService(Catalog);
        Store = new ImageStore<Bitmap>(Catalog, Viewport, Loader, new ImageProcessingPipeline([]), new SkiaThumbnailService(), new AvaloniaBitmapFactory());
        ImageSource = new ImageSourceViewModel(Catalog);
        Grid = new ThumbnailGridViewModel(Store, Layout, Viewport);
        Single = new SingleImageViewModel(Viewport, Store);
        MainViewModel = new MainWindowViewModel(Layout, Viewport, Mock.Of<IApplicationShutdown>(), ImageSource, Grid, Single);
    }

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

    public async Task LoadFolderAsync(int imageCount)
    {
        _images = Enumerable.Range(0, imageCount)
            .Select(i => new ImageReference(Path.Combine(Folder, $"image{i:D2}.png")))
            .ToArray();
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

        const int width = 8;
        const int height = 6;
        var pixels = new byte[width * height * 4];
        pixels.AsSpan().Fill(0xFF);
        return new SourceImage(reference, new ImageBuffer(pixels, width, height, width * 4, ImagePixelFormat.Bgra8888Premultiplied));
    }
}
