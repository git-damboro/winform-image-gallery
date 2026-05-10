namespace ImageGallery.Core.Services;

public static class HumanReadableSizeFormatter
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public static string Format(long bytes)
    {
        if (bytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes));
        }

        var value = (double)bytes;
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < Units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0 || Math.Abs(value - Math.Round(value)) < 0.05
            ? $"{value:0} {Units[unitIndex]}"
            : $"{value:0.#} {Units[unitIndex]}";
    }
}
