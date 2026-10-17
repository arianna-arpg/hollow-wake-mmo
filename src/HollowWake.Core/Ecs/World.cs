using System.Buffers;
using System.Runtime.CompilerServices;

namespace HollowWake.Core.Ecs;

/// <summary>
/// Entity and component storage for one simulation. Components are class instances stored per type;
/// queries iterate snapshots so systems may create and destroy entities while iterating.
/// Destruction can be deferred to the end of the tick with <see cref="DestroyDeferred"/>.
/// </summary>
public sealed class World
{
    private interface IStore
    {
        bool Remove(int index);
        bool Has(int index);
        object? GetBoxed(int index);
        int Count { get; }
        int[] RentSnapshot(out int count);
    }

    /// <summary>
    /// One component type's instances, addressed directly by entity index. The index list keeps the
    /// store's iteration order: insertion order, with a removed entry replaced by the last one.
    /// </summary>
    private sealed class Store<T> : IStore where T : class, IComponent
    {
        private T?[] _byIndex = Array.Empty<T?>();
        /// <summary>Where each entity index sits in <see cref="_indices"/>, plus one; zero when it has no component here.</summary>
        private int[] _position = Array.Empty<int>();
        private readonly List<int> _indices = new();

        public int Count => _indices.Count;

        public void Set(int index, T component)
        {
            if (index >= _position.Length)
            {
                var size = Math.Max(16, _position.Length * 2);
                while (size <= index)
                {
                    size *= 2;
                }
                Array.Resize(ref _byIndex, size);
                Array.Resize(ref _position, size);
            }
            if (_position[index] == 0)
            {
                _indices.Add(index);
                _position[index] = _indices.Count;
            }
            _byIndex[index] = component;
        }

        public bool TryGet(int index, out T component)
        {
            if ((uint)index < (uint)_position.Length && _position[index] != 0)
            {
                component = _byIndex[index]!;
                return true;
            }
            component = null!;
            return false;
        }

        public bool Has(int index) => (uint)index < (uint)_position.Length && _position[index] != 0;

        public object? GetBoxed(int index) => TryGet(index, out var c) ? c : null;

        public bool Remove(int index)
        {
            if (!Has(index))
            {
                return false;
            }
            var pos = _position[index] - 1;
            var last = _indices.Count - 1;
            var moved = _indices[last];
            _indices[pos] = moved;
            _position[moved] = pos + 1;
            _indices.RemoveAt(last);
            _position[index] = 0;
            _byIndex[index] = null;
            return true;
        }

        /// <summary>Copies the entity indices, in store order, into a buffer rented from the shared pool.</summary>
        public int[] RentSnapshot(out int count)
        {
            count = _indices.Count;
            var buffer = ArrayPool<int>.Shared.Rent(Math.Max(1, count));
            _indices.CopyTo(buffer);
            return buffer;
        }
    }

    private readonly Dictionary<Type, IStore> _stores = new();
    private readonly List<int> _generations = new();
    private readonly List<bool> _alive = new();
    private readonly Stack<int> _free = new();
    private readonly HashSet<int> _deferred = new();
    private int _aliveCount;

    public int Count => _aliveCount;

    public event Action<Entity>? Created;
    public event Action<Entity>? Destroying;

    // ------------------------------------------------------------ entities

    public Entity Create()
    {
        int index;
        if (_free.Count > 0)
        {
            index = _free.Pop();
            _generations[index]++;
            _alive[index] = true;
        }
        else
        {
            index = _generations.Count;
            _generations.Add(1);
            _alive.Add(true);
        }
        _aliveCount++;
        var e = new Entity(index, _generations[index]);
        Created?.Invoke(e);
        return e;
    }

    public bool IsAlive(Entity e) => e.Index >= 0 && e.Index < _alive.Count && _alive[e.Index] && _generations[e.Index] == e.Generation;

    public void Destroy(Entity e)
    {
        if (!IsAlive(e))
        {
            return;
        }
        Destroying?.Invoke(e);
        foreach (var store in _stores.Values)
        {
            store.Remove(e.Index);
        }
        _alive[e.Index] = false;
        _aliveCount--;
        _free.Push(e.Index);
        _deferred.Remove(e.Index);
    }

    /// <summary>Marks an entity for destruction at the next <see cref="Flush"/>. Safe from inside any system.</summary>
    public void DestroyDeferred(Entity e)
    {
        if (IsAlive(e))
        {
            _deferred.Add(e.Index);
        }
    }

    public bool IsDeferredDestroy(Entity e) => _deferred.Contains(e.Index);

    public int Flush()
    {
        if (_deferred.Count == 0)
        {
            return 0;
        }
        var list = _deferred.ToArray();
        var n = 0;
        foreach (var index in list)
        {
            if (index < _alive.Count && _alive[index])
            {
                Destroy(new Entity(index, _generations[index]));
                n++;
            }
        }
        _deferred.Clear();
        return n;
    }

    public IEnumerable<Entity> Entities
    {
        get
        {
            for (var i = 0; i < _alive.Count; i++)
            {
                if (_alive[i])
                {
                    yield return new Entity(i, _generations[i]);
                }
            }
        }
    }

    public Entity Resolve(long key)
    {
        var e = Entity.FromKey(key);
        return IsAlive(e) ? e : Entity.None;
    }

    // ------------------------------------------------------------ components

    // Every component type gets a process-wide slot number, so a world finds a type's store by array
    // index instead of hashing the type. Stores are also kept by type, in creation order, for whole-entity
    // walks (destroy, components of).
    private static int _slotCount;

    private static class SlotOf<T> where T : class, IComponent
    {
        public static readonly int Index = Interlocked.Increment(ref _slotCount) - 1;
    }

