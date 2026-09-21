using FlyerFlipper.Core.Imaging;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;

namespace FlyerFlipper.Imaging.Processors;

/// <summary>
/// Rewrites each colour channel from a chosen source channel, when enabled. Alpha is left alone.
/// </summary>
/// <remarks>
/// Done on the bytes rather than through an <c>SKColorFilter</c> colour matrix, which is how
/// <see cref="GrayscaleProcessor"/> works. Skia applies colour matrices to <em>unpremultiplied</em>
/// colours, so a matrix here would divide by alpha and multiply back — rounding every partially
/// transparent pixel, and destroying colour entirely where alpha is 0. Moving whole channels needs no
/// arithmetic at all: every channel in a premultiplied pixel carries the same alpha factor, so copying
/// one over another stays premultiplied and stays exact.
/// </remarks>
public sealed class ChannelMapProcessor : IConfigurableImageProcessor, IDisposable
{
    private readonly ProcessorSettings<ChannelMapOptions> _settings;
    private ChannelMapOptions _lastSeen;

    public ChannelMapProcessor(ProcessorSettings<ChannelMapOptions> settings)
    {
        _settings = settings;
        _lastSeen = settings.Current;
        _settings.Changed += OnSettingsChanged;
    }

    public int Order => ProcessorOrder.ChannelMap;

    public string SettingsId => "flyerflipper.channelmap";

    public event EventHandler? SettingsChanged;

    public string GetSettingsJson() => _settings.ToJson(ProcessorOptionsJsonContext.Default.ChannelMapOptions);

    public bool TryApplySettingsJson(string json)
        => _settings.TryUpdateFromJson(json, ProcessorOptionsJsonContext.Default.ChannelMapOptions);

    public ProcessedImage Process(ProcessedImage input, CancellationToken cancellationToken)
    {
        var options = _settings.Current;
        if (!options.HasEffect)
        {
            return input;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var buffer = input.Buffer;
        if (buffer.Format != ImagePixelFormat.Bgra8888Premultiplied)
        {
            throw new NotSupportedException($"Unsupported pixel format {buffer.Format}.");
        }

        var source = buffer.Pixels.Span;
        var pixels = new byte[source.Length];
        source.CopyTo(pixels);

        // Byte order within a pixel is B, G, R, A.
        var blueFrom = OffsetOf(options.Blue);
        var greenFrom = OffsetOf(options.Green);
        var redFrom = OffsetOf(options.Red);

        for (var y = 0; y < buffer.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = y * buffer.Stride;

            for (var x = 0; x < buffer.Width; x++)
            {
                var i = row + (x * 4);

                // Read all three before writing any: a mapping may read a channel it also overwrites.
                var b = source[i];
                var g = source[i + 1];
                var r = source[i + 2];

                pixels[i] = Pick(blueFrom, b, g, r);
                pixels[i + 1] = Pick(greenFrom, b, g, r);
                pixels[i + 2] = Pick(redFrom, b, g, r);

                // pixels[i + 3] (alpha) is already the copied original.
            }
        }

        var mapped = new ImageBuffer(pixels, buffer.Width, buffer.Height, buffer.Stride, buffer.Format);
        return input with { Buffer = mapped };
    }

    /// <summary>The byte offset within a BGRA pixel that <paramref name="channel"/> lives at.</summary>
    private static int OffsetOf(ColorChannel channel) => channel switch
    {
        ColorChannel.Blue => 0,
        ColorChannel.Green => 1,
        ColorChannel.Red => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, null),
    };

    private static byte Pick(int offset, byte b, byte g, byte r) => offset switch
    {
        0 => b,
        1 => g,
        _ => r,
    };

    private void OnSettingsChanged(object? sender, ChannelMapOptions e)
    {
        // Rearranging channels while the processor is off changes no output — don't reprocess for it.
        var affectsOutput = _lastSeen.HasEffect || e.HasEffect;
        _lastSeen = e;
        if (affectsOutput)
        {
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose() => _settings.Changed -= OnSettingsChanged;
}
