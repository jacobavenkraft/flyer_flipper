using FlyerFlipper.Core.Settings;

namespace FlyerFlipper.Tests.Settings;

public class WindowPlacementRulesTests
{
    private static readonly ScreenArea Primary = new(0, 0, 1920, 1040);
    private static readonly ScreenArea LeftMonitor = new(-2560, 0, 2560, 1400, Scaling: 1.5);

    [Theory]
    [InlineData(100, 100, true)]      // comfortably on screen
    [InlineData(1850, 100, false)]    // only 70 px of title bar visible
    [InlineData(1700, 100, true)]     // 220 px visible
    [InlineData(100, 1030, false)]    // title bar below the working area
    [InlineData(100, -10, false)]     // title bar partly above the top edge
    [InlineData(-800, 100, true)]     // hangs off the left, but 200 px of title bar remain visible
    [InlineData(-900, 100, false)]    // only 100 px remain
    [InlineData(5000, 5000, false)]
    public void IsReachable_RequiresEnoughTitleBarOnAScreen(int x, int y, bool expected)
    {
        var placement = new WindowPlacement(x, y, 1000, 700, IsMaximized: false);

        Assert.Equal(expected, WindowPlacementRules.IsReachable(placement, [Primary]));
    }

    [Fact]
    public void IsReachable_OnSecondMonitorWithNegativeCoordinates()
    {
        var placement = new WindowPlacement(-2400, 200, 1000, 700, IsMaximized: false);

        Assert.True(WindowPlacementRules.IsReachable(placement, [Primary, LeftMonitor]));
        Assert.False(WindowPlacementRules.IsReachable(placement, [Primary])); // that monitor was unplugged
    }

    [Theory]
    [InlineData(0, 700)]
    [InlineData(1000, -1)]
    [InlineData(double.NaN, 700)]
    [InlineData(double.PositiveInfinity, 700)]
    public void IsReachable_RejectsNonsenseSizes(double width, double height)
    {
        Assert.False(WindowPlacementRules.IsReachable(new WindowPlacement(100, 100, width, height, false), [Primary]));
    }

    [Fact]
    public void IsReachable_NoScreens_IsFalse()
    {
        Assert.False(WindowPlacementRules.IsReachable(new WindowPlacement(100, 100, 1000, 700, false), []));
    }
}
