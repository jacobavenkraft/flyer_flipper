using FlyerFlipper.Core.Source;
using Moq;
using FlyerFlipper.Tests.TestSupport;

namespace FlyerFlipper.Tests.Source;

public class ImageCatalogTests
{
    private readonly Mock<IImageSource> _source = new();

    [Fact]
    public void NewCatalog_IsEmpty()
    {
        var catalog = new ImageCatalog(_source.Object);

        Assert.Empty(catalog.Images);
        Assert.Null(catalog.Query);
    }

    [Fact]
    public async Task LoadAsync_ReplacesImagesAndQuery_AndRaisesImagesChanged()
    {
        var query = new ImageSourceQuery(TestPaths.Folder("flyers"));
        ImageReference[] images = [new(TestPaths.File("flyers", "a.png")), new(TestPaths.File("flyers", "b.png"))];
        _source.Setup(s => s.Enumerate(query, It.IsAny<CancellationToken>())).Returns(images);
        var catalog = new ImageCatalog(_source.Object);
        var raised = 0;
        catalog.ImagesChanged += (_, _) => raised++;

        await catalog.LoadAsync(query);

        Assert.Equal(images, catalog.Images);
        Assert.Same(query, catalog.Query);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task LoadAsync_WhenSourceThrows_KeepsPreviousImages_AndDoesNotRaise()
    {
        var good = new ImageSourceQuery(TestPaths.Folder("good"));
        var bad = new ImageSourceQuery(TestPaths.Folder("bad"));
        ImageReference[] images = [new(TestPaths.File("good", "a.png"))];
        _source.Setup(s => s.Enumerate(good, It.IsAny<CancellationToken>())).Returns(images);
        _source.Setup(s => s.Enumerate(bad, It.IsAny<CancellationToken>())).Throws(new DirectoryNotFoundException());
        var catalog = new ImageCatalog(_source.Object);
        await catalog.LoadAsync(good);
        var raised = 0;
        catalog.ImagesChanged += (_, _) => raised++;

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => catalog.LoadAsync(bad));

        Assert.Equal(images, catalog.Images);
        Assert.Same(good, catalog.Query);
        Assert.Equal(0, raised);
    }

    [Fact]
    public async Task LoadAsync_CancelledDuringEnumeration_DoesNotReplaceImages()
    {
        using var cts = new CancellationTokenSource();
        var query = new ImageSourceQuery(TestPaths.Folder("flyers"));
        _source.Setup(s => s.Enumerate(query, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                cts.Cancel();
                return [new ImageReference(TestPaths.File("flyers", "a.png"))];
            });
        var catalog = new ImageCatalog(_source.Object);
        var raised = 0;
        catalog.ImagesChanged += (_, _) => raised++;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => catalog.LoadAsync(query, cts.Token));

        Assert.Empty(catalog.Images);
        Assert.Equal(0, raised);
    }

    [Fact]
    public async Task LoadAsync_PassesCancellationTokenToSource()
    {
        using var cts = new CancellationTokenSource();
        var query = new ImageSourceQuery(TestPaths.Folder("flyers"));
        _source.Setup(s => s.Enumerate(query, cts.Token)).Returns([]);
        var catalog = new ImageCatalog(_source.Object);

        await catalog.LoadAsync(query, cts.Token);

        _source.Verify(s => s.Enumerate(query, cts.Token), Times.Once);
    }
}
