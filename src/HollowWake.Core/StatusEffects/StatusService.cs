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

    /// <summary>The duration is fixed: neither the applier's effect duration nor the bearer's status resistance changes it.</summary>
    public bool FixedDuration { get; init; }

    public int Stacks { get; init; } = 1;
    public double Magnitude { get; init; } = 1;
    public double Potency { get; init; } = 1;
    public string SourceKey { get; init; } = "";
    public TagSet Tags { get; init; } = TagSet.Empty;
    /// <summary>The effect depth the application came from; carried by the <see cref="StatusApplied"/> event.</summary>
    public int Depth { get; init; }

    /// <summary>For a status banked from applications: what this application adds to the bank; magnitude times duration when left out.</summary>
    public double? BankAmount { get; init; }

    /// <summary>Extra effects the applier stamps on the instance, run on each of its ticks and when it ends.</summary>
    public StatusRiders? Riders { get; init; }
}

/// <summary>Effects an applier stamps on a status instance: the hooks of the weave or effect that applied it.</summary>
public sealed class StatusRiders
{
    public CompiledEffect[] Tick { get; init; } = Array.Empty<CompiledEffect>();
    public CompiledEffect[] Expire { get; init; } = Array.Empty<CompiledEffect>();
    public bool IsEmpty => Tick.Length == 0 && Expire.Length == 0;
}

/// <summary>
/// Applies, stacks, refreshes, ticks and expires statuses, keeping their modifiers, flags and gauges on the
/// bearer in sync. Beyond the core policies it keeps banks (fed by applications or by damage taken, spent by
/// ticks and ruptures), absorption pools, per-stack clocks, the applier's riders, and the status hooks the
/// bearer's triggers feed (consumed stacks, next-hit riders, discharges when struck). Effects compile once per
/// status from content.
/// </summary>
public sealed class StatusService
{
    private readonly EffectRegistry _effects;
    private readonly Dictionary<string, CompiledStatus> _compiled = new(StringComparer.Ordinal);

    // Reused by Refresh and Tick, which never run inside each other for the same service.
    private readonly HashSet<string> _flags = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _gauges = new(StringComparer.Ordinal);
    private readonly List<string> _scratch = new();
    private readonly List<StatusInstance> _ticking = new();
    private int _refreshDepth;

    public StatusService(EffectRegistry effects)
    {
        _effects = effects;
    }

    /// <summary>A status's compiled effects and the names it is read by, built once.</summary>
    private sealed class CompiledStatus
    {
        public required StatusDef Def;
        public CompiledEffect[] Tick = Array.Empty<CompiledEffect>();
        public CompiledEffect[] OnApply = Array.Empty<CompiledEffect>();
        public CompiledEffect[] OnExpire = Array.Empty<CompiledEffect>();
        public CompiledEffect[] OnReapply = Array.Empty<CompiledEffect>();
        public CompiledEffect[] OnBankLethal = Array.Empty<CompiledEffect>();
        public CompiledEffect[] OnBearerHit = Array.Empty<CompiledEffect>();
        public CompiledEffect[] OnBearerStruck = Array.Empty<CompiledEffect>();
        public TriggerKind? ConsumeKind;
        /// <summary>Tags of tick contexts: <c>status</c>, <c>status.{id}</c>, <c>dot</c>.</summary>
        public TagSet TickTags = TagSet.Empty;
        /// <summary>Tags of every other context the status runs: <c>status</c>, <c>status.{id}</c>.</summary>
        public TagSet HookTags = TagSet.Empty;
        /// <summary><c>status.{id}</c> as a tag: the scope of the applier's per-status attributes.</summary>
        public string ScopeTag = "";
        public string StacksGauge = "";
        public string PresenceGauge = "";
        public string[] ForbidFlags = Array.Empty<string>();
        public string PopAttribute = "";
    }

    private CompiledStatus Compiled(Simulation sim, StatusDef def)
    {
        if (_compiled.TryGetValue(def.Id, out var c) && ReferenceEquals(c.Def, def))
        {
            return c;
        }
        var ctx = new EffectCompileContext { Content = sim.Content, Registry = _effects, Owner = "status " + def.Id };
        var forbid = new string[def.ForbidsTags.Count];
        for (var i = 0; i < forbid.Length; i++)
        {
            forbid[i] = "forbid:" + def.ForbidsTags.Items[i];
        }
        c = new CompiledStatus
        {
            Def = def,
            Tick = def.Tick is null ? Array.Empty<CompiledEffect>() : _effects.CompileAll(def.Tick.Effects, ctx).ToArray(),
            OnApply = _effects.CompileAll(def.OnApply, ctx).ToArray(),
            OnExpire = _effects.CompileAll(def.OnExpire, ctx).ToArray(),
            OnReapply = _effects.CompileAll(def.OnReapply, ctx).ToArray(),
            OnBankLethal = _effects.CompileAll(def.OnBankLethal, ctx).ToArray(),
            OnBearerHit = _effects.CompileAll(def.OnBearerHit, ctx).ToArray(),
            OnBearerStruck = _effects.CompileAll(def.OnBearerStruck, ctx).ToArray(),
            ConsumeKind = def.ConsumeOn is { } consume && TriggerKinds.TryGet(consume.Event, out var kind) ? kind : null,
            TickTags = TagSet.Of("status", "status." + def.Id, "dot"),
            HookTags = TagSet.Of("status", "status." + def.Id),
            ScopeTag = "status." + def.Id,
            StacksGauge = "status." + def.Id + ".stacks",
            PresenceGauge = "status." + def.Id,
            ForbidFlags = forbid,
            PopAttribute = "status." + def.Id + ".pop",
        };
        _compiled[def.Id] = c;
        return c;
    }

