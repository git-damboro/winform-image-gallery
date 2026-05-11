using ImageGallery.Core.Models;

namespace ImageGallery.Core.Services;

public static class ImageFilterPolicy
{
    public static IReadOnlyList<ImageItem> FilterByExtensions(
        IEnumerable<ImageItem> items,
        IEnumerable<string> extensions)
    {
        var normalizedExtensions = NormalizeExtensions(extensions);
        if (normalizedExtensions.Count == 0)
        {
            return items.ToArray();
        }

        return items
            .Where(item => normalizedExtensions.Contains(NormalizeExtension(item.Extension)))
            .ToArray();
    }

    public static IReadOnlyCollection<string> NormalizeExtensions(IEnumerable<string> extensions)
    {
        return extensions
            .Select(NormalizeExtension)
            .Where(extension => !string.IsNullOrWhiteSpace(extension))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var trimmed = extension.Trim();
        return trimmed.StartsWith('.') ? trimmed.ToLowerInvariant() : $".{trimmed.ToLowerInvariant()}";
    }
}
