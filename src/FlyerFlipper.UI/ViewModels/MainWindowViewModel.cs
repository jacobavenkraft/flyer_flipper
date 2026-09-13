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
    private readonly ILayoutModeService _layoutMode;
    private readonly IApplicationShutdown _shutdown;

    [ObservableProperty]
    private Dock _controlsDock;

    [ObservableProperty]
    private Thickness _controlsBorderThickness;

    public MainWindowViewModel(ILayoutModeService layoutMode, IApplicationShutdown shutdown)
    {
        _layoutMode = layoutMode;
        _shutdown = shutdown;
        ApplyOrientation(layoutMode.Orientation);
        _layoutMode.OrientationChanged += OnOrientationChanged;
    }

    [RelayCommand]
    private void ToggleOrientation() => _layoutMode.Toggle();

    [RelayCommand]
    private void Exit() => _shutdown.Shutdown();

    [RelayCommand]
    private void About() => Trace.WriteLine("Flyer Flipper — MVP Slice 1");

    private void OnOrientationChanged(object? sender, LayoutOrientation e) => ApplyOrientation(e);

    private void ApplyOrientation(LayoutOrientation orientation)
    {
        if (orientation == LayoutOrientation.Vertical)
        {
            ControlsDock = Dock.Top;
            ControlsBorderThickness = new Thickness(0, 0, 0, 1);
        }
        else
        {
            ControlsDock = Dock.Left;
            ControlsBorderThickness = new Thickness(0, 0, 1, 0);
        }
    }

    public void Dispose()
    {
        _layoutMode.OrientationChanged -= OnOrientationChanged;
    }
}
