using HollowWake.Core.Definitions;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Content.Kinds;

/// <summary>
/// Content for progression and items: the item fields of glyphs and forms (rolled axes, drop weights, level
/// floors, requirements), loot entries that name what a stone or codex holds; passive choices, realms,
/// containers, stashes, conversations and vendors as they land. Adds validators to the existing kinds and
/// registers new kinds.
/// </summary>
internal static class ProgressionKinds
{
    public static void Register()
    {
        foreach (var prefix in Progression.HeroScope.Prefixes)
        {
            ScopeNames.DeclarePrefix(prefix);
        }
        ScopeNames.Declare("node_cost");
        ScopeNames.Declare("refunds");
        ContentKinds.AddValidator<GlyphDef>(Axes);
        ContentKinds.AddValidator<GlyphDef>((d, c) => ItemFields(d.DropWeight, d.MinLevel, d.Requirements, c));
        ContentKinds.AddValidator<FormDef>((d, c) => ItemFields(d.DropWeight, d.MinLevel, d.Requirements, c));
        ContentKinds.AddValidator<LootTableDef>(LootPayloads);
        ContentKinds.Register<PassiveChoiceDef>("passive_choices", Choices);
        ContentKinds.Register<PassiveRealmDef>("passive_realms", Realm);
        ContentKinds.AddValidator<PassiveNodeDef>(NodeChoice);
        ContentKinds.Register<DialogueDef>("dialogues", Dialogue);
        ContentKinds.Register<VendorDef>("vendors", Vendor);
    }

    private static void Dialogue(DialogueDef d, ValidationContext c)
    {
        if (!d.Nodes.ContainsKey(d.Start))
        {
            c.Error($"start line '{d.Start}' is not one of the nodes.");
        }
        if (d.For.Count == 0)
        {
            c.Warn("is for no entity, so nothing opens it.");
        }
        foreach (var target in d.For)
        {
            if (!c.Registry.Has<PatternDef>(target))
            {
                c.Warn($"for '{target}' is not a pattern; it opens only for an interactable with that ref.");
            }
        }
        c.Expression(d.When, "when");
        foreach (var (id, node) in d.Nodes)
        {
            DialogueEffects(node.Effects, c, $"nodes.{id}.effects");
            var choices = new HashSet<string>(StringComparer.Ordinal);
            foreach (var choice in node.Choices)
            {
                if (!choices.Add(choice.Id))
                {
                    c.Error($"nodes.{id} declares choice '{choice.Id}' twice.");
                }
                if (choice.Goto is not null && !d.Nodes.ContainsKey(choice.Goto))
                {
                    c.Error($"nodes.{id}.choices.{choice.Id} goes to unknown line '{choice.Goto}'.");
                }
                if (choice.End && choice.Goto is not null)
                {
                    c.Warn($"nodes.{id}.choices.{choice.Id} both ends and goes on; it ends.");
                }
                c.Expression(choice.When, $"nodes.{id}.choices.{choice.Id}.when");
                c.Expression(choice.Enabled, $"nodes.{id}.choices.{choice.Id}.enabled");
                DialogueEffects(choice.Effects, c, $"nodes.{id}.choices.{choice.Id}.effects");
            }
        }
    }

    /// <summary>A conversation's effects name dialogue actions or effect types, with the parameters each declares.</summary>
    private static void DialogueEffects(List<TypedSpec> effects, ValidationContext c, string field)
    {
        foreach (var spec in effects)
        {
            if (HollowWake.Core.Dialogue.DialogueActions.Shared.TryGet(spec.Type, out _, out var parameters))
            {
                foreach (var (key, _) in spec.Params)
                {
                    if (!parameters.Contains(key))
                    {
                        c.Error($"'{field}' action '{spec.Type}' has unknown parameter '{key}'.");
                    }
                }
                if (spec.Type is "open_vendor" && JsonNum.StringOf(spec.Params["vendor"]) is { } vendor)
                {
                    c.Ref<VendorDef>(vendor, field + ".vendor");
                }
                if (spec.Type is "accept_quest" or "turn_in_quest" && JsonNum.StringOf(spec.Params["quest"]) is { } quest)
                {
                    c.Ref<QuestDef>(quest, field + ".quest");
                }
                continue;
            }
            if (!Effects.EffectRegistry.Shared.Has(spec.Type))
            {
                c.Error($"'{field}' uses unknown action or effect '{spec.Type}'.");
            }
        }
    }

