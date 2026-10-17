using System.Text.Json.Nodes;
using HollowWake.Core.Components;
using HollowWake.Core.Expressions;

namespace HollowWake.Core.Effects.Handlers;

/// <summary>Effects that steer what other bodies' behaviors do: taunts now, commands for minions later.</summary>
public static class AiHandlers
{
    public static void Register(EffectRegistry r)
    {
        r.Register(new TauntHandler());
    }
}

internal sealed class TauntHandler : IEffectHandler
{
    public string Type => "taunt";

    public EffectSignature Signature { get; } = EffectSignature.Of("duration");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect { Type = Type, Duration = ctx.ExprOf(p["duration"], Expr.Constant(4))! };

    private sealed class Effect : CompiledEffect
    {
        public required Expr Duration;

        public override void Execute(EffectContext ctx)
        {
            if (ctx.Sim.World.TryGet<Behavior>(ctx.Target, out var b))
            {
                b.Target = ctx.Source;
                b.Memory["taunted_until"] = ctx.Sim.Clock.Time + ctx.Eval(Duration, 4);
            }
        }
    }
}
