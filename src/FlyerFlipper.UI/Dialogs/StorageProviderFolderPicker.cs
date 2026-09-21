using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace FlyerFlipper.UI.Dialogs;

/// <summary>
/// Folder picker backed by Avalonia's storage provider: the native dialog on Windows, the XDG desktop
/// portal on Linux, and Avalonia's managed dialog where no portal is installed (e.g. WSLg).
/// </summary>
/// <remarks>
/// The window is found at call time rather than injected, because the picker is registered before any
/// window exists. Same approach as <c>AvaloniaApplicationShutdown</c>.
/// </remarks>
public sealed class StorageProviderFolderPicker : IFolderPicker
{
    public async Task<string?> PickFolderAsync(string? startIn)
    {
        if (TopLevel.GetTopLevel(MainWindow()) is not { StorageProvider: { CanPickFolder: true } storage })
        {
            return null;
        }

        var options = new FolderPickerOpenOptions
        {
            Title = "Choose an image folder",
            AllowMultiple = false,
            SuggestedStartLocation = await StartLocationAsync(storage, startIn),
        };

        var folders = await storage.OpenFolderPickerAsync(options);

        // TryGetLocalPath returns null for locations with no file-system path, such as a portal
        // handing back a document-provider URI. Nothing downstream could open one of those.
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    private static Window? MainWindow()
        => (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    private static async Task<IStorageFolder?> StartLocationAsync(IStorageProvider storage, string? startIn)
    {
        if (string.IsNullOrWhiteSpace(startIn))
        {
            return null;
        }

        try
        {
            return await storage.TryGetFolderFromPathAsync(startIn);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // A saved folder that has since been deleted or become unreadable just means "no preference".
            return null;
        }
    }
}
