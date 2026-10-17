using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;
using HollowWake.Core.Tags;
using static HollowWake.Core.Effects.Handlers.EffectHelpers;

namespace HollowWake.Core.Effects.Handlers;

/// <summary>Summons and spawns.</summary>
public static class SummonHandlers
{
    public static void Register(EffectRegistry r)
    {
        r.Register(new SummonHandler());
        r.Register(new SpawnHandler());
    }
}

internal sealed class SummonHandler : IEffectHandler
{
    public string Type => "summon";

    public EffectSignature Signature { get; } = EffectSignature.Of("pattern", "count", "duration", "max_active", "group");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Pattern = ctx.StringOf(p["pattern"]) ?? throw new InvalidOperationException("summon needs 'pattern'"),
        Count = ctx.ExprOf(p["count"], Expr.Parse("weave.summon.count"))!,
        Duration = ctx.ExprOf(p["duration"], Expr.Parse("weave.duration"))!,
        MaxActive = ctx.ExprOf(p["max_active"], Expr.Parse("weave.summon.max"))!,
        Group = ctx.StringOf(p["group"]),
    };

    private sealed class Effect : CompiledEffect
    {
        public required string Pattern;
        public required Expr Count;
        public required Expr Duration;
        public required Expr MaxActive;
        public string? Group;

        public override void Execute(EffectContext ctx)
        {
            var factory = ctx.Sim.Service<PatternFactory>();
            var count = Math.Clamp((int)Math.Round(ctx.Eval(Count, 1)), 1, 32);
            var duration = ctx.Eval(Duration, 20);
            var max = (int)Math.Round(ctx.Eval(MaxActive, 8));
            var group = Group ?? Pattern;
            var owned = ctx.Sim.World.Query<Minion>().Where(m => m.C1.Owner == ctx.Origin && m.C1.Group == group).Select(m => m.Entity).ToList();
            while (owned.Count + count > max && owned.Count > 0)
            {
                ctx.Sim.World.DestroyDeferred(owned[0]);
                owned.RemoveAt(0);
            }
            var center = ctx.Focus;
            for (var i = 0; i < count; i++)
            {
                var ang = ctx.Rng.NextDouble() * Math.PI * 2;
                var pos = center + new Vector3((float)Math.Cos(ang) * 1.2f, (float)Math.Sin(ang) * 1.2f, 0);
                var e = factory.Spawn(ctx.Sim, Pattern, pos, new SpawnOptions { Owner = ctx.Origin, Level = ctx.SourceStats?.Level });
                if (e.IsNone)
                {
                    continue;
                }
                ctx.Sim.World.Add(e, new Minion { Owner = ctx.Origin, ExpiresAt = duration > 0 ? ctx.Sim.Clock.Time + duration : double.PositiveInfinity, Group = group });
            }
        }
    }
}

internal sealed class SpawnHandler : IEffectHandler
{
    public string Type => "spawn";

    public EffectSignature Signature { get; } = EffectSignature.Of("pattern", "count", "lifetime");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Pattern = ctx.StringOf(p["pattern"]) ?? throw new InvalidOperationException("spawn needs 'pattern'"),
        Count = ctx.ExprOf(p["count"], Expr.Constant(1))!,
        Lifetime = ctx.ExprOf(p["lifetime"]),
    };

    private sealed class Effect : CompiledEffect
    {
        public required string Pattern;
        public required Expr Count;
        public Expr? Lifetime;

        public override void Execute(EffectContext ctx)
        {
            var factory = ctx.Sim.Service<PatternFactory>();
            var count = Math.Clamp((int)Math.Round(ctx.Eval(Count, 1)), 1, 32);
            for (var i = 0; i < count; i++)
            {
                var e = factory.Spawn(ctx.Sim, Pattern, ctx.Focus, new SpawnOptions { Owner = ctx.Origin });
                if (!e.IsNone && Lifetime is not null)
                {
                    ctx.Sim.World.Add(e, new Lifetime { ExpiresAt = ctx.Sim.Clock.Time + ctx.Eval(Lifetime, 10) });
                }
            }
        }
    }
}



