using Avalonia.Media.Imaging;
using FlyerFlipper.Core.Store;
using FlyerFlipper.UI.Imaging;
using FlyerFlipper.UI.ViewModels;
using FlyerFlipper.UI.Views;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
        return services;
    }
}
