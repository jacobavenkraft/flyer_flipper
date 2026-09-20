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

    [Fact]
    public void OriginCorrection_ExactReadBack_NeedsNoCorrection()
    {
        // Windows reports back precisely what was set.
        Assert.Null(WindowPlacementRules.OriginCorrection(500, 300, 500, 300, 500, 300));
    }

    [Fact]
    public void OriginCorrection_ShortReadBack_AimsPastTheTargetByTheSameOffset()
    {
        // WSLg/XWayland reports 32px short on both axes, so aim 32px past the target to land on it.
        Assert.Equal((532, 332), WindowPlacementRules.OriginCorrection(500, 300, 500, 300, 468, 268));
    }

    [Fact]
    public void OriginCorrection_LongReadBack_PullsBack()
    {
        Assert.Equal((490, 290), WindowPlacementRules.OriginCorrection(500, 300, 500, 300, 510, 310));
    }

    [Fact]
    public void OriginCorrection_MeasuresTheOffsetAgainstTheAssignment_NotTheTarget()
    {
        // The assignment (534,342) came back 32px short. The next assignment must be target+32,
        // regardless of how far the assignment had strayed from the target.
        Assert.Equal((532, 332), WindowPlacementRules.OriginCorrection(500, 300, 534, 342, 502, 310));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void OriginCorrection_CorrectsEvenWhenOnlyOneAxisIsOff(int offsetX, int offsetY)
    {
        var result = WindowPlacementRules.OriginCorrection(500, 300, 500, 300, 500 - offsetX, 300 - offsetY);

        Assert.Equal((500 + offsetX, 300 + offsetY), result);
    }

    [Theory]
    [InlineData(201, 0)]
    [InlineData(0, 201)]
    [InlineData(-400, -400)]
    public void OriginCorrection_IgnoresGapsTooLargeToBeAnOriginOffset(int offsetX, int offsetY)
    {
        // The window manager placed the window itself; nudging it would only make things worse.
        Assert.Null(WindowPlacementRules.OriginCorrection(500, 300, 500, 300, 500 - offsetX, 300 - offsetY));
    }

    [Fact]
    public void OriginCorrection_AtTheLimit_StillCorrects()
    {
        var limit = WindowPlacementRules.MaxOriginCorrectionPixels;

        Assert.Equal((500 + limit, 300), WindowPlacementRules.OriginCorrection(500, 300, 500, 300, 500 - limit, 300));
    }

    [Theory]
    [InlineData(500, 300)]
    [InlineData(56, 132)]
    [InlineData(0, 0)]
    public void OriginCorrection_DrivenAsTheTrackerDoes_ConvergesOnTheTarget(int targetX, int targetY)
    {
        // Mirrors WindowPlacementTracker.CompleteRestore against a platform that reports every assignment
        // 32px short on both axes, and whose initial placement is unrelated to what was asked for.
        const int offset = 32;
        var target = (X: targetX, Y: targetY);
        var reported = (X: -22, Y: -22);
        (int X, int Y)? assigned = null;

        for (var attempt = 0; attempt < 5 && reported != target; attempt++)
        {
            var next = target;
            if (assigned is { } last
                && WindowPlacementRules.OriginCorrection(target.X, target.Y, last.X, last.Y, reported.X, reported.Y)
                    is { } corrected)
            {
                next = corrected;
            }

            assigned = next;
            reported = (next.X - offset, next.Y - offset);
        }

        Assert.Equal(target, reported);
    }

    [Fact]
    public void OriginCorrection_DrivenAsTheTrackerDoes_SettlesImmediatelyWhenTheReadBackIsExact()
    {
        // Windows: the pre-show position is honoured, so the loop finds nothing to do.
        var target = (X: 500, Y: 300);

        Assert.Null(WindowPlacementRules.OriginCorrection(target.X, target.Y, target.X, target.Y, target.X, target.Y));
    }
}
