using FlyerFlipper.Core.Imaging;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;
using FlyerFlipper.Core.Source;
using FlyerFlipper.Imaging.Processors;
using FlyerFlipper.Tests.TestSupport;

namespace FlyerFlipper.Tests.Imaging;

public class ProcessorImplementationTests
{
    /// <summary>A BGRA-premultiplied image filled with one pixel value.</summary>
    private static ProcessedImage Solid(int width, int height, byte b, byte g, byte r, byte a)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = b;
            pixels[i + 1] = g;
            pixels[i + 2] = r;
            pixels[i + 3] = a;
        }

        var buffer = new ImageBuffer(pixels, width, height, width * 4, ImagePixelFormat.Bgra8888Premultiplied);
        return ProcessedImage.FromSource(new SourceImage(new ImageReference(TestPaths.File("flyers", "gig.png")), buffer));
    }

    private static byte[] PixelAt(ImageBuffer buffer, int x, int y)
        => buffer.Pixels.Span.Slice((y * buffer.Stride) + (x * 4), 4).ToArray();

    // ---- Grayscale -----------------------------------------------------------------------------

    [Fact]
    public void Grayscale_Disabled_PassesThroughSameInstance()
    {
        using var processor = new GrayscaleProcessor(new ProcessorSettings<GrayscaleOptions>(new GrayscaleOptions()));
        var input = Solid(4, 4, 0, 0, 255, 255);

        Assert.Same(input, processor.Process(input, CancellationToken.None));
    }

    [Theory]
    [InlineData(0, 0, 255, 54)]    // red   → 0.2126 · 255
    [InlineData(0, 255, 0, 182)]   // green → 0.7152 · 255
    [InlineData(255, 0, 0, 18)]    // blue  → 0.0722 · 255
    [InlineData(255, 255, 255, 255)]
    public void Grayscale_Enabled_UsesRec709Luma(byte b, byte g, byte r, byte expectedGray)
    {
        using var processor = new GrayscaleProcessor(new ProcessorSettings<GrayscaleOptions>(new GrayscaleOptions(true)));

        var output = processor.Process(Solid(3, 2, b, g, r, 255), CancellationToken.None);

        Assert.Equal((3, 2), (output.Buffer.Width, output.Buffer.Height));
        var pixel = PixelAt(output.Buffer, 1, 1);
        Assert.Equal(pixel[0], pixel[1]);
        Assert.Equal(pixel[1], pixel[2]);
        Assert.InRange(pixel[0], expectedGray - 1, expectedGray + 1);
        Assert.Equal(255, pixel[3]);
        Assert.Equal("gig.png", output.Metadata[ImageMetadataKeys.SourceFileName]);
    }

    [Fact]
    public void Grayscale_PreservesAlpha_OnPremultipliedPixels()
    {
        using var processor = new GrayscaleProcessor(new ProcessorSettings<GrayscaleOptions>(new GrayscaleOptions(true)));

        // Half-transparent pure red, premultiplied: R = 128, A = 128.
        var output = processor.Process(Solid(2, 2, 0, 0, 128, 128), CancellationToken.None);

        var pixel = PixelAt(output.Buffer, 0, 0);
        Assert.Equal(128, pixel[3]);
        Assert.InRange(pixel[0], 26, 28); // 0.2126 · 255 · (128/255) ≈ 27, premultiplied
        Assert.Equal(pixel[0], pixel[2]);
    }

    [Fact]
    public void Grayscale_DoesNotMutateInputBuffer()
    {
        using var processor = new GrayscaleProcessor(new ProcessorSettings<GrayscaleOptions>(new GrayscaleOptions(true)));
        var input = Solid(2, 2, 0, 0, 255, 255);

        processor.Process(input, CancellationToken.None);

        Assert.Equal(new byte[] { 0, 0, 255, 255 }, PixelAt(input.Buffer, 0, 0));
    }

    [Fact]
    public void Grayscale_SettingsChange_RaisesSettingsChanged()
    {
        var settings = new ProcessorSettings<GrayscaleOptions>(new GrayscaleOptions());
        using var processor = new GrayscaleProcessor(settings);
        var raised = 0;
        processor.SettingsChanged += (_, _) => raised++;

        settings.Update(new GrayscaleOptions(true));
        settings.Update(new GrayscaleOptions(false));

        Assert.Equal(2, raised);
        Assert.Equal(ProcessorOrder.Grayscale, processor.Order);
    }

    // ---- Resize --------------------------------------------------------------------------------

    [Fact]
    public void Resize_Disabled_PassesThroughSameInstance()
    {
        using var processor = new ResizeProcessor(new ProcessorSettings<ResizeOptions>(new ResizeOptions(false, 10, 10)));
        var input = Solid(40, 30, 0, 0, 255, 255);

        Assert.Same(input, processor.Process(input, CancellationToken.None));
    }

    [Fact]
    public void Resize_Enabled_ShrinksToFitKeepingAspectAndColor()
    {
        using var processor = new ResizeProcessor(new ProcessorSettings<ResizeOptions>(new ResizeOptions(true, 100, 100)));

        var output = processor.Process(Solid(400, 300, 10, 20, 200, 255), CancellationToken.None);

        Assert.Equal((100, 75), (output.Buffer.Width, output.Buffer.Height));
        Assert.Equal(new byte[] { 10, 20, 200, 255 }, PixelAt(output.Buffer, 50, 37));
        Assert.Equal("gig.png", output.Metadata[ImageMetadataKeys.SourceFileName]);
    }

    [Fact]
    public void Resize_ImageAlreadyInsideBox_IsNotEnlarged()
    {
        using var processor = new ResizeProcessor(new ProcessorSettings<ResizeOptions>(new ResizeOptions(true, 800, 800)));
        var input = Solid(400, 300, 0, 0, 255, 255);

        Assert.Same(input, processor.Process(input, CancellationToken.None));
    }

    [Fact]
    public void Resize_RaisesSettingsChanged_OnlyWhenOutputCanChange()
    {
        var settings = new ProcessorSettings<ResizeOptions>(new ResizeOptions(false, 800, 800));
        using var processor = new ResizeProcessor(settings);
        var raised = 0;
        processor.SettingsChanged += (_, _) => raised++;

        settings.Update(new ResizeOptions(false, 500, 500)); // disabled → disabled: no output change
        Assert.Equal(0, raised);

        settings.Update(new ResizeOptions(true, 500, 500));  // enabling
        settings.Update(new ResizeOptions(true, 400, 500));  // dimension change while enabled
        settings.Update(new ResizeOptions(false, 400, 500)); // disabling
        Assert.Equal(3, raised);
        Assert.Equal(ProcessorOrder.Resize, processor.Order);
    }

    [Fact]
    public void PipelineOrder_IsGrayscaleThenResize()
    {
        using var grayscale = new GrayscaleProcessor(new ProcessorSettings<GrayscaleOptions>(new GrayscaleOptions(true)));
        using var resize = new ResizeProcessor(new ProcessorSettings<ResizeOptions>(new ResizeOptions(true, 10, 10)));
        var pipeline = new ImageProcessingPipeline([resize, grayscale]);

        var output = pipeline.Process(Solid(40, 20, 0, 0, 255, 255));

        Assert.Equal([grayscale, resize], pipeline.Processors);
        Assert.Equal((10, 5), (output.Buffer.Width, output.Buffer.Height));
        var pixel = PixelAt(output.Buffer, 5, 2);
        Assert.InRange(pixel[2], 53, 55);
        Assert.Equal(pixel[0], pixel[2]);
    }
}
