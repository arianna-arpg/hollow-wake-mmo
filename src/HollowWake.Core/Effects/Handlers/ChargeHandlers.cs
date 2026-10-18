using System.Text.Json.Nodes;
using HollowWake.Core.Combat;
using HollowWake.Core.Expressions;

namespace HollowWake.Core.Effects.Handlers;

/// <summary>
/// Effects over named charges: banking, draining and spending them (<c>charge</c>). The charge service and
/// the <c>Charges</c> component hold the counts; handlers registered here change them.
/// </summary>
public static class ChargeHandlers
{
    public static void Register(EffectRegistry r)
    {
        r.Register(new ChargeHandler());
    }
}

/// <summary>
/// <c>charge</c>: on the source (or the target with <c>self: false</c>), <c>gain</c> charges of kind <c>id</c> up to
/// <c>max</c> (its <c>charge.{id}.max</c> when left out), <c>spend</c> an amount (only if all of it is held) or
/// <c>all</c> of them, or <c>drain</c> them at a rate per second, refusing gains until they are gone. Spending
/// sets <c>charges_spent</c> for the effects after it.
/// </summary>
internal sealed class ChargeHandler : IEffectHandler
{
    public string Type => "charge";

    public EffectSignature Signature { get; } = EffectSignature.Of("id", "gain", "max", "spend", "all", "drain", "self").Refers("charges", "id");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Id = ctx.StringOf(p["id"]) ?? throw new InvalidOperationException($"charge needs 'id' in {ctx.Owner}."),
        Gain = ctx.ExprOf(p["gain"]),
        Max = ctx.ExprOf(p["max"]),
        Spend = ctx.ExprOf(p["spend"]),
        All = ctx.BoolOf(p["all"], false),
        Drain = ctx.ExprOf(p["drain"]),
        OnSelf = ctx.BoolOf(p["self"], true),
    };

    private sealed class Effect : CompiledEffect
    {
        public required string Id;
        public Expr? Gain;
        public Expr? Max;
        public Expr? Spend;
        public bool All;
        public Expr? Drain;
        public bool OnSelf = true;

        public override void Execute(EffectContext ctx)
        {
            var holder = OnSelf || ctx.Target.IsNone ? ctx.Source : ctx.Target;
            var sim = ctx.Sim;
            var charges = sim.Service<ChargeService>();
            if (All)
            {
                ctx.Vars[CastVars.ChargesSpent] = charges.SpendAll(sim, holder, Id, ctx.Depth);
            }
            else if (Spend is not null)
            {
                var amount = Math.Max(0, ctx.Eval(Spend));
                ctx.Vars[CastVars.ChargesSpent] = charges.Spend(sim, holder, Id, amount, ctx.Depth) ? amount : 0;
            }
            if (Gain is not null)
            {
                var max = Max is null ? double.PositiveInfinity : ctx.Eval(Max);
                charges.Gain(sim, holder, Id, ctx.Eval(Gain), max, ctx.Depth);
            }
            if (Drain is not null)
            {
                charges.Drain(sim, holder, Id, ctx.Eval(Drain));
            }
        }
    }
}
