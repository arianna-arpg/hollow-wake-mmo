using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Expressions;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;

namespace HollowWake.Core.StatusEffects;

public sealed class StatusApplication
{
    public double? Duration { get; init; }
    public int Stacks { get; init; } = 1;
    public double Magnitude { get; init; } = 1;
    public double Potency { get; init; } = 1;
    public string SourceKey { get; init; } = "";
    public TagSet Tags { get; init; } = TagSet.Empty;
}

/// <summary>
/// Applies, stacks, refreshes, ticks and expires statuses, keeping their modifiers, flags and gauges
/// on the bearer in sync. Periodic effects compile once per status from content.
/// </summary>
public sealed class StatusService
{
    private readonly EffectRegistry _effects;
    private readonly Dictionary<string, (CompiledEffect[] Tick, CompiledEffect[] OnApply, CompiledEffect[] OnExpire)> _compiled = new(StringComparer.Ordinal);

    public StatusService(EffectRegistry effects)
    {
        _effects = effects;
    }

    private (CompiledEffect[] Tick, CompiledEffect[] OnApply, CompiledEffect[] OnExpire) Compiled(Simulation sim, StatusDef def)
    {
        if (_compiled.TryGetValue(def.Id, out var c))
        {
            return c;
        }
        var ctx = new EffectCompileContext { Content = sim.Content, Registry = _effects, Owner = "status " + def.Id };
        var tick = def.Tick is null ? Array.Empty<CompiledEffect>() : _effects.CompileAll(def.Tick.Effects, ctx).ToArray();
        var apply = _effects.CompileAll(def.OnApply, ctx).ToArray();
        var expire = _effects.CompileAll(def.OnExpire, ctx).ToArray();
        c = (tick, apply, expire);
        _compiled[def.Id] = c;
        return c;
    }

    public StatusInstance? Apply(Simulation sim, Entity target, string statusId, Entity source, StatusApplication app)
    {
        if (!sim.World.TryGet<Stats>(target, out var stats) || sim.World.Has<Dead>(target))
        {
            return null;
        }
        if (!sim.Content.TryGet<StatusDef>(statusId, out var def) || def is null)
        {
            sim.Log.Warn($"unknown status '{statusId}'");
            return null;
        }
        var statuses = sim.World.GetOrAdd<Statuses>(target);
        var duration = app.Duration ?? (def.Duration is null ? 5 : def.Duration.Evaluate(stats));
        if (sim.World.TryGet<Stats>(source, out var ss))
        {
            duration *= Math.Max(0.1, ss.Attributes.Get("effect.duration", ss, app.Tags.With("status." + statusId)) + 1 - (ss.Attributes.Catalog.Contains("effect.duration") ? ss.Attributes.Catalog.Get("effect.duration")!.Base : 0));
            if (!def.Beneficial)
            {
                duration *= Math.Max(0.1, 1 - Math.Clamp(stats.Get("status.resistance", app.Tags.With("status." + statusId)), -1, 0.9));
            }
        }
        if (duration <= 0)
        {
            return null;
        }
        var sourceKey = def.StackPolicy == StackPolicy.Independent ? $"{source.Key}:{app.SourceKey}" : "shared";
        var existing = statuses.Active.FirstOrDefault(s => s.Id == statusId && s.SourceKey == sourceKey);
        StatusInstance instance;
        if (existing is null)
        {
            instance = new StatusInstance
            {
                Id = statusId,
                SourceKey = sourceKey,
                Source = source,
                Remaining = duration,
                Total = duration,
                Stacks = Math.Min(app.Stacks, def.MaxStacks),
                Magnitude = app.Magnitude,
                NextTickAt = sim.Clock.Time + (def.Tick?.Interval ?? 1),
            };
            instance.Values["potency"] = app.Potency;
            statuses.Active.Add(instance);
            var compiled = Compiled(sim, def);
            ExecuteEffects(sim, instance, target, compiled.OnApply);
        }
        else
        {
            instance = existing;
            switch (def.StackPolicy)
            {
                case StackPolicy.Refresh:
                    instance.Remaining = Math.Max(instance.Remaining, duration);
                    instance.Total = Math.Max(instance.Total, duration);
                    instance.Magnitude = Math.Max(instance.Magnitude, app.Magnitude);
                    instance.Source = source;
                    break;
                case StackPolicy.Strongest:
                    if (app.Magnitude > instance.Magnitude)
                    {
                        instance.Magnitude = app.Magnitude;
                        instance.Remaining = duration;
                        instance.Total = duration;
                        instance.Source = source;
                    }
                    break;
                case StackPolicy.Stack:
                    instance.Stacks = Math.Min(def.MaxStacks, instance.Stacks + app.Stacks);
                    instance.Remaining = Math.Max(instance.Remaining, duration);
                    instance.Magnitude = Math.Max(instance.Magnitude, app.Magnitude);
                    instance.Source = source;
                    break;
                case StackPolicy.Independent:
                    instance.Remaining = duration;
                    instance.Magnitude = app.Magnitude;
                    break;
            }
            instance.Values["potency"] = Math.Max(instance.Values.GetValueOrDefault("potency", 1), app.Potency);
        }
        Refresh(sim, target, stats, statuses);
        sim.Events.Publish(new StatusApplied(target, source, statusId, instance.Stacks, instance.Remaining) { Tick = sim.Clock.Tick });

        if (def.BuildupInto is not null && instance.Stacks >= def.MaxStacks)
        {
            RemoveInstance(sim, target, stats, statuses, instance, expired: false);
            Apply(sim, target, def.BuildupInto, source, new StatusApplication { Magnitude = app.Magnitude, Potency = app.Potency, SourceKey = app.SourceKey, Tags = app.Tags });
        }
        return instance;
    }

