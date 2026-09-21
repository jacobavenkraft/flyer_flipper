using FlyerFlipper.Core.Imaging;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;
using FlyerFlipper.Core.Source;
using FlyerFlipper.Imaging.Processors;
using FlyerFlipper.Tests.TestSupport;

namespace FlyerFlipper.Tests.Imaging;

/// <summary>
/// Flip and rotate. Every pixel carries its own source coordinates, so the assertions pin exactly where
/// each pixel lands rather than just checking the output's size.
/// </summary>
public class GeometryProcessorTests
{
    /// <summary>An image whose pixel at (x, y) is B=x, G=y, R=0, A=255 — each pixel names itself.</summary>
    private static ProcessedImage Ramp(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = ((y * width) + x) * 4;
                pixels[i] = (byte)x;
                pixels[i + 1] = (byte)y;
                pixels[i + 2] = 0;
                pixels[i + 3] = 255;
            }
        }

        var buffer = new ImageBuffer(pixels, width, height, width * 4, ImagePixelFormat.Bgra8888Premultiplied);
        return ProcessedImage.FromSource(new SourceImage(new ImageReference(TestPaths.File("flyers", "gig.png")), buffer));
    }

    /// <summary>The source coordinates recorded in the output pixel at (x, y).</summary>
    private static (int X, int Y) SourceOf(ImageBuffer buffer, int x, int y)
    {
        var pixel = buffer.Pixels.Span.Slice((y * buffer.Stride) + (x * 4), 4);
        return (pixel[0], pixel[1]);
    }

    private static FlipProcessor Flip(bool enabled, bool horizontal = false, bool vertical = false)
        => new(new ProcessorSettings<FlipOptions>(new FlipOptions(enabled, horizontal, vertical)));

    private static RotateProcessor Rotate(bool enabled, RotationAngle angle)
        => new(new ProcessorSettings<RotateOptions>(new RotateOptions(enabled, angle)));

    /// <summary>Asserts every output pixel came from where <paramref name="expected"/> says it did.</summary>
    private static void AssertMapping(ImageBuffer output, int width, int height, Func<int, int, (int X, int Y)> expected)
    {
        Assert.Equal((width, height), (output.Width, output.Height));

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                Assert.Equal(expected(x, y), SourceOf(output, x, y));
            }
        }
    }

    // ---- Flip ----------------------------------------------------------------------------------

    [Fact]
    public void Flip_Disabled_PassesThroughSameInstance()
    {
        using var processor = Flip(enabled: false, horizontal: true, vertical: true);
        var input = Ramp(4, 3);

        Assert.Same(input, processor.Process(input, CancellationToken.None));
    }

    [Fact]
    public void Flip_EnabledWithNoAxisChosen_PassesThroughSameInstance()
    {
        using var processor = Flip(enabled: true);
        var input = Ramp(4, 3);

        Assert.Same(input, processor.Process(input, CancellationToken.None));
    }

    [Fact]
    public void Flip_MirrorHorizontally_SwapsLeftAndRight()
    {
        using var processor = Flip(enabled: true, horizontal: true);

        var output = processor.Process(Ramp(4, 3), CancellationToken.None);

        AssertMapping(output.Buffer, 4, 3, (x, y) => (3 - x, y));
    }

    [Fact]
    public void Flip_MirrorVertically_SwapsTopAndBottom()
    {
        using var processor = Flip(enabled: true, vertical: true);

        var output = processor.Process(Ramp(4, 3), CancellationToken.None);

        AssertMapping(output.Buffer, 4, 3, (x, y) => (x, 2 - y));
    }

    [Fact]
    public void Flip_BothAxes_MatchesA180Rotation()
    {
        using var flip = Flip(enabled: true, horizontal: true, vertical: true);
        using var rotate = Rotate(enabled: true, RotationAngle.Clockwise180);

        var flipped = flip.Process(Ramp(4, 3), CancellationToken.None).Buffer;
        var rotated = rotate.Process(Ramp(4, 3), CancellationToken.None).Buffer;

        AssertMapping(flipped, 4, 3, (x, y) => SourceOf(rotated, x, y));
    }

    [Fact]
    public void Flip_KeepsSourceMetadata()
    {
        using var processor = Flip(enabled: true, horizontal: true);

        var output = processor.Process(Ramp(4, 3), CancellationToken.None);

        Assert.Equal("gig.png", output.Metadata[ImageMetadataKeys.SourceFileName]);
    }

    // ---- Rotate --------------------------------------------------------------------------------

    [Fact]
    public void Rotate_Disabled_PassesThroughSameInstance()
    {
        using var processor = Rotate(enabled: false, RotationAngle.Clockwise90);
        var input = Ramp(4, 3);

        Assert.Same(input, processor.Process(input, CancellationToken.None));
    }

    [Fact]
    public void Rotate_None_PassesThroughSameInstance()
    {
        using var processor = Rotate(enabled: true, RotationAngle.None);
        var input = Ramp(4, 3);

        Assert.Same(input, processor.Process(input, CancellationToken.None));
    }

    [Fact]
    public void Rotate90_TurnsClockwise_AndSwapsDimensions()
    {
        // Source top-left must end up top-right.
        using var processor = Rotate(enabled: true, RotationAngle.Clockwise90);

        var output = processor.Process(Ramp(4, 3), CancellationToken.None);

        AssertMapping(output.Buffer, 3, 4, (x, y) => (y, 2 - x));
        Assert.Equal((0, 0), SourceOf(output.Buffer, 2, 0));
    }

    [Fact]
    public void Rotate180_KeepsDimensions()
    {
        using var processor = Rotate(enabled: true, RotationAngle.Clockwise180);

        var output = processor.Process(Ramp(4, 3), CancellationToken.None);

        AssertMapping(output.Buffer, 4, 3, (x, y) => (3 - x, 2 - y));
    }

    [Fact]
    public void Rotate270_TurnsAnticlockwise_AndSwapsDimensions()
    {
        // Source top-left must end up bottom-left.
        using var processor = Rotate(enabled: true, RotationAngle.Clockwise270);

        var output = processor.Process(Ramp(4, 3), CancellationToken.None);

        AssertMapping(output.Buffer, 3, 4, (x, y) => (3 - y, x));
        Assert.Equal((0, 0), SourceOf(output.Buffer, 0, 3));
    }

    [Fact]
    public void Rotate90_FourTimes_ReturnsTheOriginal()
    {
        using var processor = Rotate(enabled: true, RotationAngle.Clockwise90);
        var image = Ramp(4, 3);

        var turned = image;
        for (var i = 0; i < 4; i++)
        {
            turned = processor.Process(turned, CancellationToken.None);
        }

        AssertMapping(turned.Buffer, 4, 3, (x, y) => (x, y));
    }

    [Fact]
    public void Rotate_KeepsSourceMetadata()
    {
        using var processor = Rotate(enabled: true, RotationAngle.Clockwise90);

        var output = processor.Process(Ramp(4, 3), CancellationToken.None);

        Assert.Equal("gig.png", output.Metadata[ImageMetadataKeys.SourceFileName]);
    }
}
