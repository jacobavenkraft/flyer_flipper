using Avalonia.Controls;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;
using FlyerFlipper.UI.ViewModels.Processors;
using FlyerFlipper.UI.Views.Processors;

namespace FlyerFlipper.UI.Processors;

public sealed class RotateControlProvider(ProcessorSettings<RotateOptions> settings) : IProcessorControlProvider
{
    public string Header => "Rotate";

    public int Order => ProcessorOrder.Rotate;

    public Control CreateControl()
        => new RotateSettingsView { DataContext = new RotateSettingsViewModel(settings) };
}
