using System.Collections.Concurrent;
using FlyerFlipper.Core.Imaging;
using FlyerFlipper.Core.Source;
using FlyerFlipper.Core.Store;

namespace FlyerFlipper.Tests.Store;

internal sealed class FakeDisplayImage(int width, int height) : IDisposable
{
    public int Width { get; } = width;

    public int Height { get; } = height;

    public bool IsDisposed { get; private set; }

    public void Dispose() => IsDisposed = true;
}

internal sealed class FakeDisplayImageFactory : IDisplayImageFactory<FakeDisplayImage>
{
    public ConcurrentBag<FakeDisplayImage> Created { get; } = [];

    public FakeDisplayImage Create(ImageBuffer buffer)
    {
        var image = new FakeDisplayImage(buffer.Width, buffer.Height);
        Created.Add(image);
        return image;
    }
}

/// <summary>Scales dimensions only; pixel content is irrelevant to the store.</summary>
internal sealed class FakeThumbnailService : IThumbnailService
{
    public ImageBuffer CreateThumbnail(ImageBuffer source, int maxEdge)
    {
        var (width, height) = ThumbnailSizing.Fit(source.Width, source.Height, maxEdge);
        return new ImageBuffer(new byte[width * height * 4], width, height, width * 4, ImagePixelFormat.Bgra8888Premultiplied);
    }
}

/// <summary>
/// Returns 400x300 images, records every decode, and can hold a decode open (gate) or fail it.
/// </summary>
internal sealed class GatedImageLoader : IImageLoader
{
    public const int Width = 400;
    public const int Height = 300;

    private readonly ConcurrentDictionary<string, int> _started = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ManualResetEventSlim> _gates = new(StringComparer.OrdinalIgnoreCase);

    public ConcurrentDictionary<string, bool> Failing { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int Started(ImageReference reference) => _started.GetValueOrDefault(reference.FullPath);

    public int TotalStarted => _started.Values.Sum();

    /// <summary>Decodes of <paramref name="reference"/> block until <see cref="Open"/> is called.</summary>
    public void Close(ImageReference reference) => _gates[reference.FullPath] = new ManualResetEventSlim(false);

    public void Open(ImageReference reference)
    {
        if (_gates.TryGetValue(reference.FullPath, out var gate))
        {
            gate.Set();
        }
    }

    public SourceImage Load(ImageReference reference, CancellationToken cancellationToken = default)
    {
        _started.AddOrUpdate(reference.FullPath, 1, static (_, n) => n + 1);
        if (_gates.TryGetValue(reference.FullPath, out var gate))
        {
            gate.Wait(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (Failing.ContainsKey(reference.FullPath))
        {
            throw new ImageLoadException($"Corrupt image '{reference.FileName}'.");
        }

        return new SourceImage(
            reference,
            new ImageBuffer(new byte[Width * Height * 4], Width, Height, Width * 4, ImagePixelFormat.Bgra8888Premultiplied));
    }
}
