using FlyerFlipper.Imaging;
using SkiaSharp;

namespace FlyerFlipper.Tests.Imaging;

public class SkiaOrientationTests
{
    private const int SourceWidth = 3;
    private const int SourceHeight = 2;

    [Fact]
    public void Apply_Upright_ReturnsNull()
    {
        using var source = SkiaTestImages.CreateDistinct3x2();

        Assert.Null(SkiaOrientation.Apply(source, SKEncodedOrigin.TopLeft));
    }

    // Each case maps a source pixel (x, y) to where it must land in the upright image,
    // following the EXIF orientation definitions.
    public static TheoryData<SKEncodedOrigin> Origins =>
    [
        SKEncodedOrigin.TopRight,
        SKEncodedOrigin.BottomRight,
        SKEncodedOrigin.BottomLeft,
        SKEncodedOrigin.LeftTop,
        SKEncodedOrigin.RightTop,
        SKEncodedOrigin.RightBottom,
        SKEncodedOrigin.LeftBottom,
    ];

    private static (int X, int Y) ExpectedDestination(SKEncodedOrigin origin, int x, int y) => origin switch
    {
        SKEncodedOrigin.TopRight => (SourceWidth - 1 - x, y),
        SKEncodedOrigin.BottomRight => (SourceWidth - 1 - x, SourceHeight - 1 - y),
        SKEncodedOrigin.BottomLeft => (x, SourceHeight - 1 - y),
        SKEncodedOrigin.LeftTop => (y, x),
        SKEncodedOrigin.RightTop => (SourceHeight - 1 - y, x),
        SKEncodedOrigin.RightBottom => (SourceHeight - 1 - y, SourceWidth - 1 - x),
        SKEncodedOrigin.LeftBottom => (y, SourceWidth - 1 - x),
        _ => throw new ArgumentOutOfRangeException(nameof(origin)),
    };

    [Theory]
    [MemberData(nameof(Origins))]
    public void Apply_MovesEveryPixelToItsUprightPosition(SKEncodedOrigin origin)
    {
        using var source = SkiaTestImages.CreateDistinct3x2();

        using var result = SkiaOrientation.Apply(source, origin);

        Assert.NotNull(result);
        var swapsAxes = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        Assert.Equal(swapsAxes ? (SourceHeight, SourceWidth) : (SourceWidth, SourceHeight), (result.Width, result.Height));

        for (var y = 0; y < SourceHeight; y++)
        {
            for (var x = 0; x < SourceWidth; x++)
            {
                var (dx, dy) = ExpectedDestination(origin, x, y);
                Assert.Equal(source.GetPixel(x, y), result.GetPixel(dx, dy));
            }
        }
    }
}
