using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Source;
using FlyerFlipper.Core.Imaging;
using FlyerFlipper.Imaging.Processors;
using Microsoft.Extensions.Logging;

namespace FlyerFlipper.Tests.Pipeline;

public class DiagnosticLoggingProcessorTests
{
    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }

    [Fact]
    public void Process_PassesThrough_AndLogsEachRunWithFileNameAndSize()
    {
        var logger = new ListLogger<DiagnosticLoggingProcessor>();
        var processor = new DiagnosticLoggingProcessor(logger);
        var input = ProcessedImage.FromSource(new SourceImage(new ImageReference(@"C:\flyers\gig.jpg"), TestImages.Buffer(12, 8)));

        var first = processor.Process(input, CancellationToken.None);
        processor.Process(input, CancellationToken.None);

        Assert.Same(input, first);
        Assert.Equal(2, logger.Entries.Count);
        Assert.All(logger.Entries, e => Assert.Equal(LogLevel.Information, e.Level));
        Assert.StartsWith("Pipeline run #1: gig.jpg (12x8)", logger.Entries[0].Message);
        Assert.StartsWith("Pipeline run #2: gig.jpg (12x8)", logger.Entries[1].Message);
    }

    [Fact]
    public void RunsLast()
    {
        Assert.Equal(int.MaxValue, new DiagnosticLoggingProcessor(new ListLogger<DiagnosticLoggingProcessor>()).Order);
    }
}
