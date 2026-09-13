using Avalonia.Controls;
using FlyerFlipper.UI.ViewModels;

namespace FlyerFlipper.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
