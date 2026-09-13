namespace FlyerFlipper.Core.Layout;

public sealed class LayoutModeService : ILayoutModeService
{
    private LayoutOrientation _orientation = LayoutOrientation.Vertical;

    public LayoutOrientation Orientation => _orientation;

    public event EventHandler<LayoutOrientation>? OrientationChanged;

    public void SetOrientation(LayoutOrientation orientation)
    {
        if (_orientation == orientation)
        {
            return;
        }

        _orientation = orientation;
        OrientationChanged?.Invoke(this, orientation);
    }

    public void Toggle()
        => SetOrientation(_orientation == LayoutOrientation.Vertical
            ? LayoutOrientation.Horizontal
            : LayoutOrientation.Vertical);
}
