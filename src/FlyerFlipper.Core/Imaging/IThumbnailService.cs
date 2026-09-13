namespace FlyerFlipper.Core.Imaging;

public interface IThumbnailService
{
    /// <summary>
    /// Produces a copy of <paramref name="source"/> scaled to fit within a
    /// <paramref name="maxEdge"/> x <paramref name="maxEdge"/> box, preserving aspect ratio.
    /// Images already within the box are copied at their original size (never upscaled).
    /// </summary>
    ImageBuffer CreateThumbnail(ImageBuffer source, int maxEdge);
}
