using FlyerFlipper.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace FlyerFlipper.Imaging.Processors;

/// <summary>
/// Pass-through processor that logs every image leaving the pipeline. Registered in Debug builds only,
/// to make pipeline runs observable.
/// </summary>
public sealed partial class DiagnosticLoggingProcessor : IImageProcessor
{
    private readonly ILogger<DiagnosticLoggingProcessor> _logger;
    private long _runs;

    public DiagnosticLoggingProcessor(ILogger<DiagnosticLoggingProcessor> logger)
    {
        _logger = logger;
    }

    /// <summary>Runs last, so it reports the final output of the pipeline.</summary>
    public int Order => int.MaxValue;

    public ProcessedImage Process(ProcessedImage input, CancellationToken cancellationToken)
    {
        var run = Interlocked.Increment(ref _runs);
        input.Metadata.TryGetValue(ImageMetadataKeys.SourceFileName, out var fileName);
        LogProcessed(run, fileName ?? "(unknown)", input.Buffer.Width, input.Buffer.Height, Environment.CurrentManagedThreadId);
        return input;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Pipeline run #{Run}: {FileName} ({Width}x{Height}) on thread {ThreadId}")]
    private partial void LogProcessed(long run, string fileName, int width, int height, int threadId);
}
