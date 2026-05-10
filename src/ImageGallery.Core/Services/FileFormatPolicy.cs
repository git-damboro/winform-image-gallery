namespace ImageGallery.Core.Services;

public static class FileFormatPolicy
{
    private static readonly HashSet<string> NativeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".bmp",
        ".gif",
        ".tif",
        ".tiff",
        ".ico"
    };

    private static readonly HashSet<string> RecognizedExtensions = new(NativeExtensions, StringComparer.OrdinalIgnoreCase)
    {
        ".webp",
        ".heic",
        ".heif",
        ".avif"
    };

    public static string FileDialogFilter =>
        "Native image files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.ico|Recognized modern formats|*.webp;*.heic;*.heif;*.avif|All files|*.*";

    public static bool IsSupported(string pathOrExtension)
    {
        return IsNativelyDecodable(pathOrExtension);
    }

    public static bool IsNativelyDecodable(string pathOrExtension)
    {
        return NativeExtensions.Contains(GetExtension(pathOrExtension));
    }

    public static bool IsRecognized(string pathOrExtension)
    {
        return RecognizedExtensions.Contains(GetExtension(pathOrExtension));
    }

    public static string GetSupportMessage(string pathOrExtension)
    {
        var extension = GetExtension(pathOrExtension);

        if (NativeExtensions.Contains(extension))
        {
            return "原生支持";
        }

        if (RecognizedExtensions.Contains(extension))
        {
            return "已识别格式，当前解码取决于系统或扩展解码库";
        }

        return "不支持的图片格式";
    }

    private static string GetExtension(string pathOrExtension)
    {
        if (string.IsNullOrWhiteSpace(pathOrExtension))
        {
            return string.Empty;
        }

        return pathOrExtension.StartsWith('.')
            ? pathOrExtension
            : Path.GetExtension(pathOrExtension);
    }
}
