namespace FlyerFlipper.Core.Pipeline;

public interface IImageProcessingPipeline
{
    /// <summary>
    /// Raised (on the UI thread) when any processor's settings change, meaning images processed earlier
    /// no longer match what <see cref="Process"/> would now produce.
    /// </summary>
    event EventHandler? SettingsChanged;

    /// <summary>
    /// Runs every processor in <see cref="IImageProcessor.Order"/> sequence, feeding each the previous
    /// output. With no processors the input is returned unchanged. Thread-safe.
    /// </summary>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled between steps.</exception>
    ProcessedImage Process(ProcessedImage input, CancellationToken cancellationToken = default);
}
