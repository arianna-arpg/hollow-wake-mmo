using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;
using HollowWake.Protocol;
using HollowWake.Server.Config;

namespace HollowWake.Server.World;

/// <summary>
/// Everything one viewer has been told about a zone: which entities it knows, whether each is static,
/// the fingerprint of the state it was last sent, and when each is next due a refresh; plus the
/// self-state fingerprint and attribute cache, the snapshot settings it is served with, and whether it
/// takes deltas. A session owns one; a benchmark can hold one per player. It is reused across snapshots,
/// so a steady-state snapshot allocates little beyond the messages it sends.
/// </summary>
public sealed class SnapshotView
{
    internal sealed class Entry
    {
        public Entity Entity;
        public ulong Fingerprint;
        /// <summary>The viewer knows this entity: a spawn went out and no despawn since.</summary>
        public bool Sent;
        public bool Static;
        /// <summary>Changed since its last refresh, so the next refresh resends it once.</summary>
        public bool Dirty;
        /// <summary>The snapshot that last found it in range.</summary>
        public int Seen;
        public double NextRefreshAt;
        public double NextSweepAt;
        /// <summary>The state last sent, kept only for viewers that take deltas.</summary>
        public EntityState? Last;
    }

    internal readonly Dictionary<long, Entry> Entries = new();
    internal readonly List<EntityState> Changed = new();
    internal readonly List<EntityDelta> ChangedDeltas = new();
    internal int Epoch;
    internal ulong SelfFingerprint;
    internal double SelfSentAt = double.NegativeInfinity;

    /// <summary>Keys of the entities the viewer has been sent a spawn for and not yet a despawn.</summary>
    public HashSet<long> Known { get; } = new();

    public Snapshots.SelfCache SelfCache { get; } = new();

    /// <summary>Precision and redundancy for this viewer; a session gets the server's configured settings.</summary>
    public SnapshotSettings Settings { get; set; } = SnapshotSettings.Default;

    /// <summary>
    /// The viewer understands <see cref="EntityDeltaMsg"/>: changes go as deltas of the fields that
    /// changed, and full states only on refreshes. Off unless the client asks for it in its hello.
    /// </summary>
    public bool SendDeltas { get; set; }

    /// <summary>Entity snapshots built for this view.</summary>
    public long Frames { get; internal set; }
    public long Spawns { get; internal set; }
    /// <summary>Full entity states sent in updates (changes and refreshes).</summary>
    public long Updates { get; internal set; }
    /// <summary>Entity deltas sent, for a viewer that takes them.</summary>
    public long Deltas { get; internal set; }
    /// <summary>Full states resent on the refresh or sweep cadence without a change of their own.</summary>
    public long Refreshes { get; internal set; }
    public long Despawns { get; internal set; }
    public long SelfStates { get; internal set; }
    public long SelfStatesSkipped { get; internal set; }

    /// <summary>Forgets everything, as when the viewer enters a zone: the next snapshot spawns all it can see.</summary>
    public void Reset()
    {
        Entries.Clear();
        Changed.Clear();
        ChangedDeltas.Clear();
        Known.Clear();
        SelfCache.Clear();
        SelfFingerprint = 0;
        SelfSentAt = double.NegativeInfinity;
    }
}

/// <summary>
/// Builds what each viewer needs to hear about a zone, sending as little as keeps the viewer correct.
/// <list type="bullet">
/// <item>Interest is a radius around the viewer. Entities entering it are spawned; leaving it (or dying, for static ones) despawned.</item>
/// <item>Static entities (<see cref="Snapshots.IsStatic"/>) are spawned once with the static flag and never updated. They are re-verified on the refresh cadence; if one ever changes it is spawned again, which replaces its state.</item>
/// <item>Dynamic entities appear in an update only when their <see cref="Snapshots.Fingerprint"/> differs from the one last sent to this viewer. A viewer that takes deltas gets an <see cref="EntityDeltaMsg"/> with only the changed fields instead.</item>
/// <item>Refresh: on its refresh cadence, an entity that changed since its last refresh is sent as a full state (once more after it goes quiet), so a lost unreliable update or delta is repaired; every entity is resent in full on the sweep cadence regardless. Cadences are staggered by entity key so refreshes spread over snapshots.</item>
/// <item>The self state goes out when its <see cref="Snapshots.SelfFingerprint"/> changes, and at least every <see cref="SnapshotSettings.SelfMaxSeconds"/>.</item>
/// </list>
/// Cadences and precision come from each view's <see cref="SnapshotView.Settings"/>.
/// </summary>
/// <example>
/// A zone, the benchmark or a tool drives it the same way, with one view per viewer kept across snapshots:
/// <code>
/// var builder = new SnapshotBuilder(sim, interestRadius);
/// var view = new SnapshotView();                                  // default settings, full states
/// var output = new List&lt;Message&gt;();
/// builder.Entities(view, player, output);                        // every snapshot
/// if (builder.Self(view, player) is { } self) output.Add(self);  // every view.Settings.SelfSeconds
/// foreach (var m in output) bytes += MessageSerializer.Encode(m).Length;
/// </code>
/// </example>
public sealed class SnapshotBuilder
{
    private readonly Simulation _sim;

