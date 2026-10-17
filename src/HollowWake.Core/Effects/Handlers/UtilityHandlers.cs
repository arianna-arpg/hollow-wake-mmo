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

/// <summary>Utilities: events, interrupts, grants, logging.</summary>
public static class UtilityHandlers
{
    public static void Register(EffectRegistry r)
    {
        r.Register(new PublishEventHandler());
        r.Register(new InterruptHandler());
        r.Register(new GrantItemHandler());
        r.Register(new GrantXpHandler());
        r.Register(new LogHandler());
    }
}

internal sealed class PublishEventHandler : IEffectHandler
{
    public string Type => "event";

    public EffectSignature Signature { get; } = EffectSignature.Of("name", "data");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Name = ctx.StringOf(p["name"]) ?? "custom",
        Data = p["data"] is JsonObject d ? d.ToDictionary(kv => kv.Key, kv => ctx.ExprOf(kv.Value, Expr.Constant(0))!) : new Dictionary<string, Expr>(),
    };

    private sealed class Effect : CompiledEffect
    {
        public string Name = "custom";
        public Dictionary<string, Expr> Data = new();

        public override void Execute(EffectContext ctx)
        {
            var data = Data.ToDictionary(kv => kv.Key, kv => ctx.Eval(kv.Value));
            ctx.Sim.Events.Publish(new CustomEvent(Name, ctx.Source, ctx.Target, data) { Tick = ctx.Sim.Clock.Tick });
        }
    }
}

internal sealed class InterruptHandler : IEffectHandler
{
    public string Type => "interrupt";

    public EffectSignature Signature { get; } = EffectSignature.Of();

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect { Type = Type };

    private sealed class Effect : CompiledEffect
    {
        public override void Execute(EffectContext ctx)
        {
            if (ctx.Sim.TryService<Weaves.WeaveRuntime>(out var runtime))
            {
                // Through the runtime, so a guard, ramps and flags the cast put on come off at once.
                runtime.Interrupt(ctx.Sim, ctx.Target);
                return;
            }
            if (ctx.Sim.World.TryGet<WeaveBook>(ctx.Target, out var book) && book.Casting is not null)
            {
                var key = book.Casting.Slot.Key;
                book.Casting = null;
                ctx.Sim.Events.Publish(new WeaveInterrupted(ctx.Target, key) { Tick = ctx.Sim.Clock.Tick });
            }
        }
    }
}

internal sealed class GrantItemHandler : IEffectHandler
{
    public string Type => "grant_item";

    public EffectSignature Signature { get; } = EffectSignature.Of("item", "table", "count", "rarity");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Item = ctx.StringOf(p["item"]),
        Table = ctx.StringOf(p["table"]),
        Count = ctx.IntOf(p["count"], 1),
        Rarity = ctx.StringOf(p["rarity"]),
    };

    private sealed class Effect : CompiledEffect
    {
        public string? Item;
        public string? Table;
        public int Count = 1;
        public string? Rarity;

        public override void Execute(EffectContext ctx)
        {
            var target = ctx.Target.IsNone ? ctx.Source : ctx.Target;
            var loot = ctx.Sim.Service<Loot.LootService>();
            var level = ctx.Sim.World.Get<Stats>(target)?.Level ?? 1;
            if (Item is not null)
            {
                var gen = ctx.Sim.Service<Items.ItemGenerator>();
                var item = gen.Roll(Item, level, Rarity, ctx.Rng);
                item.Count = Math.Clamp(Count, 1, Math.Max(1, item.Stack));
                loot.Give(ctx.Sim, target, item);
            }
            if (Table is not null)
            {
                loot.GiveTable(ctx.Sim, target, Table, level, ctx.Rng);
            }
        }
    }
}

internal sealed class GrantXpHandler : IEffectHandler
{
    public string Type => "grant_xp";

    public EffectSignature Signature { get; } = EffectSignature.Of("amount");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect { Type = Type, Amount = ctx.ExprOf(p["amount"], Expr.Constant(10))! };

    private sealed class Effect : CompiledEffect
    {
        public required Expr Amount;

        public override void Execute(EffectContext ctx)
        {
            var target = ctx.Target.IsNone ? ctx.Source : ctx.Target;
            ctx.Sim.Service<Progression.ProgressionService>().GrantXp(ctx.Sim, target, ctx.Eval(Amount, 10));
        }
    }
}

internal sealed class LogHandler : IEffectHandler
{
    public string Type => "log";

    public EffectSignature Signature { get; } = EffectSignature.Of("text");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect { Type = Type, Text = ctx.StringOf(p["text"]) ?? "" };

    private sealed class Effect : CompiledEffect
    {
        public string Text = "";
        public override void Execute(EffectContext ctx) => ctx.Sim.Log.Info($"[effect] {Text} source={ctx.Source} target={ctx.Target}");
    }
}
