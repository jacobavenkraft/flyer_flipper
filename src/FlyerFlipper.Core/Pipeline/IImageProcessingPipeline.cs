namespace FlyerFlipper.Core.Pipeline;

public interface IImageProcessingPipeline
{
    /// <summary>
    /// Runs every processor in <see cref="IImageProcessor.Order"/> sequence, feeding each the previous
    /// output. With no processors the input is returned unchanged. Thread-safe.
    /// </summary>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled between steps.</exception>
    ProcessedImage Process(ProcessedImage input, CancellationToken cancellationToken = default);
}
