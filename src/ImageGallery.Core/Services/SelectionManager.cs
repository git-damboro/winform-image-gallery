namespace ImageGallery.Core.Services;

public sealed class SelectionManager
{
    private readonly SortedSet<int> _selectedIndexes = new();
    private int? _anchorIndex;

    public IReadOnlyCollection<int> SelectedIndexes => _selectedIndexes;

    public bool HasSelection => _selectedIndexes.Count > 0;

    public void Clear()
    {
        _selectedIndexes.Clear();
        _anchorIndex = null;
    }

    public bool IsSelected(int index)
    {
        return _selectedIndexes.Contains(index);
    }

    public void Select(int index, int itemCount, bool ctrl, bool shift)
    {
        if (index < 0 || index >= itemCount)
        {
            return;
        }

        if (shift && _anchorIndex.HasValue)
        {
            var start = Math.Min(_anchorIndex.Value, index);
            var end = Math.Max(_anchorIndex.Value, index);

            if (!ctrl)
            {
                _selectedIndexes.Clear();
            }

            for (var i = start; i <= end; i++)
            {
                _selectedIndexes.Add(i);
            }

            return;
        }

        if (ctrl)
        {
            if (!_selectedIndexes.Add(index))
            {
                _selectedIndexes.Remove(index);
            }

            _anchorIndex = index;
            return;
        }

        _selectedIndexes.Clear();
        _selectedIndexes.Add(index);
        _anchorIndex = index;
    }

    public void SelectAll(int itemCount)
    {
        _selectedIndexes.Clear();
        for (var index = 0; index < itemCount; index++)
        {
            _selectedIndexes.Add(index);
        }

        _anchorIndex = itemCount > 0 ? 0 : null;
    }

    public IReadOnlyList<int> GetSelectedIndexesDescending()
    {
        return _selectedIndexes.Reverse().ToArray();
    }

    public void RemoveIndexes(IEnumerable<int> removedIndexes)
    {
        var removed = removedIndexes.OrderBy(i => i).ToArray();
        if (removed.Length == 0)
        {
            return;
        }

        var shifted = new SortedSet<int>();

        foreach (var index in _selectedIndexes)
        {
            if (Array.BinarySearch(removed, index) >= 0)
            {
                continue;
            }

            var smallerRemovedCount = removed.Count(removedIndex => removedIndex < index);
            shifted.Add(index - smallerRemovedCount);
        }

        _selectedIndexes.Clear();
        foreach (var index in shifted)
        {
            _selectedIndexes.Add(index);
        }

        _anchorIndex = _selectedIndexes.Count > 0 ? _selectedIndexes.Min : null;
    }
}
