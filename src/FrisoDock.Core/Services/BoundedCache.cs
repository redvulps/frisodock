namespace FrisoDock.Core.Services;

/// <summary>A concurrent LRU cache that shares loads and detaches in-flight entries on invalidation.</summary>
public sealed class BoundedCache<TKey, TValue> where TKey : notnull
{
    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<Entry>> _entries;
    private readonly LinkedList<Entry> _order = new();

    public BoundedCache(int capacity, IEqualityComparer<TKey>? comparer = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
        _entries = new Dictionary<TKey, LinkedListNode<Entry>>(comparer);
    }

    public TValue GetOrAdd(TKey key, Func<TValue> load)
    {
        LinkedListNode<Entry> node;
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out LinkedListNode<Entry>? existing))
            {
                node = existing;
                _order.Remove(node);
                _order.AddLast(node);
            }
            else
            {
                node = _order.AddLast(new Entry(key, new Lazy<TValue>(load)));
                _entries.Add(key, node);
                if (_entries.Count > _capacity)
                {
                    LinkedListNode<Entry> oldest = _order.First!;
                    _entries.Remove(oldest.Value.Key);
                    _order.RemoveFirst();
                }
            }
        }

        try
        {
            // The load runs outside the gate. Clear can detach this entry while it is running;
            // completing the load never inserts it back into the new cache generation.
            return node.Value.Value.Value;
        }
        catch
        {
            lock (_gate)
            {
                if (_entries.TryGetValue(key, out LinkedListNode<Entry>? current) && current == node)
                {
                    _entries.Remove(key);
                    _order.Remove(node);
                }
            }

            throw;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
            _order.Clear();
        }
    }

    private sealed record Entry(TKey Key, Lazy<TValue> Value);
}
