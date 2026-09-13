using FlyerFlipper.Core.Source;
using FlyerFlipper.Core.Store;
using FlyerFlipper.Core.Viewport;
using FlyerFlipper.Tests.TestSupport;
using Moq;

namespace FlyerFlipper.Tests.Store;

public class ImageStoreTests
{
    private const int ThumbnailEdge = 100;

    /// <summary>Real catalog + viewport over a mocked source, as the app composes them.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly Mock<IImageSource> _source = new();
        private IReadOnlyList<ImageReference> _images = [];

        public Fixture(int maxConcurrentThumbnails = 2)
        {
            _source.Setup(s => s.Enumerate(It.IsAny<ImageSourceQuery>(), It.IsAny<CancellationToken>())).Returns(() => _images);
            Catalog = new ImageCatalog(_source.Object);
            Viewport = new ViewportModeService(Catalog);
            Store = new ImageStore<FakeDisplayImage>(
                Catalog,
                Viewport,
                Loader,
                new FakeThumbnailService(),
                Factory,
                new ImageStoreOptions { ThumbnailMaxEdge = ThumbnailEdge, MaxConcurrentThumbnailLoads = maxConcurrentThumbnails });
        }

        public ImageCatalog Catalog { get; }

        public ViewportModeService Viewport { get; }

        public GatedImageLoader Loader { get; } = new();

        public FakeDisplayImageFactory Factory { get; } = new();

        public ImageStore<FakeDisplayImage> Store { get; }

        public ImageReference this[int index] => Catalog.Images[index];

        /// <summary>Image references for the next folder, so gates can be set before it loads.</summary>
        public static ImageReference[] References(int count, string folder = @"C:\flyers")
            => Enumerable.Range(0, count).Select(i => new ImageReference($@"{folder}\image{i:D2}.png")).ToArray();

        public Task LoadFolderAsync(IReadOnlyList<ImageReference> images)
        {
            _images = images;
            return Catalog.LoadAsync(new ImageSourceQuery(@"C:\flyers"));
        }

        public Task LoadFolderAsync(int count) => LoadFolderAsync(References(count));

        public Task AllThumbnailsSettledAsync() => SingleThreadedContext.WaitUntilAsync(
            () => Enumerable.Range(0, Store.Images.Count).All(i => Store.GetThumbnail(i).State is ImageLoadState.Ready or ImageLoadState.Failed));

        public int[] FullIndices(ImageLoadState state)
            => Enumerable.Range(0, Store.Images.Count).Where(i => Store.GetFullImage(i).State == state).ToArray();

