namespace ImageGallery.Core.Services;

public static class FileFormatPolicy
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".bmp",
        ".gif",
        ".tif",
        ".tiff",
        ".ico",
        ".webp"
    };

    public static string FileDialogFilter =>
        "Image files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.ico;*.webp|All files|*.*";

    public static bool IsSupported(string pathOrExtension)
    {
        if (string.IsNullOrWhiteSpace(pathOrExtension))
        {
            return false;
        }

        var extension = pathOrExtension.StartsWith('.')
            ? pathOrExtension
            : Path.GetExtension(pathOrExtension);

        return SupportedExtensions.Contains(extension);
    }
}
