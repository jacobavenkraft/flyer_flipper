using System.Text.Json.Serialization;
using FlyerFlipper.Core.Processors;

namespace FlyerFlipper.Imaging.Processors;

/// <summary>Source-generated (AOT-safe) JSON metadata for the built-in processors' options.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GrayscaleOptions))]
[JsonSerializable(typeof(ResizeOptions))]
internal sealed partial class ProcessorOptionsJsonContext : JsonSerializerContext
{
}
