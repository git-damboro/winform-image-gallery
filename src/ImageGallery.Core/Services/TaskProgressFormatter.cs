namespace ImageGallery.Core.Services;

public static class TaskProgressFormatter
{
    public static string FormatIdle()
    {
        return "就绪";
    }

    public static string Format(string taskName, int completed, int total)
    {
        if (total <= 0)
        {
            return taskName;
        }

        var safeCompleted = Math.Clamp(completed, 0, total);
        return $"{taskName} {safeCompleted}/{total}";
    }
}
