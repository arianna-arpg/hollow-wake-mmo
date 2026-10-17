using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Maths;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Sim;

/// <summary>
/// Runs procs (content kind <c>procs</c>): when a trigger kind fires for an owner whose chance attribute
/// (<c>proc.{id}</c>, granted by any modifier source) is above zero, and the event carries the proc's tags, its
/// <c>when</c> holds on the owner and its <c>victim</c> on the other body, the proc rolls and runs its effect graph
/// one level deeper than the event. <c>when</c> and the payload also read the trigger's <c>amount</c>, <c>crit</c> and
/// <c>roll</c> (where a hit fell within its range, zero for hits that rolled none), so a high roll can proc. Rates are a chance, an internal cooldown, or procs per minute (the chance
/// growing with the time since the owner's last check). Depth gates chains: past rule <c>procs.max_depth</c> a
/// proc is refused, and below it the chance keeps <c>falloff</c> (rule <c>procs.depth_falloff</c>) per level;
/// rule <c>procs.chance_cap</c> caps any chance. Each owner rolls from its own stream derived from the
/// simulation's seed, so a fight replays exactly. The service subscribes to the trigger hub per owner and kind,
/// only for bodies that carry a proc's chance: the status system asks it to look again whenever a body's
/// modifiers change (<see cref="Watch"/>), so a body without procs costs a field compare a tick and its hits never
/// reach the service.
/// </summary>
public sealed class ProcService
{
    private readonly Simulation _sim;
    private readonly Dictionary<int, Entry[]> _byKind = new();
    private readonly Dictionary<Entity, OwnerState> _owners = new();
    private readonly int _count;
    // The kinds procs listen on, and for each watched body its subscription per kind (zero when none).
    private TriggerKind[] _kinds = Array.Empty<TriggerKind>();
    private readonly Dictionary<Entity, int[]> _subscriptions = new();
    private readonly TriggerHandler _handler;
    // The owner scope with the trigger numbers on top, reused for every when check.
    private readonly TriggerScope _when = new();

    private sealed class Entry
    {
        public required ProcDef Def;
        public required int Index;
        public required string Chance;
        public required TagSet PayloadTags;
        public CompiledEffect[]? Effects;
    }

    private sealed class OwnerState
    {
        public required Rng Rng;
        public required double[] ReadyAt;
        public required double[] LastCheck;
    }

    public ProcService(Simulation sim)
    {
        _sim = sim;
        _handler = OnTrigger;
        var procs = sim.Content.Each<ProcDef>().OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
        _count = procs.Count;
        if (procs.Count == 0)
        {
            return;
        }
        var byKind = new Dictionary<TriggerKind, List<Entry>>();
        for (var i = 0; i < procs.Count; i++)
        {
            var def = procs[i];
            if (!TriggerKinds.TryGet(def.On, out var kind))
            {
                sim.Log.Warn($"proc '{def.Id}' names unknown trigger kind '{def.On}'");
                continue;
            }
            var tags = def.NoCrit ? TagSet.Of("proc", "proc." + def.Id, "no_crit") : TagSet.Of("proc", "proc." + def.Id);
            if (!byKind.TryGetValue(kind, out var list))
            {
                list = new List<Entry>();
                byKind[kind] = list;
            }
            list.Add(new Entry { Def = def, Index = i, Chance = def.Chance ?? "proc." + def.Id, PayloadTags = tags });
        }
        foreach (var (kind, list) in byKind)
        {
            _byKind[kind.Id] = list.ToArray();
        }
        _kinds = byKind.Keys.OrderBy(k => k.Name, StringComparer.Ordinal).ToArray();
        sim.Events.Subscribe<EntityDespawned>(d =>
        {
            _owners.Remove(d.Entity);
            _subscriptions.Remove(d.Entity);
        });
    }

