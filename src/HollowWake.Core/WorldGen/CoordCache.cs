namespace HollowWake.Core.WorldGen;

/// <summary>
/// A thread-safe cache keyed by a pair of integer coordinates, for the generator's region and zone
/// caches. Reads take no lock: each bucket is a chain of immutable nodes published with volatile writes,
/// and growth publishes a whole new bucket array. Adds take a lock. As with
/// <c>ConcurrentDictionary.GetOrAdd</c>, two threads racing for one key may both build a value, but
/// both get back the one that was stored.
/// </summary>
/// <remarks>
/// One shared generic class over reference types is compiled once for every cache, where a
/// dictionary keyed by each coordinate struct is compiled again per key type; that compilation was a
/// visible part of planning the first zone.
/// </remarks>
internal sealed class CoordCache<T> where T : class
{
    private const int InitialSize = 64;

    private readonly object _lock = new();
    private Node?[] _buckets = new Node?[InitialSize];
    private int _count;

    public bool TryGet(int x, int y, out T value)
    {
        var buckets = Volatile.Read(ref _buckets);
        for (var n = Volatile.Read(ref buckets[Index(x, y, buckets.Length)]); n is not null; n = n.Next)
        {
            if (n.X == x && n.Y == y)
            {
                value = n.Value;
                return true;
            }
        }
        value = null!;
        return false;
    }

    public T GetOrAdd(int x, int y, Func<int, int, T> build)
    {
        if (TryGet(x, y, out var found))
        {
            return found;
        }
        var built = build(x, y);
        lock (_lock)
        {
            if (TryGet(x, y, out found))
            {
                return found;
            }
            var buckets = _buckets;
            if (_count >= buckets.Length)
            {
                buckets = Grow(buckets);
            }
            var i = Index(x, y, buckets.Length);
            Volatile.Write(ref buckets[i], new Node(x, y, built, buckets[i]));
            _count++;
            return built;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            Volatile.Write(ref _buckets, new Node?[InitialSize]);
            _count = 0;
        }
    }

    private Node?[] Grow(Node?[] old)
    {
        var grown = new Node?[old.Length * 2];
        foreach (var head in old)
        {
            for (var n = head; n is not null; n = n.Next)
            {
                var i = Index(n.X, n.Y, grown.Length);
                grown[i] = new Node(n.X, n.Y, n.Value, grown[i]);
            }
        }
        Volatile.Write(ref _buckets, grown);
        return grown;
    }

    private static int Index(int x, int y, int length) => (int)((uint)(x * 73856093 ^ y * 19349663) & (uint)(length - 1));

    private sealed class Node
    {
        public readonly int X;
        public readonly int Y;
        public readonly T Value;
        public readonly Node? Next;

        public Node(int x, int y, T value, Node? next)
        {
            X = x;
            Y = y;
            Value = value;
            Next = next;
        }
    }
}
