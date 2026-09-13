using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlyerFlipper.Core.Application;
using FlyerFlipper.Core.Layout;

namespace FlyerFlipper.UI.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private const double HorizontalControlsWidth = 300;

    private readonly ILayoutModeService _layoutMode;
    private readonly IApplicationShutdown _shutdown;

    [ObservableProperty]
    private Dock _controlsDock;

    [ObservableProperty]
    private Thickness _controlsBorderThickness;

    [ObservableProperty]
    private double _controlsWidth;

    public MainWindowViewModel(
        ILayoutModeService layoutMode,
        IApplicationShutdown shutdown,
        ImageSourceViewModel imageSource,
        ThumbnailGridViewModel thumbnailGrid)
    {
        _layoutMode = layoutMode;
        _shutdown = shutdown;
        ImageSource = imageSource;
        ThumbnailGrid = thumbnailGrid;
        ApplyOrientation(layoutMode.Orientation);
        _layoutMode.OrientationChanged += OnOrientationChanged;
    }

    public ImageSourceViewModel ImageSource { get; }

    public ThumbnailGridViewModel ThumbnailGrid { get; }

    [RelayCommand]
    private void ToggleOrientation() => _layoutMode.Toggle();

    [RelayCommand]
    private void Exit() => _shutdown.Shutdown();

    [RelayCommand]
    private void About() => Trace.WriteLine("Flyer Flipper — MVP Slice 2");

    private void OnOrientationChanged(object? sender, LayoutOrientation e) => ApplyOrientation(e);

    private void ApplyOrientation(LayoutOrientation orientation)
    {
        if (orientation == LayoutOrientation.Vertical)
        {
            ControlsDock = Dock.Top;
            ControlsBorderThickness = new Thickness(0, 0, 0, 1);
            ControlsWidth = double.NaN;
        }
        else
        {
            ControlsDock = Dock.Left;
            ControlsBorderThickness = new Thickness(0, 0, 1, 0);
            // Fixed so long folder paths don't widen the side panel.
            ControlsWidth = HorizontalControlsWidth;
        }
    }

    public void Dispose()
    {
        _layoutMode.OrientationChanged -= OnOrientationChanged;
    }
}
