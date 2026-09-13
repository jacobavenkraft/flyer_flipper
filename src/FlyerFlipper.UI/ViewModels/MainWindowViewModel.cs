using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlyerFlipper.Core.Application;
using FlyerFlipper.Core.Layout;
using FlyerFlipper.Core.Viewport;

namespace FlyerFlipper.UI.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private const double HorizontalControlsWidth = 300;

    private readonly ILayoutModeService _layoutMode;
    private readonly IViewportModeService _viewport;
    private readonly IApplicationShutdown _shutdown;

    [ObservableProperty]
    private Dock _controlsDock;

    [ObservableProperty]
    private Thickness _controlsBorderThickness;

    [ObservableProperty]
    private double _controlsWidth;

    public MainWindowViewModel(
        ILayoutModeService layoutMode,
        IViewportModeService viewport,
        IApplicationShutdown shutdown,
        ImageSourceViewModel imageSource,
        ThumbnailGridViewModel thumbnailGrid,
        SingleImageViewModel singleImage)
    {
        _layoutMode = layoutMode;
        _viewport = viewport;
        _shutdown = shutdown;
        ImageSource = imageSource;
        ThumbnailGrid = thumbnailGrid;
        SingleImage = singleImage;
        ApplyOrientation(layoutMode.Orientation);
        _layoutMode.OrientationChanged += OnOrientationChanged;
        _viewport.ModeChanged += OnViewportModeChanged;
        _viewport.CurrentImageChanged += OnCurrentImageChanged;
    }

    public ImageSourceViewModel ImageSource { get; }

    public ThumbnailGridViewModel ThumbnailGrid { get; }

    public SingleImageViewModel SingleImage { get; }

    public bool IsGridMode => _viewport.Mode == ViewportMode.Grid;

    public bool IsSingleMode => _viewport.Mode == ViewportMode.Single;

    [RelayCommand]
    private void ToggleOrientation() => _layoutMode.Toggle();

    [RelayCommand(CanExecute = nameof(CanToggleViewportMode))]
    private void ToggleViewportMode() => _viewport.ToggleMode();

    private bool CanToggleViewportMode() => IsSingleMode || _viewport.ImageCount > 0;

    [RelayCommand]
    private void Exit() => _shutdown.Shutdown();

    [RelayCommand]
    private void About() => Trace.WriteLine("Flyer Flipper — MVP Slice 3");

    private void OnOrientationChanged(object? sender, LayoutOrientation e) => ApplyOrientation(e);

    private void OnViewportModeChanged(object? sender, ViewportMode e)
    {
        OnPropertyChanged(nameof(IsGridMode));
        OnPropertyChanged(nameof(IsSingleMode));
        ToggleViewportModeCommand.NotifyCanExecuteChanged();
    }

    private void OnCurrentImageChanged(object? sender, EventArgs e) => ToggleViewportModeCommand.NotifyCanExecuteChanged();

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
        _viewport.ModeChanged -= OnViewportModeChanged;
        _viewport.CurrentImageChanged -= OnCurrentImageChanged;
    }
}
