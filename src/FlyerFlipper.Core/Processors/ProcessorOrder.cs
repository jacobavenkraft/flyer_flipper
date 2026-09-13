namespace FlyerFlipper.Core.Processors;

/// <summary>
/// <see cref="Pipeline.IImageProcessor.Order"/> values for the built-in processors, also used to order
/// their settings tabs. Spaced out so future processors can slot in between.
/// </summary>
public static class ProcessorOrder
{
    public const int Grayscale = 100;

    public const int Resize = 200;
}
