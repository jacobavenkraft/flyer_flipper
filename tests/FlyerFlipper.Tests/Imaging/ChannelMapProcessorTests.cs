using FlyerFlipper.Core.Imaging;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;
using FlyerFlipper.Core.Source;
using FlyerFlipper.Imaging.Processors;
using FlyerFlipper.Tests.TestSupport;

namespace FlyerFlipper.Tests.Imaging;

public class ChannelMapProcessorTests
{
    /// <summary>A one-pixel image with distinct channel values, so any mix-up is visible.</summary>
    private static ProcessedImage Pixel(byte b, byte g, byte r, byte a = 255)
    {
        var buffer = new ImageBuffer(new byte[] { b, g, r, a }, 1, 1, 4, ImagePixelFormat.Bgra8888Premultiplied);
        return ProcessedImage.FromSource(new SourceImage(new ImageReference(TestPaths.File("flyers", "gig.png")), buffer));
    }

    /// <summary>The output pixel as (B, G, R, A).</summary>
    private static (byte B, byte G, byte R, byte A) Read(ImageBuffer buffer)
    {
        var p = buffer.Pixels.Span;
        return (p[0], p[1], p[2], p[3]);
    }

    private static ChannelMapProcessor Map(
        bool enabled, ColorChannel red = ColorChannel.Red, ColorChannel green = ColorChannel.Green, ColorChannel blue = ColorChannel.Blue)
        => new(new ProcessorSettings<ChannelMapOptions>(new ChannelMapOptions(enabled, red, green, blue)));

    [Fact]
    public void Disabled_PassesThroughSameInstance()
    {
        using var processor = Map(enabled: false, red: ColorChannel.Blue);
        var input = Pixel(10, 20, 30);

        Assert.Same(input, processor.Process(input, CancellationToken.None));
    }

    [Fact]
    public void IdentityMap_PassesThroughSameInstance()
    {
        // Enabled but every channel mapped to itself: no work to do.
        using var processor = Map(enabled: true);
        var input = Pixel(10, 20, 30);

        Assert.Same(input, processor.Process(input, CancellationToken.None));
    }

    [Fact]
    public void SwapsRedAndBlue()
    {
        using var processor = Map(enabled: true, red: ColorChannel.Blue, blue: ColorChannel.Red);

        var output = processor.Process(Pixel(b: 10, g: 20, r: 30), CancellationToken.None);

        // A true swap, not a two-step copy that loses one of them.
        Assert.Equal((B: (byte)30, G: (byte)20, R: (byte)10, A: (byte)255), Read(output.Buffer));
    }

    [Fact]
    public void RotatesAllThreeChannels()
    {
        // red←green, green←blue, blue←red: every channel both read and overwritten.
        using var processor = Map(enabled: true, red: ColorChannel.Green, green: ColorChannel.Blue, blue: ColorChannel.Red);

        var output = processor.Process(Pixel(b: 10, g: 20, r: 30), CancellationToken.None);

        Assert.Equal((B: (byte)30, G: (byte)10, R: (byte)20, A: (byte)255), Read(output.Buffer));
    }

    [Fact]
    public void OneChannelCanFeedAllThree()
    {
        // The user's example: everything from blue. The result is meant to look like that.
        using var processor = Map(enabled: true, red: ColorChannel.Blue, green: ColorChannel.Blue, blue: ColorChannel.Blue);

        var output = processor.Process(Pixel(b: 10, g: 20, r: 30), CancellationToken.None);

        Assert.Equal((B: (byte)10, G: (byte)10, R: (byte)10, A: (byte)255), Read(output.Buffer));
    }

    [Fact]
    public void LeavesAlphaAlone_AndDoesNotRoundPartiallyTransparentPixels()
    {
        // Channel values in a premultiplied pixel all carry the same alpha factor, so moving them is
        // exact. Going via an unpremultiplying colour matrix would not be.
        using var processor = Map(enabled: true, red: ColorChannel.Blue, blue: ColorChannel.Red);

        var output = processor.Process(Pixel(b: 3, g: 7, r: 11, a: 17), CancellationToken.None);

        Assert.Equal((B: (byte)11, G: (byte)7, R: (byte)3, A: (byte)17), Read(output.Buffer));
    }

    [Fact]
    public void MapsEveryPixel_RespectingStride()
    {
        // Padded stride: the row gap must not be read as pixels, and must not shift the mapping.
        const int width = 2;
        const int height = 2;
        const int stride = 12; // 8 bytes of pixels + 4 bytes padding
        var pixels = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * stride) + (x * 4);
                pixels[i] = (byte)(x + 1);          // B
                pixels[i + 1] = (byte)(y + 10);     // G
                pixels[i + 2] = (byte)((x + y) + 20); // R
                pixels[i + 3] = 255;
            }
        }

        var buffer = new ImageBuffer(pixels, width, height, stride, ImagePixelFormat.Bgra8888Premultiplied);
        var input = ProcessedImage.FromSource(
            new SourceImage(new ImageReference(TestPaths.File("flyers", "gig.png")), buffer));
        using var processor = Map(enabled: true, red: ColorChannel.Blue, blue: ColorChannel.Red);

        var output = processor.Process(input, CancellationToken.None).Buffer;

        Assert.Equal(stride, output.Stride);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * stride) + (x * 4);
                var span = output.Pixels.Span;
                Assert.Equal((byte)((x + y) + 20), span[i]);      // B took R
                Assert.Equal((byte)(y + 10), span[i + 1]);        // G unchanged
                Assert.Equal((byte)(x + 1), span[i + 2]);         // R took B
                Assert.Equal(255, span[i + 3]);
            }
        }
    }

    [Fact]
    public void KeepsSourceMetadata()
    {
        using var processor = Map(enabled: true, red: ColorChannel.Blue);

        var output = processor.Process(Pixel(10, 20, 30), CancellationToken.None);

        Assert.Equal("gig.png", output.Metadata[ImageMetadataKeys.SourceFileName]);
    }
}
