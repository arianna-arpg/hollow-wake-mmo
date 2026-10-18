using System.Numerics;
using System.Runtime.CompilerServices;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Effects.Handlers;
using HollowWake.Core.Minions;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Systems;

/// <summary>
/// Keeps minions in step with their owners: what they inherit from the owner, contracts that respawn the
/// fallen, the decay of what does not last, the presences apexes cast over the rest, slotted summons that
/// replenish themselves, and domes catching projectiles before they move. Runs after spawners and before
/// behaviors think; does nothing in a simulation where nothing was ever summoned.
/// </summary>
public sealed class MinionSystem : SimSystem
{
    private static readonly ConditionalWeakTable<CompiledWeave, SummonRecipe[]> Replenishers = new();

    private readonly List<Entity> _owners = new();
    private readonly List<Entity> _caught = new();
    private readonly List<Entity> _gone = new();
    private readonly List<string> _presences = new();
    private readonly List<IReadOnlyList<Attributes.Modifier>> _presenceMods = new();
    private readonly Dictionary<(Entity Owner, string Slot), double> _replenishAt = new();
    private double _nextReplenishScan;
    private double _nextInheritCheck;
    private long _seenBirths;
    private int _decaying;

    public override string Name => "minions";
    public override SimPhase Phase => SimPhase.Think;
    public override int Order => -5;

    public override void Update(Simulation sim)
    {
        if (!sim.TryService<MinionService>(out var service))
        {
            return;
        }
        var now = sim.Clock.Time;
        // The bodies are walked when inheritance is due, while some decay, and on a tick bodies were born.
        if (sim.World.CountOf<Minion>() > 0 && (now >= _nextInheritCheck || _decaying > 0 || service.Births != _seenBirths))
        {
            Upkeep(sim, service, now);
        }
        if (service.Contracts.Count > 0)
        {
            Contracts(sim, service, now);
        }
        if (service.PresencesSeen)
        {
            Presences(sim, service);
        }
        if (sim.World.CountOf<Intercept>() > 0)
        {
            Domes(sim);
        }
        if (now >= _nextReplenishScan)
        {
            _nextReplenishScan = now + sim.Content.Rules.Number("minions.replenish_scan", 1);
            Replenish(sim, service, now);
        }
    }

    /// <summary>Folds inheritance again where an owner's attributes changed (every <c>minions.inherit_interval</c>), and drains decaying bodies.</summary>
    private void Upkeep(Simulation sim, MinionService service, double now)
    {
        var inherit = now >= _nextInheritCheck;
        if (inherit)
        {
            _nextInheritCheck = now + sim.Content.Rules.Number("minions.inherit_interval", 0.5);
        }
        _seenBirths = service.Births;
        _decaying = 0;
        var dt = sim.Clock.Dt;
        foreach (var (e, m) in sim.World.Query<Minion>())
        {
            if (inherit && m.Inheritance is { } inheritance && sim.World.TryGet<Stats>(m.Owner, out var ownerStats) && ownerStats.Attributes.Version != inheritance.OwnerVersion && sim.World.TryGet<Stats>(e, out var stats))
            {
                service.Refold(stats, inheritance, ownerStats);
            }
            if (m.Decay is { } decay && sim.World.TryGet<Stats>(e, out var body) && !sim.World.Has<Dead>(e))
            {
                _decaying++;
                if (now < decay.StartsAt)
                {
                    continue;
                }
                var drain = decay.Rate * Math.Pow(decay.Growth, now - decay.StartsAt) * dt;
                body.AddResource("life", -drain);
                if (body.Resource("life") <= 0)
                {
                    sim.Service<DeathService>().Kill(sim, e, Entity.None);
                }
            }
        }
    }

