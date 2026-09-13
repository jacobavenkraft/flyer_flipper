namespace FlyerFlipper.Core.Viewport;

/// <summary>
/// How the single-image view scales the (processed) image to the viewport. Display only — pixels are unchanged.
/// </summary>
public enum ViewportScaleMode
{
    /// <summary>Scale up or down to fit, keeping aspect ratio.</summary>
    FitToWindow,

    /// <summary>Scale down to fit when larger than the viewport; otherwise show at actual size.</summary>
    FitWithoutEnlarging,

    /// <summary>Fill the viewport exactly, ignoring aspect ratio.</summary>
    StretchToFill,

    /// <summary>One image pixel per device-independent pixel, scrollable when larger than the viewport.</summary>
    ActualSize,
}
