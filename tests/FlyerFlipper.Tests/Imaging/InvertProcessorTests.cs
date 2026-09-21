using FlyerFlipper.Core.Imaging;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;
using FlyerFlipper.Core.Source;
using FlyerFlipper.Imaging.Processors;
using FlyerFlipper.Tests.TestSupport;

namespace FlyerFlipper.Tests.Imaging;

public class InvertProcessorTests
{
    private static ProcessedImage Pixel(byte b, byte g, byte r, byte a = 255)
    {
        var buffer = new ImageBuffer(new byte[] { b, g, r, a }, 1, 1, 4, ImagePixelFormat.Bgra8888Premultiplied);
        return ProcessedImage.FromSource(new SourceImage(new ImageReference(TestPaths.File("flyers", "gig.png")), buffer));
    }

    private static (byte B, byte G, byte R, byte A) Read(ImageBuffer buffer)
    {
        var p = buffer.Pixels.Span;
        return (p[0], p[1], p[2], p[3]);
    }

    private static InvertProcessor Invert(bool enabled)
        => new(new ProcessorSettings<InvertOptions>(new InvertOptions(enabled)));

    [Fact]
    public void Disabled_PassesThroughSameInstance()
    {
        using var processor = Invert(enabled: false);
        var input = Pixel(10, 20, 30);

        Assert.Same(input, processor.Process(input, CancellationToken.None));
    }

    [Theory]
    [InlineData(0, 0, 0, 255, 255, 255)]        // black -> white
    [InlineData(255, 255, 255, 0, 0, 0)]        // white -> black
    [InlineData(10, 20, 30, 245, 235, 225)]
    public void OpaquePixels_AreInvertedPerChannel(byte b, byte g, byte r, byte eb, byte eg, byte er)
    {
        using var processor = Invert(enabled: true);

        var output = processor.Process(Pixel(b, g, r), CancellationToken.None);

        Assert.Equal((eb, eg, er, (byte)255), Read(output.Buffer));
    }

    [Fact]
    public void PartiallyTransparentPixel_InvertsAgainstItsOwnAlpha()
    {
        // The buffers are premultiplied: a stored channel is colour x alpha, so inverting the colour
        // gives (1 - colour) x alpha = alpha - stored. The naive 255 - stored would give 252/248/244
        // here — larger than the pixel's own alpha, which is not a valid premultiplied pixel and shows
        // up as a bright halo.
        using var processor = Invert(enabled: true);

        var output = processor.Process(Pixel(b: 3, g: 7, r: 11, a: 17), CancellationToken.None);

        Assert.Equal((B: (byte)14, G: (byte)10, R: (byte)6, A: (byte)17), Read(output.Buffer));
    }

    [Fact]
    public void FullyTransparentPixel_StaysTransparentAndBlack()
    {
        using var processor = Invert(enabled: true);

        var output = processor.Process(Pixel(0, 0, 0, a: 0), CancellationToken.None);

        Assert.Equal((B: (byte)0, G: (byte)0, R: (byte)0, A: (byte)0), Read(output.Buffer));
    }

    [Fact]
    public void NoChannelEverExceedsItsOwnAlpha()
    {
        // The invariant that makes a premultiplied buffer valid, checked across the whole alpha range.
        using var processor = Invert(enabled: true);

        for (var alpha = 0; alpha <= 255; alpha++)
        {
            var a = (byte)alpha;
            var output = processor.Process(Pixel(b: 0, g: (byte)(alpha / 2), r: a, a: a), CancellationToken.None);
            var (b, g, r, outAlpha) = Read(output.Buffer);

            Assert.Equal(a, outAlpha);
            Assert.True(b <= a && g <= a && r <= a, $"alpha {a} produced ({b},{g},{r})");
        }
    }

    [Fact]
    public void InvertingTwice_ReturnsTheOriginal()
    {
        using var processor = Invert(enabled: true);
        var input = Pixel(b: 3, g: 7, r: 11, a: 17);

        var once = processor.Process(input, CancellationToken.None);
        var twice = processor.Process(once, CancellationToken.None);

        Assert.Equal(Read(input.Buffer), Read(twice.Buffer));
    }

    [Fact]
    public void MapsEveryPixel_RespectingStride()
    {
        const int width = 2;
        const int height = 2;
        const int stride = 12; // 8 bytes of pixels + 4 bytes padding
        var pixels = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * stride) + (x * 4);
                pixels[i] = 10;
                pixels[i + 1] = 20;
                pixels[i + 2] = 30;
                pixels[i + 3] = 255;
            }
        }

        var input = ProcessedImage.FromSource(new SourceImage(
            new ImageReference(TestPaths.File("flyers", "gig.png")),
            new ImageBuffer(pixels, width, height, stride, ImagePixelFormat.Bgra8888Premultiplied)));
        using var processor = Invert(enabled: true);

        var output = processor.Process(input, CancellationToken.None).Buffer;

        Assert.Equal(stride, output.Stride);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * stride) + (x * 4);
                var span = output.Pixels.Span;
                Assert.Equal((245, 235, 225, 255), (span[i], span[i + 1], span[i + 2], span[i + 3]));
            }
        }
    }

    [Fact]
    public void KeepsSourceMetadata()
    {
        using var processor = Invert(enabled: true);

        var output = processor.Process(Pixel(10, 20, 30), CancellationToken.None);

        Assert.Equal("gig.png", output.Metadata[ImageMetadataKeys.SourceFileName]);
    }

    [Fact]
    public void SettingsId_IsStable_AndDefaultOrderSitsBetweenChannelMapAndGrayscale()
    {
        using var processor = Invert(enabled: false);

        Assert.Equal("flyerflipper.invert", processor.SettingsId);

        // Pins the shipped default, not a constraint: inverting the channels the user chose, and
        // letting grayscale weigh the inverted result.
        Assert.True(ProcessorOrder.ChannelMap < ProcessorOrder.Invert);
        Assert.True(ProcessorOrder.Invert < ProcessorOrder.Grayscale);
    }

    [Fact]
    public void JsonRoundTrip()
    {
        using var from = new InvertProcessor(new ProcessorSettings<InvertOptions>(new InvertOptions(true)));
        var target = new ProcessorSettings<InvertOptions>(new InvertOptions());
        using var to = new InvertProcessor(target);

        var json = from.GetSettingsJson();

        Assert.Equal("""{"enabled":true}""", json);
        Assert.True(to.TryApplySettingsJson(json));
        Assert.True(target.Current.Enabled);
    }
}
