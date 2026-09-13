using Avalonia;
using FlyerFlipper.Core.Application;
using FlyerFlipper.Core.Layout;
using FlyerFlipper.Core.Source;
using FlyerFlipper.Core.Viewport;
using FlyerFlipper.Imaging.DependencyInjection;
using FlyerFlipper.Infrastructure.DependencyInjection;
using FlyerFlipper.UI.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FlyerFlipper.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddSingleton<ILayoutModeService, LayoutModeService>();
        builder.Services.AddSingleton<IImageCatalog, ImageCatalog>();
        builder.Services.AddSingleton<IViewportModeService, ViewportModeService>();
        builder.Services.AddSingleton<IApplicationShutdown, AvaloniaApplicationShutdown>();
        builder.Services.AddFlyerFlipperInfrastructure();
        builder.Services.AddFlyerFlipperImaging();
        builder.Services.AddFlyerFlipperUi();

        using var host = builder.Build();
        App.Services = host.Services;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
