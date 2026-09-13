using System.Text.Json.Serialization;
using FlyerFlipper.Core.Settings;

namespace FlyerFlipper.Infrastructure.Settings;

/// <summary>Source-generated (AOT-safe) JSON metadata for the settings file.</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppSettingsJsonContext : JsonSerializerContext
{
}
