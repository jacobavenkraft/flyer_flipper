using FlyerFlipper.Core.Layout;

namespace FlyerFlipper.Tests.Layout;

public class LayoutModeServiceTests
{
    [Fact]
    public void DefaultOrientation_IsVertical()
    {
        var service = new LayoutModeService();

        Assert.Equal(LayoutOrientation.Vertical, service.Orientation);
    }

    [Fact]
    public void Toggle_FromVertical_YieldsHorizontal()
    {
        var service = new LayoutModeService();

        service.Toggle();

        Assert.Equal(LayoutOrientation.Horizontal, service.Orientation);
    }

    [Fact]
    public void Toggle_Twice_ReturnsToVertical()
    {
        var service = new LayoutModeService();

        service.Toggle();
        service.Toggle();

        Assert.Equal(LayoutOrientation.Vertical, service.Orientation);
    }

    [Fact]
    public void Toggle_RaisesOrientationChanged_WithNewValue()
    {
        var service = new LayoutModeService();
        LayoutOrientation? observed = null;
        service.OrientationChanged += (_, value) => observed = value;

        service.Toggle();

        Assert.Equal(LayoutOrientation.Horizontal, observed);
    }

    [Fact]
    public void SetOrientation_ToSameValue_DoesNotRaiseEvent()
    {
        var service = new LayoutModeService();
        var raiseCount = 0;
        service.OrientationChanged += (_, _) => raiseCount++;

        service.SetOrientation(LayoutOrientation.Vertical);

        Assert.Equal(0, raiseCount);
    }

    [Fact]
    public void SetOrientation_ToNewValue_RaisesEventOnce()
    {
        var service = new LayoutModeService();
        var raiseCount = 0;
        service.OrientationChanged += (_, _) => raiseCount++;

        service.SetOrientation(LayoutOrientation.Horizontal);
        service.SetOrientation(LayoutOrientation.Horizontal);

        Assert.Equal(1, raiseCount);
    }
}