    /// <summary>
    /// Looks at a body again when its modifiers changed: subscribes it to each kind one of its proc chances is
    /// carried for, and lets go of kinds it no longer carries any for. The hub drops a despawned body's
    /// subscriptions by itself.
    /// </summary>
    public void Watch(Entity body, Stats stats)
    {
        if (_kinds.Length == 0 || stats.ProcVersion == stats.Attributes.Version)
        {
            return;
        }
        stats.ProcVersion = stats.Attributes.Version;
        _subscriptions.TryGetValue(body, out var handles);
        for (var k = 0; k < _kinds.Length; k++)
        {
            var kind = _kinds[k];
            var wants = false;
            foreach (var entry in _byKind[kind.Id])
            {
                if (stats.Attributes.Carries(entry.Chance))
                {
                    wants = true;
                    break;
                }
            }
            var held = handles is not null && handles[k] != 0;
            if (wants == held)
            {
                continue;
            }
            var hub = _sim.Service<TriggerHub>();
            if (wants)
            {
                if (handles is null)
                {
                    handles = new int[_kinds.Length];
                    _subscriptions[body] = handles;
                }
                handles[k] = hub.Subscribe(kind, body, _handler);
            }
            else
            {
                hub.Unsubscribe(handles![k]);
                handles[k] = 0;
            }
        }
    }

    /// <summary>How many procs the content defines.</summary>
    public int Count => _count;

    private void OnTrigger(in TriggerEvent e)
    {
        var owner = e.Owner;
        if (owner.IsNone || !_byKind.TryGetValue(e.Kind.Id, out var entries))
        {
            return;
        }
        var world = _sim.World;
        if (!world.TryGet<Stats>(owner, out var stats) || world.Has<Dead>(owner))
        {
            return;
        }
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (!stats.Attributes.Carries(entry.Chance) || !e.Tags.HasAll(entry.Def.Tags) || e.Tags.HasAny(entry.Def.Excludes))
            {
                continue;
            }
            var chance = stats.Attributes.Get(entry.Chance, stats, e.Tags);
            if (chance <= 0)
            {
                continue;
            }
            TryFire(e, owner, stats, entry, chance);
        }
    }

    private void TryFire(in TriggerEvent e, Entity owner, Stats stats, Entry entry, double chance)
    {
        var rules = _sim.Content.Rules;
        var def = entry.Def;
        var maxDepth = (int)rules.Number("procs.max_depth", 3);
        if (e.Depth >= maxDepth)
        {
            _sim.Events.Publish(new ProcRefused(owner, def.Id, "too deep") { Tick = _sim.Clock.Tick, Depth = e.Depth });
            return;
        }
        var state = StateOf(owner);
        var now = _sim.Clock.Time;
        if (now < state.ReadyAt[entry.Index])
        {
            return;
        }
        if (def.Ppm > 0)
        {
            var last = state.LastCheck[entry.Index];
            var elapsed = double.IsNegativeInfinity(last) ? 1 : Math.Min(now - last, 60 / def.Ppm);
            state.LastCheck[entry.Index] = now;
            chance *= def.Ppm * elapsed / 60;
        }
        chance *= Math.Pow(def.Falloff ?? rules.Number("procs.depth_falloff", 0.5), e.Depth);
        chance = Math.Min(chance, rules.Number("procs.chance_cap", 1));
        if (chance <= 0)
        {
            return;
        }
        var other = e.Source == owner ? e.Target : e.Source;
        if (def.When is { } when && !_when.Holds(when, stats, e))
        {
            return;
        }
        if (def.Victim is { } victim && !(_sim.World.TryGet<Stats>(other, out var os) && victim.IsTrue(os)))
        {
            return;
        }
        if (!state.Rng.Chance(chance))
        {
            return;
        }
        state.ReadyAt[entry.Index] = now + def.Icd;
        Fire(e, owner, other, entry, state);
    }

    private void Fire(in TriggerEvent e, Entity owner, Entity other, Entry entry, OwnerState state)
    {
        var sim = _sim;
        var target = entry.Def.Target == "owner" ? owner : other;
        var effects = entry.Effects ??= Compile(entry.Def);
        var focus = sim.World.Get<Transform>(target)?.Position ?? sim.World.Get<Transform>(owner)?.Position ?? default;
        var depth = e.Depth + 1;
        sim.Events.Publish(new ProcFired(owner, entry.Def.Id, target) { Tick = sim.Clock.Tick, Depth = depth });
        var ctx = new EffectContext
        {
            Sim = sim,
            Source = owner,
            Target = sim.World.IsAlive(target) ? target : Entity.None,
            Point = focus,
            Tags = entry.PayloadTags,
            Depth = depth,
            Rng = state.Rng,
            Vars = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["amount"] = e.Amount,
                ["last_damage"] = e.Amount,
                ["crit"] = e.Crit ? 1 : 0,
                ["roll"] = TriggerScope.RollOf(e),
            },
        };
        CompiledEffect.ExecuteAll(effects, ctx);
    }

    /// <summary>
    /// A proc owner's scope with the trigger's <c>amount</c>, <c>crit</c> and <c>roll</c> layered on top. One instance
    /// serves every check: a check only evaluates, so nothing can run inside it.
    /// </summary>
    private sealed class TriggerScope : Expressions.IExprScope
    {
        private Stats _owner = null!;
        private double _amount;
        private bool _crit;
        private double _roll;

        /// <summary>Where the trigger's hit fell within its range; zero when it came from no rolled hit.</summary>
        public static double RollOf(in TriggerEvent e) => e.Cause is DamageDealt { Roll: { } roll } ? roll : 0;

        public bool Holds(Expressions.Expr expr, Stats owner, in TriggerEvent e)
        {
            _owner = owner;
            _amount = e.Amount;
            _crit = e.Crit;
            _roll = RollOf(e);
            try
            {
                return expr.IsTrue(this);
            }
            finally
            {
                _owner = null!;
            }
        }

        public bool TryResolve(string name, out double value)
        {
            switch (name)
            {
                case "amount":
                    value = _amount;
                    return true;
                case "crit":
                    value = _crit ? 1 : 0;
                    return true;
                case "roll":
                    value = _roll;
                    return true;
            }
            return _owner.TryResolve(name, out value);
        }

        public bool HasFlag(string flag) => _owner.HasFlag(flag);

        public double NextRandom() => _owner.NextRandom();
    }

    private CompiledEffect[] Compile(ProcDef def)
    {
        var effects = _sim.Service<EffectRegistry>();
        var ctx = new EffectCompileContext { Content = _sim.Content, Registry = effects, Owner = "proc " + def.Id };
        return effects.CompileAll(def.Effects, ctx).ToArray();
    }

    private OwnerState StateOf(Entity owner)
    {
        if (!_owners.TryGetValue(owner, out var state))
        {
            state = new OwnerState
            {
                Rng = new Rng(Rng.Combine(_sim.Rng.Seed, Rng.Combine((ulong)owner.Key, Rng.Hash64("procs")))),
                ReadyAt = new double[_count],
                LastCheck = Enumerable.Repeat(double.NegativeInfinity, _count).ToArray(),
            };
            _owners[owner] = state;
        }
        return state;
    }
}