    /// <summary>
    /// Fingerprints and dynamic states already worked out this tick. Viewers near each other see the same
    /// entities, and building snapshots does not change the simulation, so each entity is fingerprinted and
    /// captured at most once per tick however many viewers it has. States are never mutated once built,
    /// so sharing them between messages is safe. Fingerprints depend on precision, so the cache also
    /// starts over when a viewer with different precision asks.
    /// </summary>
    private readonly Dictionary<long, (ulong Fingerprint, EntityState? State)> _tick = new();
    private long _tickNumber = -1;
    private double _tickResourceStep = double.NaN;
    private int _tickCastSteps = -1;

    public float InterestRadius { get; }

    public SnapshotBuilder(Simulation sim, float interestRadius)
    {
        _sim = sim;
        InterestRadius = interestRadius;
    }

    /// <summary>Forgets what was worked out this tick. Call it after changing the simulation between two viewers' snapshots in one tick.</summary>
    public void Invalidate()
    {
        _tick.Clear();
        _tickNumber = -1;
    }

    private void SyncTick(SnapshotSettings settings)
    {
        if (_tickNumber != _sim.Clock.Tick || _tickResourceStep != settings.ResourceStep || _tickCastSteps != settings.CastSteps)
        {
            _tick.Clear();
            _tickNumber = _sim.Clock.Tick;
            _tickResourceStep = settings.ResourceStep;
            _tickCastSteps = settings.CastSteps;
        }
    }

    private ulong FingerprintOf(Entity e, SnapshotSettings settings)
    {
        SyncTick(settings);
        if (_tick.TryGetValue(e.Key, out var known))
        {
            return known.Fingerprint;
        }
        var fingerprint = Snapshots.Fingerprint(_sim, e, settings.ResourceStep, settings.CastSteps);
        _tick[e.Key] = (fingerprint, null);
        return fingerprint;
    }

    private EntityState DynamicStateOf(Entity e, SnapshotSettings settings)
    {
        var fingerprint = FingerprintOf(e, settings);
        var known = _tick[e.Key];
        if (known.State is { } state)
        {
            return state;
        }
        state = Snapshots.Capture(_sim, e, isStatic: false);
        _tick[e.Key] = (fingerprint, state);
        return state;
    }

    /// <summary>
    /// One entity snapshot for a viewer. Appends spawns for entities that came into range (and re-spawns
    /// for static ones that changed), then despawns for those that left, then one update carrying the
    /// full states of dynamic entities that changed or are due a refresh, then (for a viewer that takes
    /// deltas) one delta message with the rest of the changes. Appends nothing when nothing changed.
    /// </summary>
    public void Entities(SnapshotView view, Entity viewer, List<Message> output)
    {
        var world = _sim.World;
        if (!world.TryGet<Transform>(viewer, out var me))
        {
            return;
        }
        var settings = view.Settings;
        var now = _sim.Clock.Time;
        var slack = _sim.Clock.Dt * 0.5;
        var epoch = ++view.Epoch;
        view.Frames++;
        view.Changed.Clear();
        view.ChangedDeltas.Clear();
        foreach (var (e, _, _) in _sim.Spatial.Within(me.Position, InterestRadius, (float)settings.InterestHeight))
        {
            if (!world.IsAlive(e))
            {
                continue;
            }
            var key = e.Key;
            if (!view.Entries.TryGetValue(key, out var entry))
            {
                entry = new SnapshotView.Entry { Entity = e, Seen = epoch, NextRefreshAt = now + settings.RefreshSeconds * Phase(key) };
                view.Entries.Add(key, entry);
                Introduce(view, entry, now, output);
                continue;
            }
            entry.Seen = epoch;
            var due = now >= entry.NextRefreshAt - slack;
            if (due)
            {
                entry.NextRefreshAt = now + settings.RefreshSeconds;
            }
            if (!entry.Sent)
            {
                if (due)
                {
                    Introduce(view, entry, now, output);
                }
                continue;
            }
            if (entry.Static)
            {
                if (due)
                {
                    Verify(view, entry, now, output);
                }
                continue;
            }
            var fingerprint = FingerprintOf(e, settings);
            var changedNow = fingerprint != entry.Fingerprint;
            entry.Fingerprint = fingerprint;
            if (due && (changedNow || entry.Dirty || now >= entry.NextSweepAt - slack))
            {
                // A refresh slot: a full state repairs whatever a lost unreliable message left behind. An entity
                // that changed now stays dirty, so it is sent in full once more after it goes quiet.
                if (!changedNow)
                {
                    view.Refreshes++;
                }
                entry.Dirty = changedNow;
                entry.NextSweepAt = now + settings.SweepSeconds;
                Emit(view, entry, full: true);
            }
            else if (changedNow)
            {
                entry.Dirty = true;
                Emit(view, entry, full: false);
            }
        }
        foreach (var (key, entry) in view.Entries)
        {
            if (entry.Seen == epoch)
            {
                continue;
            }
            if (entry.Sent)
            {
                output.Add(new EntityDespawnMsg(key));
                view.Known.Remove(key);
                view.Despawns++;
            }
            view.Entries.Remove(key);
        }
        if (view.Changed.Count > 0)
        {
            output.Add(new EntityUpdateMsg(new List<EntityState>(view.Changed), now));
            view.Updates += view.Changed.Count;
            view.Changed.Clear();
        }
        if (view.ChangedDeltas.Count > 0)
        {
            output.Add(new EntityDeltaMsg(new List<EntityDelta>(view.ChangedDeltas), now));
            view.Deltas += view.ChangedDeltas.Count;
            view.ChangedDeltas.Clear();
        }
    }

