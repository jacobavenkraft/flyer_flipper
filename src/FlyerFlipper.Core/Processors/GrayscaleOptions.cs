namespace FlyerFlipper.Core.Processors;

/// <param name="Enabled">When false the grayscale processor passes images through unchanged.</param>
public sealed record GrayscaleOptions(bool Enabled = false);
