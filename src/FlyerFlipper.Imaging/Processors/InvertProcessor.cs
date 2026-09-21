using FlyerFlipper.Core.Imaging;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;

namespace FlyerFlipper.Imaging.Processors;

/// <summary>Replaces each colour with its opposite, when enabled. Transparency is left alone.</summary>
/// <remarks>
/// The buffers are <em>premultiplied</em>, which decides the arithmetic. A stored channel holds
/// <c>colour × alpha</c>, so inverting the colour to <c>1 − colour</c> stores
/// <c>(1 − colour) × alpha</c> = <c>alpha − stored</c>. Hence <b>alpha − value</b>, not
/// <c>255 − value</c>: the naive form is only right for fully opaque pixels, and everywhere else it
/// produces a colour larger than its own alpha — not a valid premultiplied pixel, and it renders as a
/// bright halo. A fully transparent pixel stays untouched, since alpha and stored value are both 0.
/// <para>
/// Doing it on the bytes also keeps it exact, for the same reason
/// <see cref="ChannelMapProcessor"/> does: an <c>SKColorFilter</c> matrix would unpremultiply and
/// re-premultiply around the operation, rounding every partially transparent pixel.
/// </para>
/// </remarks>
public sealed class InvertProcessor : IConfigurableImageProcessor, IDisposable
{
    private readonly ProcessorSettings<InvertOptions> _settings;

    public InvertProcessor(ProcessorSettings<InvertOptions> settings)
    {
        _settings = settings;
        _settings.Changed += OnSettingsChanged;
    }

    public int Order => ProcessorOrder.Invert;

    public string SettingsId => "flyerflipper.invert";

    public event EventHandler? SettingsChanged;

    public string GetSettingsJson() => _settings.ToJson(ProcessorOptionsJsonContext.Default.InvertOptions);

    public bool TryApplySettingsJson(string json)
        => _settings.TryUpdateFromJson(json, ProcessorOptionsJsonContext.Default.InvertOptions);

    public ProcessedImage Process(ProcessedImage input, CancellationToken cancellationToken)
    {
        if (!_settings.Current.Enabled)
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

        for (var y = 0; y < buffer.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = y * buffer.Stride;

            for (var x = 0; x < buffer.Width; x++)
            {
                var i = row + (x * 4);
                var alpha = source[i + 3];

                pixels[i] = (byte)(alpha - source[i]);
                pixels[i + 1] = (byte)(alpha - source[i + 1]);
                pixels[i + 2] = (byte)(alpha - source[i + 2]);

                // pixels[i + 3] (alpha) is already the copied original.
            }
        }

        var inverted = new ImageBuffer(pixels, buffer.Width, buffer.Height, buffer.Stride, buffer.Format);
        return input with { Buffer = inverted };
    }

    private void OnSettingsChanged(object? sender, InvertOptions e) => SettingsChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose() => _settings.Changed -= OnSettingsChanged;
}
