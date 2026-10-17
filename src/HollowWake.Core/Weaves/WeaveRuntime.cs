using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Weaves;

/// <summary>
/// Begins, resolves and interrupts weaves for players and monsters alike. One pipeline: validate the
/// request, pay costs, start the cast or resolve instantly, then run the compiled effect graph with
/// an effect context. Monsters go through exactly the same calls as players.
/// </summary>
public sealed class WeaveRuntime
{
    private readonly WeaveCompiler _compiler;

    public WeaveRuntime(WeaveCompiler compiler)
    {
        _compiler = compiler;
    }

    public WeaveCompiler Compiler => _compiler;

    public CompiledWeave? Resolve(Simulation sim, WeaveSlot slot, out string? error)
    {
        if (slot.Compiled is not null)
        {
            error = null;
            return slot.Compiled;
        }
        if (_compiler.TryCompile(slot.Spec, out var weave, out error))
        {
            slot.Compiled = weave;
            return weave;
        }
        sim.Log.Warn($"weave {slot.Spec} failed to compile: {error}");
        return null;
    }

    /// <summary>Costs after the caster's cost multipliers, per resource.</summary>
    public Dictionary<string, double> Costs(CompiledWeave weave, Stats caster)
    {
        var scope = weave.ScopeFor(caster);
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (resource, expr) in weave.Costs)
        {
            var baseCost = Math.Max(0, expr.Evaluate(scope));
            var cost = caster.Attributes.Apply("cost", baseCost, caster, weave.Tags.With("cost." + resource), weave.Modifiers);
            result[resource] = Math.Max(0, cost);
        }
        return result;
    }

    public bool CanAfford(CompiledWeave weave, Stats caster, out string? missing)
    {
        foreach (var (resource, cost) in Costs(weave, caster))
        {
            if (caster.Resource(resource) < cost - 1e-6)
            {
                missing = resource;
                return false;
            }
        }
        missing = null;
        return true;
    }

    public double CastDuration(CompiledWeave weave, Stats caster)
    {
        var time = weave.CastTime(caster);
        if (time <= 0)
        {
            return 0;
        }
        var speedAttr = weave.Tags.Has("attack") ? "attack.speed" : "cast.speed";
        var speed = Math.Max(0.1, caster.Attributes.Get(speedAttr, caster, weave.Tags, weave.Modifiers));
        return time / speed;
    }

    public bool TryBegin(Simulation sim, Entity caster, WeaveSlot slot, Entity target, Vector3 point, Vector2 direction, out string reason)
    {
        reason = "";
        if (!sim.World.TryGet<Stats>(caster, out var stats) || sim.World.Has<Dead>(caster))
        {
            reason = "dead";
            return false;
        }
        var book = sim.World.Get<WeaveBook>(caster);
        if (book is null)
        {
            reason = "no_weaves";
            return false;
        }
        var weave = Resolve(sim, slot, out var error);
        if (weave is null)
        {
            reason = "invalid:" + error;
            return Fail(sim, caster, slot, reason);
        }
        var now = sim.Clock.Time;
        if (stats.HasFlag("stunned") || stats.HasFlag("frozen"))
        {
            reason = "stunned";
            return Fail(sim, caster, slot, reason);
        }
        foreach (var tag in weave.Tags)
        {
            if (stats.HasFlag("forbid:" + tag))
            {
                reason = "forbidden:" + tag;
                return Fail(sim, caster, slot, reason);
            }
        }
        if (book.Casting is not null)
        {
            reason = "busy";
            return false;
        }
        if (!book.IsReady(slot, now) || book.GlobalReadyAt > now)
        {
            reason = "cooldown";
            return false;
        }
        if (!CanAfford(weave, stats, out var missing))
        {
            reason = "cost:" + missing;
            return Fail(sim, caster, slot, reason);
        }
        if (!ValidateTarget(sim, caster, stats, weave, ref target, ref point, ref direction, out reason))
        {
            return Fail(sim, caster, slot, reason);
        }

        foreach (var (resource, cost) in Costs(weave, stats))
        {
            stats.AddResource(resource, -cost);
        }
        var duration = CastDuration(weave, stats);
        var cooldownAtPress = sim.Content.Rules.Text("combat.cooldown_at", "complete") == "press";
        if (cooldownAtPress)
        {
            StartCooldown(sim, book, slot, weave, stats);
        }
        if (sim.World.TryGet<Transform>(caster, out var t))
        {
            var face = target.IsNone ? point : (sim.World.Get<Transform>(target)?.Position ?? point);
            if (direction.LengthSquared() > 0.001f && target.IsNone && weave.Targeting == Targeting.Direction)
            {
                t.Yaw = MathF.Atan2(direction.Y, direction.X);
            }
            else
            {
                t.Face(face);
            }
        }
        var state = new CastState
        {
            Slot = slot,
            Target = target,
            Point = point,
            Direction = direction,
            StartedAt = now,
            Duration = duration,
            Channeling = weave.CastMode == CastMode.Channel,
            NextChannelAt = now,
        };
        if (weave.CastMode == CastMode.Instant || (duration <= 0 && weave.CastMode != CastMode.Channel))
        {
            Execute(sim, caster, state, weave, cooldownAtPress);
            return true;
        }
        book.Casting = state;
        sim.Events.Publish(new WeaveStarted(caster, weave.Key, target, point, duration) { Tick = sim.Clock.Tick });
        return true;
    }

    private static bool Fail(Simulation sim, Entity caster, WeaveSlot slot, string reason)
    {
        sim.Events.Publish(new WeaveFailed(caster, slot.Key, reason) { Tick = sim.Clock.Tick });
        return false;
    }

    private bool ValidateTarget(Simulation sim, Entity caster, Stats stats, CompiledWeave weave, ref Entity target, ref Vector3 point, ref Vector2 direction, out string reason)
    {
        reason = "";
        var ct = sim.World.Get<Transform>(caster);
        var range = weave.Range(stats);
        switch (weave.Targeting)
        {
            case Targeting.Self:
                target = caster;
                point = ct?.Position ?? point;
                return true;
            case Targeting.Enemy:
            case Targeting.Ally:
            case Targeting.Any:
            {
                if (target.IsNone || !sim.World.IsAlive(target) || sim.World.Has<Dead>(target))
                {
                    reason = "no_target";
                    return false;
                }
                if (weave.Targeting == Targeting.Enemy && !Relations.IsHostile(sim, caster, target))
                {
                    reason = "not_hostile";
                    return false;
                }
                if (weave.Targeting == Targeting.Ally && Relations.IsHostile(sim, caster, target))
                {
                    reason = "not_ally";
                    return false;
                }
                var tt = sim.World.Get<Transform>(target);
                if (ct is not null && tt is not null && range > 0 && ct.GroundDistanceTo(tt.Position) > range + tt.Radius + ct.Radius)
                {
                    reason = "out_of_range";
                    return false;
                }
                point = tt?.Position ?? point;
                return true;
            }
            case Targeting.Ground:
            {
                target = Entity.None;
                if (ct is not null && range > 0)
                {
                    var d = ct.GroundDistanceTo(point);
                    if (d > range)
                    {
                        // Clamp to range instead of refusing: feels better for click casting.
                        var dir = new Vector2(point.X - ct.Position.X, point.Y - ct.Position.Y) / d;
                        point = new Vector3(ct.Position.X + dir.X * (float)range, ct.Position.Y + dir.Y * (float)range, point.Z);
                        point = sim.Terrain.SnapToGround(point, 3);
                    }
                }
                return true;
            }
            case Targeting.Direction:
            {
                target = Entity.None;
                if (direction.LengthSquared() < 0.0001f && ct is not null)
                {
                    direction = new Vector2(point.X - ct.Position.X, point.Y - ct.Position.Y);
                    if (direction.LengthSquared() < 0.0001f)
                    {
                        direction = ct.Forward;
                    }
                }
                direction = Vector2.Normalize(direction);
                return true;
            }
        }
        return true;
    }

    private void StartCooldown(Simulation sim, WeaveBook book, WeaveSlot slot, CompiledWeave weave, Stats stats)
    {
        var cooldown = weave.Cooldown(stats);
        if (cooldown > 0)
        {
            var recovery = Math.Max(0.1, stats.Attributes.Get("cooldown.recovery", stats, weave.Tags, weave.Modifiers));
            book.ReadyAt[slot.Key] = sim.Clock.Time + cooldown / recovery;
        }
        var gcd = sim.Content.Rules.Number("combat.global_cooldown", 0);
        if (gcd > 0)
        {
            book.GlobalReadyAt = sim.Clock.Time + gcd;
        }
    }

    /// <summary>Resolves a cast now: runs the effects and emits the event.</summary>
    public void Execute(Simulation sim, Entity caster, CastState state, CompiledWeave weave, bool cooldownAlreadyStarted)
    {
        if (!sim.World.TryGet<Stats>(caster, out var stats))
        {
            return;
        }
        var book = sim.World.Get<WeaveBook>(caster);
        if (book is not null && !cooldownAlreadyStarted)
        {
            StartCooldown(sim, book, state.Slot, weave, stats);
        }
        var ct = sim.World.Get<Transform>(caster);
        var target = state.Target;
        if (!target.IsNone && (!sim.World.IsAlive(target) || sim.World.Has<Dead>(target)))
        {
            target = Entity.None;
        }
        var ctx = new EffectContext
        {
            Sim = sim,
            Source = caster,
            Target = target,
            Point = target.IsNone ? state.Point : (sim.World.Get<Transform>(target)?.Position ?? state.Point),
            Direction = state.Direction,
            Weave = weave,
            Tags = weave.Tags,
            Rng = sim.RngFor(caster, "weave:" + weave.Key + ":" + state.Pulses),
            Potency = 1,
        };
        stats.Stamp("cast", sim.Clock.Time);
        if (weave.Tags.Has("movement"))
        {
            stats.Stamp("moved", sim.Clock.Time);
        }
        CompiledEffect.ExecuteAll(weave.Effects, ctx);
        sim.Events.Publish(new WeaveCast(caster, weave.Key, target, ctx.Point, weave.Tags) { Tick = sim.Clock.Tick });
        _ = ct;
    }

    public void Interrupt(Simulation sim, Entity caster)
    {
        if (sim.World.TryGet<WeaveBook>(caster, out var book) && book.Casting is not null)
        {
            var key = book.Casting.Slot.Key;
            book.Casting = null;
            sim.Events.Publish(new WeaveInterrupted(caster, key) { Tick = sim.Clock.Tick });
        }
    }

    /// <summary>Picks the best ready weave for an AI toward a target, honoring hints and range.</summary>
    public (WeaveSlot Slot, CompiledWeave Weave)? ChooseForAi(Simulation sim, Entity caster, Entity target)
    {
        if (!sim.World.TryGet<WeaveBook>(caster, out var book) || !sim.World.TryGet<Stats>(caster, out var stats))
        {
            return null;
        }
        var ct = sim.World.Get<Transform>(caster);
        var tt = sim.World.Get<Transform>(target);
        var distance = ct is not null && tt is not null ? ct.GroundDistanceTo(tt.Position) : 0;
        var best = default((WeaveSlot, CompiledWeave)?);
        double bestScore = double.NegativeInfinity;
        var rng = sim.RngFor(caster, "ai-choose");
        foreach (var slot in book.Slots)
        {
            var weave = Resolve(sim, slot, out _);
            if (weave is null || !book.IsReady(slot, sim.Clock.Time) || !CanAfford(weave, stats, out _))
            {
                continue;
            }
            var hint = weave.Ai;
            var range = weave.Range(stats);
            if (weave.Targeting is Targeting.Enemy or Targeting.Ground or Targeting.Direction && range > 0 && distance > range + (tt?.Radius ?? 0) + (ct?.Radius ?? 0))
            {
                continue;
            }
            if (hint?.When is not null && !hint.When.IsTrue(weave.ScopeFor(stats)))
            {
                continue;
            }
            var score = (hint?.Weight ?? 1) * (0.75 + rng.NextDouble() * 0.5);
            if (score > bestScore)
            {
                bestScore = score;
                best = (slot, weave);
            }
        }
        return best;
    }

    /// <summary>The longest range among an entity's weaves, for AI positioning.</summary>
    public double PreferredRange(Simulation sim, Entity caster)
    {
        if (!sim.World.TryGet<WeaveBook>(caster, out var book) || !sim.World.TryGet<Stats>(caster, out var stats))
        {
            return 1.5;
        }
        double best = 0;
        foreach (var slot in book.Slots)
        {
            var weave = Resolve(sim, slot, out _);
            if (weave is null)
            {
                continue;
            }
            var r = weave.Ai?.PreferredRange ?? Math.Max(1.2, weave.Range(stats) * 0.8);
            best = Math.Max(best, r);
        }
        return best <= 0 ? 1.5 : best;
    }
}
