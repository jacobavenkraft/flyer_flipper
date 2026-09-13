using FlyerFlipper.Core.Imaging;
using FlyerFlipper.Core.Pipeline;
using FlyerFlipper.Core.Processors;
using SkiaSharp;

namespace FlyerFlipper.Imaging.Processors;

/// <summary>
/// When enabled, shrinks images to fit inside the configured maximum width × height, keeping aspect
/// ratio. Images already inside the box pass through unchanged (never enlarged).
/// </summary>
public sealed class ResizeProcessor : IConfigurableImageProcessor, IDisposable
{
    private readonly ProcessorSettings<ResizeOptions> _settings;
    private ResizeOptions _lastSeen;

    public ResizeProcessor(ProcessorSettings<ResizeOptions> settings)
    {
        _settings = settings;
        _lastSeen = settings.Current;
        _settings.Changed += OnSettingsChanged;
    }

    public int Order => ProcessorOrder.Resize;

    public string SettingsId => "flyerflipper.resize";

    public event EventHandler? SettingsChanged;

    public string GetSettingsJson() => _settings.ToJson(ProcessorOptionsJsonContext.Default.ResizeOptions);

    public bool TryApplySettingsJson(string json)
        => _settings.TryUpdateFromJson(json, ProcessorOptionsJsonContext.Default.ResizeOptions);

    public ProcessedImage Process(ProcessedImage input, CancellationToken cancellationToken)
    {
        var options = _settings.Current;
        if (!options.Enabled)
        {
            return input;
        }

        var buffer = input.Buffer;
        var (width, height) = ThumbnailSizing.FitWithin(buffer.Width, buffer.Height, options.MaxWidth, options.MaxHeight);
        if (width == buffer.Width && height == buffer.Height)
        {
            return input;
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var source = SkiaImageBuffers.WrapPinned(buffer, out var pin);
        using (pin)
        {
            using var resized = source.Resize(SkiaImageBuffers.CreateInfo(width, height), SKFilterQuality.High)
                ?? throw new InvalidOperationException($"SkiaSharp failed to resize {buffer.Width}x{buffer.Height} to {width}x{height}.");
            return input with { Buffer = SkiaImageBuffers.ToImageBuffer(resized) };
        }
    }

    private void OnSettingsChanged(object? sender, ResizeOptions e)
    {
        // Editing the dimensions while resizing is off doesn't change any output — don't reprocess the folder.
        var affectsOutput = _lastSeen.Enabled || e.Enabled;
        _lastSeen = e;
        if (affectsOutput)
        {
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose() => _settings.Changed -= OnSettingsChanged;
}
