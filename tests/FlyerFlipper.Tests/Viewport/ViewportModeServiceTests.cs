using FlyerFlipper.Core.Source;
using FlyerFlipper.Core.Viewport;
using Moq;

namespace FlyerFlipper.Tests.Viewport;

public class ViewportModeServiceTests
{
    private readonly Mock<IImageCatalog> _catalog = new();
    private IReadOnlyList<ImageReference> _images = [];

    public ViewportModeServiceTests()
    {
        _catalog.SetupGet(c => c.Images).Returns(() => _images);
    }

    private ViewportModeService CreateWithImages(int count)
    {
        _images = Images(count);
        return new ViewportModeService(_catalog.Object);
    }

    private static ImageReference[] Images(int count)
        => Enumerable.Range(0, count).Select(i => new ImageReference($@"C:\flyers\{i}.png")).ToArray();

    private void ReplaceCatalog(int count)
    {
        _images = Images(count);
        _catalog.Raise(c => c.ImagesChanged += null, EventArgs.Empty);
    }

    [Fact]
    public void Defaults_EmptyCatalog_GridWithNoCurrentImage()
    {
        var service = CreateWithImages(0);

        Assert.Equal(ViewportMode.Grid, service.Mode);
        Assert.Equal(-1, service.CurrentIndex);
        Assert.Null(service.CurrentImage);
        Assert.False(service.CanMovePrevious);
        Assert.False(service.CanMoveNext);
    }

    [Fact]
    public void Defaults_PopulatedCatalog_CurrentIsFirstImage()
    {
        var service = CreateWithImages(3);

        Assert.Equal(ViewportMode.Grid, service.Mode);
        Assert.Equal(0, service.CurrentIndex);
        Assert.Equal(_images[0], service.CurrentImage);
    }

