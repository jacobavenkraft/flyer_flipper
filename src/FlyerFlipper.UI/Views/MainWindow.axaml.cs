using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using FlyerFlipper.UI.ViewModels;

namespace FlyerFlipper.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    public MainWindow(MainWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    /// <summary>
    /// Single-image navigation keys. Handled on the tunnel pass so focused controls (buttons,
    /// scroll viewers) can't swallow the arrows first — except text input, which keeps its caret keys.
    /// Window KeyBindings aren't used here because they fire even while a TextBox has focus.
    /// </summary>
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        IRelayCommand? command = e.Key switch
        {
            Key.Left when !IsTextInput(e.Source) => viewModel.SingleImage.PreviousCommand,
            Key.Right when !IsTextInput(e.Source) => viewModel.SingleImage.NextCommand,
            Key.Escape => viewModel.SingleImage.BackToGridCommand,
            _ => null,
        };

        // Commands are disabled outside single-image mode, so keys pass through untouched there.
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
            e.Handled = true;
        }
    }

    private static bool IsTextInput(object? source)
        => source is TextBox || (source as Visual)?.FindAncestorOfType<TextBox>() is not null;
}
