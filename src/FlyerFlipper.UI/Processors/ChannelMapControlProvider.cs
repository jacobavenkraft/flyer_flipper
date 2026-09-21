using Avalonia.Controls;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;
using FlyerFlipper.UI.ViewModels.Processors;
using FlyerFlipper.UI.Views.Processors;

namespace FlyerFlipper.UI.Processors;

public sealed class ChannelMapControlProvider(ProcessorSettings<ChannelMapOptions> settings) : IProcessorControlProvider
{
    public string Header => "Channels";

    public int Order => ProcessorOrder.ChannelMap;

    public Control CreateControl()
        => new ChannelMapSettingsView { DataContext = new ChannelMapSettingsViewModel(settings) };
}
