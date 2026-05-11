namespace ImageGallery.Core.Services;

public static class PreviewNavigationPolicy
{
    public static int Move(int currentIndex, int itemCount, int delta)
    {
        if (itemCount <= 0)
        {
            return -1;
        }

        var normalizedCurrent = currentIndex % itemCount;
        if (normalizedCurrent < 0)
        {
            normalizedCurrent += itemCount;
        }

        var nextIndex = (normalizedCurrent + delta) % itemCount;
        if (nextIndex < 0)
        {
            nextIndex += itemCount;
        }

        return nextIndex;
    }
}
