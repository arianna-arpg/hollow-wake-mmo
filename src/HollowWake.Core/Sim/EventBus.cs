namespace HollowWake.Core.Sim;

/// <summary>Base of every simulation event. Events are plain records; systems, quests, networking and tests listen.</summary>
public abstract record GameEvent
{
    public long Tick { get; init; }
}

/// <summary>
/// Typed publish/subscribe plus a per-tick log. Publishing is synchronous and in order; the log lets
/// the server translate what happened this tick into network messages without every system knowing
/// about networking.
/// </summary>
/// <remarks>
/// Handlers for an event's exact type run first, then handlers for each base type up to
/// <see cref="GameEvent"/>, then handlers for every event. Each list is copied on write, so a publish
/// iterates the list as it stood when that type's turn came, and handlers may subscribe or unsubscribe
/// while being called. Publishing calls handlers directly and allocates nothing beyond the log entry.
/// </remarks>
public sealed class EventBus
{
    private abstract class Handlers
    {
        public abstract void Invoke(GameEvent evt);
    }

    private sealed class Handlers<T> : Handlers where T : GameEvent
    {
        public Action<T>[] List = Array.Empty<Action<T>>();

        public override void Invoke(GameEvent evt)
        {
            var list = List;
            if (list.Length == 0)
            {
                return;
            }
            var typed = (T)evt;
            foreach (var handler in list)
            {
                handler(typed);
            }
        }
    }

    private readonly Dictionary<Type, Handlers> _subscribers = new();
    private readonly Dictionary<Type, Type[]> _chains = new();
    private readonly List<GameEvent> _log = new();
    private Action<GameEvent>[] _anySubscribers = Array.Empty<Action<GameEvent>>();

    public IReadOnlyList<GameEvent> Log => _log;

    public void Subscribe<T>(Action<T> handler) where T : GameEvent
    {
        if (!_subscribers.TryGetValue(typeof(T), out var handlers))
        {
            handlers = new Handlers<T>();
            _subscribers[typeof(T)] = handlers;
        }
        var typed = (Handlers<T>)handlers;
        typed.List = Append(typed.List, handler);
    }

    public void Unsubscribe<T>(Action<T> handler) where T : GameEvent
    {
        if (_subscribers.TryGetValue(typeof(T), out var handlers))
        {
            var typed = (Handlers<T>)handlers;
            typed.List = RemoveFirst(typed.List, handler);
        }
    }

    public void SubscribeAll(Action<GameEvent> handler) => _anySubscribers = Append(_anySubscribers, handler);

    public void Publish<T>(T evt) where T : GameEvent
    {
        _log.Add(evt);
        // Dispatch to handlers registered for the exact type and for base types.
        foreach (var type in ChainOf(evt.GetType()))
        {
            if (_subscribers.TryGetValue(type, out var handlers))
            {
                handlers.Invoke(evt);
            }
        }
        foreach (var any in _anySubscribers)
        {
            any(evt);
        }
    }

    /// <summary>Returns this tick's events and clears the log.</summary>
    public List<GameEvent> Drain()
    {
        var copy = new List<GameEvent>(_log);
        _log.Clear();
        return copy;
    }

    public IEnumerable<T> OfType<T>() where T : GameEvent => _log.OfType<T>();

    /// <summary>An event type and its base types that are still game events, most derived first. Computed once per type.</summary>
    private Type[] ChainOf(Type type)
    {
        if (_chains.TryGetValue(type, out var chain))
        {
            return chain;
        }
        var list = new List<Type>();
        for (Type? t = type; t is not null && typeof(GameEvent).IsAssignableFrom(t); t = t.BaseType)
        {
            list.Add(t);
        }
        chain = list.ToArray();
        _chains[type] = chain;
        return chain;
    }

    private static TDelegate[] Append<TDelegate>(TDelegate[] list, TDelegate handler)
    {
        var next = new TDelegate[list.Length + 1];
        Array.Copy(list, next, list.Length);
        next[^1] = handler;
        return next;
    }

    private static TDelegate[] RemoveFirst<TDelegate>(TDelegate[] list, TDelegate handler) where TDelegate : Delegate
    {
        var at = Array.IndexOf(list, handler);
        if (at < 0)
        {
            return list;
        }
        var next = new TDelegate[list.Length - 1];
        Array.Copy(list, 0, next, 0, at);
        Array.Copy(list, at + 1, next, at, list.Length - at - 1);
        return next;
    }
}
