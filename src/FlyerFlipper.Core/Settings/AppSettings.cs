using System.Text.Json;
using FlyerFlipper.Core.Layout;
using FlyerFlipper.Core.Viewport;

namespace FlyerFlipper.Core.Settings;

/// <summary>
/// Everything the app restores on the next launch (PLAN.md decision 16). A plain, serializable snapshot —
/// it knows nothing about individual processors, whose settings are opaque JSON keyed by settings id.
/// </summary>
public sealed record AppSettings
{
    public const int CurrentVersion = 1;

    /// <summary>Schema version, for future migrations.</summary>
    public int Version { get; init; } = CurrentVersion;

    /// <summary>Last successfully loaded image folder.</summary>
    public string? LastFolder { get; init; }

    public LayoutOrientation Orientation { get; init; } = LayoutOrientation.Vertical;

    public ViewportMode ViewportMode { get; init; } = ViewportMode.Grid;

    /// <summary>File name (not index) of the current image, so added or removed files don't shift it.</summary>
    public string? ViewedImageFileName { get; init; }

    public ViewportScaleMode ScaleMode { get; init; } = ViewportScaleMode.FitToWindow;

    /// <summary>Header text (not position) of the selected processor tab.</summary>
    public string? ActiveProcessorTab { get; init; }

    public WindowPlacement? Window { get; init; }

    /// <summary>Each configurable processor's settings JSON, keyed by its settings id.</summary>
    public IReadOnlyDictionary<string, JsonElement> Processors { get; init; } = new Dictionary<string, JsonElement>();
}
