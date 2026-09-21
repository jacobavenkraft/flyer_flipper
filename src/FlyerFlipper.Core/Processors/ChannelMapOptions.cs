namespace FlyerFlipper.Core.Processors;

/// <summary>One of the three colour channels. Alpha is not remappable.</summary>
public enum ColorChannel
{
    Red,

    Green,

    Blue,
}

/// <summary>
/// Options for the channel map processor: choose where each output channel reads its value from.
/// </summary>
/// <remarks>
/// Written as destination ← source, one source per destination. That expresses every mapping — sending
/// one source to several destinations is just naming it for each of them, e.g. blue into all three is
/// <c>Red = Blue, Green = Blue, Blue = Blue</c> — while making a destination with two sources, which has
/// no meaning, impossible to represent.
/// <para>
/// No combination is treated as a mistake. Collapsing every channel onto one is a legitimate thing to
/// ask for, and the result is meant to look like that.
/// </para>
/// </remarks>
/// <param name="Enabled">When false the processor passes images through unchanged.</param>
/// <param name="Red">The channel the output's red comes from.</param>
/// <param name="Green">The channel the output's green comes from.</param>
/// <param name="Blue">The channel the output's blue comes from.</param>
public sealed record ChannelMapOptions
{
    public ChannelMapOptions(
        bool enabled = false,
        ColorChannel red = ColorChannel.Red,
        ColorChannel green = ColorChannel.Green,
        ColorChannel blue = ColorChannel.Blue)
    {
        // System.Text.Json will deserialize any number into an enum, so a hand-edited or corrupt
        // settings file could otherwise smuggle in a channel that does not exist.
        ThrowIfUndefined(red, nameof(red));
        ThrowIfUndefined(green, nameof(green));
        ThrowIfUndefined(blue, nameof(blue));

        Enabled = enabled;
        Red = red;
        Green = green;
        Blue = blue;
    }

    public bool Enabled { get; }

    public ColorChannel Red { get; }

    public ColorChannel Green { get; }

    public ColorChannel Blue { get; }

    /// <summary>True when this would actually change the image — i.e. it is enabled and not the identity map.</summary>
    public bool HasEffect
        => Enabled && (Red != ColorChannel.Red || Green != ColorChannel.Green || Blue != ColorChannel.Blue);

    private static void ThrowIfUndefined(ColorChannel channel, string parameterName)
    {
        if (!Enum.IsDefined(channel))
        {
            throw new ArgumentOutOfRangeException(parameterName, channel, "Not a colour channel.");
        }
    }
}
