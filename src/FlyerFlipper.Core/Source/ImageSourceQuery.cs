namespace FlyerFlipper.Core.Source;

/// <summary>
/// Describes which images an <see cref="IImageSource"/> should enumerate.
/// </summary>
/// <param name="RootPath">Folder to search.</param>
/// <param name="Recursive">Whether sub-folders are searched too.</param>
/// <param name="FormatWildcard">
/// One or more file-name wildcards separated by <c>;</c> or <c>,</c> selecting image formats
/// (e.g. <c>*.jpg;*.png</c>). Matching is case-insensitive.
/// </param>
/// <param name="NameWildcard">
/// One or more file-name wildcards separated by <c>;</c> or <c>,</c> that a file must also match
/// (e.g. <c>IMG_*</c>). Matching is case-insensitive.
/// </param>
public sealed record ImageSourceQuery(
    string RootPath,
    bool Recursive = false,
    string FormatWildcard = ImageSourceQuery.DefaultFormatWildcard,
    string NameWildcard = ImageSourceQuery.DefaultNameWildcard)
{
    public const string DefaultFormatWildcard = "*.jpg;*.jpeg;*.png;*.bmp;*.webp";

    public const string DefaultNameWildcard = "*";
}
