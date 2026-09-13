namespace FlyerFlipper.Core.Store;

public sealed record ImageStoreOptions
{
    /// <summary>Longest edge, in pixels, of generated thumbnails.</summary>
    public int ThumbnailMaxEdge { get; init; } = 256;

    /// <summary>Background thumbnail decodes allowed at once.</summary>
    public int MaxConcurrentThumbnailLoads { get; init; } = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);
}
