using HollowWake.Core.Attributes;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Maths;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Components;

/// <summary>
/// The numbers of an entity: its modifier stack, its current resources, its level and progress, the
/// flags conditions read, and the gauges (stacks, charges) modifiers can scale by. Also the expression
/// scope for the entity, so formulas such as <c>life.max = 40 + level * 8</c> read straight from here.
/// </summary>
public sealed class Stats : IComponent, IExprScope
{
    public AttributeSet Attributes { get; }
    public Dictionary<string, double> Resources { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, double> Gauges { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Flags { get; } = new(StringComparer.Ordinal);
    /// <summary>Timestamps of recent happenings, read as recently_* conditions through a window rule.</summary>
    public Dictionary<string, double> Recent { get; } = new(StringComparer.Ordinal);

    public int Level
    {
        get => _level;
        set
        {
            if (_level != value)
            {
                _level = value;
                Attributes.Touch();
            }
        }
    }

    private int _level = 1;
    public double Xp;
    public int PassivePointsSpent;
    public List<string> Passives { get; } = new();

    /// <summary>Entity tags (pattern, rarity, faction) exposed to <c>has()</c> and victim scopes.</summary>
    public TagSet Tags { get; set; } = TagSet.Empty;

    /// <summary>Extra scope the owner mounts under names such as "weave", "target".</summary>
    public ScopeOverlay? Mounted;

    public double Now;
    public double RecentWindow = 4.0;

    private readonly ContentRegistry _content;
    private Rng? _rng;

    public Stats(ContentRegistry content)
    {
        _content = content;
        Attributes = new AttributeSet(content.Attributes);
    }

    public void UseRng(Rng rng) => _rng = rng;

    // ------------------------------------------------------------ resources

    public double Resource(string id) => Resources.TryGetValue(id, out var v) ? v : 0;

    /// <summary>
    /// The usable maximum of a resource: its maximum attribute less what contracts and toggles reserve of it
    /// (<c>{resource}.reserved</c>), never below zero.
    /// </summary>
    public double ResourceMax(string id)
    {
        double max;
        if (_content.TryGet<ResourceDef>(id, out var def) && def is not null)
        {
            max = Attributes.Get(def.Max, this);
        }
        else
        {
            max = Attributes.Get(id + ".max", this);
        }
        if (!AnyReserved())
        {
            return max;
        }
        var reserved = ResourceNames.ReservedOf(id);
        return Attributes.Carries(reserved) ? Math.Max(0, max - Attributes.Get(reserved, this)) : max;
    }

    private int _reservedVersion = -1;
    private bool _mirroredReserved;
    private int _anyReservedVersion = -1;
    private bool _anyReserved;

    /// <summary>Whether any resource has something reserved, recomputed only after the modifier stack changed.</summary>
    private bool AnyReserved()
    {
        var version = Attributes.Version;
        if (_anyReservedVersion != version)
        {
            var any = false;
            foreach (var id in Combat.CombatNames.For(_content).Resources)
            {
                if (Attributes.Carries(ResourceNames.ReservedOf(id)))
                {
                    any = true;
                    break;
                }
            }
            _anyReserved = any;
            _anyReservedVersion = version;
        }
        return _anyReserved;
    }

    /// <summary>
    /// Mirrors what each resource has reserved (<c>{resource}.reserved</c>) into the gauge
    /// <c>reserved.{resource}</c> the interface reads, recomputing only after the modifier stack changed. The
    /// status system calls it every tick.
    /// </summary>
    public void SyncReserved()
    {
        if (_reservedVersion == Attributes.Version)
        {
            return;
        }
        var any = AnyReserved();
        if (!any && !_mirroredReserved)
        {
            _reservedVersion = Attributes.Version;
            return;
        }
        _mirroredReserved = any;
        foreach (var id in Combat.CombatNames.For(_content).Resources)
        {
            var attribute = ResourceNames.ReservedOf(id);
            var amount = Attributes.Carries(attribute) ? Math.Max(0, Attributes.Get(attribute, this)) : 0;
            var gauge = ResourceNames.GaugeOf(id);
            if (Gauge(gauge) != amount)
            {
                SetGauge(gauge, amount);
            }
        }
        _reservedVersion = Attributes.Version;
    }

    public double ResourceFraction(string id)
    {
        var max = ResourceMax(id);
        return max <= 0 ? 0 : Math.Clamp(Resource(id) / max, 0, 1);
    }

    /// <summary>
    /// Sets a resource's current value, clamped to its maximum. A body holds a pool only where it has a maximum
    /// for it: setting a pool it lacks and has no maximum for leaves it without one (life is always held).
    /// </summary>
    public void SetResource(string id, double value)
    {
        var max = ResourceMax(id);
        var lethal = _content.TryGet<ResourceDef>(id, out var def) && def is { Lethal: true };
        if (max <= 0 && !lethal && !Resources.ContainsKey(id))
        {
            return;
        }
        Resources[id] = Math.Clamp(value, 0, Math.Max(0, max));
    }

    public double AddResource(string id, double delta)
    {
        var before = Resource(id);
        SetResource(id, before + delta);
        return Resource(id) - before;
    }

    /// <summary>
    /// Fills every pool the body has a maximum for to its starting fraction. Pools without a maximum (a shield,
    /// poise, a ward nobody granted) are not held at all, so they cost nothing and draw no ring; lethal
    /// resources (life) are always held.
    /// </summary>
    public void FillResources()
    {
        foreach (var def in _content.Each<ResourceDef>())
        {
            var max = ResourceMax(def.Id);
            if (max <= 0 && !def.Lethal)
            {
                Resources.Remove(def.Id);
                continue;
            }
            Resources[def.Id] = max * def.StartFraction;
        }
    }

    /// <summary>The entropy accumulator of evasion by entropy; not a number until the first evadable hit seeds it.</summary>
    public double Entropy = double.NaN;

    /// <summary>The modifier version the proc service last looked at this body with.</summary>
    internal int ProcVersion = -1;

    // ------------------------------------------------------------ flags & gauges

    public void Stamp(string key, double time) => Recent[key] = time;

    public bool Recently(string key)
    {
        return Recent.TryGetValue(key, out var t) && Now - t <= RecentWindow;
    }

    // ------------------------------------------------------------ leases

    private Dictionary<string, long>? _leases;

    /// <summary>
    /// Starts a new lease on a key and returns its number. Work scheduled to undo something (a temporary
    /// modifier's removal) remembers the lease it was given and acts only if <see cref="HoldsLease"/> still
    /// says so, so a refresh that took a newer lease is not undone by the older timer.
    /// </summary>
    public long Lease(string key)
    {
        _leases ??= new Dictionary<string, long>(StringComparer.Ordinal);
        var next = (_leases.TryGetValue(key, out var current) ? current : 0) + 1;
        _leases[key] = next;
        return next;
    }

    /// <summary>Whether <paramref name="lease"/> is still the latest lease taken on the key.</summary>
    public bool HoldsLease(string key, long lease) => _leases is not null && _leases.TryGetValue(key, out var current) && current == lease;

    /// <summary>Ends the lease on a key, if <paramref name="lease"/> is still the latest one; true when it was.</summary>
    public bool EndLease(string key, long lease)
    {
        if (!HoldsLease(key, lease))
        {
            return false;
        }
        _leases!.Remove(key);
        return true;
    }

    public double Gauge(string key) => Gauges.TryGetValue(key, out var v) ? v : 0;

    public void SetGauge(string key, double value)
    {
        if (value <= 0)
        {
            Gauges.Remove(key);
        }
        else
        {
            Gauges[key] = value;
        }
        Attributes.Touch();
    }

    // ------------------------------------------------------------ scope

    public double Get(string attribute) => Attributes.Get(attribute, this);
    public double Get(string attribute, TagSet context) => Attributes.Get(attribute, this, context);

    public bool TryResolve(string name, out double value)
    {
        switch (name)
        {
            case "level":
                value = Level;
                return true;
            case "xp":
                value = Xp;
                return true;
        }
        if (name.EndsWith("_pct", StringComparison.Ordinal))
        {
            var res = name[..^4];
            if (_content.Has<ResourceDef>(res) || Resources.ContainsKey(res))
            {
                value = ResourceFraction(res);
                return true;
            }
        }
        if (name.StartsWith("gauge.", StringComparison.Ordinal))
        {
            value = Gauge(name[6..]);
            return true;
        }
        if (name.StartsWith("status.", StringComparison.Ordinal) || name.StartsWith("charge.", StringComparison.Ordinal))
        {
            value = Gauge(name);
            return true;
        }
        if (Resources.TryGetValue(name, out var cur))
        {
            value = cur;
            return true;
        }
        if (Mounted is not null && Mounted.TryResolve(name, out value))
        {
            return true;
        }
        if (Attributes.Catalog.Contains(name) || Attributes.HasModifiers(name) || Attributes.TryGetBaseOverride(name, out _))
        {
            value = Attributes.Get(name, this);
            return true;
        }
        value = 0;
        return false;
    }

    public bool HasFlag(string flag)
    {
        if (Flags.Contains(flag))
        {
            return true;
        }
        if (Tags.Has(flag))
        {
            return true;
        }
        if (flag.StartsWith("recently_", StringComparison.Ordinal))
        {
            return Recently(flag[9..]);
        }
        switch (flag)
        {
            case "low_life": return ResourceFraction("life") <= Line("low_life.line", 0.35);
            case "full_life": return ResourceFraction("life") >= Line("full_life.line", 0.95);
            case "low_mana": return ResourceFraction("mana") <= Line("low_mana.line", 0.25);
            case "full_mana": return ResourceFraction("mana") >= Line("full_mana.line", 0.95);
        }
        return Mounted?.HasFlag(flag) ?? false;
    }

    /// <summary>A condition's line from its attribute (<c>low_life.line</c>), or the fallback where nothing declares it.</summary>
    private double Line(string attribute, double fallback) =>
        Attributes.Catalog.Contains(attribute) || Attributes.HasModifiers(attribute) ? Attributes.Get(attribute, this) : fallback;

    public double NextRandom() => _rng?.NextDouble() ?? 0.5;
}

/// <summary>
/// An active status on an entity. Clocks, accumulators and pools live in <see cref="Values"/> (<c>potency</c>,
/// <c>bank</c>, <c>pool</c>); the applier's riders, the bank's peak and per-stack timers have fields.
/// </summary>
public sealed class StatusInstance
{
    public required string Id;
    public required string SourceKey;
    public Entity Source = Entity.None;
    public double Remaining;
    public double Total;
    public int Stacks = 1;
    /// <summary>Damage per second for damaging statuses, potency for others. Locked at application.</summary>
    public double Magnitude = 1;
    public Dictionary<string, double> Values = new();
    public double NextTickAt;

    /// <summary>Extra tick and expiry effects the applier stamped on the instance (hooks of the applying weave).</summary>
    public StatusEffects.StatusRiders? Riders;

    /// <summary>The strongest single application a bank fed by applications has received; its cap is a multiple of it.</summary>
    public double BankPeak;

    /// <summary>With independent stack clocks: the seconds each stack has left, one entry per stack.</summary>
    public List<double>? StackTimers;

    /// <summary>True once the instance has ended; anything still holding it lets go.</summary>
    public bool Removed;

    private string? _modifierSource;

    /// <summary>The source of the modifiers the instance grants its bearer, built once.</summary>
    public string ModifierSource => _modifierSource ??= $"status:{Id}:{SourceKey}";

    /// <summary>A value of the instance, or the fallback when it has none.</summary>
    public double Value(string key, double fallback = 0) => Values.TryGetValue(key, out var v) ? v : fallback;

    // Bookkeeping of the status service: trigger subscriptions, a guard while its hooks run, whether the bank's
    // lethal hook has fired, and the scaled modifiers last granted with the scale they were built for.
    internal int[]? Subscriptions;
    internal bool Busy;
    internal bool LethalFired;
    internal Modifier[]? GrantedModifiers;
    internal double GrantedScale = double.NaN;
}

public sealed class Statuses : IComponent
{
    public List<StatusInstance> Active { get; } = new();

    /// <summary>How many active instances bank the damage their bearer takes; hits skip the scan when none do.</summary>
    internal int TakenBanks;

    /// <summary>How many active instances carry an absorption pool; the pools stage skips them when none do.</summary>
    internal int Pools;

    public bool Has(string id)
    {
        foreach (var s in Active)
        {
            if (s.Id == id)
            {
                return true;
            }
        }
        return false;
    }

    public int StacksOf(string id)
    {
        var stacks = 0;
        foreach (var s in Active)
        {
            if (s.Id == id)
            {
                stacks += s.Stacks;
            }
        }
        return stacks;
    }

    public IEnumerable<StatusInstance> Of(string id) => Active.Where(s => s.Id == id);
}

/// <summary>The names a resource is read by beyond its own: what it has reserved and the gauge mirroring that, built once per id.</summary>
public static class ResourceNames
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Reserved, string Gauge)> Names = new(StringComparer.Ordinal);

    private static (string Reserved, string Gauge) Of(string id) => Names.GetOrAdd(id, static i => (i + ".reserved", "reserved." + i));

    /// <summary><c>{resource}.reserved</c>: the attribute contracts and toggles add flat modifiers to.</summary>
    public static string ReservedOf(string id) => Of(id).Reserved;

    /// <summary><c>reserved.{resource}</c>: the gauge the interface reads the reserved amount from.</summary>
    public static string GaugeOf(string id) => Of(id).Gauge;
}
