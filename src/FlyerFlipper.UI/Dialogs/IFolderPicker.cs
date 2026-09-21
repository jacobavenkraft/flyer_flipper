namespace FlyerFlipper.UI.Dialogs;

/// <summary>
/// Asks the user to choose a folder, using whatever dialog the platform provides.
/// </summary>
/// <remarks>
/// An interface so the view model can be tested without a window. The real implementation goes through
/// Avalonia's storage provider, which is the native picker on Windows and the XDG desktop portal on
/// Linux, with Avalonia's own managed dialog as the fallback where no portal is installed.
/// </remarks>
public interface IFolderPicker
{
    /// <summary>
    /// Shows the picker and returns the chosen folder's local path, or <see langword="null"/> if the
    /// user cancelled or no path could be resolved.
    /// </summary>
    /// <param name="startIn">A folder to open at, if it still exists. Ignored when null or missing.</param>
    Task<string?> PickFolderAsync(string? startIn);
}
