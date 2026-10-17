using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Minions;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Effects.Handlers;

/// <summary>Summons, constructs and plain spawns, and the corpses they leave and raise.</summary>
public static class SummonHandlers
{
    public static void Register(EffectRegistry r)
    {
        r.Register(new SummonHandler());
        r.Register(new DeployHandler());
        r.Register(new SpawnHandler());
        r.Register(new GatherCorpsesHandler());
    }

    /// <summary>The hook a summon's and a deploy's birth effects answer to: they run on each body as it is born.</summary>
    public const string OnBirth = "on_birth";

    /// <summary>The hook a summon's and a deploy's death effects answer to: they run where each body falls when it dies.</summary>
    public const string OnDeath = "on_death";
}

/// <summary>
/// <c>summon</c>: raises minions for the caster. A <c>pattern</c>, a weighted <c>pool</c> re-rolled per body, or
/// the corpse's own kind (<c>from_corpse</c>, nearest the focus within <c>search_radius</c>, or the target);
/// <c>count</c> bodies for <c>duration</c> seconds; at most <c>max_active</c> in the group (the oldest are
/// dismissed), or in the <c>pool_group</c>, whose capacity is the owner's <c>minion.max</c>; an
/// <c>exclusive_group</c> dismisses the other groups in it. Bodies appear where <c>place</c> says, run the
/// summon's <c>behavior</c> (else rule <c>minions.behavior</c>), inherit their owner's minion attributes
/// unless <c>inherit</c> is false, run <c>on_birth</c> as they are born and <c>on_death</c> where they fall.
/// A <c>contract</c> holds a reservation and brings fallen bodies back; <c>decay</c>, <c>waves</c>,
/// <c>presence</c> and <c>replenish</c> shape the rest.
/// </summary>
internal sealed class SummonHandler : IEffectHandler
{
    public string Type => "summon";

    public EffectSignature Signature { get; } = EffectSignature.Of(SummonRecipe.SharedParams.Concat(new[]
        {
            "duration", "contract", "decay", "waves", "presence", "replenish", "from_corpse", "search_radius",
        }).ToArray())
        .Hook(SummonHandlers.OnBirth, "on_birth")
        .Hook(SummonHandlers.OnDeath, "on_death");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new SummonEffect { Type = Type, Recipe = SummonRecipe.Read(p, ctx, construct: false) };
}

/// <summary>
/// <c>deploy</c>: places constructs for the caster, sharing summon capacity: totems, traps, walls, domes.
/// Like <c>summon</c> without contracts or corpses; constructs keep their pattern's behavior, last
/// <c>lifetime</c> seconds, stand where <c>place</c> lays them (a <c>line</c> across the facing or a
/// <c>circle</c> makes walls and rings), cast with their owner's numbers and credit when
/// <c>inherit_caster</c> is set, intercept projectiles with an <c>intercept</c> dome, and burst with
/// <c>on_death</c> when broken.
/// </summary>
internal sealed class DeployHandler : IEffectHandler
{
    public string Type => "deploy";

    public EffectSignature Signature { get; } = EffectSignature.Of(SummonRecipe.SharedParams.Concat(new[]
        {
            "lifetime", "inherit_caster", "intercept", "decay", "presence",
        }).ToArray())
        .Hook(SummonHandlers.OnBirth, "on_birth")
        .Hook(SummonHandlers.OnDeath, "on_death");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new SummonEffect { Type = Type, Recipe = SummonRecipe.Read(p, ctx, construct: true) };
}

/// <summary>A compiled summon or deploy; the minion system reads its recipe for respawns and replenishment.</summary>
internal sealed class SummonEffect : CompiledEffect
{
    public required SummonRecipe Recipe;

    public override void Execute(EffectContext ctx) => Recipe.Run(ctx);
}

/// <summary>
/// <c>spawn</c>: places a pattern at the focus with no ownership terms: <c>count</c> of them, gone after
/// <c>lifetime</c> seconds when one is given; <c>as_corpse</c> lays them down as fresh corpses to raise or consume.
/// </summary>
internal sealed class SpawnHandler : IEffectHandler
{
    public string Type => "spawn";

    public EffectSignature Signature { get; } = EffectSignature.Of("pattern", "count", "lifetime", "as_corpse");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx)
    {
        var pattern = ctx.StringOf(p["pattern"]) ?? throw new InvalidOperationException("spawn needs 'pattern'");
        if (!ctx.Content.Has<Definitions.PatternDef>(pattern))
        {
            throw new InvalidOperationException($"spawn names unknown pattern '{pattern}' in {ctx.Owner}.");
        }
        return new Effect
        {
            Type = Type,
            Pattern = pattern,
            Count = ctx.ExprOf(p["count"], Expr.Constant(1))!,
            Lifetime = ctx.ExprOf(p["lifetime"]),
            AsCorpse = ctx.BoolOf(p["as_corpse"], false),
        };
    }

    private sealed class Effect : CompiledEffect
    {
        public required string Pattern;
        public required Expr Count;
        public Expr? Lifetime;
        public bool AsCorpse;

        public override void Execute(EffectContext ctx)
        {
            var factory = ctx.Sim.Service<PatternFactory>();
            var count = Math.Clamp((int)Math.Round(ctx.Eval(Count, 1)), 1, 32);
            for (var i = 0; i < count; i++)
            {
                var at = ctx.Focus;
                if (AsCorpse)
                {
                    // A pile of fresh dead, a little apart, so each can be raised on its own.
                    var ang = ctx.Rng.NextDouble() * Math.PI * 2;
                    at += new Vector3((float)Math.Cos(ang) * 0.8f, (float)Math.Sin(ang) * 0.8f, 0);
                }
                var e = factory.Spawn(ctx.Sim, Pattern, at, new SpawnOptions { Owner = ctx.Origin });
                if (e.IsNone)
                {
                    continue;
                }
                if (AsCorpse)
                {
                    Corpses.LayDown(ctx.Sim, e);
                }
                else if (Lifetime is not null)
                {
                    ctx.Sim.World.Add(e, new Lifetime { ExpiresAt = ctx.Sim.Clock.Time + ctx.Eval(Lifetime, 10) });
                }
            }
        }
    }
}

/// <summary><c>gather_corpses {radius}</c>: drags the corpses within <c>radius</c> of the focus into a pile there.</summary>
internal sealed class GatherCorpsesHandler : IEffectHandler
{
    public string Type => "gather_corpses";

    public EffectSignature Signature { get; } = EffectSignature.Of("radius");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect { Type = Type, Radius = ctx.ExprOf(p["radius"], Expr.Constant(8))! };

    private sealed class Effect : CompiledEffect
    {
        public required Expr Radius;

        public override void Execute(EffectContext ctx)
        {
            var sim = ctx.Sim;
            var service = MinionService.Of(sim);
            var pile = ctx.Focus;
            var found = service.RentList();
            foreach (var (e, _, _) in sim.Spatial.Within(pile, (float)Math.Max(0.5, ctx.Eval(Radius, 8))))
            {
                if (Corpses.Is(sim.World, e))
                {
                    found.Add(e);
                }
            }
            // Moving bodies between grid cells inside the query would break it, so they move afterwards.
            for (var i = 0; i < found.Count; i++)
            {
                var ang = i * 2.39996;
                var r = 0.35 * Math.Sqrt(i);
                service.Teleport(found[i], pile, pile + new Vector3((float)(Math.Cos(ang) * r), (float)(Math.Sin(ang) * r), 0));
            }
            service.ReturnList(found);
        }
    }
}
