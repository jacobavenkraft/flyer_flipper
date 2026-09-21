namespace FlyerFlipper.Core.Processors;

/// <summary>
/// Options for the flip processor: mirror the image about one or both axes.
/// </summary>
/// <remarks>
/// The property names say which way the pixels move, not which axis is the mirror line — "flip
/// horizontal" is read both ways in the wild. <see cref="MirrorHorizontally"/> swaps left and right
/// (reflecting about the vertical axis); <see cref="MirrorVertically"/> swaps top and bottom.
/// Both together are the same as rotating 180°.
/// </remarks>
/// <param name="Enabled">When false the processor passes images through unchanged.</param>
/// <param name="MirrorHorizontally">Swap left and right.</param>
/// <param name="MirrorVertically">Swap top and bottom.</param>
public sealed record FlipOptions(
    bool Enabled = false,
    bool MirrorHorizontally = false,
    bool MirrorVertically = false);
