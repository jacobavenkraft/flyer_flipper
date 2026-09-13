using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FlyerFlipper.UI.Settings;
using FlyerFlipper.UI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace FlyerFlipper.App;

public partial class App : Avalonia.Application
{
    internal static IServiceProvider Services { get; set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Restore saved state (decision 16): layout, scaling, processors, and tab before the window is shown;
            // window placement as it is created; the folder and viewed image once it is open.
            var settings = Services.GetRequiredService<SettingsCoordinator>();
            var saved = settings.LoadAndApplyStartupState();

            var window = Services.GetRequiredService<MainWindow>();
            Services.GetRequiredService<WindowPlacementTracker>().Attach(window, saved.Window);

            void OnOpened(object? sender, EventArgs e)
            {
                window.Opened -= OnOpened;
                _ = settings.RestoreImagesAsync();
            }

            window.Opened += OnOpened;
            desktop.Exit += (_, _) => settings.Flush();
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
