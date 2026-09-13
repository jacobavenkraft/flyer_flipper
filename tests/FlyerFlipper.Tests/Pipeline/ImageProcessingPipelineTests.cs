using System.Collections.Concurrent;
using FlyerFlipper.Core.Imaging;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Source;
using Moq;

namespace FlyerFlipper.Tests.Pipeline;

public class ImageProcessingPipelineTests
{
    private static ProcessedImage Input(int width = 40, int height = 20)
        => ProcessedImage.FromSource(new SourceImage(new ImageReference(@"C:\flyers\flyer.png"), TestImages.Buffer(width, height)));

    [Fact]
    public void NoProcessors_ReturnsInputUnchanged()
    {
        var pipeline = new ImageProcessingPipeline([]);
        var input = Input();

        Assert.Same(input, pipeline.Process(input));
    }

    [Fact]
    public void Processors_RunInOrderValue_NotRegistrationOrder()
    {
        var log = new ConcurrentQueue<string>();
        var pipeline = new ImageProcessingPipeline(
        [
            new RecordingProcessor(30, "third") { SharedLog = log },
            new RecordingProcessor(10, "first") { SharedLog = log },
            new RecordingProcessor(20, "second") { SharedLog = log },
        ]);

        pipeline.Process(Input());

        Assert.Equal(["first", "second", "third"], log);
        Assert.Equal(["first", "second", "third"], pipeline.Processors.Cast<RecordingProcessor>().Select(p => p.Name));
    }

    [Fact]
    public void EqualOrderValues_KeepRegistrationOrder()
    {
        var log = new ConcurrentQueue<string>();
        var pipeline = new ImageProcessingPipeline(
        [
            new RecordingProcessor(5, "a") { SharedLog = log },
            new RecordingProcessor(5, "b") { SharedLog = log },
            new RecordingProcessor(1, "before") { SharedLog = log },
            new RecordingProcessor(5, "c") { SharedLog = log },
        ]);

        pipeline.Process(Input());

        Assert.Equal(["before", "a", "b", "c"], log);
    }

    [Fact]
    public void EachProcessor_ReceivesPreviousOutput()
    {
        var halve = new RecordingProcessor(1, "halve", TestImages.Halve);
        var observe = new RecordingProcessor(2, "observe");
        var pipeline = new ImageProcessingPipeline([observe, halve]);

        var output = pipeline.Process(Input(40, 20));

        Assert.Equal(("flyer.png", 40, 20), Assert.Single(halve.Calls));
        Assert.Equal(("flyer.png", 20, 10), Assert.Single(observe.Calls));
        Assert.Equal((20, 10), (output.Buffer.Width, output.Buffer.Height));
        Assert.Equal("flyer.png", output.Metadata[ImageMetadataKeys.SourceFileName]);
    }

    [Fact]
    public void AlreadyCancelled_Throws_WithoutRunningProcessors()
    {
        var processor = new RecordingProcessor(1, "p");
        var pipeline = new ImageProcessingPipeline([processor]);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => pipeline.Process(Input(), cts.Token));
        Assert.Empty(processor.Calls);
    }

    [Fact]
    public void CancelledDuringAProcessor_StopsBeforeTheNext()
    {
        using var cts = new CancellationTokenSource();
        var cancelling = new RecordingProcessor(1, "cancels", image =>
        {
            cts.Cancel();
            return image;
        });
        var later = new RecordingProcessor(2, "later");
        var pipeline = new ImageProcessingPipeline([cancelling, later]);

        Assert.Throws<OperationCanceledException>(() => pipeline.Process(Input(), cts.Token));
        Assert.Single(cancelling.Calls);
        Assert.Empty(later.Calls);
    }

    [Fact]
    public void CancelledDuringLastProcessor_StillThrows()
    {
        using var cts = new CancellationTokenSource();
        var pipeline = new ImageProcessingPipeline([new RecordingProcessor(1, "cancels", image =>
        {
            cts.Cancel();
            return image;
        })]);

        Assert.Throws<OperationCanceledException>(() => pipeline.Process(Input(), cts.Token));
    }

    [Fact]
    public void ProcessorReceivesTheCallersToken()
    {
        using var cts = new CancellationTokenSource();
        var processor = new Mock<IImageProcessor>();
        processor.Setup(p => p.Process(It.IsAny<ProcessedImage>(), It.IsAny<CancellationToken>())).Returns((ProcessedImage i, CancellationToken _) => i);
        var pipeline = new ImageProcessingPipeline([processor.Object]);

        pipeline.Process(Input(), cts.Token);

        processor.Verify(p => p.Process(It.IsAny<ProcessedImage>(), cts.Token), Times.Once);
    }

    [Fact]
    public void ProcessorReturningNull_ThrowsInvalidOperation()
    {
        var processor = new Mock<IImageProcessor>();
        processor.Setup(p => p.Process(It.IsAny<ProcessedImage>(), It.IsAny<CancellationToken>())).Returns((ProcessedImage)null!);
        var pipeline = new ImageProcessingPipeline([processor.Object]);

        Assert.Throws<InvalidOperationException>(() => pipeline.Process(Input()));
    }

    [Fact]
    public void ProcessorsEnumerable_IsSnapshotAtConstruction()
    {
        var processors = new List<IImageProcessor> { new RecordingProcessor(1, "only") };
        var pipeline = new ImageProcessingPipeline(processors);

        processors.Add(new RecordingProcessor(2, "added later"));

        Assert.Single(pipeline.Processors);
    }
}
