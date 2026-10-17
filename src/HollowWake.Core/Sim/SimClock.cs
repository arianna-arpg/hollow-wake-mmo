namespace HollowWake.Core.Sim;

/// <summary>
/// Fixed-step simulation time. Everything in the simulation reads time from here, never from the
/// system clock, so a zone can be paused, replayed, or run faster than real time in tools.
/// </summary>
public sealed class SimClock
{
    public int TicksPerSecond { get; }
    public double Dt { get; }
    public long Tick { get; private set; }
    public double Time => Tick * Dt;

    public SimClock(int ticksPerSecond = 20)
    {
        TicksPerSecond = Math.Max(1, ticksPerSecond);
        Dt = 1.0 / TicksPerSecond;
    }

    public void Advance() => Tick++;

    public long TicksFromSeconds(double seconds) => (long)Math.Ceiling(Math.Max(0, seconds) * TicksPerSecond);

    public double SecondsUntil(long tick) => (tick - Tick) * Dt;
}

/// <summary>Deferred and repeating work keyed to simulation ticks.</summary>
public sealed class Scheduler
{
    private sealed class Entry
    {
        public long Tick;
        public long Seq;
        public Action Action = () => { };
        public double RepeatSeconds;
        public bool Cancelled;
        public int Handle;
    }

    private readonly PriorityQueue<Entry, (long, long)> _queue = new();
    private readonly Dictionary<int, Entry> _handles = new();
    private readonly SimClock _clock;
    private long _seq;
    private int _nextHandle = 1;

    public Scheduler(SimClock clock)
    {
        _clock = clock;
    }

    public int Pending => _queue.Count;

    /// <summary>Runs the action after a delay in seconds (at least next tick). Returns a cancel handle.</summary>
    public int After(double seconds, Action action)
    {
        var tick = _clock.Tick + Math.Max(1, _clock.TicksFromSeconds(seconds));
        return Enqueue(tick, action, 0);
    }

    public int AtTick(long tick, Action action) => Enqueue(Math.Max(tick, _clock.Tick + 1), action, 0);

    /// <summary>Runs the action every interval until cancelled. The first run happens after one interval.</summary>
    public int Every(double seconds, Action action)
    {
        var interval = Math.Max(_clock.Dt, seconds);
        var tick = _clock.Tick + Math.Max(1, _clock.TicksFromSeconds(interval));
        return Enqueue(tick, action, interval);
    }

    public bool Cancel(int handle)
    {
        if (_handles.TryGetValue(handle, out var e))
        {
            e.Cancelled = true;
            _handles.Remove(handle);
            return true;
        }
        return false;
    }

    /// <summary>Runs everything due at or before the clock's current tick.</summary>
    public int RunDue()
    {
        var ran = 0;
        while (_queue.TryPeek(out var entry, out var key) && key.Item1 <= _clock.Tick)
        {
            _queue.Dequeue();
            if (entry.Cancelled)
            {
                continue;
            }
            entry.Action();
            ran++;
            if (entry.RepeatSeconds > 0 && !entry.Cancelled)
            {
                entry.Tick = _clock.Tick + Math.Max(1, _clock.TicksFromSeconds(entry.RepeatSeconds));
                entry.Seq = _seq++;
                _queue.Enqueue(entry, (entry.Tick, entry.Seq));
            }
            else
            {
                _handles.Remove(entry.Handle);
            }
        }
        return ran;
    }

    private int Enqueue(long tick, Action action, double repeat)
    {
        var e = new Entry { Tick = tick, Seq = _seq++, Action = action, RepeatSeconds = repeat, Handle = _nextHandle++ };
        _handles[e.Handle] = e;
        _queue.Enqueue(e, (e.Tick, e.Seq));
        return e.Handle;
    }
}
