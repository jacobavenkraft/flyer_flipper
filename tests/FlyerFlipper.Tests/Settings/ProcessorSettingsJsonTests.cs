using System.Text.Json;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;
using FlyerFlipper.Imaging.Processors;

namespace FlyerFlipper.Tests.Settings;

public class ProcessorSettingsJsonTests
{
    [Fact]
    public void SettingsIds_AreStableAndDistinct()
    {
        using var grayscale = new GrayscaleProcessor(new ProcessorSettings<GrayscaleOptions>(new GrayscaleOptions()));
        using var resize = new ResizeProcessor(new ProcessorSettings<ResizeOptions>(new ResizeOptions()));

        // Changing these orphans users' saved settings.
        Assert.Equal("flyerflipper.grayscale", grayscale.SettingsId);
        Assert.Equal("flyerflipper.resize", resize.SettingsId);
    }

    [Fact]
    public void Grayscale_JsonRoundTrip()
    {
        var source = new ProcessorSettings<GrayscaleOptions>(new GrayscaleOptions(true));
        using var from = new GrayscaleProcessor(source);
        var target = new ProcessorSettings<GrayscaleOptions>(new GrayscaleOptions());
        using var to = new GrayscaleProcessor(target);

        var json = from.GetSettingsJson();

        Assert.Equal("""{"enabled":true}""", json);
        Assert.True(to.TryApplySettingsJson(json));
        Assert.True(target.Current.Enabled);
    }

    [Fact]
    public void Resize_JsonRoundTrip()
    {
        using var from = new ResizeProcessor(new ProcessorSettings<ResizeOptions>(new ResizeOptions(true, 640, 480)));
        var target = new ProcessorSettings<ResizeOptions>(new ResizeOptions());
        using var to = new ResizeProcessor(target);

        var json = from.GetSettingsJson();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(640, document.RootElement.GetProperty("maxWidth").GetInt32());
        Assert.True(to.TryApplySettingsJson(json));
        Assert.Equal(new ResizeOptions(true, 640, 480), target.Current);
    }

    [Fact]
    public void Apply_RaisesSettingsChanged_SoProcessedImagesRefresh()
    {
        using var processor = new GrayscaleProcessor(new ProcessorSettings<GrayscaleOptions>(new GrayscaleOptions()));
        var raised = 0;
        processor.SettingsChanged += (_, _) => raised++;

        processor.TryApplySettingsJson("""{"enabled":true}""");

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Apply_MissingProperties_UseOptionDefaults()
    {
        var settings = new ProcessorSettings<ResizeOptions>(new ResizeOptions(true, 100, 100));
        using var processor = new ResizeProcessor(settings);

        Assert.True(processor.TryApplySettingsJson("""{"enabled":true}"""));

        Assert.Equal(new ResizeOptions(true, 800, 800), settings.Current);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""{"enabled":"yes"}""")]
    [InlineData("""{"enabled":true,"maxWidth":0}""")]       // rejected by ResizeOptions validation
    [InlineData("""{"enabled":true,"maxHeight":999999}""")]
    public void Resize_InvalidJson_IsRejected_LeavingSettingsUnchanged(string json)
    {
        var settings = new ProcessorSettings<ResizeOptions>(new ResizeOptions(true, 640, 480));
        using var processor = new ResizeProcessor(settings);
        var raised = 0;
        processor.SettingsChanged += (_, _) => raised++;

        Assert.False(processor.TryApplySettingsJson(json));

        Assert.Equal(new ResizeOptions(true, 640, 480), settings.Current);
        Assert.Equal(0, raised);
    }
}