    /// <summary>Ends contracts whose owner or slot is gone, and brings back the bodies whose time came.</summary>
    private static void Contracts(Simulation sim, MinionService service, double now)
    {
        var contracts = service.Contracts;
        for (var i = contracts.Count - 1; i >= 0; i--)
        {
            if (i >= contracts.Count)
            {
                continue;
            }
            var contract = contracts[i];
            if (!sim.World.IsAlive(contract.Owner))
            {
                service.EndContract(contract);
                continue;
            }
            if (contract.Slot is not null && (!sim.World.TryGet<WeaveBook>(contract.Owner, out var book) || !book.Slots.Contains(contract.Slot)))
            {
                service.EndContract(contract);
                continue;
            }
            if (contract.Due.Count == 0 || sim.World.Has<Dead>(contract.Owner))
            {
                continue;
            }
            for (var d = 0; d < contract.Due.Count;)
            {
                if (contract.Due[d] > now)
                {
                    d++;
                    continue;
                }
                contract.Due.RemoveAt(d);
                var body = contract.Recipe.Respawn(service, contract);
                if (!body.IsNone)
                {
                    sim.Events.Publish(new ContractRespawned(contract.Owner, body, contract.Key) { Tick = sim.Clock.Tick });
                }
            }
        }
    }

    /// <summary>
    /// For owners whose minions changed: while a body with a presence lives, the owner's minions of other
    /// groups wear its modifiers under <c>presence:{group}</c>; those who wear one no longer cast shed it.
    /// </summary>
    private void Presences(Simulation sim, MinionService service)
    {
        _owners.Clear();
        service.DirtyOwners(_owners);
        if (_owners.Count == 0)
        {
            return;
        }
        foreach (var owner in _owners)
        {
            service.TakePresenceDirty(owner);
            var list = service.RentList();
            service.Living(owner, MinionScope.All, list);
            _presences.Clear();
            _presenceMods.Clear();
            foreach (var e in list)
            {
                if (sim.World.Get<Minion>(e) is { Presence: { } mods } m)
                {
                    var key = m.PresenceKey;
                    if (!_presences.Contains(key))
                    {
                        _presences.Add(key);
                        _presenceMods.Add(mods);
                    }
                }
            }
            foreach (var e in list)
            {
                if (!sim.World.TryGet<Minion>(e, out var m) || !sim.World.TryGet<Stats>(e, out var stats))
                {
                    continue;
                }
                var own = m.PresenceKey;
                if (m.WornPresences is { } worn)
                {
                    for (var i = worn.Count - 1; i >= 0; i--)
                    {
                        if (!_presences.Contains(worn[i]) || worn[i] == own)
                        {
                            stats.Attributes.RemoveSource(worn[i]);
                            worn.RemoveAt(i);
                        }
                    }
                }
                for (var i = 0; i < _presences.Count; i++)
                {
                    var key = _presences[i];
                    if (key == own || (m.WornPresences?.Contains(key) ?? false))
                    {
                        continue;
                    }
                    stats.Attributes.AddRange(_presenceMods[i], key);
                    (m.WornPresences ??= new List<string>()).Add(key);
                }
            }
            service.ReturnList(list);
        }
    }

    /// <summary>
    /// Domes catch hostile projectiles about to reach them, before projectiles move: dissolved, turned back
    /// on their own side, or slowed while inside and given their speed back when they leave.
    /// </summary>
    private void Domes(Simulation sim)
    {
        var dt = (float)sim.Clock.Dt;
        foreach (var (dome, intercept, at) in sim.World.Query<Intercept, Transform>())
        {
            if (sim.World.Has<Dead>(dome))
            {
                continue;
            }
            var owner = sim.World.Get<Minion>(dome)?.Owner ?? Entity.None;
            if (owner.IsNone || !sim.World.IsAlive(owner))
            {
                owner = dome;
            }
            Restore(sim, intercept, at);
            _caught.Clear();
            foreach (var (e, _, _) in sim.Spatial.Within(at.Position, intercept.Radius + 3, 6f))
            {
                if (sim.World.TryGet<Projectile>(e, out var p) && Relations.IsHostile(sim, p.Context.Origin, owner) && !(intercept.Slowed?.ContainsKey(e) ?? false))
                {
                    _caught.Add(e);
                }
            }
            foreach (var e in _caught)
            {
                var p = sim.World.Get<Projectile>(e)!;
                var t = sim.World.Get<Transform>(e)!;
                var next = t.Position + p.Velocity * dt;
                if (Vector2.Distance(new Vector2(next.X, next.Y), new Vector2(at.Position.X, at.Position.Y)) > intercept.Radius)
                {
                    continue;
                }
                switch (intercept.Mode)
                {
                    case "deflect":
                        Deflect(sim, e, p, owner);
                        break;
                    case "slow":
                        p.Velocity *= intercept.Slow;
                        (intercept.Slowed ??= new Dictionary<Entity, float>())[e] = intercept.Slow;
                        break;
                    default:
                        // Without its projectile component nothing flies or hits with it before the world flushes.
                        sim.World.Remove<Projectile>(e);
                        sim.Spatial.Remove(e);
                        sim.Events.Publish(new EntityDespawned(e, "projectile") { Tick = sim.Clock.Tick });
                        sim.World.DestroyDeferred(e);
                        break;
                }
                sim.Events.Publish(new ProjectileIntercepted(e, dome, intercept.Mode) { Tick = sim.Clock.Tick });
            }
        }
    }

