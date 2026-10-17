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
public sealed class EventBus
{
    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();
    private readonly List<GameEvent> _log = new();
    private readonly List<Action<GameEvent>> _anySubscribers = new();

    public IReadOnlyList<GameEvent> Log => _log;

    public void Subscribe<T>(Action<T> handler) where T : GameEvent
    {
        if (!_subscribers.TryGetValue(typeof(T), out var list))
        {
            list = new List<Delegate>();
            _subscribers[typeof(T)] = list;
        }
        list.Add(handler);
    }

    public void Unsubscribe<T>(Action<T> handler) where T : GameEvent
    {
        if (_subscribers.TryGetValue(typeof(T), out var list))
        {
            list.Remove(handler);
        }
    }

    public void SubscribeAll(Action<GameEvent> handler) => _anySubscribers.Add(handler);

    public void Publish<T>(T evt) where T : GameEvent
    {
        _log.Add(evt);
        var type = evt.GetType();
        // Dispatch to handlers registered for the exact type and for base types.
        while (type is not null && typeof(GameEvent).IsAssignableFrom(type))
        {
            if (_subscribers.TryGetValue(type, out var list))
            {
                foreach (var d in list.ToArray())
                {
                    d.DynamicInvoke(evt);
                }
            }
            type = type.BaseType;
        }
        foreach (var any in _anySubscribers.ToArray())
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
}