    private IStore?[] _bySlot = new IStore?[32];

    private Store<T>? Find<T>() where T : class, IComponent
    {
        var slot = SlotOf<T>.Index;
        var stores = _bySlot;
        return (uint)slot < (uint)stores.Length && stores[slot] is { } store ? Unsafe.As<Store<T>>(store) : null;
    }

    private Store<T> StoreFor<T>() where T : class, IComponent
    {
        var store = Find<T>();
        if (store is null)
        {
            store = new Store<T>();
            _stores[typeof(T)] = store;
            var slot = SlotOf<T>.Index;
            if (slot >= _bySlot.Length)
            {
                Array.Resize(ref _bySlot, Math.Max(slot + 1, _bySlot.Length * 2));
            }
            _bySlot[slot] = store;
        }
        return store;
    }

    public T Add<T>(Entity e, T component) where T : class, IComponent
    {
        EnsureAlive(e);
        StoreFor<T>().Set(e.Index, component);
        return component;
    }

    public T GetOrAdd<T>(Entity e) where T : class, IComponent, new()
    {
        if (TryGet<T>(e, out var c))
        {
            return c;
        }
        return Add(e, new T());
    }

    public bool TryGet<T>(Entity e, out T component) where T : class, IComponent
    {
        if (IsAlive(e) && Find<T>() is { } store && store.TryGet(e.Index, out component))
        {
            return true;
        }
        component = null!;
        return false;
    }

    public T? Get<T>(Entity e) where T : class, IComponent => TryGet<T>(e, out var c) ? c : null;

    public T Require<T>(Entity e) where T : class, IComponent =>
        TryGet<T>(e, out var c) ? c : throw new InvalidOperationException($"{e} has no {typeof(T).Name}.");

    public bool Has<T>(Entity e) where T : class, IComponent => IsAlive(e) && Find<T>() is { } s && s.Has(e.Index);

    public bool Remove<T>(Entity e) where T : class, IComponent => IsAlive(e) && Find<T>() is { } s && s.Remove(e.Index);

    public IEnumerable<IComponent> ComponentsOf(Entity e)
    {
        if (!IsAlive(e))
        {
            yield break;
        }
        foreach (var store in _stores.Values)
        {
            if (store.GetBoxed(e.Index) is IComponent c)
            {
                yield return c;
            }
        }
    }

    public int CountOf<T>() where T : class, IComponent => Find<T>()?.Count ?? 0;

    // ------------------------------------------------------------ queries
    //
    // A query snapshots the entity indices of one store when it starts (into a pooled buffer, so it does
    // not allocate per entity) and checks every component as it reaches each entity. Systems may create
    // and destroy entities and add or remove components while iterating: entities created during the
    // iteration are not visited, entities destroyed or stripped of a queried component before they are
    // reached are skipped. Multi-component queries snapshot the smallest store, so iteration order is
    // that store's order. It is deterministic, but no system should depend on one query's order.

    public IEnumerable<(Entity Entity, T1 C1)> Query<T1>() where T1 : class, IComponent
    {
        if (Find<T1>() is not { } store)
        {
            yield break;
        }
        var indices = store.RentSnapshot(out var count);
        try
        {
            for (var i = 0; i < count; i++)
            {
                var index = indices[i];
                if (index < _alive.Count && _alive[index] && store.TryGet(index, out var c1))
                {
                    yield return (new Entity(index, _generations[index]), c1);
                }
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(indices);
        }
    }

    public IEnumerable<(Entity Entity, T1 C1, T2 C2)> Query<T1, T2>() where T1 : class, IComponent where T2 : class, IComponent
    {
        if (Find<T1>() is not { } a || Find<T2>() is not { } b)
        {
            // A store appears with its first component; until then only the existing store can drive.
            foreach (var (e, c1) in Query<T1>())
            {
                if (TryGet<T2>(e, out var c2))
                {
                    yield return (e, c1, c2);
                }
            }
            yield break;
        }
        var indices = (a.Count <= b.Count ? (IStore)a : b).RentSnapshot(out var count);
        try
        {
            for (var i = 0; i < count; i++)
            {
                var index = indices[i];
                if (index < _alive.Count && _alive[index] && a.TryGet(index, out var c1) && b.TryGet(index, out var c2))
                {
                    yield return (new Entity(index, _generations[index]), c1, c2);
                }
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(indices);
        }
    }

    public IEnumerable<(Entity Entity, T1 C1, T2 C2, T3 C3)> Query<T1, T2, T3>() where T1 : class, IComponent where T2 : class, IComponent where T3 : class, IComponent
    {
        if (Find<T1>() is not { } a || Find<T2>() is not { } b || Find<T3>() is not { } c)
        {
            foreach (var (e, c1, c2) in Query<T1, T2>())
            {
                if (TryGet<T3>(e, out var c3))
                {
                    yield return (e, c1, c2, c3);
                }
            }
            yield break;
        }
        IStore driver = a.Count <= b.Count ? a : b;
        if (c.Count < driver.Count)
        {
            driver = c;
        }
        var indices = driver.RentSnapshot(out var count);
        try
        {
            for (var i = 0; i < count; i++)
            {
                var index = indices[i];
                if (index < _alive.Count && _alive[index] && a.TryGet(index, out var c1) && b.TryGet(index, out var c2) && c.TryGet(index, out var c3))
                {
                    yield return (new Entity(index, _generations[index]), c1, c2, c3);
                }
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(indices);
        }
    }

    private void EnsureAlive(Entity e)
    {
        if (!IsAlive(e))
        {
            throw new InvalidOperationException($"{e} is not alive.");
        }
    }
}
