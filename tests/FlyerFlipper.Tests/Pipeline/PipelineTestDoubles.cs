using System.Collections.Concurrent;
using FlyerFlipper.Core.Imaging;
using FlyerFlipper.Core.Pipeline;

namespace FlyerFlipper.Tests.Pipeline;

/// <summary>Records each call (source file name, input size) and optionally transforms the image.</summary>
internal sealed class RecordingProcessor(int order, string name, Func<ProcessedImage, ProcessedImage>? transform = null) : IImageProcessor
{
    public ConcurrentQueue<(string FileName, int Width, int Height)> Calls { get; } = new();

    public string Name { get; } = name;

    public int Order { get; } = order;

    /// <summary>Shared log across processors, to assert relative execution order.</summary>
    public ConcurrentQueue<string>? SharedLog { get; init; }

    public ProcessedImage Process(ProcessedImage input, CancellationToken cancellationToken)
    {
        input.Metadata.TryGetValue(ImageMetadataKeys.SourceFileName, out var fileName);
        Calls.Enqueue((fileName ?? string.Empty, input.Buffer.Width, input.Buffer.Height));
        SharedLog?.Enqueue(Name);
        return transform is null ? input : transform(input);
    }

    public int CallsFor(string fileName) => Calls.Count(c => c.FileName == fileName);
}

internal static class TestImages
{
    public static ImageBuffer Buffer(int width, int height)
        => new(new byte[width * height * 4], width, height, width * 4, ImagePixelFormat.Bgra8888Premultiplied);

    /// <summary>A processor transform that halves both dimensions (stands in for a resize processor).</summary>
    public static ProcessedImage Halve(ProcessedImage image)
        => image with { Buffer = Buffer(Math.Max(1, image.Buffer.Width / 2), Math.Max(1, image.Buffer.Height / 2)) };
}
