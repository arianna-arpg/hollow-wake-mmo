using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Sim;

/// <summary>
/// One trigger firing for one owner: what kind, whose, who did what to whom, how much, whether it was a
/// critical, the tags involved and how deep in an effect chain it happened. Glyph triggers, procs, charge
/// taps and consumable statuses all read this.
/// </summary>
public readonly struct TriggerEvent
{
    public TriggerEvent(Entity source, Entity target, double amount = 0, bool crit = false, TagSet? tags = null, int depth = 0, string? status = null, GameEvent? cause = null)
    {
        Kind = null!;
        Owner = Entity.None;
        Source = source;
        Target = target;
        Amount = amount;
        Crit = crit;
        Tags = tags ?? TagSet.Empty;
        Depth = depth;
        Status = status;
        Cause = cause;
    }

    private TriggerEvent(in TriggerEvent e, TriggerKind kind, Entity owner)
    {
        this = e;
        Kind = kind;
        Owner = owner;
    }

    /// <summary>The kind that fired; set by the hub.</summary>
    public TriggerKind Kind { get; }
    /// <summary>Whose trigger this is; set by the hub.</summary>
    public Entity Owner { get; }
    public Entity Source { get; }
    public Entity Target { get; }
    /// <summary>Damage dealt or taken, healing, stacks applied, or seconds for interval beats.</summary>
    public double Amount { get; }
    public bool Crit { get; }
    public TagSet Tags { get; }
    public int Depth { get; }
    /// <summary>The status involved, for status kinds.</summary>
    public string? Status { get; }
    /// <summary>The simulation event the trigger came from, when one did.</summary>
    public GameEvent? Cause { get; }

    internal TriggerEvent For(TriggerKind kind, Entity owner) => new(this, kind, owner);
}

public delegate void TriggerHandler(in TriggerEvent e);

/// <summary>
/// A named trigger kind. Built-in kinds are fed by the simulation's events the first time anyone
/// subscribes to them; packages register kinds their own systems raise (a flawless press, an overcharge
/// stage, a collision) and raise them through <see cref="TriggerHub.Raise(Entity, TriggerKind, in TriggerEvent)"/>.
/// </summary>
public sealed class TriggerKind
{
    internal TriggerKind(string name, int id, string description, Action<Simulation, TriggerHub>? attach)
    {
        Name = name;
        Id = id;
        Description = description;
        Attach = attach;
    }

    public string Name { get; }
    public int Id { get; }
    public string Description { get; }
    internal Action<Simulation, TriggerHub>? Attach { get; }

    public override string ToString() => Name;
}

/// <summary>
/// The registry of trigger kinds, by name. Content names kinds in glyph and form triggers, status
/// consumption and procs, and validation checks the names here, so packages register their kinds from
/// their content kind files, before content loads.
/// </summary>
public static class TriggerKinds
{
    private static readonly Dictionary<string, TriggerKind> ByName = new(StringComparer.Ordinal);
    private static readonly List<TriggerKind> ById = new();
    private static readonly object Gate = new();

    static TriggerKinds()
    {
        Hit = Register("hit", "the owner's hit lands for damage", TriggerHub.AttachDamage);
        Crit = Register("crit", "the owner's hit lands as a critical", TriggerHub.AttachDamage);
        Hurt = Register("hurt", "a hit lands on the owner for damage", TriggerHub.AttachDamage);
        Block = Register("block", "the owner blocks a hit", TriggerHub.AttachDamage);
        Evade = Register("evade", "the owner evades a hit", TriggerHub.AttachDamage);
        Kill = Register("kill", "the owner is credited with a kill", TriggerHub.AttachDeaths);
        DeathNear = Register("death_near", "something dies near the owner", TriggerHub.AttachDeaths);
        MinionDied = Register("minion_died", "one of the owner's minions dies", TriggerHub.AttachDeaths);
        StatusApplied = Register("status_applied", "the owner applies a status", TriggerHub.AttachStatuses);
        Cast = Register("cast", "the owner casts a weave", TriggerHub.AttachCasts);
        Heal = Register("heal", "the owner heals someone, itself included", TriggerHub.AttachHeals);
        Interval = Register("interval", "every tick, with the seconds elapsed as the amount", TriggerHub.AttachInterval);
    }