/// <summary>
/// The trigger kinds the combat package raises from its own stages and services, registered before content
/// loads so procs, glyph triggers and consumable statuses can name them.
/// </summary>
public static class CombatTriggers
{
    static CombatTriggers()
    {
        Break = TriggerKinds.Register("break", "the owner empties a pool or a break bar (poise) of another body; the pool id rides as the status");
        Broken = TriggerKinds.Register("broken", "a pool or a break bar of the owner is emptied; the pool id rides as the status");
        Cull = TriggerKinds.Register("cull", "the owner culls a body");
        CheatDeath = TriggerKinds.Register("cheat_death", "the owner cheats death");
        ChargeGained = TriggerKinds.Register("charge_gained", "the owner gains charges; the charge id rides as the status and the count as the amount");
    }

    public static TriggerKind Break { get; }
    public static TriggerKind Broken { get; }
    public static TriggerKind Cull { get; }
    public static TriggerKind CheatDeath { get; }
    public static TriggerKind ChargeGained { get; }

    /// <summary>Makes sure the kinds are registered; content kind registration calls it before content loads.</summary>
    public static void EnsureRegistered()
    {
    }

    /// <summary>Raises a kind for an owner when anyone listens.</summary>
    public static void Raise(Simulation sim, Entity owner, TriggerKind kind, Entity source, Entity target, string? status, int depth, double amount = 0)
    {
        if (owner.IsNone || !sim.TryService<TriggerHub>(out var hub) || !hub.HasSubscribers(kind))
        {
            return;
        }
        hub.Raise(owner, kind, new TriggerEvent(source, target, amount, false, null, depth, status));
    }
}
