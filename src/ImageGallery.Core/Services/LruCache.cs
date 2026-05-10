namespace ImageGallery.Core.Services;

public sealed class LruCache<TKey, TValue> : IDisposable
    where TKey : notnull
{
    private readonly int _capacity;
    private readonly Action<TValue>? _onEvicted;
    private readonly Dictionary<TKey, LinkedListNode<Entry>> _nodes = new();
    private readonly LinkedList<Entry> _entries = new();

    public LruCache(int capacity, Action<TValue>? onEvicted = null)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _capacity = capacity;
        _onEvicted = onEvicted;
    }

    public int Count => _nodes.Count;

    public bool ContainsKey(TKey key)
    {
        return _nodes.ContainsKey(key);
    }

    public bool TryGet(TKey key, out TValue value)
    {
        if (!_nodes.TryGetValue(key, out var node))
        {
            value = default!;
            return false;
        }

        _entries.Remove(node);
        _entries.AddFirst(node);
        value = node.Value.Value;
        return true;
    }

    public void Set(TKey key, TValue value)
    {
        if (_nodes.TryGetValue(key, out var existing))
        {
            var oldValue = existing.Value.Value;
            existing.Value = new Entry(key, value);
            _entries.Remove(existing);
            _entries.AddFirst(existing);
            _onEvicted?.Invoke(oldValue);
            return;
        }

        var node = new LinkedListNode<Entry>(new Entry(key, value));
        _entries.AddFirst(node);
        _nodes[key] = node;

        while (_nodes.Count > _capacity)
        {
            EvictLast();
        }
    }

    public void Clear()
    {
        foreach (var entry in _entries)
        {
            _onEvicted?.Invoke(entry.Value);
        }

        _entries.Clear();
        _nodes.Clear();
    }

    public void Dispose()
    {
        Clear();
    }

    private void EvictLast()
    {
        var last = _entries.Last;
        if (last == null)
        {
            return;
        }

        _entries.RemoveLast();
        _nodes.Remove(last.Value.Key);
        _onEvicted?.Invoke(last.Value.Value);
    }

    private readonly record struct Entry(TKey Key, TValue Value);
}
