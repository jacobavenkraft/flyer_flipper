using FlyerFlipper.Core.Source;
using FlyerFlipper.Infrastructure.Source;
using FlyerFlipper.Tests.TestSupport;

namespace FlyerFlipper.Tests.Source;

public class FileSystemImageSourceTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FileSystemImageSource _source = new();

    public void Dispose() => _temp.Dispose();

    private string[] FileNames(IReadOnlyList<ImageReference> images)
        => images.Select(i => Path.GetRelativePath(_temp.Path, i.FullPath)).ToArray();

    [Fact]
    public void Enumerate_DefaultQuery_ReturnsOnlySupportedFormats_CaseInsensitive()
    {
        _temp.CreateFile("a.jpg");
        _temp.CreateFile("b.JPEG");
        _temp.CreateFile("c.png");
        _temp.CreateFile("d.bmp");
        _temp.CreateFile("e.webp");
        _temp.CreateFile("f.gif");
        _temp.CreateFile("g.txt");
        _temp.CreateFile("h.jpg.bak");

        var images = _source.Enumerate(new ImageSourceQuery(_temp.Path));

        Assert.Equal(["a.jpg", "b.JPEG", "c.png", "d.bmp", "e.webp"], FileNames(images));
    }

    [Fact]
    public void Enumerate_NonRecursive_IgnoresSubfolders()
    {
        _temp.CreateFile("top.png");
        _temp.CreateFile(Path.Combine("sub", "nested.png"));

        var images = _source.Enumerate(new ImageSourceQuery(_temp.Path));

        Assert.Equal(["top.png"], FileNames(images));
    }

    [Fact]
    public void Enumerate_Recursive_IncludesSubfolders()
    {
        _temp.CreateFile("top.png");
        _temp.CreateFile(Path.Combine("sub", "nested.png"));
        _temp.CreateFile(Path.Combine("sub", "deeper", "deepest.jpg"));

        var images = _source.Enumerate(new ImageSourceQuery(_temp.Path, Recursive: true));

        Assert.Equal(
            [Path.Combine("sub", "deeper", "deepest.jpg"), Path.Combine("sub", "nested.png"), "top.png"],
            FileNames(images));
    }

    [Fact]
    public void Enumerate_CustomFormatWildcard_RestrictsFormats()
    {
        _temp.CreateFile("a.jpg");
        _temp.CreateFile("b.png");
        _temp.CreateFile("c.webp");

        var images = _source.Enumerate(new ImageSourceQuery(_temp.Path, FormatWildcard: " *.png , *.WEBP "));

        Assert.Equal(["b.png", "c.webp"], FileNames(images));
    }

    [Fact]
    public void Enumerate_NameWildcard_IsCombinedWithFormatWildcard()
    {
        _temp.CreateFile("IMG_001.jpg");
        _temp.CreateFile("img_002.png");
        _temp.CreateFile("PIC_003.jpg");
        _temp.CreateFile("IMG_004.txt");

        var images = _source.Enumerate(new ImageSourceQuery(_temp.Path, NameWildcard: "IMG_*"));

        Assert.Equal(["IMG_001.jpg", "img_002.png"], FileNames(images));
    }

    [Fact]
    public void Enumerate_MultipleNameWildcards_MatchAny()
    {
        _temp.CreateFile("IMG_001.jpg");
        _temp.CreateFile("PIC_002.jpg");
        _temp.CreateFile("DSC_003.jpg");

        var images = _source.Enumerate(new ImageSourceQuery(_temp.Path, NameWildcard: "IMG_*;PIC_*"));

        Assert.Equal(["IMG_001.jpg", "PIC_002.jpg"], FileNames(images));
    }

    [Fact]
    public void Enumerate_BlankWildcards_MatchEverything()
    {
        _temp.CreateFile("a.png");
        _temp.CreateFile("b.txt");

        var images = _source.Enumerate(new ImageSourceQuery(_temp.Path, FormatWildcard: "", NameWildcard: " ; "));

        Assert.Equal(["a.png", "b.txt"], FileNames(images));
    }

    [Fact]
    public void Enumerate_ResultsAreSortedByPath()
    {
        _temp.CreateFile("c.png");
        _temp.CreateFile("A.png");
        _temp.CreateFile("b.png");

        var images = _source.Enumerate(new ImageSourceQuery(_temp.Path));

        Assert.Equal(["A.png", "b.png", "c.png"], FileNames(images));
    }

    [Fact]
    public void Enumerate_ReturnsAbsolutePaths_ForRelativeRoot()
    {
        _temp.CreateFile("a.png");
        var relativeRoot = Path.GetRelativePath(Environment.CurrentDirectory, _temp.Path);

        var images = _source.Enumerate(new ImageSourceQuery(relativeRoot));

        var image = Assert.Single(images);
        Assert.True(Path.IsPathFullyQualified(image.FullPath));
        Assert.Equal("a.png", image.FileName);
    }

    [Fact]
    public void Enumerate_EmptyFolder_ReturnsEmpty()
    {
        Assert.Empty(_source.Enumerate(new ImageSourceQuery(_temp.Path)));
    }

    [Fact]
    public void Enumerate_MissingFolder_ThrowsDirectoryNotFound()
    {
        var missing = Path.Combine(_temp.Path, "does-not-exist");

        Assert.Throws<DirectoryNotFoundException>(() => _source.Enumerate(new ImageSourceQuery(missing)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Enumerate_BlankRoot_ThrowsArgumentException(string root)
    {
        Assert.Throws<ArgumentException>(() => _source.Enumerate(new ImageSourceQuery(root)));
    }

    [Fact]
    public void Enumerate_CancelledToken_Throws()
    {
        _temp.CreateFile("a.png");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => _source.Enumerate(new ImageSourceQuery(_temp.Path), cts.Token));
    }
}
