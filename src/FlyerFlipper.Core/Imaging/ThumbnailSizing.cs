namespace FlyerFlipper.Core.Imaging;

public static class ThumbnailSizing
{
    /// <summary>
    /// Fits <paramref name="width"/> x <paramref name="height"/> inside a square of
    /// <paramref name="maxEdge"/>, preserving aspect ratio and never upscaling.
    /// </summary>
    public static (int Width, int Height) Fit(int width, int height, int maxEdge)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEdge);

        var longest = Math.Max(width, height);
        if (longest <= maxEdge)
        {
            return (width, height);
        }

        var scale = (double)maxEdge / longest;
        return (
            Math.Max(1, (int)Math.Round(width * scale)),
            Math.Max(1, (int)Math.Round(height * scale)));
    }
}
