using Avalonia;
using FlyerFlipper.Core.Application;
using FlyerFlipper.Core.Layout;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;
using FlyerFlipper.Core.Source;
using FlyerFlipper.Core.Viewport;
using FlyerFlipper.Imaging.DependencyInjection;
using FlyerFlipper.Imaging.Processors;
using FlyerFlipper.Infrastructure.DependencyInjection;
using FlyerFlipper.UI.DependencyInjection;
using FlyerFlipper.UI.Processors;
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
        builder.Services.AddSingleton<IImageProcessingPipeline, ImageProcessingPipeline>();
        builder.Services.AddSingleton<IApplicationShutdown, AvaloniaApplicationShutdown>();
        // Optional "--settings-path <file>" (e.g. for testing without touching the real per-user settings).
        builder.Services.AddFlyerFlipperInfrastructure(builder.Configuration["settings-path"]);
        builder.Services.AddFlyerFlipperImaging();
        builder.Services.AddFlyerFlipperUi();

        // Processors (IImageProcessor), resolved by the pipeline in Order sequence. Each configurable
        // processor is paired with a settings object (shared with its tab) and an IProcessorControlProvider.
        builder.Services.AddSingleton(new ProcessorSettings<GrayscaleOptions>(new GrayscaleOptions()));
        builder.Services.AddSingleton<IImageProcessor, GrayscaleProcessor>();
        builder.Services.AddSingleton<IProcessorControlProvider, GrayscaleControlProvider>();

        builder.Services.AddSingleton(new ProcessorSettings<ResizeOptions>(new ResizeOptions()));
        builder.Services.AddSingleton<IImageProcessor, ResizeProcessor>();
        builder.Services.AddSingleton<IProcessorControlProvider, ResizeControlProvider>();

#if DEBUG
        builder.Services.AddSingleton<IImageProcessor, DiagnosticLoggingProcessor>();
#endif

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
