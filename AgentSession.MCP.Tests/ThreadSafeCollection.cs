using System.Collections;
using System.Collections.Concurrent;

namespace AgentSession.MCP.Tests;

internal sealed class ThreadSafeCollection<T> : ICollection<T>, IReadOnlyCollection<T>
{
    private readonly ConcurrentQueue<T> _items = new();

    public int Count => _items.Count;
    public bool IsReadOnly => false;

    public void Add(T item) => _items.Enqueue(item);

    public void Clear()
    {
        while (_items.TryDequeue(out _)) { }
    }

    public bool Contains(T item) => _items.Contains(item);
    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public T[] ToArray() => _items.ToArray();
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items.ToArray()).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Remove(T item) => false;
}
