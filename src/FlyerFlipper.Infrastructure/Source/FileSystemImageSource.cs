using System.IO.Enumeration;
using FlyerFlipper.Core.Source;

namespace FlyerFlipper.Infrastructure.Source;

public sealed class FileSystemImageSource : IImageSource
{
    private static readonly char[] WildcardSeparators = [';', ','];

    public IReadOnlyList<ImageReference> Enumerate(ImageSourceQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.RootPath);

        var root = Path.GetFullPath(query.RootPath);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Folder not found: {root}");
        }

        var formatPatterns = ParseWildcards(query.FormatWildcard);
        var namePatterns = ParseWildcards(query.NameWildcard);

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = query.Recursive,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        };

        var results = new List<ImageReference>();
        foreach (var path in Directory.EnumerateFiles(root, "*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(path.AsSpan());
            if (MatchesAny(fileName, formatPatterns) && MatchesAny(fileName, namePatterns))
            {
                results.Add(new ImageReference(path));
            }
        }

        results.Sort(static (a, b) => ComparePaths(a.FullPath, b.FullPath));
        return results;
    }

    /// <summary>
    /// Orders paths case-insensitively so the grid reads naturally, then breaks ties ordinally.
    /// The tiebreak matters on case-sensitive file systems (Linux), where "A.jpg" and "a.jpg" can
    /// coexist: <see cref="List{T}.Sort(Comparison{T})"/> is unstable, so equal-comparing entries
    /// would otherwise land in an arbitrary order that varies between runs.
    /// </summary>
    internal static int ComparePaths(string a, string b)
    {
        var byName = StringComparer.OrdinalIgnoreCase.Compare(a, b);
        return byName != 0 ? byName : StringComparer.Ordinal.Compare(a, b);
    }

    private static string[] ParseWildcards(string? wildcard)
    {
        var patterns = (wildcard ?? string.Empty)
            .Split(WildcardSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return patterns.Length == 0 ? ["*"] : patterns;
    }

    private static bool MatchesAny(ReadOnlySpan<char> fileName, string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            if (FileSystemName.MatchesSimpleExpression(pattern, fileName, ignoreCase: true))
            {
                return true;
            }
        }

        return false;
    }
}
