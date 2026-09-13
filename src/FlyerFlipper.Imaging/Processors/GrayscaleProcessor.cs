using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;
using SkiaSharp;

namespace FlyerFlipper.Imaging.Processors;

/// <summary>Converts images to grayscale using Rec. 709 luma weights, when enabled.</summary>
public sealed class GrayscaleProcessor : IConfigurableImageProcessor, IDisposable
{
    // Rec. 709 / sRGB luma. Skia applies color matrices to unpremultiplied colors; alpha is preserved.
    private static readonly float[] LumaMatrix =
    [
        0.2126f, 0.7152f, 0.0722f, 0, 0,
        0.2126f, 0.7152f, 0.0722f, 0, 0,
        0.2126f, 0.7152f, 0.0722f, 0, 0,
        0,       0,       0,       1, 0,
    ];

    // Immutable native object; safe to share across concurrent Process calls.
    private static readonly SKColorFilter LumaFilter = SKColorFilter.CreateColorMatrix(LumaMatrix);

    private readonly ProcessorSettings<GrayscaleOptions> _settings;

    public GrayscaleProcessor(ProcessorSettings<GrayscaleOptions> settings)
    {
        _settings = settings;
        _settings.Changed += OnSettingsChanged;
    }

    public int Order => ProcessorOrder.Grayscale;

    public string SettingsId => "flyerflipper.grayscale";

    public event EventHandler? SettingsChanged;

    public string GetSettingsJson() => _settings.ToJson(ProcessorOptionsJsonContext.Default.GrayscaleOptions);

    public bool TryApplySettingsJson(string json)
        => _settings.TryUpdateFromJson(json, ProcessorOptionsJsonContext.Default.GrayscaleOptions);

    public ProcessedImage Process(ProcessedImage input, CancellationToken cancellationToken)
    {
        if (!_settings.Current.Enabled)
        {
            return input;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var buffer = input.Buffer;

        using var source = SkiaImageBuffers.WrapPinned(buffer, out var pin);
        using (pin)
        {
            using var result = new SKBitmap(SkiaImageBuffers.CreateInfo(buffer.Width, buffer.Height));
            using (var canvas = new SKCanvas(result))
            using (var paint = new SKPaint { ColorFilter = LumaFilter, BlendMode = SKBlendMode.Src })
            {
                canvas.DrawBitmap(source, 0, 0, paint);
                canvas.Flush();
            }

            return input with { Buffer = SkiaImageBuffers.ToImageBuffer(result) };
        }
    }

    private void OnSettingsChanged(object? sender, GrayscaleOptions e) => SettingsChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose() => _settings.Changed -= OnSettingsChanged;
}
