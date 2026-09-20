using Avalonia.Controls;
using FlyerFlipper.UI.Chrome;

namespace FlyerFlipper.Tests.Chrome;

public class WindowChromeRulesTests
{
    [Theory]
    [InlineData(WindowState.Normal, WindowState.Maximized)]
    [InlineData(WindowState.Maximized, WindowState.Normal)]
    [InlineData(WindowState.FullScreen, WindowState.Normal)] // already enlarged, so restore down
    [InlineData(WindowState.Minimized, WindowState.Maximized)]
    public void ToggleMaximized(WindowState current, WindowState expected)
    {
        Assert.Equal(expected, WindowChromeRules.ToggleMaximized(current));
    }

    [Theory]
    [InlineData(WindowState.Normal, true, true)]
    [InlineData(WindowState.Minimized, true, true)]
    [InlineData(WindowState.Maximized, true, false)]   // the OS owns a maximized window's size
    [InlineData(WindowState.FullScreen, true, false)]
    [InlineData(WindowState.Normal, false, false)]     // a fixed-size window has nothing to grip
    public void ShowsResizeGrips(WindowState current, bool canResize, bool expected)
    {
        Assert.Equal(expected, WindowChromeRules.ShowsResizeGrips(current, canResize));
    }
}
