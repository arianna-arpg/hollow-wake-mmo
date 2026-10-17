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
    }

    private sealed class Store<T> : IStore where T : class, IComponent
    {
        private readonly Dictionary<int, T> _byIndex = new();
        private readonly List<int> _indices = new();

        public int Count => _byIndex.Count;

        public void Set(int index, T component)
        {
            if (!_byIndex.ContainsKey(index))
            {
                _indices.Add(index);
            }
            _byIndex[index] = component;
        }

        public bool TryGet(int index, out T component) => _byIndex.TryGetValue(index, out component!);

        public bool Has(int index) => _byIndex.ContainsKey(index);

        public object? GetBoxed(int index) => _byIndex.TryGetValue(index, out var c) ? c : null;

        public bool Remove(int index)
        {
            if (_byIndex.Remove(index))
            {
                var pos = _indices.IndexOf(index);
                if (pos >= 0)
                {
                    var last = _indices.Count - 1;
                    _indices[pos] = _indices[last];
                    _indices.RemoveAt(last);
                }
                return true;
            }
            return false;
        }

        public int[] Snapshot() => _indices.ToArray();
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

    private Store<T> StoreFor<T>() where T : class, IComponent
    {
        if (!_stores.TryGetValue(typeof(T), out var store))
        {
            store = new Store<T>();
            _stores[typeof(T)] = store;
        }
        return (Store<T>)store;
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
        if (IsAlive(e) && _stores.TryGetValue(typeof(T), out var store) && ((Store<T>)store).TryGet(e.Index, out component!))
        {
            return true;
        }
        component = null!;
        return false;
    }

    public T? Get<T>(Entity e) where T : class, IComponent => TryGet<T>(e, out var c) ? c : null;

    public T Require<T>(Entity e) where T : class, IComponent =>
        TryGet<T>(e, out var c) ? c : throw new InvalidOperationException($"{e} has no {typeof(T).Name}.");

    public bool Has<T>(Entity e) where T : class, IComponent => IsAlive(e) && _stores.TryGetValue(typeof(T), out var s) && s.Has(e.Index);

    public bool Remove<T>(Entity e) where T : class, IComponent => IsAlive(e) && _stores.TryGetValue(typeof(T), out var s) && s.Remove(e.Index);

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

    public int CountOf<T>() where T : class, IComponent => _stores.TryGetValue(typeof(T), out var s) ? s.Count : 0;

    // ------------------------------------------------------------ queries

    public IEnumerable<(Entity Entity, T1 C1)> Query<T1>() where T1 : class, IComponent
    {
        if (!_stores.TryGetValue(typeof(T1), out var s1))
        {
            yield break;
        }
        var store = (Store<T1>)s1;
        foreach (var index in store.Snapshot())
        {
            if (index < _alive.Count && _alive[index] && store.TryGet(index, out var c1))
            {
                yield return (new Entity(index, _generations[index]), c1);
            }
        }
    }

    public IEnumerable<(Entity Entity, T1 C1, T2 C2)> Query<T1, T2>() where T1 : class, IComponent where T2 : class, IComponent
    {
        foreach (var (e, c1) in Query<T1>())
        {
            if (TryGet<T2>(e, out var c2))
            {
                yield return (e, c1, c2);
            }
        }
    }

    public IEnumerable<(Entity Entity, T1 C1, T2 C2, T3 C3)> Query<T1, T2, T3>() where T1 : class, IComponent where T2 : class, IComponent where T3 : class, IComponent
    {
        foreach (var (e, c1, c2) in Query<T1, T2>())
        {
            if (TryGet<T3>(e, out var c3))
            {
                yield return (e, c1, c2, c3);
            }
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
