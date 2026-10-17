using HollowWake.Core.Content;
using HollowWake.Core.Ecs;
using HollowWake.Core.Maths;
using HollowWake.Core.World;

namespace HollowWake.Core.Sim;

/// <summary>
/// One running simulation: normally a zone of the world, sometimes a headless arena in a tool or test.
/// Owns the entity world, the terrain window, the clock, the scheduler, the event bus, the seeded
/// randomness, the registered systems and a bag of services the systems share. Ticking runs the
/// systems in phase order and flushes deferred destruction.
/// </summary>
public sealed class Simulation
{
    private readonly List<ISimSystem> _systems = new();
    private readonly Dictionary<Type, object> _services = new();
    private bool _sorted;

    public ContentRegistry Content { get; }
    public Ecs.World World { get; } = new();
    public Terrain Terrain { get; }
    public SimClock Clock { get; }
    public Scheduler Scheduler { get; }
    public EventBus Events { get; } = new();
    public Rng Rng { get; }
    public ISimLog Log { get; set; } = NullSimLog.Instance;
    public WorldLayout Layout { get; }
    public SpatialGrid Spatial { get; } = new();

    /// <summary>The zone this simulation is responsible for, when it is a zone of the world.</summary>
    public ZoneCoord? Zone { get; init; }

    /// <summary>Shared world state (events, time of day, generator) when part of a world; null in arenas.</summary>
    public WorldGen.WorldGenerator? Generator { get; init; }

    public Simulation(ContentRegistry content, Terrain terrain, ulong seed, int ticksPerSecond = 20, WorldLayout? layout = null)
    {
        Content = content;
        Terrain = terrain;
        Layout = layout ?? WorldLayout.Default;
        Clock = new SimClock(ticksPerSecond);
        Scheduler = new Scheduler(Clock);
        Rng = new Rng(seed);
    }

    // ------------------------------------------------------------ services

    public void AddService<T>(T service) where T : class => _services[typeof(T)] = service;

    public T Service<T>() where T : class =>
        _services.TryGetValue(typeof(T), out var s) ? (T)s : throw new InvalidOperationException($"No service {typeof(T).Name} registered.");

    public bool TryService<T>(out T service) where T : class
    {
        if (_services.TryGetValue(typeof(T), out var s))
        {
            service = (T)s;
            return true;
        }
        service = null!;
        return false;
    }

    // ------------------------------------------------------------ systems

    public IReadOnlyList<ISimSystem> Systems
    {
        get
        {
            EnsureSorted();
            return _systems;
        }
    }

    public void AddSystem(ISimSystem system)
    {
        _systems.Add(system);
        _sorted = false;
    }

    public bool RemoveSystem(string name)
    {
        var removed = _systems.RemoveAll(s => s.Name == name) > 0;
        _sorted = false;
        return removed;
    }

    public T? GetSystem<T>() where T : class, ISimSystem => _systems.OfType<T>().FirstOrDefault();

    private void EnsureSorted()
    {
        if (_sorted)
        {
            return;
        }
        _systems.Sort((a, b) => a.Phase != b.Phase ? a.Phase.CompareTo(b.Phase) : a.Order.CompareTo(b.Order));
        _sorted = true;
    }

    // ------------------------------------------------------------ ticking

    /// <summary>Per-system cost for the last tick, when <see cref="Profile"/> is on.</summary>
    public sealed class SystemCost
    {
        public string Name = "";
        public double Milliseconds;
        public long AllocatedBytes;
        public double TotalMilliseconds;
        public long TotalAllocatedBytes;
        public long Ticks;
    }

    /// <summary>Turn on to measure every system's time and allocations each tick (tools and benchmarks).</summary>
    public bool Profile { get; set; }

    public IReadOnlyDictionary<string, SystemCost> Costs => _costs;

    private readonly Dictionary<string, SystemCost> _costs = new(StringComparer.Ordinal);

    /// <summary>
    /// Advances one fixed step. The event log holds the previous tick's events until this call clears it,
    /// so readers see "what happened last tick" and the log never grows. Exceptions in a system are
    /// logged and do not stop the tick.
    /// </summary>
    public void Tick()
    {
        Events.Drain();
        Clock.Advance();
        EnsureSorted();
        try
        {
            Scheduler.RunDue();
        }
        catch (Exception ex)
        {
            Log.Error("scheduler failed", ex);
        }
        foreach (var system in _systems)
        {
            long allocBefore = 0;
            long started = 0;
            if (Profile)
            {
                allocBefore = GC.GetAllocatedBytesForCurrentThread();
                started = System.Diagnostics.Stopwatch.GetTimestamp();
            }
            try
            {
                system.Update(this);
            }
            catch (Exception ex)
            {
                Log.Error($"system {system.Name} failed at tick {Clock.Tick}", ex);
            }
            if (Profile)
            {
                var ms = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                var bytes = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
                if (!_costs.TryGetValue(system.Name, out var cost))
                {
                    cost = new SystemCost { Name = system.Name };
                    _costs[system.Name] = cost;
                }
                cost.Milliseconds = ms;
                cost.AllocatedBytes = bytes;
                cost.TotalMilliseconds += ms;
                cost.TotalAllocatedBytes += bytes;
                cost.Ticks++;
            }
        }
        World.Flush();
    }

    public void Run(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            Tick();
        }
    }

    public void RunSeconds(double seconds) => Run((int)Math.Ceiling(seconds * Clock.TicksPerSecond));

    /// <summary>A deterministic generator for one purpose at this tick: seed, tick and salt decide it.</summary>
    public Rng RngFor(string salt) => new(Rng.Combine(Rng.Combine(Rng.Seed, (ulong)Clock.Tick), Rng.Hash64(salt)));

    public Rng RngFor(Entity e, string salt) => new(Rng.Combine(Rng.Combine(Rng.Seed, (ulong)e.Key), Rng.Combine((ulong)Clock.Tick, Rng.Hash64(salt))));
}