    /// <summary>Queues a known dynamic entity's state: as a delta when the viewer takes them and one can express the change, else in full.</summary>
    private void Emit(SnapshotView view, SnapshotView.Entry entry, bool full)
    {
        var state = DynamicStateOf(entry.Entity, view.Settings);
        if (!full && view.SendDeltas && entry.Last is { } last)
        {
            var delta = Snapshots.Delta(last, state, out var empty);
            if (delta is not null)
            {
                entry.Last = state;
                if (!empty)
                {
                    view.ChangedDeltas.Add(delta);
                }
                return;
            }
        }
        entry.Last = view.SendDeltas ? state : null;
        view.Changed.Add(state);
    }

    /// <summary>
    /// The viewer's self state when its fingerprint changed or <see cref="SnapshotSettings.SelfMaxSeconds"/>
    /// have passed since the last one; otherwise null. Call it on the <see cref="SnapshotSettings.SelfSeconds"/> cadence.
    /// </summary>
    public SelfStateMsg? Self(SnapshotView view, Entity viewer)
    {
        if (!_sim.World.Has<Stats>(viewer))
        {
            return null;
        }
        var settings = view.Settings;
        var now = _sim.Clock.Time;
        var fingerprint = Snapshots.SelfFingerprint(_sim, viewer, settings.SelfSeconds, settings.ResourceStep);
        if (fingerprint == view.SelfFingerprint && now - view.SelfSentAt < settings.SelfMaxSeconds - _sim.Clock.Dt * 0.5)
        {
            view.SelfStatesSkipped++;
            return null;
        }
        view.SelfFingerprint = fingerprint;
        view.SelfSentAt = now;
        view.SelfStates++;
        return Snapshots.Self(_sim, viewer, view.SelfCache);
    }

    // ------------------------------------------------------------ entries

    /// <summary>An entity in range the viewer does not know: spawn it unless it is never shown.</summary>
    private void Introduce(SnapshotView view, SnapshotView.Entry entry, double now, List<Message> output)
    {
        var world = _sim.World;
        var e = entry.Entity;
        if (world.Has<Spawner>(e))
        {
            return;
        }
        var isStatic = Snapshots.IsStatic(_sim, e);
        if (isStatic && world.Has<Dead>(e))
        {
            return;
        }
        Spawn(view, entry, isStatic, now, output);
    }

    /// <summary>A known static entity on its refresh cadence: despawn it if it died, re-spawn it if it changed or became dynamic.</summary>
    private void Verify(SnapshotView view, SnapshotView.Entry entry, double now, List<Message> output)
    {
        var e = entry.Entity;
        var isStatic = Snapshots.IsStatic(_sim, e);
        if (isStatic && _sim.World.Has<Dead>(e))
        {
            output.Add(new EntityDespawnMsg(e.Key));
            view.Known.Remove(e.Key);
            view.Despawns++;
            entry.Sent = false;
            return;
        }
        if (isStatic && FingerprintOf(e, view.Settings) == entry.Fingerprint)
        {
            return;
        }
        Spawn(view, entry, isStatic, now, output);
    }

    private void Spawn(SnapshotView view, SnapshotView.Entry entry, bool isStatic, double now, List<Message> output)
    {
        var e = entry.Entity;
        var state = isStatic ? Snapshots.Capture(_sim, e, isStatic: true) : DynamicStateOf(e, view.Settings);
        entry.Fingerprint = FingerprintOf(e, view.Settings);
        entry.Static = isStatic;
        entry.Sent = true;
        entry.Dirty = false;
        entry.NextSweepAt = now + view.Settings.SweepSeconds;
        entry.Last = view.SendDeltas && !isStatic ? state : null;
        view.Known.Add(e.Key);
        view.Spawns++;
        output.Add(new EntitySpawnMsg(state));
    }

    /// <summary>A fixed fraction in [0, 1) per entity key, so refresh cadences are spread across snapshots.</summary>
    private static double Phase(long key) => (FingerprintHash.Mix((ulong)key) >> 11) * (1.0 / (1UL << 53));
}
