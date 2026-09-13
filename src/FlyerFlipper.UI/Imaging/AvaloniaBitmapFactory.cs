using Avalonia.Media.Imaging;
using FlyerFlipper.Core.Imaging;
using FlyerFlipper.Core.Store;

namespace FlyerFlipper.UI.Imaging;

/// <summary>Display images for the store are Avalonia bitmaps.</summary>
public sealed class AvaloniaBitmapFactory : IDisplayImageFactory<Bitmap>
{
    public Bitmap Create(ImageBuffer buffer) => ImageBufferBitmap.Create(buffer);
}
