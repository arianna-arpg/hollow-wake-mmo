using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Ai;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Maths;
using HollowWake.Core.Minions;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Effects.Handlers;

/// <summary>
/// Effects over a caster's minions: recalling, commanding, making them cast and consuming them
/// (<c>recall_minions</c>, <c>command_minions</c>, <c>minion_cast</c>, <c>consume_minion</c>). Each addresses the
/// caster's minions of one <c>group</c> when given; otherwise, run by a weave that summons or deploys, only
/// that weave's own minions (its host's); otherwise all of them.
/// </summary>
public static class MinionHandlers
{
    public static void Register(EffectRegistry r)
    {
        r.Register(new RecallMinionsHandler());
        r.Register(new CommandMinionsHandler());
        r.Register(new MinionCastHandler());
        r.Register(new ConsumeMinionHandler());
    }

    /// <summary>
    /// The key of the weave whose minions an effect addresses when it names no group: its host (the slot's
    /// weave a meta, trigger or <c>cast</c> runs for), else the running weave, when that weave raises minions
    /// or constructs; otherwise none, and the effect addresses all of them.
    /// </summary>
    internal static string? HostWeave(EffectContext ctx, string? group) =>
        group is null && SummonRecipe.HostOf(ctx) is { } w && (w.Mechanisms.Has("summon") || w.Mechanisms.Has("construct")) ? w.Key : null;

    /// <summary>The body the minions answer to: the context's origin.</summary>
    internal static Vector3 OwnerPosition(EffectContext ctx) => ctx.Sim.World.Get<Transform>(ctx.Origin)?.Position ?? ctx.SourceTransform?.Position ?? ctx.Point;
}

/// <summary><c>recall_minions {radius, group}</c>: every mobile minion blinks to an even ring of <c>radius</c> around its owner; constructs stay.</summary>
internal sealed class RecallMinionsHandler : IEffectHandler
{
    public string Type => "recall_minions";

    public EffectSignature Signature { get; } = EffectSignature.Of("radius", "group");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect { Type = Type, Radius = ctx.ExprOf(p["radius"]), Group = ctx.StringOf(p["group"]) };

    private sealed class Effect : CompiledEffect
    {
        public Expr? Radius;
        public string? Group;

        public override void Execute(EffectContext ctx)
        {
            var service = MinionService.Of(ctx.Sim);
            var list = service.RentList();
            service.Living(ctx.Origin, MinionScope.Commandable(Group, MinionHandlers.HostWeave(ctx, Group)), list);
            var center = MinionHandlers.OwnerPosition(ctx);
            var radius = Radius is null ? ctx.Sim.Content.Rules.Number("minions.recall_radius", 1.5) : ctx.Eval(Radius, 1.5);
            for (var i = 0; i < list.Count; i++)
            {
                var ang = i * Math.PI * 2 / list.Count;
                service.Teleport(list[i], center, center + new Vector3((float)(Math.Cos(ang) * radius), (float)(Math.Sin(ang) * radius), 0));
            }
            service.ReturnList(list);
        }
    }
}

/// <summary>
/// <c>command_minions {command, duration, affects, radius, discipline, group}</c>: puts recipients under an
/// order of a command from <c>minions.commands</c> (default <c>assault</c>) at the context's point, pinning the
/// context's target when it is a foe. <c>affects</c> is <c>minions</c> (the caster's), <c>squad</c> (bodies of
/// the caster's faction within <c>radius</c>, by default <c>minions.earshot</c>, that answer to no owner) or
/// <c>both</c>; each recipient heeds by its <c>obedience</c> plus <c>discipline</c> and the caster's
/// <c>command.discipline</c>.
/// </summary>
internal sealed class CommandMinionsHandler : IEffectHandler
{
    public string Type => "command_minions";

