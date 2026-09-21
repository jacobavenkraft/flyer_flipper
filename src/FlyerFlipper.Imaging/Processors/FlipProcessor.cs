using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;
using SkiaSharp;

namespace FlyerFlipper.Imaging.Processors;

/// <summary>Mirrors images left-to-right and/or top-to-bottom, when enabled.</summary>
public sealed class FlipProcessor : IConfigurableImageProcessor, IDisposable
{
    private readonly ProcessorSettings<FlipOptions> _settings;
    private FlipOptions _lastSeen;

    public FlipProcessor(ProcessorSettings<FlipOptions> settings)
    {
        _settings = settings;
        _lastSeen = settings.Current;
        _settings.Changed += OnSettingsChanged;
    }

    public int Order => ProcessorOrder.Flip;

    public string SettingsId => "flyerflipper.flip";

    public event EventHandler? SettingsChanged;

    public string GetSettingsJson() => _settings.ToJson(ProcessorOptionsJsonContext.Default.FlipOptions);

    public bool TryApplySettingsJson(string json)
        => _settings.TryUpdateFromJson(json, ProcessorOptionsJsonContext.Default.FlipOptions);

    public ProcessedImage Process(ProcessedImage input, CancellationToken cancellationToken)
    {
        var options = _settings.Current;
        if (!HasEffect(options))
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
            using (var paint = new SKPaint { BlendMode = SKBlendMode.Src })
            {
                // Negating an axis reflects about the origin, so shift the image back into view by its
                // own extent on that axis.
                canvas.Scale(options.MirrorHorizontally ? -1 : 1, options.MirrorVertically ? -1 : 1);
                canvas.Translate(
                    options.MirrorHorizontally ? -buffer.Width : 0,
                    options.MirrorVertically ? -buffer.Height : 0);

                canvas.DrawBitmap(source, 0, 0, paint);
                canvas.Flush();
            }

            return input with { Buffer = SkiaImageBuffers.ToImageBuffer(result) };
        }
    }

    private static bool HasEffect(FlipOptions options)
        => options.Enabled && (options.MirrorHorizontally || options.MirrorVertically);

    private void OnSettingsChanged(object? sender, FlipOptions e)
    {
        // Toggling an axis while flipping is off changes no output — don't reprocess the folder for it.
        var affectsOutput = HasEffect(_lastSeen) || HasEffect(e);
        _lastSeen = e;
        if (affectsOutput)
        {
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose() => _settings.Changed -= OnSettingsChanged;
}
