namespace FlyerFlipper.Core.Processors;

/// <summary>
/// The <em>default</em> <see cref="Pipeline.IImageProcessor.Order"/> values for the built-in processors,
/// also used to order their settings tabs. Spaced out so further processors can slot in between.
/// </summary>
/// <remarks>
/// These are starting values, not a fixed property of the pipeline: the architecture resolves processors
/// as <c>IEnumerable&lt;IImageProcessor&gt;</c> and sorts by <c>Order</c>, and letting the user reorder
/// them is a planned enhancement. Any order is legitimate, and reordering produces genuinely different
/// (and sometimes desirable) results — so nothing should present this sequence to the user as permanent.
/// <para>
/// Why this particular default: geometry before <see cref="Resize"/>, so that the resize box applies to
/// the final orientation rather than being undone by a later quarter turn swapping width and height.
/// <see cref="Flip"/> before <see cref="Rotate"/> because the two do not commute, and a default has to
/// pick one. <see cref="ChannelMap"/> before <see cref="Grayscale"/> so the two compose: rearranging
/// channels first changes which colours dominate the resulting luma, whereas after grayscale every
/// channel is already equal and moving them around would do nothing at all.
/// </para>
/// </remarks>
public static class ProcessorOrder
{
    public const int ChannelMap = 50;

    public const int Grayscale = 100;

    public const int Flip = 120;

    public const int Rotate = 150;

    public const int Resize = 200;
}