    public EffectSignature Signature { get; } = EffectSignature.Of("command", "duration", "affects", "radius", "discipline", "group");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx)
    {
        var command = ctx.StringOf(p["command"]) ?? "assault";
        var known = CommandDef.Read(ctx.Content);
        if (known.Count > 0 && !known.ContainsKey(command))
        {
            throw new InvalidOperationException($"command_minions names unknown command '{command}' in {ctx.Owner}; commands are listed in rule minions.commands.");
        }
        return new Effect
        {
            Type = Type,
            Command = command,
            Duration = ctx.ExprOf(p["duration"]),
            Affects = ctx.ChoiceOf(p["affects"], "affects", "minions", "minions", "squad", "both"),
            Radius = ctx.ExprOf(p["radius"]),
            Discipline = ctx.ExprOf(p["discipline"]),
            Group = ctx.StringOf(p["group"]),
        };
    }

    private sealed class Effect : CompiledEffect
    {
        public required string Command;
        public Expr? Duration;
        public string Affects = "minions";
        public Expr? Radius;
        public Expr? Discipline;
        public string? Group;

        public override void Execute(EffectContext ctx)
        {
            var sim = ctx.Sim;
            var service = MinionService.Of(sim);
            if (!service.TryCommand(Command, out var command))
            {
                sim.Log.Warn($"command_minions: unknown command '{Command}'");
                return;
            }
            var issuer = ctx.Origin;
            var at = MinionHandlers.OwnerPosition(ctx);
            var radius = Radius is null ? -1 : ctx.Eval(Radius, -1);
            var list = service.RentList();
            if (Affects != "squad")
            {
                service.Living(issuer, MinionScope.Commandable(Group, MinionHandlers.HostWeave(ctx, Group)), list);
                if (radius >= 0)
                {
                    list.RemoveAll(e => sim.World.Get<Transform>(e) is not { } t || t.GroundDistanceTo(at) > radius);
                }
            }
            if (Affects != "minions")
            {
                Squad(sim, issuer, at, radius >= 0 ? radius : sim.Content.Rules.Number("minions.earshot", 21), list);
            }
            var duration = Duration is null ? command.Duration : ctx.Eval(Duration, command.Duration);
            var discipline = (Discipline is null ? 0 : ctx.Eval(Discipline, 0)) + (sim.World.Get<Stats>(issuer)?.Get("command.discipline") ?? 0);
            MinionCommands.Issue(sim, issuer, command, ctx.Point, ctx.Target, duration, discipline, list, ctx.Rng);
            service.ReturnList(list);
        }

        /// <summary>Bodies of the issuer's faction in earshot that think for themselves and answer to no owner.</summary>
        private static void Squad(Simulation sim, Entity issuer, Vector3 at, double radius, List<Entity> into)
        {
            var faction = Relations.FactionOf(sim, issuer);
            if (faction is null)
            {
                return;
            }
            foreach (var (e, _, _) in sim.Spatial.Within(at, (float)radius, 6f))
            {
                if (e == issuer || !sim.World.Has<Behavior>(e) || sim.World.Has<Minion>(e) || sim.World.Has<Dead>(e) || into.Contains(e))
                {
                    continue;
                }
                if (string.Equals(Relations.FactionOf(sim, e), faction, StringComparison.Ordinal))
                {
                    into.Add(e);
                }
            }
        }
    }
}

/// <summary>
/// <c>minion_cast {weave, at, range, group}</c>: every addressed minion executes a preset weave, free and at
/// once, at the context's point (<c>at: point</c>, the default) or at its own nearest foe within <c>range</c>
/// (<c>at: enemy</c>; by default <c>minions.cast_range</c>), with its own numbers and at its host's level. An
/// order is executed, never cast: it raises no cast of the minion's and does not wait on its clocks.
/// </summary>
internal sealed class MinionCastHandler : IEffectHandler
{
    public string Type => "minion_cast";

