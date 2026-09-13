using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace FlyerFlipper.UI.Views.Processors;

public partial class ResizeSettingsView : UserControl
{
    public ResizeSettingsView()
    {
        InitializeComponent();

        // Values commit on Enter, focus loss, or a spin — not per keystroke (decision 15b). NumericUpDown
        // updates its Value while typing, so its bindings use UpdateSourceTrigger=LostFocus and we push
        // explicitly for Enter and spins.
        foreach (var input in new[] { MaxWidthInput, MaxHeightInput })
        {
            input.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
            input.Spinned += OnInputSpinned;
        }
    }

    private static void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is NumericUpDown input)
        {
            Commit(input);
        }
    }

    // Spinned is raised before the value is stepped; commit once the step has been applied.
    private static void OnInputSpinned(object? sender, SpinEventArgs e)
    {
        if (sender is NumericUpDown input)
        {
            Dispatcher.UIThread.Post(() => Commit(input));
        }
    }

    private static void Commit(NumericUpDown input)
        => BindingOperations.GetBindingExpressionBase(input, NumericUpDown.ValueProperty)?.UpdateSource();
}
