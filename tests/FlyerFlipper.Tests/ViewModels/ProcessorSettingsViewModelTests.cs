using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;
using FlyerFlipper.UI.ViewModels.Processors;

namespace FlyerFlipper.Tests.ViewModels;

public class ProcessorSettingsViewModelTests
{
    [Fact]
    public void Grayscale_ReflectsCurrentSettings_AndAppliesEnabled()
    {
        var settings = new ProcessorSettings<GrayscaleOptions>(new GrayscaleOptions(true));
        var vm = new GrayscaleSettingsViewModel(settings);
        Assert.True(vm.Enabled);

        vm.Enabled = false;

        Assert.False(settings.Current.Enabled);
    }

    [Fact]
    public void Resize_ReflectsCurrentSettings()
    {
        var vm = new ResizeSettingsViewModel(new ProcessorSettings<ResizeOptions>(new ResizeOptions(true, 640, 480)));

        Assert.True(vm.Enabled);
        Assert.Equal(640m, vm.MaxWidth);
        Assert.Equal(480m, vm.MaxHeight);
        Assert.Equal(1m, vm.Minimum);
        Assert.Equal(20_000m, vm.Maximum);
    }

    [Fact]
    public void Resize_AppliesEachCommittedValue()
    {
        var settings = new ProcessorSettings<ResizeOptions>(new ResizeOptions());
        var vm = new ResizeSettingsViewModel(settings);

        vm.Enabled = true;
        vm.MaxWidth = 1200;
        vm.MaxHeight = 900;

        Assert.Equal(new ResizeOptions(true, 1200, 900), settings.Current);
    }

    [Fact]
    public void Resize_ClearedField_KeepsLastValidValue()
    {
        var settings = new ProcessorSettings<ResizeOptions>(new ResizeOptions(true, 640, 480));
        var vm = new ResizeSettingsViewModel(settings);

        vm.MaxWidth = null;

        Assert.Equal(640, settings.Current.MaxWidth);
    }

    [Fact]
    public void Grayscale_FollowsSettingsChangedElsewhere()
    {
        var settings = new ProcessorSettings<GrayscaleOptions>(new GrayscaleOptions());
        var vm = new GrayscaleSettingsViewModel(settings);

        settings.Update(new GrayscaleOptions(true));

        Assert.True(vm.Enabled);
    }

    [Fact]
    public void Resize_FollowsSettingsChangedElsewhere_WithoutOverwritingThem()
    {
        var settings = new ProcessorSettings<ResizeOptions>(new ResizeOptions());
        var vm = new ResizeSettingsViewModel(settings);
        var raised = 0;
        settings.Changed += (_, _) => raised++;

        settings.Update(new ResizeOptions(true, 30, 40));

        Assert.Equal((true, 30m, 40m), (vm.Enabled, vm.MaxWidth, vm.MaxHeight));
        Assert.Equal(new ResizeOptions(true, 30, 40), settings.Current);
        Assert.Equal(1, raised); // syncing the view model didn't write intermediate combinations back
    }

    [Fact]
    public void Dispose_StopsFollowingSettings()
    {
        var settings = new ProcessorSettings<GrayscaleOptions>(new GrayscaleOptions());
        var vm = new GrayscaleSettingsViewModel(settings);

        vm.Dispose();
        settings.Update(new GrayscaleOptions(true));

        Assert.False(vm.Enabled);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-50, 1)]
    [InlineData(99999, 20_000)]
    [InlineData(640.6, 641)]
    public void Resize_OutOfRangeOrFractionalValues_AreClamped(double entered, int applied)
    {
        var settings = new ProcessorSettings<ResizeOptions>(new ResizeOptions());
        var vm = new ResizeSettingsViewModel(settings);

        vm.MaxWidth = (decimal)entered;

        Assert.Equal(applied, settings.Current.MaxWidth);
    }
}