    public EffectSignature Signature { get; } = EffectSignature.Of("weave", "at", "range", "group");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx)
    {
        var weave = ctx.StringOf(p["weave"]) ?? throw new InvalidOperationException($"minion_cast needs 'weave' in {ctx.Owner}.");
        if (!ctx.Content.Has<WeaveDef>(weave))
        {
            throw new InvalidOperationException($"minion_cast names unknown preset weave '{weave}' in {ctx.Owner}.");
        }
        var effect = new Effect
        {
            Type = Type,
            Weave = weave,
            At = ctx.ChoiceOf(p["at"], "at", "point", "point", "enemy"),
            Range = ctx.ExprOf(p["range"]),
            Group = ctx.StringOf(p["group"]),
            Compiler = ctx.Compiler,
        };
        if (ctx.Compiler is { } compiler && !compiler.IsBuilding(weave))
        {
            if (!compiler.TryCompile(new WeaveSpec { Preset = weave }, out var compiled, out var error, GlyphFit.Dormant))
            {
                throw new InvalidOperationException($"minion_cast weave '{weave}' does not compile: {error}");
            }
            effect.Remember(1, compiled!);
        }
        return effect;
    }

    private sealed class Effect : CompiledEffect
    {
        public required string Weave;
        public string At = "point";
        public Expr? Range;
        public string? Group;
        public WeaveCompiler? Compiler;

        private readonly Dictionary<int, CompiledWeave> _byLevel = new();

        public void Remember(int level, CompiledWeave weave) => _byLevel[level] = weave;

        public override void Execute(EffectContext ctx)
        {
            var sim = ctx.Sim;
            var weave = Resolve(ctx, SummonRecipe.HostOf(ctx)?.Level ?? 1);
            if (weave is null)
            {
                return;
            }
            var service = MinionService.Of(sim);
            var list = service.RentList();
            service.Living(ctx.Origin, MinionScope.Commandable(Group, MinionHandlers.HostWeave(ctx, Group)), list);
            var range = (float)(Range is null ? sim.Content.Rules.Number("minions.cast_range", 11) : ctx.Eval(Range, 11));
            foreach (var minion in list)
            {
                if (!sim.World.TryGet<Transform>(minion, out var mt) || !sim.World.TryGet<Stats>(minion, out var stats) || stats.HasFlag("stunned") || stats.HasFlag("frozen"))
                {
                    continue;
                }
                var target = Entity.None;
                Vector3 point;
                if (At == "enemy")
                {
                    target = MinionCommands.NearestFoe(sim, minion, mt.Position, range);
                    if (target.IsNone)
                    {
                        continue;
                    }
                    point = sim.World.Get<Transform>(target)!.Position;
                }
                else
                {
                    point = ctx.Point;
                    if (MinionCommands.IsFoe(sim, minion, ctx.Target))
                    {
                        target = ctx.Target;
                    }
                }
                mt.Face(point);
                var direction = new Vector2(point.X - mt.Position.X, point.Y - mt.Position.Y);
                var order = new EffectContext
                {
                    CastId = ctx.CastId,
                    Sim = sim,
                    Source = minion,
                    Target = target,
                    Point = point,
                    Direction = direction.LengthSquared() > 0.0001f ? Vector2.Normalize(direction) : mt.Forward,
                    Weave = weave,
                    Tags = weave.Tags,
                    Depth = ctx.Depth + 1,
                    Rng = new Rng(Rng.Combine(ctx.Rng.NextUInt64(), (ulong)minion.Key)),
                };
                ExecuteAll(weave.Effects, order);
                sim.Events.Publish(new MinionOrdered(minion, ctx.Origin, weave.Key, point) { Tick = sim.Clock.Tick });
            }
            service.ReturnList(list);
        }

        private CompiledWeave? Resolve(EffectContext ctx, int level)
        {
            if (_byLevel.TryGetValue(level, out var cached))
            {
                return cached;
            }
            var compiler = Compiler ?? (ctx.Sim.TryService<WeaveCompiler>(out var service) ? service : null);
            if (compiler is null || !compiler.TryCompile(new WeaveSpec { Preset = Weave, Level = level }, out var weave, out var error, GlyphFit.Dormant))
            {
                ctx.Sim.Log.Warn($"minion_cast of '{Weave}' cannot compile");
                return null;
            }
            _byLevel[level] = weave!;
            return weave;
        }
    }
}

/// <summary>
/// <c>consume_minion {count, pick, group, radius, self, of, effects}</c>: spends minions for power. Picks
/// <c>count</c> of the caster's minions (<c>of</c>: <c>minions</c>, <c>constructs</c> or <c>any</c>) within
/// <c>radius</c> of the focus when given, the <c>oldest</c>, <c>newest</c> or <c>nearest</c> first; with
/// <c>pick: target</c> the context's target when it is one of the caster's minions (what a form's
/// <c>minion</c> target kind chose); or with <c>self</c> the source itself. Each one's <c>effects</c> run
/// where it stands, with <c>consumed_life</c> and <c>consumed_life_max</c> set, and then it dies, crediting no one.
/// </summary>
internal sealed class ConsumeMinionHandler : IEffectHandler
{
    public string Type => "consume_minion";

