using FlyerFlipper.Core.Imaging;
using FlyerFlipper.Imaging;

namespace FlyerFlipper.Tests.Imaging;

public class SkiaThumbnailServiceTests
{
    private readonly SkiaThumbnailService _service = new();

    private static ImageBuffer SolidBuffer(int width, int height, byte b, byte g, byte r)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = b;
            pixels[i + 1] = g;
            pixels[i + 2] = r;
            pixels[i + 3] = 255;
        }

        return new ImageBuffer(pixels, width, height, width * 4, ImagePixelFormat.Bgra8888Premultiplied);
    }

    [Fact]
    public void CreateThumbnail_LargeImage_ScalesToFitAndKeepsColor()
    {
        var source = SolidBuffer(400, 200, b: 10, g: 20, r: 200);

        var thumbnail = _service.CreateThumbnail(source, 100);

        Assert.Equal((100, 50), (thumbnail.Width, thumbnail.Height));
        Assert.Equal(ImagePixelFormat.Bgra8888Premultiplied, thumbnail.Format);
        Assert.True(thumbnail.Pixels.Length >= thumbnail.Stride * thumbnail.Height);
        var center = thumbnail.Pixels.Span.Slice((25 * thumbnail.Stride) + (50 * 4), 4).ToArray();
        Assert.Equal(new byte[] { 10, 20, 200, 255 }, center);
    }

    [Fact]
    public void CreateThumbnail_SmallImage_IsNotUpscaled_AndIsACopy()
    {
        var source = SolidBuffer(40, 30, b: 1, g: 2, r: 3);

        var thumbnail = _service.CreateThumbnail(source, 100);

        Assert.Equal((40, 30), (thumbnail.Width, thumbnail.Height));
        Assert.True(source.Pixels.Span.SequenceEqual(thumbnail.Pixels.Span[..source.Pixels.Length]));
        Assert.False(source.Pixels.Equals(thumbnail.Pixels));
    }

    [Fact]
    public void CreateThumbnail_HonoursSourceStridePadding()
    {
        // 2x2 with 4 bytes of row padding; row 0 red, row 1 blue.
        byte[] pixels =
        [
            0, 0, 255, 255, 0, 0, 255, 255, 0xEE, 0xEE, 0xEE, 0xEE,
            255, 0, 0, 255, 255, 0, 0, 255,
        ];
        var source = new ImageBuffer(pixels, 2, 2, 12, ImagePixelFormat.Bgra8888Premultiplied);

        var thumbnail = _service.CreateThumbnail(source, 10);

        var row1 = thumbnail.Pixels.Span.Slice(thumbnail.Stride, 4).ToArray();
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, row1);
    }
}