    public static TriggerKind Hit { get; }
    public static TriggerKind Crit { get; }
    public static TriggerKind Hurt { get; }
    public static TriggerKind Block { get; }
    public static TriggerKind Evade { get; }
    public static TriggerKind Kill { get; }
    public static TriggerKind DeathNear { get; }
    public static TriggerKind MinionDied { get; }
    public static TriggerKind StatusApplied { get; }
    public static TriggerKind Cast { get; }
    public static TriggerKind Heal { get; }
    public static TriggerKind Interval { get; }

    /// <summary>
    /// Registers a kind, or returns the one already registered under the name. <paramref name="attach"/>
    /// wires the kind to the events that raise it, once per simulation, when the first subscriber arrives;
    /// kinds raised directly by a package's system need none.
    /// </summary>
    public static TriggerKind Register(string name, string description = "", Action<Simulation, TriggerHub>? attach = null)
    {
        lock (Gate)
        {
            if (ByName.TryGetValue(name, out var existing))
            {
                return existing;
            }
            var kind = new TriggerKind(name, ById.Count, description, attach);
            ByName[name] = kind;
            ById.Add(kind);
            return kind;
        }
    }

    public static bool TryGet(string name, out TriggerKind kind)
    {
        lock (Gate)
        {
            return ByName.TryGetValue(name, out kind!);
        }
    }

    public static TriggerKind Get(string name) =>
        TryGet(name, out var kind) ? kind : throw new InvalidOperationException($"Unknown trigger kind '{name}'.");

    public static IReadOnlyList<TriggerKind> All
    {
        get
        {
            lock (Gate)
            {
                return ById.OrderBy(k => k.Name, StringComparer.Ordinal).ToList();
            }
        }
    }

    internal static int Count
    {
        get
        {
            lock (Gate)
            {
                return ById.Count;
            }
        }
    }
}

/// <summary>
/// One simulation's trigger subscriptions, indexed by kind and then by owner. Subscribers name an owner
/// (whose hits, kills or casts they care about) or <see cref="Entity.None"/> for every owner. Raising
/// walks only that kind's subscribers for that owner, with no allocation; subscribers may subscribe and
/// unsubscribe while being called. A kind's feed from the event bus is attached the first time anyone
/// subscribes to it, so a simulation without trigger subscribers pays nothing.
/// </summary>
public sealed class TriggerHub
{
    private sealed class Subscription
    {
        public int Id;
        public TriggerKind Kind = null!;
        public Entity Owner;
        public TriggerHandler Handler = null!;
        public bool Removed;
    }

    private sealed class KindSlot
    {
        public readonly Dictionary<Entity, List<Subscription>> ByOwner = new();
        public readonly List<Subscription> AnyOwner = new();
        public int Count;
        public bool Attached;
    }

    private readonly Simulation _sim;
    private KindSlot?[] _slots = Array.Empty<KindSlot?>();
    private readonly Dictionary<int, Subscription> _byId = new();
    private readonly List<Entity> _ownerBuffer = new();
    private int _nextId = 1;
    private int _dispatching;
    private bool _compactPending;
    private bool _damageFeed;
    private bool _deathFeed;
    private bool _statusFeed;
    private bool _castFeed;
    private bool _healFeed;
    private bool _intervalFeed;
    private bool _despawnFeed;

    public TriggerHub(Simulation sim)
    {
        _sim = sim;
    }

    /// <summary>Subscribes a handler to a kind for one owner, or for every owner with <see cref="Entity.None"/>. Returns a handle for <see cref="Unsubscribe"/>.</summary>
    public int Subscribe(string kind, Entity owner, TriggerHandler handler) => Subscribe(TriggerKinds.Get(kind), owner, handler);

