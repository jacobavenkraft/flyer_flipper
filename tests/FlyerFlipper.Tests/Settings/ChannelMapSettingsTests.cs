using System.Text.Json;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;
using FlyerFlipper.Imaging.Processors;

namespace FlyerFlipper.Tests.Settings;

public class ChannelMapSettingsTests
{
    [Fact]
    public void SettingsId_IsStable()
    {
        using var processor = new ChannelMapProcessor(new ProcessorSettings<ChannelMapOptions>(new ChannelMapOptions()));

        // Changing this orphans users' saved settings.
        Assert.Equal("flyerflipper.channelmap", processor.SettingsId);
    }

    [Fact]
    public void JsonRoundTrip_WritesChannelsAsNames()
    {
        using var from = new ChannelMapProcessor(new ProcessorSettings<ChannelMapOptions>(
            new ChannelMapOptions(true, red: ColorChannel.Blue, green: ColorChannel.Blue, blue: ColorChannel.Red)));
        var target = new ProcessorSettings<ChannelMapOptions>(new ChannelMapOptions());
        using var to = new ChannelMapProcessor(target);

        var json = from.GetSettingsJson();
        using var document = JsonDocument.Parse(json);

        Assert.Equal("Blue", document.RootElement.GetProperty("red").GetString());
        Assert.Equal("Red", document.RootElement.GetProperty("blue").GetString());
        Assert.True(to.TryApplySettingsJson(json));
        Assert.Equal(
            new ChannelMapOptions(true, red: ColorChannel.Blue, green: ColorChannel.Blue, blue: ColorChannel.Red),
            target.Current);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""{"enabled":"yes"}""")]
    [InlineData("""{"enabled":true,"red":"Cyan"}""")]
    [InlineData("""{"enabled":true,"red":7}""")]
    [InlineData("""{"enabled":true,"blue":-1}""")]
    public void InvalidJson_IsRejected_LeavingSettingsUnchanged(string json)
    {
        var original = new ChannelMapOptions(true, red: ColorChannel.Green);
        var settings = new ProcessorSettings<ChannelMapOptions>(original);
        using var processor = new ChannelMapProcessor(settings);
        var raised = 0;
        processor.SettingsChanged += (_, _) => raised++;

        Assert.False(processor.TryApplySettingsJson(json));

        Assert.Equal(original, settings.Current);
        Assert.Equal(0, raised);
    }

    [Theory]
    [InlineData(false, ColorChannel.Blue, false)]                 // disabled
    [InlineData(true, ColorChannel.Red, false)]                   // identity
    [InlineData(true, ColorChannel.Blue, true)]
    public void HasEffect_IsTrueOnlyWhenEnabledAndNotIdentity(bool enabled, ColorChannel red, bool expected)
    {
        Assert.Equal(expected, new ChannelMapOptions(enabled, red: red).HasEffect);
    }

    [Fact]
    public void RearrangingChannelsWhileDisabled_DoesNotAskForReprocessing()
    {
        var settings = new ProcessorSettings<ChannelMapOptions>(new ChannelMapOptions());
        using var processor = new ChannelMapProcessor(settings);
        var raised = 0;
        processor.SettingsChanged += (_, _) => raised++;

        settings.Update(new ChannelMapOptions(enabled: false, red: ColorChannel.Blue));

        Assert.Equal(0, raised);
    }

    [Fact]
    public void EnablingANonIdentityMap_AsksForReprocessing()
    {
        var settings = new ProcessorSettings<ChannelMapOptions>(new ChannelMapOptions(red: ColorChannel.Blue));
        using var processor = new ChannelMapProcessor(settings);
        var raised = 0;
        processor.SettingsChanged += (_, _) => raised++;

        settings.Update(new ChannelMapOptions(enabled: true, red: ColorChannel.Blue));

        Assert.Equal(1, raised);
    }

    [Fact]
    public void DefaultOrder_PutsChannelMapBeforeGrayscale()
    {
        // Pins the shipped default, not a constraint. This way round the two compose: rearranging
        // channels changes the resulting luma. After grayscale every channel is equal, so a channel
        // map would do nothing at all.
        Assert.True(ProcessorOrder.ChannelMap < ProcessorOrder.Grayscale);
    }
}
