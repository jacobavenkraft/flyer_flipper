using FlyerFlipper.Core.Imaging;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Source;
using FlyerFlipper.Tests.TestSupport;

namespace FlyerFlipper.Tests.Pipeline;

public class ProcessedImageTests
{
    [Fact]
    public void FromSource_KeepsBuffer_AndRecordsSourceMetadata()
    {
        var buffer = TestImages.Buffer(4, 3);
        var path = TestPaths.File("flyers", "gig.jpg");
        var source = new SourceImage(new ImageReference(path), buffer);

        var image = ProcessedImage.FromSource(source);

        Assert.Same(buffer, image.Buffer);
        Assert.Equal(path, image.Metadata[ImageMetadataKeys.SourcePath]);
        Assert.Equal("gig.jpg", image.Metadata[ImageMetadataKeys.SourceFileName]);
    }

    [Fact]
    public void WithMetadata_ReturnsCopy_LeavingOriginalUntouched()
    {
        var original = ProcessedImage.FromSource(new SourceImage(new ImageReference(TestPaths.File("flyers", "gig.jpg")), TestImages.Buffer(4, 3)));

        var updated = original.WithMetadata("grayscale", "true");

        Assert.Equal("true", updated.Metadata["grayscale"]);
        Assert.Equal("gig.jpg", updated.Metadata[ImageMetadataKeys.SourceFileName]);
        Assert.False(original.Metadata.ContainsKey("grayscale"));
        Assert.Same(original.Buffer, updated.Buffer);
    }

    [Fact]
    public void Constructor_RequiresBufferAndMetadata()
    {
        Assert.Throws<ArgumentNullException>(() => new ProcessedImage(null!, new Dictionary<string, string>()));
        Assert.Throws<ArgumentNullException>(() => new ProcessedImage(TestImages.Buffer(1, 1), null!));
    }
}
