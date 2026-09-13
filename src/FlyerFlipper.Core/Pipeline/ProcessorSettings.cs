using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace FlyerFlipper.Core.Pipeline;

/// <summary>
/// Holds a processor's current options as an immutable value. The processor reads <see cref="Current"/>
/// (any thread, once per image); its settings tab calls <see cref="Update"/> (UI thread).
/// Registered as a DI singleton shared by both.
/// </summary>
/// <typeparam name="TOptions">An immutable record, so equality detects real changes.</typeparam>
public sealed class ProcessorSettings<TOptions>
    where TOptions : class
{
    private TOptions _current;

    public ProcessorSettings(TOptions initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        _current = initial;
    }

    public TOptions Current => Volatile.Read(ref _current);

    /// <summary>Raised (on the caller's thread) after <see cref="Current"/> changes.</summary>
    public event EventHandler<TOptions>? Changed;

    /// <summary>Replaces the options; does nothing (and raises nothing) if they are equal to the current ones.</summary>
    public void Update(TOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (EqualityComparer<TOptions>.Default.Equals(Current, options))
        {
            return;
        }

        Volatile.Write(ref _current, options);
        Changed?.Invoke(this, options);
    }

    /// <summary>Serializes <see cref="Current"/> with source-generated (AOT-safe) metadata.</summary>
    public string ToJson(JsonTypeInfo<TOptions> typeInfo)
        => JsonSerializer.Serialize(Current, typeInfo);

    /// <summary>
    /// Deserializes options from <paramref name="json"/> and applies them. Returns false, changing nothing,
    /// when the JSON is malformed, empty, or rejected by the options type's validation.
    /// </summary>
    public bool TryUpdateFromJson(string json, JsonTypeInfo<TOptions> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(json);
        TOptions? options;
        try
        {
            options = JsonSerializer.Deserialize(json, typeInfo);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or NotSupportedException or InvalidOperationException)
        {
            return false;
        }

        if (options is null)
        {
            return false;
        }

        Update(options);
        return true;
    }
}
