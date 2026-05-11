using ImageGallery.Core.Models;
using System.Text.Json;

namespace ImageGallery.Core.Services;

public sealed class GallerySessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public IReadOnlyList<string> Load(string filePath)
    {
        return LoadState(filePath).ImagePaths;
    }

    public GallerySessionState LoadState(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new GallerySessionState(Array.Empty<string>(), GalleryDisplayStyle.Crystal);
            }

            var json = File.ReadAllText(filePath);
            var state = JsonSerializer.Deserialize<GallerySessionStateDto>(json);
            var imagePaths = NormalizePaths(state?.ImagePaths ?? Array.Empty<string>()).ToArray();
            var displayStyle = ParseDisplayStyle(state?.DisplayStyle);
            return new GallerySessionState(imagePaths, displayStyle);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return new GallerySessionState(Array.Empty<string>(), GalleryDisplayStyle.Crystal);
        }
    }

    public void Save(string filePath, IEnumerable<string> imagePaths)
    {
        Save(filePath, imagePaths, GalleryDisplayStyle.Crystal);
    }

    public void Save(string filePath, IEnumerable<string> imagePaths, GalleryDisplayStyle displayStyle)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var state = new GallerySessionStateDto(
                NormalizePaths(imagePaths).ToArray(),
                displayStyle.ToString());
            var json = JsonSerializer.Serialize(state, JsonOptions);
            var tempFile = $"{filePath}.tmp";
            File.WriteAllText(tempFile, json);
            File.Move(tempFile, filePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
        }
    }

    private static IEnumerable<string> NormalizePaths(IEnumerable<string> imagePaths)
    {
        return imagePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static GalleryDisplayStyle ParseDisplayStyle(string? displayStyle)
    {
        if (!string.IsNullOrWhiteSpace(displayStyle) &&
            Enum.TryParse(displayStyle, ignoreCase: true, out GalleryDisplayStyle parsedStyle))
        {
            return parsedStyle;
        }

        return GalleryDisplayStyle.Crystal;
    }

    private sealed record GallerySessionStateDto(string[] ImagePaths, string? DisplayStyle);
}

public sealed record GallerySessionState(IReadOnlyList<string> ImagePaths, GalleryDisplayStyle DisplayStyle);
