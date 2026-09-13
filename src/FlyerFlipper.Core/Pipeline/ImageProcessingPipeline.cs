namespace FlyerFlipper.Core.Pipeline;

public sealed class ImageProcessingPipeline : IImageProcessingPipeline
{
    private readonly IImageProcessor[] _processors;

    public ImageProcessingPipeline(IEnumerable<IImageProcessor> processors)
    {
        ArgumentNullException.ThrowIfNull(processors);

        // OrderBy is stable, so equal Order values keep DI registration order.
        _processors = processors.OrderBy(static p => p.Order).ToArray();
    }

    /// <summary>The processors in execution order.</summary>
    public IReadOnlyList<IImageProcessor> Processors => _processors;

    public ProcessedImage Process(ProcessedImage input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var current = input;
        foreach (var processor in _processors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            current = processor.Process(current, cancellationToken)
                ?? throw new InvalidOperationException($"{processor.GetType().Name} returned no image.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return current;
    }
}