    public int Remove(Simulation sim, Entity target, string? statusId, TagSet tags, int count = int.MaxValue)
    {
        if (!sim.World.TryGet<Stats>(target, out var stats) || !sim.World.TryGet<Statuses>(target, out var statuses))
        {
            return 0;
        }
        var removed = 0;
        foreach (var s in statuses.Active.ToList())
        {
            if (removed >= count)
            {
                break;
            }
            var def = sim.Content.Get<StatusDef>(s.Id);
            if (statusId is not null && s.Id != statusId)
            {
                continue;
            }
            if (!tags.IsEmpty && (def is null || !def.Tags.HasAny(tags)))
            {
                continue;
            }
            if (statusId is null && tags.IsEmpty && def is { Dispellable: false })
            {
                continue;
            }
            RemoveInstance(sim, target, stats, statuses, s, expired: false);
            removed++;
        }
        return removed;
    }

    private void RemoveInstance(Simulation sim, Entity target, Stats stats, Statuses statuses, StatusInstance instance, bool expired)
    {
        statuses.Active.Remove(instance);
        stats.Attributes.RemoveSource(instance.ModifierSource);
        if (sim.Content.TryGet<StatusDef>(instance.Id, out var def) && def is not null)
        {
            var compiled = Compiled(sim, def);
            ExecuteEffects(sim, instance, target, compiled.OnExpire);
        }
        Refresh(sim, target, stats, statuses);
        sim.Events.Publish(new StatusExpired(target, instance.Id) { Tick = sim.Clock.Tick });
    }