    public EffectSignature Signature { get; } = EffectSignature.Of("count", "pick", "group", "radius", "self", "of", "effects");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Count = ctx.ExprOf(p["count"], Expr.Constant(1))!,
        Pick = ctx.ChoiceOf(p["pick"], "pick", "oldest", "oldest", "newest", "nearest", "target"),
        Group = ctx.StringOf(p["group"]),
        Radius = ctx.ExprOf(p["radius"]),
        Self = ctx.BoolOf(p["self"], false),
        Of = ctx.ChoiceOf(p["of"], "of", "minions", "minions", "constructs", "any"),
        Effects = ctx.CompileList(p["effects"], "effects"),
    };

    private sealed class Effect : CompiledEffect
    {
        public required Expr Count;
        public string Pick = "oldest";
        public string? Group;
        public Expr? Radius;
        public bool Self;
        public string Of = "minions";
        public CompiledEffect[] Effects = Array.Empty<CompiledEffect>();

        public override void Execute(EffectContext ctx)
        {
            var sim = ctx.Sim;
            var service = MinionService.Of(sim);
            var list = service.RentList();
            if (Self)
            {
                if (sim.World.Has<Minion>(ctx.Source) && !sim.World.Has<Dead>(ctx.Source))
                {
                    list.Add(ctx.Source);
                }
            }
            else if (Pick == "target")
            {
                if (MinionTargets.Owns(sim, ctx.Origin, ctx.Target))
                {
                    list.Add(ctx.Target);
                }
            }
            else
            {
                var host = MinionHandlers.HostWeave(ctx, Group);
                var scope = Of switch
                {
                    "constructs" => MinionScope.Constructs(Group, host),
                    "any" => MinionScope.Any(Group, host),
                    _ => MinionScope.Commandable(Group, host),
                };
                service.Living(ctx.Origin, scope, list);
                Choose(ctx, list);
            }
            foreach (var victim in list)
            {
                Consume(ctx, service, victim);
            }
            service.ReturnList(list);
        }

        /// <summary>Keeps the minions within reach, ordered as picked, and the first <c>count</c> of them.</summary>
        private void Choose(EffectContext ctx, List<Entity> list)
        {
            var focus = ctx.Focus;
            var world = ctx.Sim.World;
            if (Radius is not null)
            {
                var radius = ctx.Eval(Radius, 0);
                list.RemoveAll(e => world.Get<Transform>(e) is not { } t || t.GroundDistanceTo(focus) > radius);
            }
            if (Pick == "newest")
            {
                list.Reverse();
            }
            else if (Pick == "nearest")
            {
                // Stable on ties: the list arrives oldest first.
                var ordered = list.Select((e, i) => (e, i, d: world.Get<Transform>(e)!.GroundDistanceTo(focus))).OrderBy(x => x.d).ThenBy(x => x.i).Select(x => x.e).ToList();
                list.Clear();
                list.AddRange(ordered);
            }
            var count = Math.Max(0, (int)Math.Round(ctx.Eval(Count, 1)));
            if (list.Count > count)
            {
                list.RemoveRange(count, list.Count - count);
            }
        }

        private void Consume(EffectContext ctx, MinionService service, Entity victim)
        {
            var sim = ctx.Sim;
            if (!sim.World.TryGet<Transform>(victim, out var vt) || !sim.World.TryGet<Stats>(victim, out var stats))
            {
                return;
            }
            var owner = sim.World.Get<Minion>(victim)?.Owner ?? ctx.Origin;
            var life = stats.Resource("life");
            var lifeMax = stats.ResourceMax("life");
            var at = vt.Position;
            if (Effects.Length > 0)
            {
                var vars = new Dictionary<string, double>(ctx.Vars, StringComparer.Ordinal)
                {
                    ["consumed_life"] = life,
                    ["consumed_life_max"] = lifeMax,
                };
                ExecuteAll(Effects, new EffectContext
                {
                    CastId = ctx.CastId,
                    Sim = sim,
                    Origin = ctx.Origin,
                    Source = ctx.Source,
                    Point = at,
                    Direction = ctx.Direction,
                    Weave = ctx.Weave,
                    Tags = ctx.Tags,
                    Depth = ctx.Depth + 1,
                    Rng = ctx.Rng,
                    Potency = ctx.Potency,
                    DamageScale = ctx.DamageScale,
                    From = at,
                    Vars = vars,
                });
            }
            sim.Events.Publish(new MinionConsumed(victim, owner, life) { Tick = sim.Clock.Tick });
            sim.Service<DeathService>().Kill(sim, victim, Entity.None, ctx.Depth + 1);
            _ = service;
        }
    }
}