    /// <summary>The status's definition with its compiled effects, or null for an unknown id.</summary>
    private CompiledStatus? Compiled(Simulation sim, string statusId) =>
        sim.Content.TryGet<StatusDef>(statusId, out var def) && def is not null ? Compiled(sim, def) : null;

    // ------------------------------------------------------------ applying

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
        var compiled = Compiled(sim, def);
        var statuses = sim.World.GetOrAdd<Statuses>(target);
        var duration = app.Duration ?? (def.Duration is null ? 5 : def.Duration.Evaluate(stats));
        sim.World.TryGet<Stats>(source, out var ss);
        TagSet? scoped = null;
        if (ss is not null && !app.FixedDuration)
        {
            scoped = app.Tags.With(compiled.ScopeTag);
            duration *= Math.Max(0.1, ss.Attributes.Get("effect.duration", ss, scoped) + 1 - (ss.Attributes.Catalog.Contains("effect.duration") ? ss.Attributes.Catalog.Get("effect.duration")!.Base : 0));
            if (!def.Beneficial)
            {
                duration *= Math.Max(0.1, 1 - Math.Clamp(stats.Get("status.resistance", scoped), -1, 0.9));
            }
        }
        if (duration <= 0)
        {
            return null;
        }
        // The applier's status.stacks, scoped to the status, raises the cap on stacks.
        var maxStacks = def.MaxStacks;
        if (ss is not null && ss.Attributes.HasModifiers("status.stacks"))
        {
            maxStacks += Math.Max(0, (int)Math.Floor(ss.Attributes.Get("status.stacks", ss, scoped ?? app.Tags.With(compiled.ScopeTag))));
        }
        var sourceKey = def.StackPolicy == StackPolicy.Independent ? $"{source.Key}:{app.SourceKey}" : "shared";
        StatusInstance? existing = null;
        foreach (var s in statuses.Active)
        {
            if (s.Id == statusId && s.SourceKey == sourceKey)
            {
                existing = s;
                break;
            }
        }
        var appBank = def.Bank is { From: BankSource.Applications } ? Math.Max(0, app.BankAmount ?? app.Magnitude * duration) : 0;
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
                Stacks = Math.Min(app.Stacks, maxStacks),
                Magnitude = app.Magnitude,
                NextTickAt = sim.Clock.Time + (def.Tick?.Interval ?? 1),
                Riders = app.Riders is { IsEmpty: false } ? app.Riders : null,
            };
            instance.Values["potency"] = app.Potency;
            if (def.StackClock == StackClock.Independent)
            {
                instance.StackTimers = new List<double>();
                for (var i = 0; i < instance.Stacks; i++)
                {
                    instance.StackTimers.Add(duration);
                }
            }
            if (def.Bank is { } bank)
            {
                if (bank.From == BankSource.Applications)
                {
                    instance.Values["bank"] = appBank;
                    instance.BankPeak = appBank;
                }
                else
                {
                    instance.Values["bank"] = 0;
                    statuses.TakenBanks++;
                }
            }
            if (def.Pool is not null)
            {
                instance.Values["pool"] = PoolOf(def, ss ?? stats, app);
                statuses.Pools++;
            }
            statuses.Active.Add(instance);
            Subscribe(sim, target, instance, compiled);
            Run(sim, instance, target, compiled.OnApply, compiled.HookTags, app.Depth);
            if (instance.Removed)
            {
                return instance;
            }
        }
        else
        {
            instance = existing;
            if (compiled.OnReapply.Length > 0)
            {
                Reapplied(sim, target, source, ss, instance, compiled, app);
                if (instance.Removed || sim.World.Has<Dead>(target))
                {
                    return instance;
                }
            }
            var keep = def.KeepSource;
            switch (def.StackPolicy)
            {
                case StackPolicy.Refresh:
                    instance.Remaining = Math.Max(instance.Remaining, duration);
                    instance.Total = Math.Max(instance.Total, duration);
                    instance.Magnitude = Math.Max(instance.Magnitude, app.Magnitude);
                    if (!keep)
                    {
                        instance.Source = source;
                    }
                    break;
                case StackPolicy.Strongest:
                    if (app.Magnitude > instance.Magnitude)
                    {
                        instance.Magnitude = app.Magnitude;
                        instance.Remaining = duration;
                        instance.Total = duration;
                        if (!keep)
                        {
                            instance.Source = source;
                        }
                    }
                    break;
                case StackPolicy.Stack:
                    instance.Stacks = Math.Min(maxStacks, instance.Stacks + app.Stacks);
                    instance.Remaining = Math.Max(instance.Remaining, duration);
                    instance.Magnitude = Math.Max(instance.Magnitude, app.Magnitude);
                    if (!keep)
                    {
                        instance.Source = source;
                    }
                    break;
                case StackPolicy.Independent:
                    instance.Remaining = duration;
                    instance.Magnitude = app.Magnitude;
                    break;
            }
            if (instance.StackTimers is { } timers)
            {
                AddStackTimers(instance, timers, app.Stacks, duration, maxStacks);
            }
            if (def.Bank is { From: BankSource.Applications } appBankSpec)
            {
                instance.BankPeak = Math.Max(instance.BankPeak, appBank);
                var banked = instance.Value("bank") + appBank;
                if (appBankSpec.Cap is { } cap)
                {
                    banked = Math.Min(banked, Math.Max(0, cap.Evaluate(stats)) * instance.BankPeak);
                }
                instance.Values["bank"] = banked;
                instance.Remaining = Math.Max(instance.Remaining, duration);
                instance.Total = Math.Max(instance.Total, duration);
            }
            if (def.Pool is not null)
            {
                instance.Values["pool"] = Math.Max(instance.Value("pool"), PoolOf(def, ss ?? stats, app));
            }
            if (app.Riders is { IsEmpty: false })
            {
                instance.Riders = app.Riders;
            }
            instance.Values["potency"] = Math.Max(instance.Values.GetValueOrDefault("potency", 1), app.Potency);
        }
        Refresh(sim, target, stats, statuses);
        sim.Events.Publish(new StatusApplied(target, source, statusId, instance.Stacks, instance.Remaining) { Tick = sim.Clock.Tick, Depth = app.Depth });

        if (def.BuildupInto is not null && instance.Stacks >= maxStacks && !instance.Removed)
        {
            RemoveInstance(sim, target, stats, statuses, instance, expired: false);
            Apply(sim, target, def.BuildupInto, source, new StatusApplication { Magnitude = app.Magnitude, Potency = app.Potency, SourceKey = app.SourceKey, Tags = app.Tags, Depth = app.Depth });
        }
        else if (def.Bank is { From: BankSource.Applications })
        {
            CheckBankLethal(sim, target, stats, instance, compiled, app.Depth);
        }
        return instance;
    }

    /// <summary>The absorption a status pool holds, read on the applier with the application's magnitude and potency.</summary>
    private static double PoolOf(StatusDef def, Stats applier, StatusApplication app)
    {
        var scope = new ScopeOverlay(applier).Set("magnitude", app.Magnitude).Set("potency", app.Potency);
        return Math.Max(0, def.Pool!.Evaluate(scope) * app.Potency);
    }

    /// <summary>Adds per-stack timers for an application, dropping the stacks nearest to expiry past the cap.</summary>
    private static void AddStackTimers(StatusInstance instance, List<double> timers, int stacks, double duration, int maxStacks)
    {
        for (var i = 0; i < stacks; i++)
        {
            timers.Add(duration);
        }
        if (timers.Count > maxStacks)
        {
            timers.Sort();
            timers.RemoveRange(0, timers.Count - maxStacks);
        }
        instance.Stacks = Math.Max(1, timers.Count);
        var longest = 0.0;
        foreach (var t in timers)
        {
            longest = Math.Max(longest, t);
        }
        instance.Remaining = longest;
        instance.Total = Math.Max(instance.Total, duration);
    }

    /// <summary>The pop: a live instance is applied again; its hook reads what was left to deal and the applier's pop power.</summary>
    private void Reapplied(Simulation sim, Entity target, Entity source, Stats? applier, StatusInstance instance, CompiledStatus compiled, StatusApplication app)
    {
        var remaining = RemainingDamage(instance, compiled.Def);
        var pop = applier is not null && applier.Attributes.HasModifiers(compiled.PopAttribute) ? applier.Attributes.Get(compiled.PopAttribute, applier, app.Tags) : 0;
        var ctx = Context(sim, instance, target, compiled.HookTags, app.Depth, source);
        ctx.Vars["remaining_damage"] = remaining;
        ctx.Vars["pop"] = pop;
        RunGuarded(instance, compiled.OnReapply, ctx);
    }

    /// <summary>What an instance still has to deal: its bank, or its damage per second times stacks times the seconds left.</summary>
    public static double RemainingDamage(StatusInstance instance, StatusDef def) =>
        def.Bank is { From: BankSource.Applications } ? instance.Value("bank") : instance.Magnitude * instance.Stacks * Math.Max(0, instance.Remaining);

    // ------------------------------------------------------------ removing

    /// <summary>
    /// Removes statuses from a body: a named one regardless of kind, otherwise those carrying any of the tags (or
    /// any status when no tags are given) whose kind matches, up to <paramref name="count"/>, newest first unless
    /// <paramref name="oldestFirst"/>. Statuses that cannot be dispelled are only removed by name.
    /// </summary>
    public int Remove(Simulation sim, Entity target, string? statusId, TagSet tags, int count = int.MaxValue, bool oldestFirst = true, StatusKind kind = StatusKind.Any)
    {
        if (!sim.World.TryGet<Stats>(target, out var stats) || !sim.World.TryGet<Statuses>(target, out var statuses))
        {
            return 0;
        }
        var removed = 0;
        var list = statuses.Active.ToArray();
        for (var k = 0; k < list.Length && removed < count; k++)
        {
            var s = list[oldestFirst ? k : list.Length - 1 - k];
            if (s.Removed)
            {
                continue;
            }
            var def = sim.Content.Get<StatusDef>(s.Id);
            if (statusId is not null)
            {
                if (s.Id != statusId)
                {
                    continue;
                }
            }
            else
            {
                if (!tags.IsEmpty && (def is null || !def.Tags.HasAny(tags)))
                {
                    continue;
                }
                if (def is { Dispellable: false } && tags.IsEmpty)
                {
                    continue;
                }
                if (kind == StatusKind.Harmful && def is { Beneficial: true } || kind == StatusKind.Beneficial && def is not { Beneficial: true })
                {
                    continue;
                }
            }
            RemoveInstance(sim, target, stats, statuses, s, expired: false);
            removed++;
        }
        return removed;
    }

    /// <summary>Ends one instance: its modifiers, flags and subscriptions go, its expiry effects run, statuses bound to it end.</summary>
    public void RemoveInstance(Simulation sim, Entity target, StatusInstance instance)
    {
        if (sim.World.TryGet<Stats>(target, out var stats) && sim.World.TryGet<Statuses>(target, out var statuses))
        {
            RemoveInstance(sim, target, stats, statuses, instance, expired: false);
        }
    }

    private void RemoveInstance(Simulation sim, Entity target, Stats stats, Statuses statuses, StatusInstance instance, bool expired)
    {
        if (!End(sim, target, stats, statuses, instance))
        {
            return;
        }
        Refresh(sim, target, stats, statuses);
        sim.Events.Publish(new StatusExpired(target, instance.Id) { Tick = sim.Clock.Tick });
        _ = expired;
        EndBound(sim, target, stats, statuses, instance.Id);
    }

    /// <summary>
    /// Takes an instance off its bearer without running its expiry effects, as when it moves to another body;
    /// statuses bound to it still end.
    /// </summary>
    public void Detach(Simulation sim, Entity target, StatusInstance instance)
    {
        if (!sim.World.TryGet<Stats>(target, out var stats) || !sim.World.TryGet<Statuses>(target, out var statuses))
        {
            return;
        }
        if (!End(sim, target, stats, statuses, instance, runExpire: false))
        {
            return;
        }
        Refresh(sim, target, stats, statuses);
        sim.Events.Publish(new StatusExpired(target, instance.Id) { Tick = sim.Clock.Tick });
        EndBound(sim, target, stats, statuses, instance.Id);
    }

    /// <summary>Takes an instance off its bearer and runs its expiry effects; false when it had already ended.</summary>
    private bool End(Simulation sim, Entity target, Stats stats, Statuses statuses, StatusInstance instance, bool runExpire = true)
    {
        if (instance.Removed)
        {
            return false;
        }
        instance.Removed = true;
        statuses.Active.Remove(instance);
        stats.Attributes.RemoveSource(instance.ModifierSource);
        Unsubscribe(sim, instance);
        var compiled = Compiled(sim, instance.Id);
        if (compiled is not null)
        {
            if (compiled.Def.Bank is { From: BankSource.Taken })
            {
                statuses.TakenBanks--;
            }
            if (compiled.Def.Pool is not null)
            {
                statuses.Pools--;
            }
            if (runExpire)
            {
                Run(sim, instance, target, compiled.OnExpire, compiled.HookTags, 0);
                if (instance.Riders is { Expire.Length: > 0 } riders)
                {
                    Run(sim, instance, target, riders.Expire, compiled.HookTags, 0);
                }
            }
        }
        return true;
    }

    /// <summary>Statuses bound to a pool status end once no instance of that status is left.</summary>
    private void EndBound(Simulation sim, Entity target, Stats stats, Statuses statuses, string ended)
    {
        if (statuses.Has(ended))
        {
            return;
        }
        for (var i = statuses.Active.Count - 1; i >= 0; i--)
        {
            if (i >= statuses.Active.Count)
            {
                continue;
            }
            var s = statuses.Active[i];
            if (sim.Content.Get<StatusDef>(s.Id) is { BoundTo: { } bound } && bound == ended)
            {
                RemoveInstance(sim, target, stats, statuses, s, expired: false);
            }
        }
    }

    // ------------------------------------------------------------ the bearer's flags, gauges and modifiers

    /// <summary>Rebuilds status modifiers, flags and gauges on the bearer from what is active.</summary>
    public void Refresh(Simulation sim, Entity target, Stats stats, Statuses statuses)
    {
        // Expiry effects can apply statuses while a refresh is being built; the outer refresh sees them.
        if (_refreshDepth > 0)
        {
            return;
        }
        _refreshDepth++;
        try
        {
            RefreshNow(sim, stats, statuses);
        }
        finally
        {
            _refreshDepth--;
        }
    }

    private void RefreshNow(Simulation sim, Stats stats, Statuses statuses)
    {
        var flags = _flags;
        var gauges = _gauges;
        flags.Clear();
        gauges.Clear();
        foreach (var s in statuses.Active)
        {
            stats.Attributes.RemoveSource(s.ModifierSource);
            var compiled = Compiled(sim, s.Id);
            if (compiled is null)
            {
                continue;
            }
            var def = compiled.Def;
            if (def.Modifiers.Count > 0)
            {
                var potency = def.ScalesWithPotency ? s.Values.GetValueOrDefault("potency", 1) : 1;
                var scale = (def.ModifiersPerStack ? s.Stacks : 1) * potency;
                // The scaled modifiers are kept per instance and rebuilt only when the scale changes.
                if (s.GrantedModifiers is null || !s.GrantedScale.Equals(scale))
                {
                    var granted = new Modifier[def.Modifiers.Count];
                    for (var i = 0; i < granted.Length; i++)
                    {
                        granted[i] = def.Modifiers[i].Scaled(scale).WithSource(s.ModifierSource);
                    }
                    s.GrantedModifiers = granted;
                    s.GrantedScale = scale;
                }
                foreach (var m in s.GrantedModifiers)
                {
                    stats.Attributes.Add(m);
                }
            }
            foreach (var f in def.Flags)
            {
                flags.Add(f);
            }
            // Forbidden weave tags become forbid:<tag> flags, the ones WeaveRuntime.TryBegin refuses on.
            foreach (var f in compiled.ForbidFlags)
            {
                flags.Add(f);
            }
            gauges[compiled.StacksGauge] = gauges.GetValueOrDefault(compiled.StacksGauge) + s.Stacks;
            gauges[compiled.PresenceGauge] = 1;
        }
        // Flags owned by statuses are prefixed so permanent flags set by patterns survive.
        var scratch = _scratch;
        scratch.Clear();
        foreach (var f in stats.Flags)
        {
            if (f.StartsWith("status:", StringComparison.Ordinal))
            {
                scratch.Add(f);
            }
        }
        foreach (var f in scratch)
        {
            stats.Flags.Remove(f);
        }
        foreach (var f in flags)
        {
            stats.Flags.Add(f);
            stats.Flags.Add(StatusFlag(f));
        }
        scratch.Clear();
        foreach (var key in stats.Gauges.Keys)
        {
            if (key.StartsWith("status.", StringComparison.Ordinal))
            {
                scratch.Add(key);
            }
        }
        foreach (var key in scratch)
        {
            stats.Gauges.Remove(key);
        }
        foreach (var (k, v) in gauges)
        {
            stats.Gauges[k] = v;
        }
        // Recompute which status-granted flags remain.
        scratch.Clear();
        foreach (var f in stats.Flags)
        {
            if (!f.StartsWith("status:", StringComparison.Ordinal) && !flags.Contains(f) && IsStatusFlag(sim, f))
            {
                scratch.Add(f);
            }
        }
        foreach (var f in scratch)
        {
            stats.Flags.Remove(f);
        }
        scratch.Clear();
        stats.Attributes.Touch();
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> StatusFlags = new(StringComparer.Ordinal);

    /// <summary><c>status:{flag}</c>, the prefixed copy that marks a flag as a status's, built once per flag.</summary>
    private static string StatusFlag(string flag) => StatusFlags.GetOrAdd(flag, static f => "status:" + f);

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
                foreach (var f in Compiled(sim, d).ForbidFlags)
                {
                    _knownStatusFlags.Add(f);
                }
            }
            _knownStatusFlags.Add("__init");
        }
        return _knownStatusFlags.Contains(flag);
    }

    // ------------------------------------------------------------ ticking

    /// <summary>Advances every status on an entity by dt: per-stack clocks, periodic effects, then expiry.</summary>
    public void Tick(Simulation sim, Entity target, Stats stats, Statuses statuses, double dt)
    {
        if (statuses.Active.Count == 0)
        {
            return;
        }
        var now = sim.Clock.Time;
        var changed = false;
        var ticking = _ticking;
        var start = ticking.Count;
        ticking.AddRange(statuses.Active);
        try
        {
            for (var i = start; i < ticking.Count; i++)
            {
                var s = ticking[i];
                if (s.Removed)
                {
                    continue;
                }
                s.Remaining -= dt;
                var compiled = Compiled(sim, s.Id);
                if (s.StackTimers is { } timers && AdvanceStacks(s, timers, dt))
                {
                    changed = true;
                }
                if (compiled?.Def.Tick is { } tick && now >= s.NextTickAt)
                {
                    TickOnce(sim, s, target, compiled, tick);
                    s.NextTickAt = now + tick.Interval;
                    if (sim.World.Has<Dead>(target))
                    {
                        return;
                    }
                }
                if (!s.Removed && s.Remaining <= 0)
                {
                    if (End(sim, target, stats, statuses, s))
                    {
                        sim.Events.Publish(new StatusExpired(target, s.Id) { Tick = sim.Clock.Tick });
                        changed = true;
                        EndBound(sim, target, stats, statuses, s.Id);
                    }
                }
            }
        }
        finally
        {
            ticking.RemoveRange(start, ticking.Count - start);
        }
        if (changed)
        {
            Refresh(sim, target, stats, statuses);
        }
    }

    /// <summary>Counts per-stack timers down; true when stacks fell off (the modifiers need rebuilding).</summary>
    private static bool AdvanceStacks(StatusInstance s, List<double> timers, double dt)
    {
        var before = timers.Count;
        var longest = 0.0;
        for (var i = timers.Count - 1; i >= 0; i--)
        {
            var left = timers[i] - dt;
            if (left <= 0)
            {
                timers.RemoveAt(i);
                continue;
            }
            timers[i] = left;
            longest = Math.Max(longest, left);
        }
        s.Remaining = timers.Count == 0 ? 0 : longest;
        if (timers.Count != before && timers.Count > 0)
        {
            s.Stacks = timers.Count;
            return true;
        }
        return false;
    }

    /// <summary>One periodic beat: the status's tick effects with its amount, then the applier's tick riders.</summary>
    private void TickOnce(Simulation sim, StatusInstance s, Entity target, CompiledStatus compiled, StatusTick tick)
    {
        double amount;
        if (compiled.Def.Bank is { From: BankSource.Applications })
        {
            // A bank deals itself out evenly over the time it has left; an empty bank has nothing to deal.
            var bank = s.Value("bank");
            amount = bank * Math.Min(1, tick.Interval / Math.Max(tick.Interval, s.Remaining));
            s.Values["bank"] = bank - amount;
            if (amount <= 1e-9)
            {
                return;
            }
        }
        else
        {
            amount = s.Magnitude * tick.Interval;
        }
        if (compiled.Tick.Length > 0)
        {
            Execute(sim, s, target, compiled.Tick, compiled.TickTags, amount);
        }
        if (s.Riders is { Tick.Length: > 0 } riders && !s.Removed && !sim.World.Has<Dead>(target))
        {
            Execute(sim, s, target, riders.Tick, compiled.TickTags, amount);
        }
    }

    private void Execute(Simulation sim, StatusInstance s, Entity target, CompiledEffect[] effects, TagSet tags, double amount)
    {
        var ctx = Context(sim, s, target, tags, 0, null);
        ctx.Vars["amount"] = amount;
        CompiledEffect.ExecuteAll(effects, ctx);
    }

    private void Run(Simulation sim, StatusInstance s, Entity target, CompiledEffect[] effects, TagSet tags, int depth)
    {
        if (effects.Length == 0)
        {
            return;
        }
        CompiledEffect.ExecuteAll(effects, Context(sim, s, target, tags, depth, null));
    }

    /// <summary>
    /// The context a status's effects run in: from its applier (or the bearer, when the applier is gone, or the
    /// given source) at the bearer, reading <c>amount</c>, <c>stacks</c>, <c>magnitude</c>, <c>remaining</c>,
    /// <c>elapsed</c>, <c>total</c> and <c>banked</c>.
    /// </summary>
    private static EffectContext Context(Simulation sim, StatusInstance s, Entity target, TagSet tags, int depth, Entity? source)
    {
        var from = source ?? (sim.World.IsAlive(s.Source) ? s.Source : target);
        return new EffectContext
        {
            Sim = sim,
            Source = from,
            Target = target,
            Point = sim.World.Get<Transform>(target)?.Position ?? default,
            Tags = tags,
            Depth = depth,
            Rng = sim.RngFor(target, "status:" + s.Id),
            Potency = s.Values.GetValueOrDefault("potency", 1),
            Vars = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["amount"] = 0,
                ["stacks"] = s.Stacks,
                ["magnitude"] = s.Magnitude,
                ["remaining"] = s.Remaining,
                ["elapsed"] = Math.Max(0, s.Total - s.Remaining),
                ["total"] = s.Total,
                ["banked"] = s.Value("bank"),
            },
        };
    }

    /// <summary>Runs a status hook once, refusing to run it again from inside itself.</summary>
    private static void RunGuarded(StatusInstance s, CompiledEffect[] effects, EffectContext ctx)
    {
        if (s.Busy || effects.Length == 0)
        {
            return;
        }
        s.Busy = true;
        try
        {
            CompiledEffect.ExecuteAll(effects, ctx);
        }
        finally
        {
            s.Busy = false;
        }
    }

    // ------------------------------------------------------------ hooks fed by the bearer's triggers

    private void Subscribe(Simulation sim, Entity bearer, StatusInstance instance, CompiledStatus compiled)
    {
        var def = compiled.Def;
        if (compiled.OnBearerHit.Length == 0 && compiled.OnBearerStruck.Length == 0 && compiled.ConsumeKind is null)
        {
            return;
        }
        if (!sim.TryService<TriggerHub>(out var hub))
        {
            return;
        }
        var handles = new List<int>(3);
        // A next-hit rider and consumption on the same kind share one handler, so the rider runs on the hit that
        // spends the stack and not again on the hits the rider itself lands.
        var consumeOnHit = compiled.ConsumeKind == TriggerKinds.Hit;
        if (compiled.OnBearerHit.Length > 0)
        {
            handles.Add(hub.Subscribe(TriggerKinds.Hit, bearer, (in TriggerEvent e) => OnBearerHit(sim, bearer, instance, compiled, e, consumeOnHit)));
        }
        if (compiled.OnBearerStruck.Length > 0)
        {
            var consumeOnHurt = compiled.ConsumeKind == TriggerKinds.Hurt;
            handles.Add(hub.Subscribe(TriggerKinds.Hurt, bearer, (in TriggerEvent e) => OnBearerStruck(sim, bearer, instance, compiled, e, consumeOnHurt)));
            if (consumeOnHurt)
            {
                instance.Subscriptions = handles.ToArray();
                return;
            }
        }
        if (compiled.ConsumeKind is { } kind && !(consumeOnHit && compiled.OnBearerHit.Length > 0))
        {
            handles.Add(hub.Subscribe(kind, bearer, (in TriggerEvent e) => OnConsume(sim, bearer, instance, compiled, e)));
        }
        instance.Subscriptions = handles.ToArray();
        _ = def;
    }

    private static void Unsubscribe(Simulation sim, StatusInstance instance)
    {
        if (instance.Subscriptions is not { } handles || !sim.TryService<TriggerHub>(out var hub))
        {
            return;
        }
        foreach (var h in handles)
        {
            hub.Unsubscribe(h);
        }
        instance.Subscriptions = null;
    }

    /// <summary>
    /// Whether a trigger may reach a status's hook: the instance is live, the event is not too deep, and a hit
    /// it hears of is not damage over time (a burning tick spends no next-hit buff and discharges no bank).
    /// </summary>
    private static bool Accepts(Simulation sim, StatusInstance instance, in TriggerEvent e)
    {
        if (instance.Removed || instance.Busy)
        {
            return false;
        }
        if ((e.Kind == TriggerKinds.Hit || e.Kind == TriggerKinds.Crit || e.Kind == TriggerKinds.Hurt) && e.Tags.Has("dot"))
        {
            return false;
        }
        return e.Depth < (int)sim.Content.Rules.Number("procs.max_depth", 3);
    }

    private void OnConsume(Simulation sim, Entity bearer, StatusInstance instance, CompiledStatus compiled, in TriggerEvent e)
    {
        var consume = compiled.Def.ConsumeOn!;
        if (!Accepts(sim, instance, e) || !e.Tags.HasAll(consume.Tags))
        {
            return;
        }
        Consume(sim, bearer, instance, consume.Count, e.Source == bearer ? e.Target : e.Source, consume.Event);
    }

    private void OnBearerHit(Simulation sim, Entity bearer, StatusInstance instance, CompiledStatus compiled, in TriggerEvent e, bool consume)
    {
        if (!Accepts(sim, instance, e))
        {
            return;
        }
        var spec = compiled.Def.ConsumeOn;
        if (consume && spec is not null && !e.Tags.HasAll(spec.Tags))
        {
            return;
        }
        var ctx = new EffectContext
        {
            Sim = sim,
            Source = bearer,
            Target = e.Target,
            Point = sim.World.Get<Transform>(e.Target)?.Position ?? default,
            Tags = compiled.HookTags,
            Depth = e.Depth + 1,
            Rng = sim.RngFor(bearer, "rider:" + instance.Id),
            Potency = instance.Values.GetValueOrDefault("potency", 1),
            Vars = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["amount"] = e.Amount,
                ["last_damage"] = e.Amount,
                ["stacks"] = instance.Stacks,
                ["magnitude"] = instance.Magnitude,
                ["banked"] = instance.Value("bank"),
            },
        };
        if (consume && spec is not null)
        {
            // Spend first, so the rider's own hits find the stack gone; the rider still runs for this hit.
            instance.Busy = true;
            try
            {
                Consume(sim, bearer, instance, spec.Count, e.Target, spec.Event);
            }
            finally
            {
                instance.Busy = false;
            }
        }
        RunGuarded(instance, compiled.OnBearerHit, ctx);
    }

    private void OnBearerStruck(Simulation sim, Entity bearer, StatusInstance instance, CompiledStatus compiled, in TriggerEvent e, bool consume)
    {
        if (!Accepts(sim, instance, e))
        {
            return;
        }
        var spec = compiled.Def.ConsumeOn;
        if (consume && spec is not null && !e.Tags.HasAll(spec.Tags))
        {
            return;
        }
        var ctx = new EffectContext
        {
            Sim = sim,
            Source = sim.World.IsAlive(e.Source) ? e.Source : bearer,
            Target = bearer,
            Point = sim.World.Get<Transform>(bearer)?.Position ?? default,
            Tags = compiled.HookTags,
            Depth = e.Depth + 1,
            Rng = sim.RngFor(bearer, "struck:" + instance.Id),
            Potency = instance.Values.GetValueOrDefault("potency", 1),
            Vars = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["amount"] = e.Amount,
                ["last_damage"] = e.Amount,
                ["stacks"] = instance.Stacks,
                ["magnitude"] = instance.Magnitude,
                ["banked"] = instance.Value("bank"),
            },
        };
        RunGuarded(instance, compiled.OnBearerStruck, ctx);
        if (consume && spec is not null && !instance.Removed)
        {
            Consume(sim, bearer, instance, spec.Count, e.Source, spec.Event);
        }
    }

    /// <summary>Spends stacks of an instance; the last stack spent ends it. Returns how many were spent.</summary>
    public int Consume(Simulation sim, Entity bearer, StatusInstance instance, int count, Entity by, string reason)
    {
        if (instance.Removed || count <= 0 || !sim.World.TryGet<Stats>(bearer, out var stats) || !sim.World.TryGet<Statuses>(bearer, out var statuses))
        {
            return 0;
        }
        var spent = Math.Min(count, instance.Stacks);
        sim.Events.Publish(new StatusConsumed(bearer, by, instance.Id, spent, reason) { Tick = sim.Clock.Tick });
        if (spent >= instance.Stacks)
        {
            RemoveInstance(sim, bearer, stats, statuses, instance, expired: false);
            return spent;
        }
        instance.Stacks -= spent;
        if (instance.StackTimers is { } timers)
        {
            // The stacks nearest to falling off go first.
            timers.Sort();
            timers.RemoveRange(0, Math.Min(spent, timers.Count));
        }
        Refresh(sim, bearer, stats, statuses);
        return spent;
    }

    // ------------------------------------------------------------ banks and pools

    /// <summary>
    /// Damage a body took: statuses banking what their bearer takes add their fraction, up to their cap. The
    /// pools stage calls it for every hit that landed.
    /// </summary>
    public void DamageTaken(Simulation sim, Entity bearer, Stats stats, double amount, int depth)
    {
        if (amount <= 0 || !sim.World.TryGet<Statuses>(bearer, out var statuses) || statuses.TakenBanks <= 0)
        {
            return;
        }
        for (var i = 0; i < statuses.Active.Count; i++)
        {
            var s = statuses.Active[i];
            if (Compiled(sim, s.Id) is not { Def.Bank: { From: BankSource.Taken } bank } compiled)
            {
                continue;
            }
            var fraction = bank.Fraction is null ? 1 : Math.Max(0, bank.Fraction.Evaluate(stats));
            var banked = s.Value("bank") + amount * fraction;
            if (bank.Cap is { } cap)
            {
                banked = Math.Min(banked, Math.Max(0, cap.Evaluate(stats)));
            }
            s.Values["bank"] = banked;
            CheckBankLethal(sim, bearer, stats, s, compiled, depth);
        }
    }

    private void CheckBankLethal(Simulation sim, Entity bearer, Stats stats, StatusInstance s, CompiledStatus compiled, int depth)
    {
        if (compiled.OnBankLethal.Length == 0 || s.LethalFired || s.Removed || s.Value("bank") < stats.Resource("life"))
        {
            return;
        }
        s.LethalFired = true;
        RunGuarded(s, compiled.OnBankLethal, Context(sim, s, bearer, compiled.HookTags, depth, null));
    }

    /// <summary>
    /// Status pools absorb part of a hit, newest first; a pool emptied ends its status. Returns what was left
    /// for the pools after them.
    /// </summary>
    public double Absorb(Simulation sim, Entity bearer, Stats stats, double amount)
    {
        if (amount <= 0 || !sim.World.TryGet<Statuses>(bearer, out var statuses) || statuses.Pools <= 0)
        {
            return amount;
        }
        for (var i = statuses.Active.Count - 1; i >= 0 && amount > 0; i--)
        {
            if (i >= statuses.Active.Count)
            {
                continue;
            }
            var s = statuses.Active[i];
            if (!s.Values.TryGetValue("pool", out var pool) || pool <= 0)
            {
                continue;
            }
            var take = Math.Min(pool, amount);
            amount -= take;
            s.Values["pool"] = pool - take;
            if (pool - take <= 1e-9)
            {
                RemoveInstance(sim, bearer, stats, statuses, s, expired: false);
            }
        }
        return amount;
    }

    // ------------------------------------------------------------ rupture

    /// <summary>
    /// Spends a fraction of each instance of a status on a body as damage now: its bank, or the damage it still
    /// had to deal, whose time shrinks by the same fraction. Returns the total spent; the caller deals it.
    /// </summary>
    public double Rupture(Simulation sim, Entity bearer, string statusId, double fraction)
    {
        if (fraction <= 0 || !sim.World.TryGet<Stats>(bearer, out var stats) || !sim.World.TryGet<Statuses>(bearer, out var statuses) || Compiled(sim, statusId) is not { } compiled)
        {
            return 0;
        }
        fraction = Math.Min(1, fraction);
        var total = 0.0;
        var list = statuses.Active.ToArray();
        foreach (var s in list)
        {
            if (s.Id != statusId || s.Removed)
            {
                continue;
            }
            var left = RemainingDamage(s, compiled.Def);
            var spent = left * fraction;
            total += spent;
            if (compiled.Def.Bank is { From: BankSource.Applications })
            {
                s.Values["bank"] = left - spent;
            }
            else if (compiled.Def.Bank is { From: BankSource.Taken })
            {
                // A bank of damage taken bursts as itself: what it holds, not what the status deals.
                var banked = s.Value("bank");
                total += banked * fraction - spent;
                s.Values["bank"] = banked * (1 - fraction);
            }
            else
            {
                s.Remaining *= 1 - fraction;
            }
            if (fraction >= 1 && compiled.Def.Bank is not { From: BankSource.Taken })
            {
                RemoveInstance(sim, bearer, stats, statuses, s, expired: false);
            }
        }
        return total;
    }

    // ------------------------------------------------------------ death

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

/// <summary>Which statuses a removal may take when it names none.</summary>
public enum StatusKind
{
    /// <summary>Harmful and beneficial alike.</summary>
    Any,
    /// <summary>Only statuses that are not beneficial: a cleanse.</summary>
    Harmful,
    /// <summary>Only beneficial statuses: a purge.</summary>
    Beneficial,
}
