using System.Text.Json.Nodes;
using HollowWake.Core.Ai;
using HollowWake.Core.Definitions;

namespace HollowWake.Core.Content.Kinds;

/// <summary>
/// Content for minions and constructs: the AI actions behaviors name, the minion rules (commands and the
/// inheritance table), glyph forwarding, and the names minion perception and minion effects add to
/// expression scopes. Validators are added to the existing kinds with <see cref="ContentKinds.AddValidator{T}"/>.
/// </summary>
internal static class MinionKinds
{
    /// <summary>Names minions perceive and minion effects set, readable in behavior transitions and effect expressions.</summary>
    public static readonly IReadOnlyList<string> ScopeNamesAdded = new[]
    {
        "has_owner", "owner_distance", "owner_has_target", "owner_target_distance", "commanded", "command_distance",
        "consumed_life", "consumed_life_max", "body_life_max",
    };

    public static void Register()
    {
        foreach (var name in ScopeNamesAdded)
        {
            ScopeNames.Declare(name);
        }
        ContentKinds.AddValidator<BehaviorDef>(Behavior);
        ContentKinds.AddValidator<RulesDef>(Rules);
        ContentKinds.AddValidator<GlyphDef>(Glyph);
    }

    /// <summary>Every action a behavior names must be a registered AI action.</summary>
    private static void Behavior(BehaviorDef d, ValidationContext c)
    {
        foreach (var (name, state) in d.States)
        {
            Actions(state.Enter, c, $"states.{name}.enter");
            Actions(state.Tick, c, $"states.{name}.tick");
            Actions(state.Exit, c, $"states.{name}.exit");
        }
    }

    private static void Actions(List<TypedSpec> actions, ValidationContext c, string field)
    {
        foreach (var a in actions)
        {
            if (!AiActions.TryGet(a.Type, out _))
            {
                c.Error($"'{field}' uses unknown ai action '{a.Type}'; known actions are {string.Join(", ", AiActions.Names.OrderBy(n => n, StringComparer.Ordinal))}.");
            }
        }
    }

    /// <summary>The minion rules: the default behavior exists, commands name registered kinds, inheritance lines read and land on attributes.</summary>
    private static void Rules(RulesDef d, ValidationContext c)
    {
        if (d.Id != "minions" || d.Values is not { } values)
        {
            return;
        }
        if (JsonNum.StringOf(values["behavior"]) is { Length: > 0 } behavior)
        {
            c.Ref<BehaviorDef>(behavior, "behavior");
        }
        if (values["commands"] is JsonObject commands)
        {
            foreach (var (name, node) in commands)
            {
                if (node is not JsonObject command)
                {
                    c.Error($"commands.{name} must be an object.");
                    continue;
                }
                var kind = JsonNum.StringOf(command["kind"]) ?? name;
                if (!MinionCommands.TryGet(kind, out _))
                {
                    c.Error($"commands.{name} names unknown command kind '{kind}'; known kinds are {string.Join(", ", MinionCommands.Names)}.");
                }
                if (JsonNum.Of(command["duration"], 1) <= 0)
                {
                    c.Error($"commands.{name}.duration must be above zero.");
                }
            }
        }
        if (values["inherit"] is JsonArray inherit)
        {
            for (var i = 0; i < inherit.Count; i++)
            {
                if (inherit[i] is not JsonObject line || JsonNum.StringOf(line["from"]) is not { } from || JsonNum.StringOf(line["attribute"]) is not { } to)
                {
                    c.Error($"inherit[{i}] needs 'from' and 'attribute'.");
                    continue;
                }
                var form = JsonNum.StringOf(line["form"]) ?? "flat";
                if (form is not ("flat" or "increased" or "more" or "override"))
                {
                    c.Error($"inherit[{i}] has form '{form}'; it must be flat, increased, more or override.");
                }
                if (from.Contains('{'))
                {
                    continue;
                }
                if (!c.Registry.Attributes.Contains(from))
                {
                    c.Warn($"inherit[{i}] folds undeclared attribute '{from}'.");
                }
                if (!c.Registry.Attributes.Contains(to))
                {
                    c.Warn($"inherit[{i}] lands on undeclared attribute '{to}'.");
                }
            }
        }
    }

    private static void Glyph(GlyphDef d, ValidationContext c)
    {
        if (d.Forwards == true && d.Trigger is not null)
        {
            c.Warn("forwards a trigger to minions; a trigger's machinery needs its owner's hands, so it will not fire for them.");
        }
    }
}
