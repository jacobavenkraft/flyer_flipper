using FlyerFlipper.Core.Source;
using FlyerFlipper.Infrastructure.Source;
using Microsoft.Extensions.DependencyInjection;

namespace FlyerFlipper.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddFlyerFlipperInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IImageSource, FileSystemImageSource>();
        return services;
    }
}
