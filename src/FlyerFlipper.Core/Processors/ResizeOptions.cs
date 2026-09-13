namespace FlyerFlipper.Core.Processors;

/// <summary>
/// Options for the resize processor: shrink images to fit inside <see cref="MaxWidth"/> x
/// <see cref="MaxHeight"/>, keeping aspect ratio and never enlarging (PLAN.md decision 15).
/// </summary>
public sealed record ResizeOptions
{
    public const int MinDimension = 1;

    public const int MaxDimension = 20_000;

    public ResizeOptions(bool enabled = false, int maxWidth = 800, int maxHeight = 800)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxWidth, MinDimension);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxWidth, MaxDimension);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxHeight, MinDimension);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxHeight, MaxDimension);

        Enabled = enabled;
        MaxWidth = maxWidth;
        MaxHeight = maxHeight;
    }

    public bool Enabled { get; }

    public int MaxWidth { get; }

    public int MaxHeight { get; }
}