    public int Subscribe(TriggerKind kind, Entity owner, TriggerHandler handler)
    {
        var slot = SlotFor(kind, create: true)!;
        var sub = new Subscription { Id = _nextId++, Kind = kind, Owner = owner, Handler = handler };
        if (owner.IsNone)
        {
            slot.AnyOwner.Add(sub);
        }
        else
        {
            if (!slot.ByOwner.TryGetValue(owner, out var list))
            {
                list = new List<Subscription>();
                slot.ByOwner[owner] = list;
            }
            list.Add(sub);
            EnsureDespawnFeed();
        }
        slot.Count++;
        _byId[sub.Id] = sub;
        if (!slot.Attached)
        {
            slot.Attached = true;
            kind.Attach?.Invoke(_sim, this);
        }
        return sub.Id;
    }

    public bool Unsubscribe(int handle)
    {
        if (!_byId.Remove(handle, out var sub))
        {
            return false;
        }
        sub.Removed = true;
        var slot = SlotFor(sub.Kind, create: false);
        if (slot is not null)
        {
            slot.Count--;
        }
        if (_dispatching > 0)
        {
            _compactPending = true;
        }
        else
        {
            Compact();
        }
        return true;
    }

    /// <summary>Removes every subscription an owner holds, of every kind.</summary>
    public int UnsubscribeAll(Entity owner)
    {
        var removed = 0;
        foreach (var slot in _slots)
        {
            if (slot is null || !slot.ByOwner.TryGetValue(owner, out var list))
            {
                continue;
            }
            foreach (var sub in list)
            {
                if (!sub.Removed)
                {
                    sub.Removed = true;
                    _byId.Remove(sub.Id);
                    slot.Count--;
                    removed++;
                }
            }
        }
        if (removed > 0)
        {
            if (_dispatching > 0)
            {
                _compactPending = true;
            }
            else
            {
                Compact();
            }
        }
        return removed;
    }

    public bool HasSubscribers(TriggerKind kind) => SlotFor(kind, create: false) is { Count: > 0 };

    /// <summary>Raises a kind for an owner: its subscribers for that owner, then its every-owner subscribers.</summary>
    public void Raise(Entity owner, string kind, in TriggerEvent e) => Raise(owner, TriggerKinds.Get(kind), e);

    public void Raise(Entity owner, TriggerKind kind, in TriggerEvent e) => Dispatch(owner, kind, e, toOwner: true, toAny: true);

    /// <summary>
    /// Calls the owner's subscribers and, when asked, the every-owner ones. Subscribers added while this
    /// runs wait for the next raise; removed ones are skipped at once.
    /// </summary>
    private void Dispatch(Entity owner, TriggerKind kind, in TriggerEvent e, bool toOwner, bool toAny)
    {
        var slot = SlotFor(kind, create: false);
        if (slot is null || slot.Count == 0)
        {
            return;
        }
        var stamped = e.For(kind, owner);
        _dispatching++;
        try
        {
            if (toOwner && !owner.IsNone && slot.ByOwner.TryGetValue(owner, out var list))
            {
                var n = list.Count;
                for (var i = 0; i < n; i++)
                {
                    var sub = list[i];
                    if (!sub.Removed)
                    {
                        sub.Handler(stamped);
                    }
                }
            }
            if (toAny)
            {
                var any = slot.AnyOwner;
                var m = any.Count;
                for (var i = 0; i < m; i++)
                {
                    var sub = any[i];
                    if (!sub.Removed)
                    {
                        sub.Handler(stamped);
                    }
                }
            }
        }
        finally
        {
            _dispatching--;
            if (_dispatching == 0 && _compactPending)
            {
                Compact();
            }
        }
    }

    private KindSlot? SlotFor(TriggerKind kind, bool create)
    {
        if (kind.Id < _slots.Length && _slots[kind.Id] is { } slot)
        {
            return slot;
        }
        if (!create)
        {
            return null;
        }
        if (kind.Id >= _slots.Length)
        {
            Array.Resize(ref _slots, Math.Max(kind.Id + 1, Math.Max(16, TriggerKinds.Count)));
        }
        slot = new KindSlot();
        _slots[kind.Id] = slot;
        return slot;
    }

