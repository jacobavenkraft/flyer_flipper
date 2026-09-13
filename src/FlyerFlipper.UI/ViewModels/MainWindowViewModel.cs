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
        SingleImageViewModel singleImage,
        ProcessorTabHostViewModel processorTabs)
    {
        _layoutMode = layoutMode;
        _viewport = viewport;
        _shutdown = shutdown;
        ImageSource = imageSource;
        ThumbnailGrid = thumbnailGrid;
        SingleImage = singleImage;
        ProcessorTabs = processorTabs;
        ApplyOrientation(layoutMode.Orientation);
        _layoutMode.OrientationChanged += OnOrientationChanged;
        _viewport.ModeChanged += OnViewportModeChanged;
        _viewport.CurrentImageChanged += OnCurrentImageChanged;
        _viewport.ScaleModeChanged += OnScaleModeChanged;
    }

    public ImageSourceViewModel ImageSource { get; }

    public ThumbnailGridViewModel ThumbnailGrid { get; }

    public SingleImageViewModel SingleImage { get; }

    public ProcessorTabHostViewModel ProcessorTabs { get; }

    public bool IsGridMode => _viewport.Mode == ViewportMode.Grid;

    public bool IsSingleMode => _viewport.Mode == ViewportMode.Single;

    public bool IsScaleFitToWindow => _viewport.ScaleMode == ViewportScaleMode.FitToWindow;

    public bool IsScaleFitWithoutEnlarging => _viewport.ScaleMode == ViewportScaleMode.FitWithoutEnlarging;

    public bool IsScaleStretchToFill => _viewport.ScaleMode == ViewportScaleMode.StretchToFill;

    public bool IsScaleActualSize => _viewport.ScaleMode == ViewportScaleMode.ActualSize;

    [RelayCommand]
    private void SetScaleMode(ViewportScaleMode scaleMode) => _viewport.SetScaleMode(scaleMode);

    private void OnScaleModeChanged(object? sender, ViewportScaleMode e)
    {
        OnPropertyChanged(nameof(IsScaleFitToWindow));
        OnPropertyChanged(nameof(IsScaleFitWithoutEnlarging));
        OnPropertyChanged(nameof(IsScaleStretchToFill));
        OnPropertyChanged(nameof(IsScaleActualSize));
    }

    [RelayCommand]
    private void ToggleOrientation() => _layoutMode.Toggle();

    [RelayCommand(CanExecute = nameof(CanToggleViewportMode))]
    private void ToggleViewportMode() => _viewport.ToggleMode();

    private bool CanToggleViewportMode() => IsSingleMode || _viewport.ImageCount > 0;

    [RelayCommand]
    private void Exit() => _shutdown.Shutdown();

    [RelayCommand]
    private void About() => Trace.WriteLine("Flyer Flipper — MVP Slice 5");

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
        _viewport.ScaleModeChanged -= OnScaleModeChanged;
    }
}
