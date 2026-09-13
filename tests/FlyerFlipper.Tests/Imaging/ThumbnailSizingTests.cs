using FlyerFlipper.Core.Imaging;

namespace FlyerFlipper.Tests.Imaging;

public class ThumbnailSizingTests
{
    [Theory]
    [InlineData(4000, 3000, 256, 256, 192)] // landscape
    [InlineData(3000, 4000, 256, 192, 256)] // portrait
    [InlineData(1000, 1000, 256, 256, 256)] // square
    [InlineData(100, 50, 256, 100, 50)]     // smaller than box: never upscaled
    [InlineData(256, 100, 256, 256, 100)]   // exactly on the edge
    [InlineData(10000, 1, 256, 256, 1)]     // extreme aspect keeps at least 1px
    public void Fit_PreservesAspectRatio_WithinBox(int width, int height, int maxEdge, int expectedWidth, int expectedHeight)
    {
        Assert.Equal((expectedWidth, expectedHeight), ThumbnailSizing.Fit(width, height, maxEdge));
    }

    [Theory]
    [InlineData(0, 10, 10)]
    [InlineData(10, 0, 10)]
    [InlineData(10, 10, 0)]
    public void Fit_NonPositiveArguments_Throw(int width, int height, int maxEdge)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ThumbnailSizing.Fit(width, height, maxEdge));
    }
}