    private void Compact()
    {
        _compactPending = false;
        foreach (var slot in _slots)
        {
            if (slot is null)
            {
                continue;
            }
            slot.AnyOwner.RemoveAll(static s => s.Removed);
            _ownerBuffer.Clear();
            foreach (var (owner, list) in slot.ByOwner)
            {
                list.RemoveAll(static s => s.Removed);
                if (list.Count == 0)
                {
                    _ownerBuffer.Add(owner);
                }
            }
            foreach (var owner in _ownerBuffer)
            {
                slot.ByOwner.Remove(owner);
            }
        }
        _ownerBuffer.Clear();
    }

    private void EnsureDespawnFeed()
    {
        if (_despawnFeed)
        {
            return;
        }
        _despawnFeed = true;
        _sim.Events.Subscribe<EntityDespawned>(d => UnsubscribeAll(d.Entity));
    }

    /// <summary>Owners subscribed to a kind, copied so raising may change the subscriptions.</summary>
    private List<Entity> OwnersOf(TriggerKind kind)
    {
        _ownerBuffer.Clear();
        if (SlotFor(kind, create: false) is { } slot)
        {
            foreach (var owner in slot.ByOwner.Keys)
            {
                _ownerBuffer.Add(owner);
            }
        }
        return _ownerBuffer;
    }

    // ------------------------------------------------------------ feeds from the event bus

    internal static void AttachDamage(Simulation sim, TriggerHub hub)
    {
        if (!hub._damageFeed)
        {
            hub._damageFeed = true;
            sim.Events.Subscribe<DamageDealt>(hub.OnDamage);
        }
    }

    internal static void AttachDeaths(Simulation sim, TriggerHub hub)
    {
        if (!hub._deathFeed)
        {
            hub._deathFeed = true;
            sim.Events.Subscribe<EntityDied>(hub.OnDeath);
        }
    }

    internal static void AttachStatuses(Simulation sim, TriggerHub hub)
    {
        if (!hub._statusFeed)
        {
            hub._statusFeed = true;
            sim.Events.Subscribe<StatusApplied>(hub.OnStatus);
        }
    }

    internal static void AttachCasts(Simulation sim, TriggerHub hub)
    {
        if (!hub._castFeed)
        {
            hub._castFeed = true;
            sim.Events.Subscribe<WeaveCast>(hub.OnCast);
        }
    }

    internal static void AttachHeals(Simulation sim, TriggerHub hub)
    {
        if (!hub._healFeed)
        {
            hub._healFeed = true;
            sim.Events.Subscribe<Healed>(hub.OnHeal);
        }
    }

    internal static void AttachInterval(Simulation sim, TriggerHub hub)
    {
        if (!hub._intervalFeed)
        {
            hub._intervalFeed = true;
            sim.Scheduler.Every(sim.Clock.Dt, hub.OnInterval);
        }
    }

    private void OnDamage(DamageDealt d)
    {
        if (d.Evaded)
        {
            if (HasSubscribers(TriggerKinds.Evade))
            {
                Raise(d.Target, TriggerKinds.Evade, new TriggerEvent(d.Source, d.Target, 0, false, d.Tags, d.Depth, cause: d));
            }
            return;
        }
        if (d.Blocked && HasSubscribers(TriggerKinds.Block))
        {
            Raise(d.Target, TriggerKinds.Block, new TriggerEvent(d.Source, d.Target, d.Total, d.Crit, d.Tags, d.Depth, cause: d));
        }
        if (d.Total <= 0)
        {
            return;
        }
        if (HasSubscribers(TriggerKinds.Hit))
        {
            Raise(d.Source, TriggerKinds.Hit, new TriggerEvent(d.Source, d.Target, d.Total, d.Crit, d.Tags, d.Depth, cause: d));
        }
        if (d.Crit && HasSubscribers(TriggerKinds.Crit))
        {
            Raise(d.Source, TriggerKinds.Crit, new TriggerEvent(d.Source, d.Target, d.Total, true, d.Tags, d.Depth, cause: d));
        }
        if (HasSubscribers(TriggerKinds.Hurt))
        {
            Raise(d.Target, TriggerKinds.Hurt, new TriggerEvent(d.Source, d.Target, d.Total, d.Crit, d.Tags, d.Depth, cause: d));
        }
    }

