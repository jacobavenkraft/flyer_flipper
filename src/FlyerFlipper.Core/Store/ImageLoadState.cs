namespace FlyerFlipper.Core.Store;

public enum ImageLoadState
{
    /// <summary>Not requested (or released). Thumbnails in this state are queued for generation.</summary>
    NotLoaded,
    Loading,
    Ready,
    Failed,
}