    /// <summary>Gives slowed projectiles that left the dome (or are gone) their speed back.</summary>
    private void Restore(Simulation sim, Intercept intercept, Transform at)
    {
        if (intercept.Slowed is not { Count: > 0 } slowed)
        {
            return;
        }
        _gone.Clear();
        foreach (var (e, factor) in slowed)
        {
            if (!sim.World.TryGet<Projectile>(e, out var p) || !sim.World.TryGet<Transform>(e, out var t))
            {
                _gone.Add(e);
                continue;
            }
            if (Vector2.Distance(new Vector2(t.Position.X, t.Position.Y), new Vector2(at.Position.X, at.Position.Y)) > intercept.Radius)
            {
                p.Velocity /= Math.Max(0.01f, factor);
                _gone.Add(e);
            }
        }
        foreach (var e in _gone)
        {
            slowed.Remove(e);
        }
    }

    /// <summary>Turns a projectile around and gives it to the dome's side: it flies back, hits its own side and credits the dome's owner.</summary>
    private static void Deflect(Simulation sim, Entity e, Projectile p, Entity owner)
    {
        var old = p.Context;
        p.Velocity = new Vector3(-p.Velocity.X, -p.Velocity.Y, p.Velocity.Z);
        p.Traveled = 0;
        p.Hit.Clear();
        p.Context = new EffectContext
        {
            Sim = sim,
            Origin = owner,
            Source = owner,
            Point = old.Point,
            Direction = -old.Direction,
            Weave = old.Weave,
            Tags = old.Tags,
            Depth = old.Depth,
            Rng = old.Rng,
            Potency = old.Potency,
            DamageScale = old.DamageScale,
            Vars = old.Vars,
        };
        if (sim.World.TryGet<Identity>(e, out var identity))
        {
            identity.Owner = owner;
            identity.Faction = Relations.FactionOf(sim, owner);
        }
    }

    /// <summary>Slotted summons that replenish: a body is born every interval while their group has room.</summary>
    private void Replenish(Simulation sim, MinionService service, double now)
    {
        if (sim.World.CountOf<WeaveBook>() == 0)
        {
            return;
        }
        foreach (var (owner, book) in sim.World.Query<WeaveBook>())
        {
            if (sim.World.Has<Dead>(owner))
            {
                continue;
            }
            foreach (var slot in book.Slots)
            {
                if (slot.Compiled is not { } weave)
                {
                    continue;
                }
                var recipes = Replenishers.GetValue(weave, static w => FindReplenishers(w));
                foreach (var recipe in recipes)
                {
                    var key = (owner, slot.Key);
                    if (!_replenishAt.TryGetValue(key, out var due))
                    {
                        _replenishAt[key] = now + Interval(sim, owner, weave, recipe);
                        continue;
                    }
                    if (now < due)
                    {
                        continue;
                    }
                    recipe.ReplenishOne(service, owner, weave);
                    _replenishAt[key] = now + Interval(sim, owner, weave, recipe);
                }
            }
        }
    }

    private static double Interval(Simulation sim, Entity owner, CompiledWeave weave, SummonRecipe recipe) =>
        Math.Max(sim.Clock.Dt, SummonRecipe.ContextFor(sim, owner, weave, "replenish").Eval(recipe.Replenish, 4));

    private static SummonRecipe[] FindReplenishers(CompiledWeave weave)
    {
        List<SummonRecipe>? found = null;
        foreach (var effect in weave.Effects)
        {
            if (effect is SummonEffect { Recipe.Replenish: not null } summon)
            {
                (found ??= new List<SummonRecipe>()).Add(summon.Recipe);
            }
        }
        return found?.ToArray() ?? Array.Empty<SummonRecipe>();
    }
}
