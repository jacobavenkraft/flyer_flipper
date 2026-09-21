using FlyerFlipper.Core.Source;
using FlyerFlipper.Tests.TestSupport;
using FlyerFlipper.UI.ViewModels;
using Moq;

namespace FlyerFlipper.Tests.ViewModels;

public class ImageSourceViewModelTests
{
    private readonly Mock<IImageCatalog> _catalog = new();
    private readonly StubFolderPicker _picker = new();

    [Theory]
    [InlineData(@"C:\flyers", @"C:\flyers")]
    [InlineData(@"  C:\flyers  ", @"C:\flyers")]
    [InlineData(@"""C:\my flyers""", @"C:\my flyers")]
    [InlineData(@" ""C:\my flyers"" ", @"C:\my flyers")]
    [InlineData(null, "")]
    [InlineData("  ", "")]
    public void NormalizePath_TrimsWhitespaceAndQuotes(string? input, string expected)
    {
        Assert.Equal(expected, ImageSourceViewModel.NormalizePath(input));
    }

    [Fact]
    public async Task LoadFolder_BlankPath_ShowsErrorWithoutLoading()
    {
        var vm = new ImageSourceViewModel(_catalog.Object, _picker) { FolderPath = "   " };

        await vm.LoadFolderCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        _catalog.Verify(c => c.LoadAsync(It.IsAny<ImageSourceQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoadFolder_LoadsNormalizedRootPathOnly_AndReportsCount()
    {
        ImageSourceQuery? loaded = null;
        _catalog.Setup(c => c.LoadAsync(It.IsAny<ImageSourceQuery>(), It.IsAny<CancellationToken>()))
            .Callback<ImageSourceQuery, CancellationToken>((q, _) => loaded = q)
            .Returns(Task.CompletedTask);
        _catalog.SetupGet(c => c.Images).Returns([new(@"C:\flyers\a.png"), new(@"C:\flyers\b.png")]);
        var vm = new ImageSourceViewModel(_catalog.Object, _picker) { FolderPath = @" ""C:\flyers"" " };

        await vm.LoadFolderCommand.ExecuteAsync(null);

        Assert.Equal(new ImageSourceQuery(@"C:\flyers"), loaded);
        Assert.False(vm.HasError);
        Assert.Equal("2 images", vm.StatusMessage);
    }

    [Fact]
    public async Task LoadFolder_MissingFolder_ShowsErrorMessage()
    {
        _catalog.Setup(c => c.LoadAsync(It.IsAny<ImageSourceQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DirectoryNotFoundException("Folder not found: C:\\nope"));
        var vm = new ImageSourceViewModel(_catalog.Object, _picker) { FolderPath = @"C:\nope" };

        await vm.LoadFolderCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Equal("Folder not found: C:\\nope", vm.StatusMessage);
    }

    [Fact]
    public async Task LoadFolder_NewLoad_CancelsPreviousLoad()
    {
        var firstStarted = new TaskCompletionSource();
        CancellationToken firstToken = default;
        var call = 0;
        _catalog.Setup(c => c.LoadAsync(It.IsAny<ImageSourceQuery>(), It.IsAny<CancellationToken>()))
            .Returns<ImageSourceQuery, CancellationToken>(async (_, ct) =>
            {
                if (Interlocked.Increment(ref call) == 1)
                {
                    firstToken = ct;
                    firstStarted.SetResult();
                    await Task.Delay(Timeout.Infinite, ct);
                }
            });
        _catalog.SetupGet(c => c.Images).Returns([]);
        var vm = new ImageSourceViewModel(_catalog.Object, _picker) { FolderPath = @"C:\one" };

        var first = vm.LoadFolderCommand.ExecuteAsync(null);
        await firstStarted.Task;
        vm.FolderPath = @"C:\two";
        await vm.LoadFolderCommand.ExecuteAsync(null);
        await first;

        Assert.True(firstToken.IsCancellationRequested);
        Assert.False(vm.HasError);
        Assert.Equal("0 images", vm.StatusMessage);
    }

    // ---- Browse ---------------------------------------------------------------------------------

    [Fact]
    public async Task Browse_PickingAFolder_SetsThePathAndLoadsIt()
    {
        ImageSourceQuery? loaded = null;
        _catalog.Setup(c => c.LoadAsync(It.IsAny<ImageSourceQuery>(), It.IsAny<CancellationToken>()))
            .Callback<ImageSourceQuery, CancellationToken>((q, _) => loaded = q)
            .Returns(Task.CompletedTask);
        _catalog.SetupGet(c => c.Images).Returns([new(@"C:\picked\a.png")]);
        var vm = new ImageSourceViewModel(_catalog.Object, _picker);
        _picker.NextPickedFolder = @"C:\picked";

        await vm.BrowseCommand.ExecuteAsync(null);

        Assert.Equal(@"C:\picked", vm.FolderPath);
        Assert.Equal(new ImageSourceQuery(@"C:\picked"), loaded);
        Assert.Equal("1 image", vm.StatusMessage);
    }

    [Fact]
    public async Task Browse_Cancelled_ChangesNothing()
    {
        var vm = new ImageSourceViewModel(_catalog.Object, _picker) { FolderPath = @"C:\existing" };
        _picker.NextPickedFolder = null;

        await vm.BrowseCommand.ExecuteAsync(null);

        // In particular the path already typed is not cleared.
        Assert.Equal(@"C:\existing", vm.FolderPath);
        _catalog.Verify(c => c.LoadAsync(It.IsAny<ImageSourceQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Browse_StartsAtTheCurrentFolder_Normalized()
    {
        // The picker should open where the user already is, quotes and whitespace removed.
        var vm = new ImageSourceViewModel(_catalog.Object, _picker) { FolderPath = @" ""C:\my flyers"" " };

        await vm.BrowseCommand.ExecuteAsync(null);

        Assert.Equal(1, _picker.Calls);
        Assert.Equal(@"C:\my flyers", _picker.LastStartIn);
    }
}