    [Fact]
    public void ShowSingle_EmptyCatalog_ReturnsFalseAndStaysInGrid()
    {
        var service = CreateWithImages(0);
        var raised = 0;
        service.ModeChanged += (_, _) => raised++;

        Assert.False(service.ShowSingle());
        Assert.False(service.ToggleMode());

        Assert.Equal(ViewportMode.Grid, service.Mode);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void ShowSingle_WithIndex_SetsIndexAndMode_RaisingBothEvents()
    {
        var service = CreateWithImages(5);
        ViewportMode? mode = null;
        var imageChanges = 0;
        service.ModeChanged += (_, m) => mode = m;
        service.CurrentImageChanged += (_, _) => imageChanges++;

        Assert.True(service.ShowSingle(3));

        Assert.Equal(ViewportMode.Single, mode);
        Assert.Equal(3, service.CurrentIndex);
        Assert.Equal(_images[3], service.CurrentImage);
        Assert.Equal(1, imageChanges);
    }

    [Fact]
    public void Select_ChangesCurrentImage_WithoutChangingMode()
    {
        var service = CreateWithImages(5);
        var modeChanges = 0;
        var imageChanges = 0;
        service.ModeChanged += (_, _) => modeChanges++;
        service.CurrentImageChanged += (_, _) => imageChanges++;

        service.Select(3);

        Assert.Equal(ViewportMode.Grid, service.Mode);
        Assert.Equal(3, service.CurrentIndex);
        Assert.Equal(1, imageChanges);
        Assert.Equal(0, modeChanges);
    }

    [Fact]
    public void ToggleMode_AfterSelect_OpensSelectedImage()
    {
        var service = CreateWithImages(5);

        service.Select(4);
        service.ToggleMode();

        Assert.Equal(ViewportMode.Single, service.Mode);
        Assert.Equal(4, service.CurrentIndex);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void Select_IndexOutOfRange_Throws(int index)
    {
        var service = CreateWithImages(5);

        Assert.Throws<ArgumentOutOfRangeException>(() => service.Select(index));
        Assert.Equal(0, service.CurrentIndex);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void ShowSingle_IndexOutOfRange_Throws(int index)
    {
        var service = CreateWithImages(5);

        Assert.Throws<ArgumentOutOfRangeException>(() => service.ShowSingle(index));
        Assert.Equal(ViewportMode.Grid, service.Mode);
    }

    [Fact]
    public void ToggleMode_FlipsBetweenGridAndSingle_KeepingCurrentIndex()
    {
        var service = CreateWithImages(5);
        service.ShowSingle(2);

        Assert.True(service.ToggleMode());
        Assert.Equal(ViewportMode.Grid, service.Mode);
        Assert.Equal(2, service.CurrentIndex);

        Assert.True(service.ToggleMode());
        Assert.Equal(ViewportMode.Single, service.Mode);
        Assert.Equal(2, service.CurrentIndex);
    }

    [Fact]
    public void SettingSameMode_DoesNotRaiseModeChanged()
    {
        var service = CreateWithImages(2);
        var raised = 0;
        service.ModeChanged += (_, _) => raised++;

        service.ShowGrid();
        service.ShowSingle();
        service.ShowSingle();

        Assert.Equal(1, raised);
    }

    [Fact]
    public void MoveNextAndPrevious_StopAtEnds_WithoutWrapping()
    {
        var service = CreateWithImages(3);
        service.ShowSingle(0);

        Assert.False(service.CanMovePrevious);
        Assert.False(service.MovePrevious());
        Assert.Equal(0, service.CurrentIndex);

        Assert.True(service.MoveNext());
        Assert.True(service.MoveNext());
        Assert.Equal(2, service.CurrentIndex);
        Assert.False(service.CanMoveNext);
        Assert.False(service.MoveNext());
        Assert.Equal(2, service.CurrentIndex);

        Assert.True(service.MovePrevious());
        Assert.Equal(1, service.CurrentIndex);
    }

    [Fact]
    public void Move_RaisesCurrentImageChanged_OnlyWhenIndexChanges()
    {
        var service = CreateWithImages(2);
        var raised = 0;
        service.CurrentImageChanged += (_, _) => raised++;

        service.MovePrevious();
        service.MoveNext();
        service.MoveNext();

        Assert.Equal(1, raised);
    }

    [Fact]
    public void CatalogReplaced_ResetsToFirstImage_KeepsSingleMode_AndRaisesEvenIfIndexUnchanged()
    {
        var service = CreateWithImages(5);
        service.ShowSingle(0);
        var raised = 0;
        service.CurrentImageChanged += (_, _) => raised++;

        ReplaceCatalog(3);

        Assert.Equal(ViewportMode.Single, service.Mode);
        Assert.Equal(0, service.CurrentIndex);
        Assert.Equal(_images[0], service.CurrentImage);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void CatalogReplacedWithEmpty_ReturnsToGrid()
    {
        var service = CreateWithImages(5);
        service.ShowSingle(4);
        ViewportMode? mode = null;
        service.ModeChanged += (_, m) => mode = m;

        ReplaceCatalog(0);

        Assert.Equal(ViewportMode.Grid, mode);
        Assert.Equal(-1, service.CurrentIndex);
        Assert.Null(service.CurrentImage);
    }

    [Fact]
    public void ScaleMode_DefaultsToFitToWindow_AndRaisesOnlyOnChange()
    {
        var service = CreateWithImages(1);
        var raised = new List<ViewportScaleMode>();
        service.ScaleModeChanged += (_, mode) => raised.Add(mode);

        Assert.Equal(ViewportScaleMode.FitToWindow, service.ScaleMode);
        service.SetScaleMode(ViewportScaleMode.FitToWindow);
        service.SetScaleMode(ViewportScaleMode.ActualSize);
        service.SetScaleMode(ViewportScaleMode.ActualSize);

        Assert.Equal(ViewportScaleMode.ActualSize, service.ScaleMode);
        Assert.Equal([ViewportScaleMode.ActualSize], raised);
    }

    [Fact]
    public void ScaleMode_IsIndependentOfViewportModeAndFolder()
    {
        var service = CreateWithImages(3);
        service.SetScaleMode(ViewportScaleMode.StretchToFill);

        service.ShowSingle(1);
        ReplaceCatalog(0);

        Assert.Equal(ViewportScaleMode.StretchToFill, service.ScaleMode);
    }

    [Fact]
    public void ScaleMode_UndefinedValue_Throws()
    {
        var service = CreateWithImages(1);

        Assert.Throws<ArgumentOutOfRangeException>(() => service.SetScaleMode((ViewportScaleMode)42));
    }

    [Fact]
    public void Dispose_UnsubscribesFromCatalog()
    {
        var service = CreateWithImages(5);
        service.ShowSingle(3);

        service.Dispose();
        ReplaceCatalog(2);

        Assert.Equal(3, service.CurrentIndex);
    }
}
