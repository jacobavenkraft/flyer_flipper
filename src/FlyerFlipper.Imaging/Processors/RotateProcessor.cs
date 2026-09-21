using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;
using SkiaSharp;

namespace FlyerFlipper.Imaging.Processors;

/// <summary>Turns images by a quarter-turn multiple, clockwise, when enabled.</summary>
public sealed class RotateProcessor : IConfigurableImageProcessor, IDisposable
{
    private readonly ProcessorSettings<RotateOptions> _settings;
    private RotateOptions _lastSeen;

    public RotateProcessor(ProcessorSettings<RotateOptions> settings)
    {
        _settings = settings;
        _lastSeen = settings.Current;
        _settings.Changed += OnSettingsChanged;
    }

    public int Order => ProcessorOrder.Rotate;

    public string SettingsId => "flyerflipper.rotate";

    public event EventHandler? SettingsChanged;

    public string GetSettingsJson() => _settings.ToJson(ProcessorOptionsJsonContext.Default.RotateOptions);

    public bool TryApplySettingsJson(string json)
        => _settings.TryUpdateFromJson(json, ProcessorOptionsJsonContext.Default.RotateOptions);

    public ProcessedImage Process(ProcessedImage input, CancellationToken cancellationToken)
    {
        var options = _settings.Current;
        if (!options.HasEffect)
        {
            return input;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var buffer = input.Buffer;

        // A quarter turn swaps the axes, so the destination is the transpose of the source.
        var width = options.SwapsDimensions ? buffer.Height : buffer.Width;
        var height = options.SwapsDimensions ? buffer.Width : buffer.Height;

        using var source = SkiaImageBuffers.WrapPinned(buffer, out var pin);
        using (pin)
        {
            using var result = new SKBitmap(SkiaImageBuffers.CreateInfo(width, height));
            using (var canvas = new SKCanvas(result))
            using (var paint = new SKPaint { BlendMode = SKBlendMode.Src })
            {
                // Rotation is about the origin, so translate the corner that ends up at the origin.
                // Skia's y axis points down, so positive degrees are clockwise on screen.
                switch (options.Angle)
                {
                    case RotationAngle.Clockwise90:
                        canvas.Translate(width, 0);
                        break;
                    case RotationAngle.Clockwise180:
                        canvas.Translate(width, height);
                        break;
                    case RotationAngle.Clockwise270:
                        canvas.Translate(0, height);
                        break;
                }

                canvas.RotateDegrees((int)options.Angle);
                canvas.DrawBitmap(source, 0, 0, paint);
                canvas.Flush();
            }

            return input with { Buffer = SkiaImageBuffers.ToImageBuffer(result) };
        }
    }

    private void OnSettingsChanged(object? sender, RotateOptions e)
    {
        // Changing the angle while rotation is off changes no output — don't reprocess the folder for it.
        var affectsOutput = _lastSeen.HasEffect || e.HasEffect;
        _lastSeen = e;
        if (affectsOutput)
        {
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose() => _settings.Changed -= OnSettingsChanged;
}
