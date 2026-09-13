using Avalonia.Media.Imaging;
using FlyerFlipper.Core.Settings;
using FlyerFlipper.Core.Store;
using FlyerFlipper.UI.Imaging;
using FlyerFlipper.UI.Settings;
using FlyerFlipper.UI.ViewModels;
using FlyerFlipper.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlyerFlipper.UI.DependencyInjection;

public static class UiServiceCollectionExtensions
{
    public static IServiceCollection AddFlyerFlipperUi(this IServiceCollection services)
    {
        // The store lives in Core but is closed over the UI's display image type.
        services.AddSingleton(new ImageStoreOptions());
        services.AddSingleton<IDisplayImageFactory<Bitmap>, AvaloniaBitmapFactory>();
        services.AddSingleton<IImageStore<Bitmap>, ImageStore<Bitmap>>();

        services.AddSingleton<ImageSourceViewModel>();
        services.AddSingleton<ThumbnailGridViewModel>();
        services.AddSingleton<SingleImageViewModel>();
        services.AddSingleton<ProcessorTabHostViewModel>();

        // Settings persistence (decision 16).
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<WindowPlacementTracker>();
        services.AddSingleton<IWindowPlacementSource>(static sp => sp.GetRequiredService<WindowPlacementTracker>());
        services.AddSingleton<SettingsCoordinator>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
        return services;
    }
}