    /// <summary>Rebuilds status modifiers, flags and gauges on the bearer from what is active.</summary>
    public void Refresh(Simulation sim, Entity target, Stats stats, Statuses statuses)
    {
        var flags = new HashSet<string>(StringComparer.Ordinal);
        var gauges = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var s in statuses.Active)
        {
            stats.Attributes.RemoveSource(s.ModifierSource);
            if (!sim.Content.TryGet<StatusDef>(s.Id, out var def) || def is null)
            {
                continue;
            }
            var potency = def.ScalesWithPotency ? s.Values.GetValueOrDefault("potency", 1) : 1;
            var scale = (def.ModifiersPerStack ? s.Stacks : 1) * potency;
            foreach (var m in def.Modifiers)
            {
                stats.Attributes.Add(m.Scaled(scale).WithSource(s.ModifierSource));
            }
            foreach (var f in def.Flags)
            {
                flags.Add(f);
            }
            gauges["status." + s.Id + ".stacks"] = gauges.GetValueOrDefault("status." + s.Id + ".stacks") + s.Stacks;
            gauges["status." + s.Id] = 1;
        }
        // Flags owned by statuses are prefixed so permanent flags set by patterns survive.
        foreach (var f in stats.Flags.Where(f => f.StartsWith("status:", StringComparison.Ordinal)).ToList())
        {
            stats.Flags.Remove(f);
        }
        foreach (var f in flags)
        {
            stats.Flags.Add(f);
            stats.Flags.Add("status:" + f);
        }
        foreach (var key in stats.Gauges.Keys.Where(k => k.StartsWith("status.", StringComparison.Ordinal)).ToList())
        {
            stats.Gauges.Remove(key);
        }
        foreach (var (k, v) in gauges)
        {
            stats.Gauges[k] = v;
        }
        // Recompute which status-granted flags remain.
        var granted = flags;
        foreach (var f in stats.Flags.ToList())
        {
            if (!f.StartsWith("status:", StringComparison.Ordinal) && !granted.Contains(f) && IsStatusFlag(sim, f))
            {
                stats.Flags.Remove(f);
            }
        }
        stats.Attributes.Touch();
    }

    private readonly HashSet<string> _knownStatusFlags = new(StringComparer.Ordinal);

    private bool IsStatusFlag(Simulation sim, string flag)
    {
        if (_knownStatusFlags.Count == 0)
        {
            foreach (var d in sim.Content.Each<StatusDef>())
            {
                foreach (var f in d.Flags)
                {
                    _knownStatusFlags.Add(f);
                }
            }
            _knownStatusFlags.Add("__init");
        }
        return _knownStatusFlags.Contains(flag);
    }

    /// <summary>Advances every status on an entity by dt: periodic effects, then expiry.</summary>
    public void Tick(Simulation sim, Entity target, Stats stats, Statuses statuses, double dt)
    {
        if (statuses.Active.Count == 0)
        {
            return;
        }
        var now = sim.Clock.Time;
        var changed = false;
        foreach (var s in statuses.Active.ToList())
        {
            s.Remaining -= dt;
            if (sim.Content.TryGet<StatusDef>(s.Id, out var def) && def?.Tick is not null && now >= s.NextTickAt)
            {
                var compiled = Compiled(sim, def);
                var scaled = s.Magnitude * def.Tick.Interval;
                ExecuteEffects(sim, s, target, compiled.Tick, scaled);
                s.NextTickAt = now + def.Tick.Interval;
                if (sim.World.Has<Dead>(target))
                {
                    return;
                }
            }
            if (s.Remaining <= 0)
            {
                statuses.Active.Remove(s);
                stats.Attributes.RemoveSource(s.ModifierSource);
                if (def is not null)
                {
                    var compiled = Compiled(sim, def);
                    ExecuteEffects(sim, s, target, compiled.OnExpire);
                }
                sim.Events.Publish(new StatusExpired(target, s.Id) { Tick = sim.Clock.Tick });
                changed = true;
            }
        }
        if (changed)
        {
            Refresh(sim, target, stats, statuses);
        }
    }

    private void ExecuteEffects(Simulation sim, StatusInstance s, Entity target, CompiledEffect[] effects, double amount = 0)
    {
        if (effects.Length == 0)
        {
            return;
        }
        var source = sim.World.IsAlive(s.Source) ? s.Source : target;
        var ctx = new EffectContext
        {
            Sim = sim,
            Source = source,
            Target = target,
            Point = sim.World.Get<Transform>(target)?.Position ?? default,
            Tags = TagSet.Of("status", "status." + s.Id, "dot"),
            Rng = sim.RngFor(target, "status:" + s.Id),
            Potency = s.Values.GetValueOrDefault("potency", 1),
            Vars = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["amount"] = amount,
                ["stacks"] = s.Stacks,
                ["magnitude"] = s.Magnitude,
                ["remaining"] = s.Remaining,
            },
        };
        CompiledEffect.ExecuteAll(effects, ctx);
    }

    public void PropagateOnDeath(Simulation sim, Entity dead)
    {
        if (!sim.World.TryGet<Statuses>(dead, out var statuses) || !sim.World.TryGet<Transform>(dead, out var t))
        {
            return;
        }
        foreach (var s in statuses.Active.ToList())
        {
            if (!sim.Content.TryGet<StatusDef>(s.Id, out var def) || def is null || !def.PropagateOnDeath)
            {
                continue;
            }
            var radius = (float)sim.Content.Rules.Number("combat.propagate_radius", 4);
            foreach (var (other, _, _) in sim.Spatial.Within(t.Position, radius).ToList())
            {
                if (other == dead || !Combat.Relations.IsAlly(sim, dead, other))
                {
                    continue;
                }
                Apply(sim, other, s.Id, s.Source, new StatusApplication { Duration = s.Remaining, Magnitude = s.Magnitude, Stacks = s.Stacks, Potency = s.Values.GetValueOrDefault("potency", 1), SourceKey = "propagate" });
            }
        }
    }
}
