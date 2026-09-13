using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace FlyerFlipper.Tests.Headless;

public class SingleImageViewLayoutTests
{
    [AvaloniaFact]
    public async Task ChevronGlyphs_AreCenteredInTheirButtons()
    {
        using var app = new AppHarness();
        var window = app.ShowWindow();
        await app.LoadFolderAsync(3);
        app.Viewport.ShowSingle(1); // middle image: both chevrons visible
        Dispatcher.UIThread.RunJobs();

        var chevrons = window.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("chevron")).ToList();
        Assert.Equal(2, chevrons.Count);

        foreach (var button in chevrons)
        {
            var path = button.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single();
            var buttonCenter = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;

            // Where the stroke is actually drawn: the geometry's bounds within the Path's own box.
            var geometry = path.Data!.Bounds;
            var glyphCenter = path.TranslatePoint(geometry.Center, window)!.Value;

            Assert.True(Math.Abs(glyphCenter.X - buttonCenter.X) < 0.5, $"{button.HorizontalAlignment} chevron off by {glyphCenter.X - buttonCenter.X:F1}px horizontally.");
            Assert.True(Math.Abs(glyphCenter.Y - buttonCenter.Y) < 0.5, $"{button.HorizontalAlignment} chevron off by {glyphCenter.Y - buttonCenter.Y:F1}px vertically.");
        }
    }
}
