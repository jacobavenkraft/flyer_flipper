using Avalonia.Controls;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;
using FlyerFlipper.UI.ViewModels.Processors;
using FlyerFlipper.UI.Views.Processors;

namespace FlyerFlipper.UI.Processors;

public sealed class FlipControlProvider(ProcessorSettings<FlipOptions> settings) : IProcessorControlProvider
{
    public string Header => "Flip";

    public int Order => ProcessorOrder.Flip;

    public Control CreateControl()
        => new FlipSettingsView { DataContext = new FlipSettingsViewModel(settings) };
}