    private static void Vendor(VendorDef d, ValidationContext c)
    {
        c.Ref<CurrencyDef>(d.Currency, "currency");
        c.Expression(d.Price, "price");
        c.Expression(d.SellPrice, "sell_price");
        c.Expression(d.When, "when");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in d.Stock)
        {
            if (!ids.Add(s.Id))
            {
                c.Error($"stock '{s.Id}' is declared twice.");
            }
            c.Ref<ItemBaseDef>(s.Item, $"stock.{s.Id}.item");
            if (s.Rarity is not null)
            {
                c.Ref<ItemRarityDef>(s.Rarity, $"stock.{s.Id}.rarity");
            }
            if (s.Glyph is not null)
            {
                c.Ref<GlyphDef>(s.Glyph, $"stock.{s.Id}.glyph");
            }
            if (s.Form is not null)
            {
                c.Ref<FormDef>(s.Form, $"stock.{s.Id}.form");
            }
            if (s.Weave is not null)
            {
                c.Ref<WeaveDef>(s.Weave, $"stock.{s.Id}.weave");
            }
            foreach (var (currency, expr) in s.Prices)
            {
                c.Ref<CurrencyDef>(currency, $"stock.{s.Id}.prices");
                c.Expression(expr, $"stock.{s.Id}.prices.{currency}");
            }
            c.Expression(s.Level, $"stock.{s.Id}.level");
            c.Expression(s.When, $"stock.{s.Id}.when");
            c.Expression(s.Enabled, $"stock.{s.Id}.enabled");
            if (s.Count < 1 || s.Stock is < 0)
            {
                c.Error($"stock.{s.Id} needs a count of at least 1 and a stock that is not negative.");
            }
        }
    }

    private static void Choices(PassiveChoiceDef d, ValidationContext c)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var option in d.Options)
        {
            if (!ids.Add(option.Id))
            {
                c.Error($"option '{option.Id}' is declared twice.");
            }
            c.Modifiers(option.Modifiers, $"options.{option.Id}.modifiers");
        }
        if (d.Options.Count == 0 && d.Generate is null)
        {
            c.Error("a choice group needs options or a generate block.");
        }
        if (d.Generate is { } generate && !c.Registry.Attributes.All.Any(a => a.Category == generate.Category))
        {
            c.Error($"generate.category '{generate.Category}' has no attributes.");
        }
        if (d.Pick < 1)
        {
            c.Error("pick must be at least 1.");
        }
        if (d.Max is < 1)
        {
            c.Error("max must be at least 1.");
        }
        if (d.Default is { } fallback && !ids.Contains(fallback) && d.Generate is null)
        {
            c.Error($"default '{fallback}' is not one of the options.");
        }
    }

    private static void Realm(PassiveRealmDef d, ValidationContext c)
    {
        if (d.Adjacency is not ("tree" or "free"))
        {
            c.Error($"adjacency must be tree or free, not '{d.Adjacency}'.");
        }
        c.Refs<PassiveNodeDef>(d.Roots, "roots");
        c.Expression(d.Unlock, "unlock");
        if (d.Calling is not null)
        {
            c.Ref<CallingDef>(d.Calling, "calling");
        }
        foreach (var root in d.Roots)
        {
            if (c.Registry.Get<PassiveNodeDef>(root) is { } node && node.Realm != d.Id)
            {
                c.Error($"root '{root}' belongs to realm '{node.Realm}'.");
            }
        }
    }

    private static void NodeChoice(PassiveNodeDef d, ValidationContext c)
    {
        if (d.Realm != Progression.ProgressionService.MainRealm)
        {
            c.Ref<PassiveRealmDef>(d.Realm, "realm");
        }
        if (d.Choice is not { } choice)
        {
            return;
        }
        if (!c.Ref<PassiveChoiceDef>(choice.Group, "choice.group"))
        {
            return;
        }
        if (choice.Pick is < 1)
        {
            c.Error("choice.pick must be at least 1.");
        }
        if (choice.Default is { } fallback && Progression.PassiveChoices.Option(c.Registry, choice.Group, fallback) is null)
        {
            c.Error($"choice.default '{fallback}' is not an option of group '{choice.Group}'.");
        }
    }

    private static void ItemFields(double dropWeight, int minLevel, Dictionary<string, double> requirements, ValidationContext c)
    {
        if (dropWeight < 0)
        {
            c.Error("drop_weight cannot be negative.");
        }
        if (minLevel < 1)
        {
            c.Error("min_level must be at least 1.");
        }
        foreach (var attr in requirements.Keys)
        {
            if (attr != "level" && !c.Registry.Attributes.Contains(attr))
            {
                c.Warn($"requirements name undeclared attribute '{attr}'.");
            }
        }
    }

    /// <summary>A loot entry that names a glyph, form or preset must name real ones, on an item whose class holds them.</summary>
    private static void LootPayloads(LootTableDef d, ValidationContext c)
    {
        foreach (var roll in d.Rolls)
        {
            foreach (var e in roll.Entries)
            {
                if (e.Glyph is null && e.Form is null && e.Weave is null)
                {
                    continue;
                }
                if (e.Glyph is not null)
                {
                    c.Ref<GlyphDef>(e.Glyph, "entries.glyph");
                }
                if (e.Form is not null)
                {
                    c.Ref<FormDef>(e.Form, "entries.form");
                }
                if (e.Weave is not null)
                {
                    c.Ref<WeaveDef>(e.Weave, "entries.weave");
                }
                if (e.Kind != "item" || e.Item is null)
                {
                    c.Error("entries naming a glyph, form or weave must be item entries that name the stone or codex base.");
                }
            }
        }
    }

    private static void Axes(GlyphDef d, ValidationContext c)
    {
        var axes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var axis in d.Axes)
        {
            if (!axes.Add(axis.Id))
            {
                c.Error($"axis '{axis.Id}' is declared twice.");
            }
            if (axis.Rows.Count == 0)
            {
                c.Error($"axis '{axis.Id}' has no rows.");
            }
            var rows = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in axis.Rows)
            {
                if (!rows.Add(row.Id))
                {
                    c.Error($"axis '{axis.Id}' declares row '{row.Id}' twice.");
                }
                if (row.Weight < 0)
                {
                    c.Error($"axis '{axis.Id}' row '{row.Id}' has a negative weight.");
                }
                c.Modifiers(row.Modifiers, $"axes.{axis.Id}.{row.Id}.modifiers");
                foreach (var m in row.RequiresMechanisms)
                {
                    if (MechanismRegistry.Validate(m, c.Registry) is { } problem)
                    {
                        c.Error($"axes.{axis.Id}.{row.Id}.requires_mechanisms {problem}.");
                    }
                }
            }
        }
    }
}
