namespace FlyerFlipper.Core.Layout;

public interface ILayoutModeService
{
    LayoutOrientation Orientation { get; }

    event EventHandler<LayoutOrientation>? OrientationChanged;

    void SetOrientation(LayoutOrientation orientation);

    void Toggle();
}
