namespace FlyerFlipper.Core.Imaging;

public static class ThumbnailSizing
{
    /// <summary>
    /// Fits <paramref name="width"/> x <paramref name="height"/> inside a square of
    /// <paramref name="maxEdge"/>, preserving aspect ratio and never upscaling.
    /// </summary>
    public static (int Width, int Height) Fit(int width, int height, int maxEdge)
        => FitWithin(width, height, maxEdge, maxEdge);

    /// <summary>
    /// Fits <paramref name="width"/> x <paramref name="height"/> inside
    /// <paramref name="maxWidth"/> x <paramref name="maxHeight"/>, preserving aspect ratio and never upscaling.
    /// </summary>
    public static (int Width, int Height) FitWithin(int width, int height, int maxWidth, int maxHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHeight);

        if (width <= maxWidth && height <= maxHeight)
        {
            return (width, height);
        }

        var scale = Math.Min((double)maxWidth / width, (double)maxHeight / height);
        return (
            Math.Clamp((int)Math.Round(width * scale), 1, maxWidth),
            Math.Clamp((int)Math.Round(height * scale), 1, maxHeight));
    }
}
