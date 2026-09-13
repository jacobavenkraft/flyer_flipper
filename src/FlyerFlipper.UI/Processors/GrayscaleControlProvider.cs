using Avalonia.Controls;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;
using FlyerFlipper.UI.ViewModels.Processors;
using FlyerFlipper.UI.Views.Processors;

namespace FlyerFlipper.UI.Processors;

public sealed class GrayscaleControlProvider(ProcessorSettings<GrayscaleOptions> settings) : IProcessorControlProvider
{
    public string Header => "Grayscale";

    public int Order => ProcessorOrder.Grayscale;

    public Control CreateControl()
        => new GrayscaleSettingsView { DataContext = new GrayscaleSettingsViewModel(settings) };
}
