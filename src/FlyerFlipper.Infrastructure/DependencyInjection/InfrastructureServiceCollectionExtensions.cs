using FlyerFlipper.Core.Settings;
using FlyerFlipper.Core.Source;
using FlyerFlipper.Infrastructure.Settings;
using FlyerFlipper.Infrastructure.Source;
using Microsoft.Extensions.DependencyInjection;

namespace FlyerFlipper.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    /// <param name="services">The service collection.</param>
    /// <param name="settingsFilePath">Settings file location; defaults to <see cref="JsonSettingsStore.DefaultFilePath"/>.</param>
    public static IServiceCollection AddFlyerFlipperInfrastructure(this IServiceCollection services, string? settingsFilePath = null)
    {
        services.AddSingleton<IImageSource, FileSystemImageSource>();
        services.AddSingleton<ISettingsStore>(new JsonSettingsStore(settingsFilePath ?? JsonSettingsStore.DefaultFilePath));
        return services;
    }
}
