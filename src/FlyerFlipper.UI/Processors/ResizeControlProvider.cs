using Avalonia.Controls;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;
using FlyerFlipper.UI.ViewModels.Processors;
using FlyerFlipper.UI.Views.Processors;

namespace FlyerFlipper.UI.Processors;

public sealed class ResizeControlProvider(ProcessorSettings<ResizeOptions> settings) : IProcessorControlProvider
{
    public string Header => "Resize";

    public int Order => ProcessorOrder.Resize;

    public Control CreateControl()
        => new ResizeSettingsView { DataContext = new ResizeSettingsViewModel(settings) };
}
