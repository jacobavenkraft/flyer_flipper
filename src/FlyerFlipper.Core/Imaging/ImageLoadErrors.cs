using System.Diagnostics;
using FlyerFlipper.Core.Source;

namespace FlyerFlipper.Core.Imaging;

public static class ImageLoadErrors
{
    /// <summary>
    /// Logs <paramref name="exception"/> and returns a message suitable for showing in place of the image.
    /// </summary>
    public static string Describe(ImageReference reference, Exception exception)
    {
        Trace.WriteLine($"Image load failed for '{reference.FullPath}': {exception}");
        return exception is ImageLoadException or IOException or UnauthorizedAccessException
            ? exception.Message
            : "Could not load image.";
    }
}
