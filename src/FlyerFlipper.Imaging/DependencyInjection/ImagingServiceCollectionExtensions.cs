using FlyerFlipper.Core.Imaging;
using Microsoft.Extensions.DependencyInjection;

namespace FlyerFlipper.Imaging.DependencyInjection;

public static class ImagingServiceCollectionExtensions
{
    public static IServiceCollection AddFlyerFlipperImaging(this IServiceCollection services)
    {
        services.AddSingleton<IImageLoader, SkiaImageLoader>();
        services.AddSingleton<IThumbnailService, SkiaThumbnailService>();
        return services;
    }
}
