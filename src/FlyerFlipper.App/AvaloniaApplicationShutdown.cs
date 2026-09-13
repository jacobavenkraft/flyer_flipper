using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using FlyerFlipper.Core.Application;

namespace FlyerFlipper.App;

internal sealed class AvaloniaApplicationShutdown : IApplicationShutdown
{
    public void Shutdown(int exitCode = 0)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown(exitCode);
            return;
        }

        Environment.Exit(exitCode);
    }
}
