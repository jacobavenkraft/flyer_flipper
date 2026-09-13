using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using FlyerFlipper.UI.Processors;

namespace FlyerFlipper.UI.ViewModels;

/// <summary>One processor settings tab.</summary>
public sealed record ProcessorTab(string Header, Control Content);

/// <summary>
/// Builds the processor tab strip from every registered <see cref="IProcessorControlProvider"/>, in
/// <see cref="IProcessorControlProvider.Order"/> sequence.
/// </summary>
public sealed partial class ProcessorTabHostViewModel : ObservableObject
{
    private readonly IReadOnlyList<IProcessorControlProvider> _providers;
    private IReadOnlyList<ProcessorTab>? _tabs;

    [ObservableProperty]
    private int _selectedIndex;

    public ProcessorTabHostViewModel(IEnumerable<IProcessorControlProvider> providers)
    {
        _providers = providers.OrderBy(static p => p.Order).ToArray();
    }

    /// <summary>Created on first access (on the UI thread), since providers build Avalonia controls.</summary>
    public IReadOnlyList<ProcessorTab> Tabs
        => _tabs ??= _providers.Select(static p => new ProcessorTab(p.Header, p.CreateControl())).ToArray();

    public bool HasTabs => _providers.Count > 0;
}
