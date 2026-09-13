using SkiaSharp;

namespace FlyerFlipper.Imaging;

internal static class SkiaOrientation
{
    /// <summary>
    /// Returns a new bitmap with <paramref name="origin"/> (EXIF orientation) undone, or
    /// <see langword="null"/> when the source is already upright.
    /// </summary>
    public static SKBitmap? Apply(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.TopLeft)
        {
            return null;
        }

        var swapsAxes = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var width = swapsAxes ? source.Height : source.Width;
        var height = swapsAxes ? source.Width : source.Height;

        var result = new SKBitmap(new SKImageInfo(width, height, source.ColorType, source.AlphaType));
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);

        // Canvas calls compose right-to-left: the last call is applied to the source first.
        switch (origin)
        {
            case SKEncodedOrigin.TopRight: // mirror horizontally
                canvas.Translate(width, 0);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.BottomRight: // rotate 180
                canvas.Translate(width, height);
                canvas.RotateDegrees(180);
                break;
            case SKEncodedOrigin.BottomLeft: // mirror vertically
                canvas.Translate(0, height);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftTop: // transpose: (x, y) -> (y, x)
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.RightTop: // rotate 90 clockwise
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom: // transverse: (x, y) -> (w - y, h - x)
                canvas.Translate(width, height);
                canvas.RotateDegrees(90);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.LeftBottom: // rotate 90 counter-clockwise
                canvas.Translate(0, height);
                canvas.RotateDegrees(270);
                break;
        }

        using var paint = new SKPaint { BlendMode = SKBlendMode.Src };
        canvas.DrawBitmap(source, 0, 0, paint);
        canvas.Flush();
        return result;
    }
}
