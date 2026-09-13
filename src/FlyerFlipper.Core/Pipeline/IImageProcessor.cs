namespace FlyerFlipper.Core.Pipeline;

/// <summary>
/// One step of the image-processing pipeline. In-process processors implement this directly; future
/// native plugins will be wrapped in an adapter that satisfies it.
/// </summary>
public interface IImageProcessor
{
    /// <summary>Position in the pipeline; lower runs first. Ties keep registration order.</summary>
    int Order { get; }

    /// <summary>
    /// Transforms <paramref name="input"/>. Must not mutate the input's buffer; return it unchanged to pass through.
    /// Called concurrently from background threads (thumbnail generation and full-size loads), so
    /// implementations must be thread-safe.
    /// </summary>
    ProcessedImage Process(ProcessedImage input, CancellationToken cancellationToken);
}
