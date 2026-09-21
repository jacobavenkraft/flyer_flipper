namespace FlyerFlipper.Core.Processors;

/// <summary>
/// Options for the invert processor: replace each colour with its opposite. Transparency is unchanged.
/// </summary>
/// <param name="Enabled">When false the processor passes images through unchanged.</param>
public sealed record InvertOptions(bool Enabled = false);
