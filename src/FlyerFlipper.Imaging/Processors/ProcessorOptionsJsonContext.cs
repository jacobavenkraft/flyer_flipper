using System.Text.Json.Serialization;
using FlyerFlipper.Core.Processors;

namespace FlyerFlipper.Imaging.Processors;

/// <summary>Source-generated (AOT-safe) JSON metadata for the built-in processors' options.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(GrayscaleOptions))]
[JsonSerializable(typeof(ChannelMapOptions))]
[JsonSerializable(typeof(InvertOptions))]
[JsonSerializable(typeof(ResizeOptions))]
[JsonSerializable(typeof(FlipOptions))]
[JsonSerializable(typeof(RotateOptions))]
internal sealed partial class ProcessorOptionsJsonContext : JsonSerializerContext
{
}
