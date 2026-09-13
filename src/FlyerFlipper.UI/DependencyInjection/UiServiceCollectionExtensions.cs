using FlyerFlipper.UI.ViewModels;
using FlyerFlipper.UI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace FlyerFlipper.UI.DependencyInjection;

public static class UiServiceCollectionExtensions
{
    public static IServiceCollection AddFlyerFlipperUi(this IServiceCollection services)
    {
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
        return services;
    }
}
