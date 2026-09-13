using FlyerFlipper.Core.Source;
using FlyerFlipper.UI.ViewModels;
using Moq;

namespace FlyerFlipper.Tests.ViewModels;

public class ImageSourceViewModelTests
{
    private readonly Mock<IImageCatalog> _catalog = new();

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
        var vm = new ImageSourceViewModel(_catalog.Object) { FolderPath = "   " };

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
        var vm = new ImageSourceViewModel(_catalog.Object) { FolderPath = @" ""C:\flyers"" " };

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
        var vm = new ImageSourceViewModel(_catalog.Object) { FolderPath = @"C:\nope" };

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
        var vm = new ImageSourceViewModel(_catalog.Object) { FolderPath = @"C:\one" };

        var first = vm.LoadFolderCommand.ExecuteAsync(null);
        await firstStarted.Task;
        vm.FolderPath = @"C:\two";
        await vm.LoadFolderCommand.ExecuteAsync(null);
        await first;

        Assert.True(firstToken.IsCancellationRequested);
        Assert.False(vm.HasError);
        Assert.Equal("0 images", vm.StatusMessage);
    }
}
