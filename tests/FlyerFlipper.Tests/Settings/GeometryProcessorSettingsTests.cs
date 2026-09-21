using System.Text.Json;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;
using FlyerFlipper.Imaging.Processors;

namespace FlyerFlipper.Tests.Settings;

public class GeometryProcessorSettingsTests
{
    [Fact]
    public void SettingsIds_AreStableAndDistinct()
    {
        using var flip = new FlipProcessor(new ProcessorSettings<FlipOptions>(new FlipOptions()));
        using var rotate = new RotateProcessor(new ProcessorSettings<RotateOptions>(new RotateOptions()));

        // Changing these orphans users' saved settings.
        Assert.Equal("flyerflipper.flip", flip.SettingsId);
        Assert.Equal("flyerflipper.rotate", rotate.SettingsId);
    }

    [Fact]
    public void Flip_JsonRoundTrip()
    {
        using var from = new FlipProcessor(
            new ProcessorSettings<FlipOptions>(new FlipOptions(true, MirrorHorizontally: true)));
        var target = new ProcessorSettings<FlipOptions>(new FlipOptions());
        using var to = new FlipProcessor(target);

        var json = from.GetSettingsJson();

        Assert.Equal("""{"enabled":true,"mirrorHorizontally":true,"mirrorVertically":false}""", json);
        Assert.True(to.TryApplySettingsJson(json));
        Assert.Equal(new FlipOptions(true, MirrorHorizontally: true), target.Current);
    }

    [Fact]
    public void Rotate_JsonRoundTrip_WritesTheAngleAsAName()
    {
        // A name rather than a number, so a hand-edited settings file stays readable and a reordered
        // enum cannot silently change what a saved file means.
        using var from = new RotateProcessor(
            new ProcessorSettings<RotateOptions>(new RotateOptions(true, RotationAngle.Clockwise270)));
        var target = new ProcessorSettings<RotateOptions>(new RotateOptions());
        using var to = new RotateProcessor(target);

        var json = from.GetSettingsJson();
        using var document = JsonDocument.Parse(json);

        Assert.Equal("Clockwise270", document.RootElement.GetProperty("angle").GetString());
        Assert.True(to.TryApplySettingsJson(json));
        Assert.Equal(new RotateOptions(true, RotationAngle.Clockwise270), target.Current);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""{"enabled":"yes"}""")]
    [InlineData("""{"enabled":true,"angle":"Clockwise45"}""")]
    [InlineData("""{"enabled":true,"angle":42}""")]
    public void Rotate_InvalidJson_IsRejected_LeavingSettingsUnchanged(string json)
    {
        var settings = new ProcessorSettings<RotateOptions>(new RotateOptions(true, RotationAngle.Clockwise90));
        using var processor = new RotateProcessor(settings);
        var raised = 0;
        processor.SettingsChanged += (_, _) => raised++;

        Assert.False(processor.TryApplySettingsJson(json));

        Assert.Equal(new RotateOptions(true, RotationAngle.Clockwise90), settings.Current);
        Assert.Equal(0, raised);
    }

    // ---- Reprocessing is only requested when output actually changes ---------------------------

    [Fact]
    public void Flip_ChangingAnAxisWhileDisabled_DoesNotAskForReprocessing()
    {
        var settings = new ProcessorSettings<FlipOptions>(new FlipOptions());
        using var processor = new FlipProcessor(settings);
        var raised = 0;
        processor.SettingsChanged += (_, _) => raised++;

        settings.Update(new FlipOptions(Enabled: false, MirrorHorizontally: true));

        Assert.Equal(0, raised);
    }

    [Fact]
    public void Flip_EnablingWithAnAxisChosen_AsksForReprocessing()
    {
        var settings = new ProcessorSettings<FlipOptions>(new FlipOptions(MirrorHorizontally: true));
        using var processor = new FlipProcessor(settings);
        var raised = 0;
        processor.SettingsChanged += (_, _) => raised++;

        settings.Update(new FlipOptions(Enabled: true, MirrorHorizontally: true));

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Rotate_ChangingTheAngleWhileDisabled_DoesNotAskForReprocessing()
    {
        var settings = new ProcessorSettings<RotateOptions>(new RotateOptions());
        using var processor = new RotateProcessor(settings);
        var raised = 0;
        processor.SettingsChanged += (_, _) => raised++;

        settings.Update(new RotateOptions(enabled: false, angle: RotationAngle.Clockwise180));

        Assert.Equal(0, raised);
    }

    [Fact]
    public void Rotate_EnablingWithAnAngle_AsksForReprocessing()
    {
        var settings = new ProcessorSettings<RotateOptions>(new RotateOptions(angle: RotationAngle.Clockwise180));
        using var processor = new RotateProcessor(settings);
        var raised = 0;
        processor.SettingsChanged += (_, _) => raised++;

        settings.Update(new RotateOptions(enabled: true, angle: RotationAngle.Clockwise180));

        Assert.Equal(1, raised);
    }

    // ---- Options -------------------------------------------------------------------------------

    [Theory]
    [InlineData(false, RotationAngle.Clockwise90, false, false)]
    [InlineData(true, RotationAngle.None, false, false)]
    [InlineData(true, RotationAngle.Clockwise90, true, true)]
    [InlineData(true, RotationAngle.Clockwise180, true, false)]
    [InlineData(true, RotationAngle.Clockwise270, true, true)]
    public void RotateOptions_ReportsEffectAndDimensionSwap(
        bool enabled, RotationAngle angle, bool hasEffect, bool swaps)
    {
        var options = new RotateOptions(enabled, angle);

        Assert.Equal(hasEffect, options.HasEffect);
        Assert.Equal(swaps, options.SwapsDimensions);
    }

    [Fact]
    public void DefaultOrder_PutsGeometryBeforeResize()
    {
        // Pins the shipped default, not a constraint: any order is legitimate and reordering the
        // pipeline is a planned enhancement. This default resizes last so the box applies to the
        // final orientation, rather than being undone by a later quarter turn swapping the sides.
        Assert.True(ProcessorOrder.Flip < ProcessorOrder.Resize);
        Assert.True(ProcessorOrder.Rotate < ProcessorOrder.Resize);
        Assert.True(ProcessorOrder.Flip < ProcessorOrder.Rotate);
    }
}
