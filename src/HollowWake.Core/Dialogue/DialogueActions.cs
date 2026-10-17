using System.Text.Json;
using System.Text.Json.Nodes;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Crafting;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Interaction;
using HollowWake.Core.Items;
using HollowWake.Core.Loot;
using HollowWake.Core.Progression;
using HollowWake.Core.Quests;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Dialogue;

/// <summary>What a conversation's effect runs with: the simulation, the hero, the speaker and the conversation.</summary>
public sealed class DialogueContext
{
    public required Simulation Sim { get; init; }
    public required Entity Hero { get; init; }
    public required Entity Speaker { get; init; }
    public required DialogueDef Dialogue { get; init; }

    public IExprScope Scope => _scope ??= new HeroScope(Sim, Hero);
    private IExprScope? _scope;

    public string? Text(JsonObject args, string name) => JsonNum.StringOf(args[name]);

    public double Number(JsonObject args, string name, double fallback)
    {
        var node = args[name];
        if (node is JsonValue v && v.TryGetValue<string>(out var text))
        {
            return Expr.Parse(text).Evaluate(Scope);
        }
        return JsonNum.Of(node, fallback);
    }
}

public delegate void DialogueAction(DialogueContext ctx, JsonObject args);

/// <summary>
/// The things a conversation's lines and choices can do, by name: story flags, quests, rewards, and opening a vendor
/// or a station in the same book. An open set: register more by name. A type this registry does not know is run
/// through the effect registry instead, with the speaker as source and the hero as target, so any effect composes.
/// </summary>
public sealed class DialogueActions
{
    private readonly Dictionary<string, (DialogueAction Action, string[] Params)> _actions = new(StringComparer.Ordinal);

    private static readonly Lazy<DialogueActions> SharedDefault = new(CreateDefault, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>A default registry for reading only: validators check action types and parameters against it.</summary>
    public static DialogueActions Shared => SharedDefault.Value;

    public void Register(string type, DialogueAction action, params string[] parameters) => _actions[type] = (action, parameters);

    public bool Has(string type) => _actions.ContainsKey(type);

    public IEnumerable<string> Types => _actions.Keys.OrderBy(k => k, StringComparer.Ordinal);

    public bool TryGet(string type, out DialogueAction action, out IReadOnlyList<string> parameters)
    {
        if (_actions.TryGetValue(type, out var entry))
        {
            action = entry.Action;
            parameters = entry.Params;
            return true;
        }
        action = null!;
        parameters = Array.Empty<string>();
        return false;
    }

    public static DialogueActions CreateDefault()
    {
        var a = new DialogueActions();
        a.Register("set_flag", static (ctx, p) => SetFlag(ctx, ctx.Text(p, "flag"), true), "flag");
        a.Register("clear_flag", static (ctx, p) => SetFlag(ctx, ctx.Text(p, "flag"), false), "flag");
        a.Register("accept_quest", static (ctx, p) =>
        {
            if (ctx.Text(p, "quest") is { } quest)
            {
                ctx.Sim.Service<QuestService>().Accept(ctx.Sim, ctx.Hero, quest, out _);
            }
        }, "quest");
        a.Register("turn_in_quest", static (ctx, p) =>
        {
            if (ctx.Text(p, "quest") is { } quest)
            {
                ctx.Sim.Service<QuestService>().TurnIn(ctx.Sim, ctx.Hero, quest, out _);
            }
        }, "quest");
        a.Register("offer_quests", static (ctx, _) => QuestOffers.Publish(ctx.Sim, ctx.Hero, ctx.Speaker));
        a.Register("open_vendor", static (ctx, p) =>
        {
            if (ctx.Text(p, "vendor") is { } vendor)
            {
                ctx.Sim.Service<VendorService>().Open(ctx.Sim, ctx.Hero, ctx.Speaker, vendor, out _);
            }
        }, "vendor");
        a.Register("open_station", static (ctx, p) =>
        {
            var tags = p["tags"] is { } t ? t.Deserialize<TagSet>(ContentJson.Options) ?? TagSet.Empty : ctx.Sim.World.Get<Interactable>(ctx.Speaker)?.Tags ?? TagSet.Empty;
            ctx.Sim.Service<StationService>().Open(ctx.Sim, ctx.Hero, ctx.Speaker, tags);
        }, "tags");
        a.Register("give_item", static (ctx, p) => GiveItem(ctx, p), "item", "count", "rarity", "level", "glyph", "form", "weave");
        a.Register("give_currency", static (ctx, p) =>
        {
            if (ctx.Text(p, "currency") is { } currency)
            {
                ctx.Sim.Service<LootService>().GiveCurrency(ctx.Sim, ctx.Hero, currency, Math.Max(0, ctx.Number(p, "amount", 0)));
            }
        }, "currency", "amount");
        a.Register("take_currency", static (ctx, p) =>
        {
            if (ctx.Text(p, "currency") is { } currency && ctx.Sim.World.TryGet<Inventory>(ctx.Hero, out var inv))
            {
                var amount = Math.Min(inv.CurrencyOf(currency), Math.Max(0, ctx.Number(p, "amount", 0)));
                if (amount > 0)
                {
                    ctx.Sim.Service<LootService>().GiveCurrency(ctx.Sim, ctx.Hero, currency, -amount);
                }
            }
        }, "currency", "amount");
        a.Register("grant_points", static (ctx, p) =>
        {
            if (ctx.Text(p, "currency") is { } currency)
            {
                ctx.Sim.Service<ProgressionService>().GrantPoints(ctx.Sim, ctx.Hero, currency, (int)ctx.Number(p, "amount", 1));
            }
        }, "currency", "amount");
        return a;
    }

    private static void SetFlag(DialogueContext ctx, string? flag, bool on)
    {
        if (flag is null || !ctx.Sim.World.TryGet<QuestLog>(ctx.Hero, out var log))
        {
            return;
        }
        var changed = on ? log.Flags.Add(flag) : log.Flags.Remove(flag);
        if (changed)
        {
            ctx.Sim.Events.Publish(new StoryFlagSet(ctx.Hero, flag, on) { Tick = ctx.Sim.Clock.Tick });
        }
    }

    private static void GiveItem(DialogueContext ctx, JsonObject p)
    {
        if (ctx.Text(p, "item") is not { } baseId || !ctx.Sim.Content.Has<ItemBaseDef>(baseId))
        {
            return;
        }
        var level = (int)Math.Max(1, ctx.Number(p, "level", ctx.Sim.World.Get<Stats>(ctx.Hero)?.Level ?? 1));
        var gen = ctx.Sim.Service<ItemGenerator>();
        var item = gen.Roll(baseId, level, ctx.Text(p, "rarity") ?? "common", ctx.Sim.RngFor(ctx.Hero, "dialogue:" + ctx.Dialogue.Id),
            new ItemGenerator.PayloadRequest { Glyph = ctx.Text(p, "glyph"), Form = ctx.Text(p, "form"), Weave = ctx.Text(p, "weave") });
        item.Count = Math.Clamp((int)ctx.Number(p, "count", 1), 1, Math.Max(1, item.Stack));
        ctx.Sim.Service<LootService>().Give(ctx.Sim, ctx.Hero, item);
    }
}
