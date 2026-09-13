using FlyerFlipper.Core.Imaging;
using FlyerFlipper.Core.Source;
using FlyerFlipper.Imaging;
using FlyerFlipper.Tests.TestSupport;
using SkiaSharp;

namespace FlyerFlipper.Tests.Imaging;

public class SkiaImageLoaderTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly SkiaImageLoader _loader = new();

    public void Dispose() => _temp.Dispose();

    private static (byte B, byte G, byte R, byte A) PixelAt(ImageBuffer buffer, int x, int y)
    {
        var span = buffer.Pixels.Span.Slice((y * buffer.Stride) + (x * 4), 4);
        return (span[0], span[1], span[2], span[3]);
    }

    [Fact]
    public void Load_Png_DecodesExactPixels_AsBgraPremultiplied()
    {
        using var bitmap = SkiaTestImages.CreateDistinct3x2();
        var path = _temp.CreateFile("image.png", SkiaTestImages.Encode(bitmap, SKEncodedImageFormat.Png));
        var reference = new ImageReference(path);

        var image = _loader.Load(reference);

        Assert.Same(reference, image.Reference);
        Assert.Equal(3, image.Buffer.Width);
        Assert.Equal(2, image.Buffer.Height);
        Assert.Equal(ImagePixelFormat.Bgra8888Premultiplied, image.Buffer.Format);
        Assert.Equal(((byte)0, (byte)0, (byte)255, (byte)255), PixelAt(image.Buffer, 0, 0)); // red
        Assert.Equal(((byte)255, (byte)0, (byte)0, (byte)255), PixelAt(image.Buffer, 2, 0)); // blue
        Assert.Equal(((byte)255, (byte)0, (byte)255, (byte)255), PixelAt(image.Buffer, 2, 1)); // magenta
    }

    [Fact]
    public void Load_Bmp_Decodes()
    {
        var pixels = new SKColor[,] { { SKColors.Red, SKColors.Lime }, { SKColors.Blue, SKColors.White } };
        var path = _temp.CreateFile("image.bmp", SkiaTestImages.CreateBmp24(pixels));

        var image = _loader.Load(new ImageReference(path));

        Assert.Equal((2, 2), (image.Buffer.Width, image.Buffer.Height));
        Assert.Equal(((byte)0, (byte)0, (byte)255, (byte)255), PixelAt(image.Buffer, 0, 0));
        Assert.Equal(((byte)255, (byte)0, (byte)0, (byte)255), PixelAt(image.Buffer, 0, 1));
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg, "image.jpg")]
    [InlineData(SKEncodedImageFormat.Webp, "image.webp")]
    public void Load_LossyFormats_DecodeWithCorrectDimensions(SKEncodedImageFormat format, string fileName)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(64, 32, SKColorType.Bgra8888, SKAlphaType.Premul));
        bitmap.Erase(SKColors.Red);
        var path = _temp.CreateFile(fileName, SkiaTestImages.Encode(bitmap, format));

        var image = _loader.Load(new ImageReference(path));

        Assert.Equal((64, 32), (image.Buffer.Width, image.Buffer.Height));
        var (b, g, r, a) = PixelAt(image.Buffer, 32, 16);
        Assert.True(r > 200 && g < 60 && b < 60 && a == 255, $"Expected red-ish, got R{r} G{g} B{b} A{a}.");
    }

    [Fact]
    public void Load_CorruptFile_ThrowsImageLoadException()
    {
        var path = _temp.CreateFile("broken.png", "definitely not a png"u8.ToArray());

        var ex = Assert.Throws<ImageLoadException>(() => _loader.Load(new ImageReference(path)));
        Assert.Contains("broken.png", ex.Message);
    }

    [Fact]
    public void Load_MissingFile_ThrowsIOException()
    {
        var path = Path.Combine(_temp.Path, "missing.png");

        Assert.ThrowsAny<IOException>(() => _loader.Load(new ImageReference(path)));
    }

    [Fact]
    public void Load_CancelledToken_Throws()
    {
        using var bitmap = SkiaTestImages.CreateDistinct3x2();
        var path = _temp.CreateFile("image.png", SkiaTestImages.Encode(bitmap, SKEncodedImageFormat.Png));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => _loader.Load(new ImageReference(path), cts.Token));
    }
}