    private void OnDeath(EntityDied d)
    {
        if (!d.Killer.IsNone && HasSubscribers(TriggerKinds.Kill))
        {
            Raise(d.Killer, TriggerKinds.Kill, new TriggerEvent(d.Killer, d.Entity, 0, false, d.Tags, d.Depth, cause: d));
        }
        if (HasSubscribers(TriggerKinds.MinionDied) && _sim.World.TryGet<Minion>(d.Entity, out var minion) && !minion.Owner.IsNone)
        {
            Raise(minion.Owner, TriggerKinds.MinionDied, new TriggerEvent(d.Killer, d.Entity, 0, false, d.Tags, d.Depth, cause: d));
        }
        if (HasSubscribers(TriggerKinds.DeathNear))
        {
            // Owners within the rule's radius of the death each get it; every-owner subscribers get it once.
            var radius = (float)_sim.Content.Rules.Number("triggers.death_near_radius", 8);
            var owners = OwnersOf(TriggerKinds.DeathNear);
            var count = owners.Count;
            Span<Entity> near = count <= 64 ? stackalloc Entity[count] : new Entity[count];
            var found = 0;
            for (var i = 0; i < count; i++)
            {
                var owner = owners[i];
                if (owner != d.Entity && _sim.World.TryGet<Transform>(owner, out var t) && t.GroundDistanceTo(d.Position) <= radius)
                {
                    near[found++] = owner;
                }
            }
            var evt = new TriggerEvent(d.Killer, d.Entity, 0, false, d.Tags, d.Depth, cause: d);
            for (var i = 0; i < found; i++)
            {
                Dispatch(near[i], TriggerKinds.DeathNear, evt, toOwner: true, toAny: false);
            }
            Dispatch(Entity.None, TriggerKinds.DeathNear, evt, toOwner: false, toAny: true);
        }
    }

    private void OnStatus(StatusApplied s)
    {
        if (HasSubscribers(TriggerKinds.StatusApplied))
        {
            Raise(s.Source, TriggerKinds.StatusApplied, new TriggerEvent(s.Source, s.Target, s.Stacks, false, null, s.Depth, s.Status, s));
        }
    }

    private void OnCast(WeaveCast w)
    {
        if (HasSubscribers(TriggerKinds.Cast))
        {
            Raise(w.Caster, TriggerKinds.Cast, new TriggerEvent(w.Caster, w.Target, 0, false, w.Tags, 0, cause: w));
        }
    }

    private void OnHeal(Healed h)
    {
        if (HasSubscribers(TriggerKinds.Heal))
        {
            Raise(h.Source, TriggerKinds.Heal, new TriggerEvent(h.Source, h.Target, h.Amount, false, null, h.Depth, cause: h));
        }
    }

    private void OnInterval()
    {
        if (!HasSubscribers(TriggerKinds.Interval))
        {
            return;
        }
        // Each subscribed owner gets a beat; every-owner subscribers get one beat per tick.
        var dt = _sim.Clock.Dt;
        var owners = OwnersOf(TriggerKinds.Interval);
        var count = owners.Count;
        Span<Entity> copy = count <= 64 ? stackalloc Entity[count] : new Entity[count];
        for (var i = 0; i < count; i++)
        {
            copy[i] = owners[i];
        }
        for (var i = 0; i < count; i++)
        {
            Dispatch(copy[i], TriggerKinds.Interval, new TriggerEvent(copy[i], Entity.None, dt), toOwner: true, toAny: false);
        }
        Dispatch(Entity.None, TriggerKinds.Interval, new TriggerEvent(Entity.None, Entity.None, dt), toOwner: false, toAny: true);
    }
}
