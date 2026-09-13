using FlyerFlipper.Core.Imaging;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;
using FlyerFlipper.Core.Source;

namespace FlyerFlipper.Tests.Pipeline;

public class ProcessorSettingsTests
{
    [Fact]
    public void Update_WithDifferentValue_ReplacesCurrentAndRaisesOnce()
    {
        var settings = new ProcessorSettings<GrayscaleOptions>(new GrayscaleOptions());
        var raised = new List<GrayscaleOptions>();
        settings.Changed += (_, options) => raised.Add(options);

        settings.Update(new GrayscaleOptions(Enabled: true));

        Assert.True(settings.Current.Enabled);
        Assert.Equal([new GrayscaleOptions(true)], raised);
    }

    [Fact]
    public void Update_WithEqualValue_DoesNotRaise()
    {
        var settings = new ProcessorSettings<ResizeOptions>(new ResizeOptions(true, 640, 480));
        var raised = 0;
        settings.Changed += (_, _) => raised++;

        settings.Update(new ResizeOptions(true, 640, 480));

        Assert.Equal(0, raised);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(20_001, 10)]
    [InlineData(10, 20_001)]
    public void ResizeOptions_RejectsOutOfRangeDimensions(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResizeOptions(true, width, height));
    }

    [Fact]
    public void ResizeOptions_DefaultsToDisabled800Box()
    {
        Assert.Equal(new ResizeOptions(false, 800, 800), new ResizeOptions());
    }

    [Fact]
    public void Pipeline_ForwardsSettingsChanged_FromConfigurableProcessorsOnly()
    {
        var configurable = new FakeConfigurableProcessor();
        var pipeline = new ImageProcessingPipeline([new RecordingProcessor(1, "plain"), configurable]);
        var raised = 0;
        pipeline.SettingsChanged += (_, _) => raised++;

        configurable.RaiseSettingsChanged();
        configurable.RaiseSettingsChanged();

        Assert.Equal(2, raised);
    }

    [Fact]
    public void Pipeline_Dispose_StopsForwarding()
    {
        var configurable = new FakeConfigurableProcessor();
        var pipeline = new ImageProcessingPipeline([configurable]);
        var raised = 0;
        pipeline.SettingsChanged += (_, _) => raised++;

        pipeline.Dispose();
        configurable.RaiseSettingsChanged();

        Assert.Equal(0, raised);
    }

    [Theory]
    [InlineData(4000, 3000, 800, 800, 800, 600)]
    [InlineData(3000, 4000, 800, 800, 600, 800)]
    [InlineData(1080, 1350, 1080, 1080, 864, 1080)] // height is the limiting side
    [InlineData(1080, 1350, 500, 2000, 500, 625)]   // width is the limiting side
    [InlineData(640, 480, 800, 800, 640, 480)]      // already inside: never enlarged
    [InlineData(10000, 2, 100, 100, 100, 1)]
    public void FitWithin_KeepsAspectRatio_InsideBox(int width, int height, int maxWidth, int maxHeight, int expectedWidth, int expectedHeight)
    {
        Assert.Equal((expectedWidth, expectedHeight), ThumbnailSizing.FitWithin(width, height, maxWidth, maxHeight));
    }

    private sealed class FakeConfigurableProcessor : IConfigurableImageProcessor
    {
        public int Order => 2;

        public event EventHandler? SettingsChanged;

        public void RaiseSettingsChanged() => SettingsChanged?.Invoke(this, EventArgs.Empty);

        public ProcessedImage Process(ProcessedImage input, CancellationToken cancellationToken) => input;
    }
}
