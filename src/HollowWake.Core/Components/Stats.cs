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

    public double ResourceMax(string id)
    {
        if (_content.TryGet<ResourceDef>(id, out var def) && def is not null)
        {
            return Attributes.Get(def.Max, this);
        }
        return Attributes.Get(id + ".max", this);
    }

    public double ResourceFraction(string id)
    {
        var max = ResourceMax(id);
        return max <= 0 ? 0 : Math.Clamp(Resource(id) / max, 0, 1);
    }

    public void SetResource(string id, double value)
    {
        var max = ResourceMax(id);
        Resources[id] = Math.Clamp(value, 0, Math.Max(0, max));
    }

    public double AddResource(string id, double delta)
    {
        var before = Resource(id);
        SetResource(id, before + delta);
        return Resource(id) - before;
    }

    public void FillResources()
    {
        foreach (var def in _content.Each<ResourceDef>())
        {
            Resources[def.Id] = ResourceMax(def.Id) * def.StartFraction;
        }
    }

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
            case "low_life": return ResourceFraction("life") <= 0.35;
            case "full_life": return ResourceFraction("life") >= 0.95;
            case "low_mana": return ResourceFraction("mana") <= 0.25;
            case "full_mana": return ResourceFraction("mana") >= 0.95;
        }
        return Mounted?.HasFlag(flag) ?? false;
    }

    public double NextRandom() => _rng?.NextDouble() ?? 0.5;
}

/// <summary>An active status on an entity.</summary>
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
    public string ModifierSource => $"status:{Id}:{SourceKey}";
}

public sealed class Statuses : IComponent
{
    public List<StatusInstance> Active { get; } = new();

    public bool Has(string id) => Active.Any(s => s.Id == id);
    public int StacksOf(string id) => Active.Where(s => s.Id == id).Sum(s => s.Stacks);
    public IEnumerable<StatusInstance> Of(string id) => Active.Where(s => s.Id == id);
}
