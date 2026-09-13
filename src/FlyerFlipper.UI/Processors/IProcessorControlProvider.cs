using Avalonia.Controls;

namespace FlyerFlipper.UI.Processors;

/// <summary>
/// Supplies the settings tab for one image processor. Resolved from DI (one per processor) and hosted by
/// the processor tab strip. Lives in the UI layer because it produces an Avalonia control (PLAN.md
/// decision 15); a future native plugin would return a <c>NativeControlHost</c>-based control here.
/// </summary>
public interface IProcessorControlProvider
{
    /// <summary>Tab header text.</summary>
    string Header { get; }

    /// <summary>Tab position; conventionally the paired processor's <c>Order</c>.</summary>
    int Order { get; }

    /// <summary>Creates the tab content. Called once, on the UI thread.</summary>
    Control CreateControl();
}
