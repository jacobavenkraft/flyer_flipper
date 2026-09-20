namespace FlyerFlipper.Tests.TestSupport;

/// <summary>
/// Builds rooted fixture paths that are real paths on every platform.
/// </summary>
/// <remarks>
/// Hard-coded Windows literals such as <c>@"C:\flyers\a.png"</c> are a single long file name on
/// Linux, not a path. Anything deriving a name from one — <see cref="Core.Source.ImageReference.FileName"/>
/// and the <c>SourceFileName</c> metadata built from it — then silently returns the whole string,
/// so fixtures keyed on a file name stop matching. Build fixture paths through here instead.
/// </remarks>
internal static class TestPaths
{
    /// <summary>A rooted folder path: <c>C:\flyers</c> on Windows, <c>/flyers</c> elsewhere.</summary>
    public static string Folder(string name) => Path.Combine(Root, name);

    /// <summary>A rooted path to <paramref name="fileName"/> inside <paramref name="folderName"/>.</summary>
    public static string File(string folderName, string fileName) => Path.Combine(Folder(folderName), fileName);

    private static string Root => OperatingSystem.IsWindows() ? @"C:\" : "/";
}