        public void Dispose()
        {
            Store.Dispose();
            Viewport.Dispose();
        }
    }

    [Fact]
    public void EnteringFolder_GeneratesThumbnailsForEveryImage_AndNoFullImagesInGridMode() => SingleThreadedContext.Run(async () =>
    {
        using var f = new Fixture();
        var resets = 0;
        f.Store.ImagesReset += (_, _) => resets++;

        await f.LoadFolderAsync(5);
        await f.AllThumbnailsSettledAsync();

        Assert.Equal(1, resets);
        Assert.Equal(f.Catalog.Images, f.Store.Images);
        for (var i = 0; i < 5; i++)
        {
            var thumbnail = f.Store.GetThumbnail(i);
            Assert.Equal(ImageLoadState.Ready, thumbnail.State);
            Assert.Equal((100, 75), (thumbnail.Image!.Width, thumbnail.Image.Height));
            Assert.Equal(1, f.Loader.Started(f[i]));
        }

        Assert.Empty(f.FullIndices(ImageLoadState.Ready));
        Assert.Empty(f.FullIndices(ImageLoadState.Loading));
    });

    [Fact]
    public void SingleMode_LoadsCurrentAndBothNeighbours_AtFullSize() => SingleThreadedContext.Run(async () =>
    {
        using var f = new Fixture();
        await f.LoadFolderAsync(5);
        await f.AllThumbnailsSettledAsync();

        f.Viewport.ShowSingle(2);
        await SingleThreadedContext.WaitUntilAsync(() => f.FullIndices(ImageLoadState.Ready).Length == 3);

        Assert.Equal([1, 2, 3], f.FullIndices(ImageLoadState.Ready));
        var full = f.Store.GetFullImage(2).Image!;
        Assert.Equal((GatedImageLoader.Width, GatedImageLoader.Height), (full.Width, full.Height));
    });

    [Theory]
    [InlineData(0, new[] { 0, 1 })]
    [InlineData(4, new[] { 3, 4 })]
    public void SingleMode_AtEnds_LoadsOnlyExistingNeighbour(int index, int[] expected) => SingleThreadedContext.Run(async () =>
    {
        using var f = new Fixture();
        await f.LoadFolderAsync(5);

        f.Viewport.ShowSingle(index);
        await SingleThreadedContext.WaitUntilAsync(() => f.FullIndices(ImageLoadState.Ready).Length == expected.Length);

        Assert.Equal(expected, f.FullIndices(ImageLoadState.Ready));
    });

    [Fact]
    public void Navigating_SlidesWindow_ReusingRetainedImages_AndReleasingTheRest() => SingleThreadedContext.Run(async () =>
    {
        using var f = new Fixture();
        await f.LoadFolderAsync(6);
        await f.AllThumbnailsSettledAsync();
        f.Viewport.ShowSingle(2);
        await SingleThreadedContext.WaitUntilAsync(() => f.FullIndices(ImageLoadState.Ready).Length == 3);
        var released = f.Store.GetFullImage(1).Image!;
        var kept = f.Store.GetFullImage(3).Image!;

        f.Viewport.MoveNext();
        await SingleThreadedContext.WaitUntilAsync(() => f.Store.GetFullImage(4).State == ImageLoadState.Ready);

        Assert.Equal([2, 3, 4], f.FullIndices(ImageLoadState.Ready));
        Assert.Equal(ImageLoadState.NotLoaded, f.Store.GetFullImage(1).State);
        Assert.True(released.IsDisposed);
        Assert.Same(kept, f.Store.GetFullImage(3).Image);
        Assert.False(kept.IsDisposed);
        Assert.Equal(2, f.Loader.Started(f[3])); // thumbnail + first full load; not reloaded on slide
    });

    [Fact]
    public void ReturningToGrid_ReleasesAllFullImages_ButKeepsThumbnails() => SingleThreadedContext.Run(async () =>
    {
        using var f = new Fixture();
        await f.LoadFolderAsync(3);
        await f.AllThumbnailsSettledAsync();
        f.Viewport.ShowSingle(1);
        await SingleThreadedContext.WaitUntilAsync(() => f.FullIndices(ImageLoadState.Ready).Length == 3);
        var fullImages = Enumerable.Range(0, 3).Select(i => f.Store.GetFullImage(i).Image!).ToList();

        f.Viewport.ShowGrid();

        Assert.Empty(f.FullIndices(ImageLoadState.Ready));
        Assert.All(fullImages, image => Assert.True(image.IsDisposed));
        Assert.All(Enumerable.Range(0, 3), i => Assert.False(f.Store.GetThumbnail(i).Image!.IsDisposed));
    });

    [Fact]
    public void ReleasedImage_IsStillAliveWhileItsChangeEventIsRaised() => SingleThreadedContext.Run(async () =>
    {
        using var f = new Fixture();
        await f.LoadFolderAsync(3);
        f.Viewport.ShowSingle(1);
        await SingleThreadedContext.WaitUntilAsync(() => f.FullIndices(ImageLoadState.Ready).Length == 3);
        var displayed = f.Store.GetFullImage(1).Image!;
        bool? disposedDuringEvent = null;
        f.Store.FullImageChanged += (_, index) =>
        {
            if (index == 1)
            {
                disposedDuringEvent = displayed.IsDisposed;
            }
        };

        f.Viewport.ShowGrid();

        Assert.False(disposedDuringEvent);
        Assert.True(displayed.IsDisposed);
    });

    [Fact]
    public void LoadFinishingAfterRelease_IsDiscardedAndDisposed() => SingleThreadedContext.Run(async () =>
    {
        using var f = new Fixture();
        var images = Fixture.References(3);
        await f.LoadFolderAsync(images);
        await f.AllThumbnailsSettledAsync();
        f.Loader.Close(images[1]);

        f.Viewport.ShowSingle(1);
        await SingleThreadedContext.WaitUntilAsync(() => f.Loader.Started(images[1]) == 2);
        f.Viewport.ShowGrid();
        f.Loader.Open(images[1]);
        await Task.Delay(100);

        Assert.Equal(ImageLoadState.NotLoaded, f.Store.GetFullImage(1).State);
        Assert.All(f.Factory.Created.Where(i => i.Width == GatedImageLoader.Width), i => Assert.True(i.IsDisposed));
    });

    [Fact]
    public void FullSizeLoads_TakePriorityOverBackgroundThumbnails() => SingleThreadedContext.Run(async () =>
    {
        using var f = new Fixture(maxConcurrentThumbnails: 1);
        var images = Fixture.References(8);
        f.Loader.Close(images[0]); // the single thumbnail worker is stuck on image 0
        f.Loader.Close(images[6]); // the current full-size image stays loading
        await f.LoadFolderAsync(images);
        await SingleThreadedContext.WaitUntilAsync(() => f.Loader.Started(images[0]) == 1);

        f.Viewport.ShowSingle(6);
        f.Loader.Open(images[0]); // worker is free, but a full-size load is still active
        await SingleThreadedContext.WaitUntilAsync(() => f.Store.GetThumbnail(0).State == ImageLoadState.Ready);
        await Task.Delay(100);

        Assert.Equal(0, f.Loader.Started(images[1]));

        f.Loader.Open(images[6]);
        await f.AllThumbnailsSettledAsync();
        Assert.Equal(1, f.Loader.Started(images[1]));
    });

    [Fact]
    public void FullSizeLoad_ProducesMissingThumbnail_SoItIsNotDecodedAgain() => SingleThreadedContext.Run(async () =>
    {
        using var f = new Fixture(maxConcurrentThumbnails: 1);
        var images = Fixture.References(6);
        f.Loader.Close(images[0]);
        await f.LoadFolderAsync(images);
        await SingleThreadedContext.WaitUntilAsync(() => f.Loader.Started(images[0]) == 1);

        f.Viewport.ShowSingle(4); // window 3, 4, 5 — none have thumbnails yet
        await SingleThreadedContext.WaitUntilAsync(() => f.FullIndices(ImageLoadState.Ready).Length == 3);

        Assert.Equal(ImageLoadState.Ready, f.Store.GetThumbnail(4).State);
        Assert.Equal((100, 75), (f.Store.GetThumbnail(4).Image!.Width, f.Store.GetThumbnail(4).Image!.Height));

        f.Loader.Open(images[0]);
        await f.AllThumbnailsSettledAsync();
        Assert.Equal(1, f.Loader.Started(images[3]));
        Assert.Equal(1, f.Loader.Started(images[4]));
        Assert.Equal(1, f.Loader.Started(images[5]));
    });

    [Fact]
    public void DecodeFailure_MarksThumbnailAndFullImageFailed() => SingleThreadedContext.Run(async () =>
    {
        using var f = new Fixture();
        var images = Fixture.References(3);
        f.Loader.Failing[images[1].FullPath] = true;
        await f.LoadFolderAsync(images);
        await f.AllThumbnailsSettledAsync();

        f.Viewport.ShowSingle(1);
        await SingleThreadedContext.WaitUntilAsync(() => f.Store.GetFullImage(1).State == ImageLoadState.Failed);

        Assert.Equal(ImageLoadState.Failed, f.Store.GetThumbnail(1).State);
        Assert.Contains("image01.png", f.Store.GetThumbnail(1).Error);
        Assert.Contains("image01.png", f.Store.GetFullImage(1).Error);
        Assert.Null(f.Store.GetFullImage(1).Image);
        Assert.Equal(ImageLoadState.Ready, f.Store.GetFullImage(2).State);
    });

    [Fact]
    public void NewFolder_ResetsSlots_CancelsWork_AndDisposesPreviousImages() => SingleThreadedContext.Run(async () =>
    {
        using var f = new Fixture(maxConcurrentThumbnails: 1);
        var first = Fixture.References(8, @"C:\first");
        f.Loader.Close(first[1]); // leave the first folder's thumbnail generation unfinished
        await f.LoadFolderAsync(first);
        await SingleThreadedContext.WaitUntilAsync(() => f.Loader.Started(first[1]) == 1);
        f.Viewport.ShowSingle(3);
        await SingleThreadedContext.WaitUntilAsync(() => f.FullIndices(ImageLoadState.Ready).Length == 3);
        var oldImages = f.Factory.Created.ToList();
        var resetImagesCount = -1;
        f.Store.ImagesReset += (_, _) => resetImagesCount = f.Store.Images.Count;

        var second = Fixture.References(2, @"C:\second");
        await f.LoadFolderAsync(second);
        f.Loader.Open(first[1]);
        await f.AllThumbnailsSettledAsync();
        await SingleThreadedContext.WaitUntilAsync(() => f.FullIndices(ImageLoadState.Ready).Length == 2);

        Assert.Equal(2, resetImagesCount);
        Assert.Equal(second, f.Store.Images);
        Assert.All(oldImages, image => Assert.True(image.IsDisposed));
        // Window was 2–4 and the only worker was stuck on 1, so 5–7 were never started — and never will be.
        Assert.Equal(0, f.Loader.Started(first[5]) + f.Loader.Started(first[6]) + f.Loader.Started(first[7]));
        Assert.Equal([0, 1], f.FullIndices(ImageLoadState.Ready)); // still single mode: new first image + neighbour
    });

    [Fact]
    public void Dispose_DisposesEveryHeldImage() => SingleThreadedContext.Run(async () =>
    {
        var f = new Fixture();
        await f.LoadFolderAsync(3);
        await f.AllThumbnailsSettledAsync();
        f.Viewport.ShowSingle(1);
        await SingleThreadedContext.WaitUntilAsync(() => f.FullIndices(ImageLoadState.Ready).Length == 3);

        f.Dispose();

        Assert.Equal(6, f.Factory.Created.Count);
        Assert.All(f.Factory.Created, image => Assert.True(image.IsDisposed));
    });
}
