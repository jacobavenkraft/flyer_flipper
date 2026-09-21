using Avalonia.Controls;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;
using FlyerFlipper.UI.ViewModels.Processors;
using FlyerFlipper.UI.Views.Processors;

namespace FlyerFlipper.UI.Processors;

public sealed class InvertControlProvider(ProcessorSettings<InvertOptions> settings) : IProcessorControlProvider
{
    public string Header => "Invert";

    public int Order => ProcessorOrder.Invert;

    public Control CreateControl()
        => new InvertSettingsView { DataContext = new InvertSettingsViewModel(settings) };
}
