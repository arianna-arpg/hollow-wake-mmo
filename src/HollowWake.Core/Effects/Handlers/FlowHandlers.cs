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

/// <summary>Control flow: condition, random, repeat, sequence.</summary>
public static class FlowHandlers
{
    public static void Register(EffectRegistry r)
    {
        r.Register(new ConditionHandler());
        r.Register(new RandomHandler());
        r.Register(new RepeatHandler());
        r.Register(new SequenceHandler());
    }
}

internal sealed class ConditionHandler : IEffectHandler
{
    public string Type => "condition";

    public EffectSignature Signature { get; } = EffectSignature.Of("if", "then", "else");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        If = ctx.ExprOf(p["if"], Expr.Constant(1))!,
        Then = ctx.CompileList(p["then"], "then"),
        Else = ctx.CompileList(p["else"], "else"),
    };

    private sealed class Effect : CompiledEffect
    {
        public required Expr If;
        public CompiledEffect[] Then = Array.Empty<CompiledEffect>();
        public CompiledEffect[] Else = Array.Empty<CompiledEffect>();

        public override void Execute(EffectContext ctx) => ExecuteAll(If.IsTrue(ctx.Scope) ? Then : Else, ctx);
    }
}

internal sealed class RandomHandler : IEffectHandler
{
    public string Type => "random";

    public EffectSignature Signature { get; } = EffectSignature.Of("choices");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx)
    {
        var choices = new List<(double, CompiledEffect[])>();
        if (p["choices"] is JsonArray arr)
        {
            foreach (var c in arr.OfType<JsonObject>())
            {
                choices.Add((ctx.NumberOf(c["weight"], 1), ctx.CompileList(c["effects"], "effects")));
            }
        }
        return new Effect { Type = Type, Choices = choices, Weights = choices.Select(c => c.Item1).ToArray() };
    }

    private sealed class Effect : CompiledEffect
    {
        public List<(double Weight, CompiledEffect[] Effects)> Choices = new();
        // The weights in choice order, built once rather than per pick.
        public double[] Weights = Array.Empty<double>();

        public override void Execute(EffectContext ctx)
        {
            if (Choices.Count == 0)
            {
                return;
            }
            var idx = ctx.Rng.PickWeighted(Weights);
            if (idx >= 0)
            {
                ExecuteAll(Choices[idx].Effects, ctx);
            }
        }
    }
}

internal sealed class RepeatHandler : IEffectHandler
{
    public string Type => "repeat";

    public EffectSignature Signature { get; } = EffectSignature.Of("times", "interval").Wrapping("effects");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Times = ctx.ExprOf(p["times"], Expr.Constant(3))!,
        Interval = ctx.ExprOf(p["interval"], Expr.Constant(0.3))!,
        Effects = ctx.CompileList(p["effects"], "effects"),
    };

    private sealed class Effect : CompiledEffect
    {
        public required Expr Times;
        public required Expr Interval;
        public CompiledEffect[] Effects = Array.Empty<CompiledEffect>();

        public override void Execute(EffectContext ctx)
        {
            var times = Math.Clamp((int)Math.Round(ctx.Eval(Times, 3)), 1, 50);
            var interval = ctx.Eval(Interval, 0.3);
            var deeper = ctx.Deeper();
            ExecuteAll(Effects, deeper);
            for (var i = 1; i < times; i++)
            {
                var n = i;
                ctx.Sim.Scheduler.After(interval * n, () =>
                {
                    if (ctx.Sim.World.IsAlive(ctx.Source))
                    {
                        ExecuteAll(Effects, deeper);
                    }
                });
            }
        }
    }
}

internal sealed class SequenceHandler : IEffectHandler
{
    public string Type => "sequence";

    public EffectSignature Signature { get; } = EffectSignature.Of().Wrapping("effects");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect { Type = Type, Effects = ctx.CompileList(p["effects"], "effects") };

    private sealed class Effect : CompiledEffect
    {
        public CompiledEffect[] Effects = Array.Empty<CompiledEffect>();
        public override void Execute(EffectContext ctx) => ExecuteAll(Effects, ctx);
    }
}

