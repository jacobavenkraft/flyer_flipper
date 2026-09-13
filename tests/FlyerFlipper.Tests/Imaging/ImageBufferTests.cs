using FlyerFlipper.Core.Imaging;

namespace FlyerFlipper.Tests.Imaging;

public class ImageBufferTests
{
    [Fact]
    public void Constructor_ValidBuffer_ExposesProperties()
    {
        var pixels = new byte[2 * 3 * 4];

        var buffer = new ImageBuffer(pixels, 2, 3, 8, ImagePixelFormat.Bgra8888Premultiplied);

        Assert.Equal(2, buffer.Width);
        Assert.Equal(3, buffer.Height);
        Assert.Equal(8, buffer.Stride);
        Assert.Equal(pixels.Length, buffer.Pixels.Length);
    }

    [Fact]
    public void Constructor_AllowsPaddedStride_WithoutTrailingPaddingOnLastRow()
    {
        // 2 rows, stride 12 (8 bytes of pixels + 4 padding); last row needs no padding.
        var pixels = new byte[12 + 8];

        var buffer = new ImageBuffer(pixels, 2, 2, 12, ImagePixelFormat.Bgra8888Premultiplied);

        Assert.Equal(12, buffer.Stride);
    }

    [Theory]
    [InlineData(0, 1, 4)]
    [InlineData(1, 0, 4)]
    [InlineData(-1, 1, 4)]
    [InlineData(2, 1, 7)]  // stride shorter than a row
    [InlineData(2, 1, 10)] // stride not a whole number of pixels
    public void Constructor_InvalidDimensionsOrStride_Throws(int width, int height, int stride)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new ImageBuffer(new byte[64], width, height, stride, ImagePixelFormat.Bgra8888Premultiplied));
    }

    [Fact]
    public void Constructor_BufferTooSmall_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new ImageBuffer(new byte[15], 2, 2, 8, ImagePixelFormat.Bgra8888Premultiplied));
    }
}
