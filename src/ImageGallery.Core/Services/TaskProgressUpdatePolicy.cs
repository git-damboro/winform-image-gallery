namespace ImageGallery.Core.Services;

public sealed class TaskProgressUpdatePolicy
{
    private readonly int _total;
    private readonly int _minimumStep;
    private int _lastReported;

    public TaskProgressUpdatePolicy(int total, int minimumStep)
    {
        _total = Math.Max(0, total);
        _minimumStep = Math.Max(1, minimumStep);
    }

    public bool ShouldReport(int completed)
    {
        if (completed <= 0)
        {
            return false;
        }

        if (_lastReported == 0 || completed >= _total)
        {
            _lastReported = completed;
            return true;
        }

        if (completed - _lastReported < _minimumStep)
        {
            return false;
        }

        _lastReported = completed;
        return true;
    }
}
