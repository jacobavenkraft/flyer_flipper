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

    /// <summary>Header of the selected tab, or null when there is none. Persisted by name (decision 16g).</summary>
    public string? SelectedHeader => (uint)SelectedIndex < (uint)_providers.Count ? _providers[SelectedIndex].Header : null;

    /// <summary>Selects the tab with <paramref name="header"/>; returns false (selection unchanged) if there is none.</summary>
    public bool SelectTab(string header)
    {
        for (var i = 0; i < _providers.Count; i++)
        {
            if (string.Equals(_providers[i].Header, header, StringComparison.Ordinal))
            {
                SelectedIndex = i;
                return true;
            }
        }

        return false;
    }
}
