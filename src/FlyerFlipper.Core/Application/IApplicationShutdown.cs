namespace FlyerFlipper.Core.Application;

public interface IApplicationShutdown
{
    void Shutdown(int exitCode = 0);
}
