namespace FlyerFlipper.Core.Processors;

/// <summary>Quarter-turn rotations, measured clockwise. Arbitrary angles are out of scope.</summary>
public enum RotationAngle
{
    None = 0,

    Clockwise90 = 90,

    Clockwise180 = 180,

    Clockwise270 = 270,
}

/// <summary>
/// Options for the rotate processor: turn the image by a quarter-turn multiple, clockwise.
/// </summary>
/// <remarks>
/// A 90° or 270° turn swaps the image's width and height, which is why the default pipeline order puts
/// this before the resize processor — see <see cref="ProcessorOrder"/>. That is only a default.
/// <para>
/// The angle is validated rather than merely typed. <c>System.Text.Json</c> happily deserializes any
/// number into an enum, so a saved settings file reading <c>"angle": 42</c> would otherwise produce a
/// genuine 42° rotation with clipped corners.
/// </para>
/// </remarks>
public sealed record RotateOptions
{
    public RotateOptions(bool enabled = false, RotationAngle angle = RotationAngle.None)
    {
        if (!Enum.IsDefined(angle))
        {
            throw new ArgumentOutOfRangeException(nameof(angle), angle, "Not a quarter-turn rotation.");
        }

        Enabled = enabled;
        Angle = angle;
    }

    public bool Enabled { get; }

    public RotationAngle Angle { get; }

    /// <summary>True when this would actually change the image.</summary>
    public bool HasEffect => Enabled && Angle != RotationAngle.None;

    /// <summary>True when applying it swaps the image's width and height.</summary>
    public bool SwapsDimensions => HasEffect && Angle is RotationAngle.Clockwise90 or RotationAngle.Clockwise270;
}
