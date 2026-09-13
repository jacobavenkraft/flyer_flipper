using FlyerFlipper.Core.Source;

namespace FlyerFlipper.Core.Imaging;

/// <summary>
/// An image as decoded from its source, already rotated to its display orientation.
/// </summary>
public sealed record SourceImage(ImageReference Reference, ImageBuffer Buffer);
