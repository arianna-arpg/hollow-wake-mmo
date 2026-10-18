using System.Text.Json;
using System.Text.Json.Nodes;
using HollowWake.Core.Attributes;
using HollowWake.Core.Definitions;
using HollowWake.Core.Effects;
using HollowWake.Core.Effects.Handlers;
using HollowWake.Core.Expressions;
using HollowWake.Core.Systems;

namespace HollowWake.Core.Content.Kinds;

/// <summary>
/// Content for deliveries: the delivery parameters of every effect graph in content (shapes, firing styles,
/// returns, picks, placements, movement kinds), wherever a graph appears: forms, presets, glyph effects,
/// hooks and patches, status effects. Registers the delivery variables as scope names and the
/// <c>collision</c> trigger kind before content loads.
/// </summary>
internal static class DeliveryKinds
{
    private static readonly string[] Fires = { "fan", "volley", "ring", "salvo" };
    private static readonly string[] Returns = { "none", "origin", "caster" };
    private static readonly string[] Origins = { "caster", "point" };
    private static readonly string[] Centers = { "focus", "source", "point" };
    private static readonly string[] Reaches = { "center", "edge" };
    private static readonly string[] MoveKinds = { "teleport", "dash", "leap", "blink", "recall", "knockback", "pull", "buffet" };
    private static readonly string[] Links = { "target", "point", "owner" };
    private static readonly string[] DetonatePicks = { "all", "nearest" };
    private static readonly string[] MarkAt = { "caster", "point" };
    private static readonly string[] Movers = { "source", "target" };
    private static readonly string[] ScatterAt = { "random", "enemies" };
    private static readonly string[] CascadeDirs = { "forward", "backward", "axis", "out", "random" };
    private static readonly string[] SegmentAxes = { "along", "across" };
    private static readonly string[] Occlusions = { "blocked", "free" };

    public static void Register()
    {
        foreach (var name in DeliveryVars.All)
        {
            ScopeNames.Declare(name);
        }
        _ = DisplacementSystem.Collision;
        ContentKinds.AddValidator<FormDef>((d, c) =>
        {
            Specs(d.Effects, c, "effects");
            if (d.Channel?.Release is { } release)
            {
                Specs(release.Effects, c, "channel.release.effects");
            }
        });
        ContentKinds.AddValidator<WeaveDef>((d, c) =>
        {
            Specs(d.Effects, c, "effects");
            if (d.Channel?.Release is { } release)
            {
                Specs(release.Effects, c, "channel.release.effects");
            }
        });
        ContentKinds.AddValidator<GlyphDef>((d, c) =>
        {
            Specs(d.AddsEffects, c, "adds_effects");
            foreach (var (hook, list) in d.Hooks)
            {
                Specs(list, c, "hooks." + hook);
            }
            for (var i = 0; i < d.Patches.Count; i++)
            {
                var patch = d.Patches[i];
                if (patch.Set is { } set && patch.EffectiveMode != PatchMode.Append)
                {
                    Params(patch.Type, set, c, $"patches[{i}].set");
                }
                if (patch.Wrap is { } wrap)
                {
                    Params(wrap.Type, wrap.Params, c, $"patches[{i}].wrap");
                }
            }
        });
        ContentKinds.AddValidator<StatusDef>((d, c) =>
        {
            Specs(d.OnApply, c, "on_apply");
            Specs(d.OnExpire, c, "on_expire");
            if (d.Tick is { } tick)
            {
                Specs(tick.Effects, c, "tick.effects");
            }
        });
    }

    /// <summary>Checks every delivery node in a list of effects, nested lists included.</summary>
    public static void Specs(IEnumerable<TypedSpec>? specs, ValidationContext c, string field)
    {
        if (specs is null)
        {
            return;
        }
        var i = 0;
        foreach (var spec in specs)
        {
            var at = $"{field}[{i++}]";
            Params(spec.Type, spec.Params, c, at);
            Nested(spec.Params, c, at);
        }
    }

    private static void Nested(JsonObject node, ValidationContext c, string field)
    {
        foreach (var (key, value) in node)
        {
            switch (value)
            {
                case JsonArray list:
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (list[i] is JsonObject element)
                        {
                            Effect(element, c, $"{field}.{key}[{i}]");
                        }
                    }
                    break;
                case JsonObject obj when key is not ("spread_by_aim" or "radius_curve" or "segments"):
                    Nested(obj, c, $"{field}.{key}");
                    break;
            }
        }
    }

    private static void Effect(JsonObject node, ValidationContext c, string field)
    {
        if (JsonNum.StringOf(node["type"]) is { } type && EffectRegistry.Shared.Has(type))
        {
            Params(type, node, c, field);
        }
        Nested(node, c, field);
    }

    /// <summary>The delivery parameters one node of a type sets.</summary>
    private static void Params(string type, JsonObject p, ValidationContext c, string field)
    {
        if (type is "area" or "projectile" or "field" or "aura")
        {
            Choice(p, "occlusion", Occlusions, c, field);
        }
        switch (type)
        {
            case "area":
                ShapeOf(p, "shape", c, field);
                Choice(p, "center", Centers, c, field);
                Choice(p, "pick", Picks.Names, c, field);
                Choice(p, "reach", Reaches, c, field);
                break;
            case "projectile":
                ShapeOf(p, "shape", c, field);
                Choice(p, "fire", Fires, c, field);
                Choice(p, "return", Returns, c, field);
                Choice(p, "origin", Origins, c, field);
                Positive(p, "tick", c, field);
                Positive(p, "travel", c, field);
                if (p["spread_by_aim"] is { } aim && aim is not JsonObject)
                {
                    c.Error($"'{field}.spread_by_aim' must be {{ near, far, range }}.");
                }
                break;
            case "chain":
                Choice(p, "pick", Picks.Names, c, field);
                break;
            case "field":
                ShapeOf(p, "shape", c, field);
                if (p["radius_curve"] is JsonObject curve && JsonNum.StringOf(curve["curve"]) is { } name && !Curves.Has(name))
                {
                    c.Error($"'{field}.radius_curve' names unknown curve '{name}'; known curves are {string.Join(", ", Curves.Names)}.");
                }
                if (p["segments"] is JsonObject segments)
                {
                    Choice(segments, "axis", SegmentAxes, c, field + ".segments");
                }
                if (p["domain"] is { } domain)
                {
                    Domain(domain, c, field);
                }
                break;
            case "move":
                Choice(p, "kind", MoveKinds, c, field);
                Choice(p, "who", Movers, c, field);
                if (JsonNum.StringOf(p["status"]) is { } status)
                {
                    c.Ref<StatusDef>(status, field + ".status");
                }
                break;
            case "scatter":
                Choice(p, "at", ScatterAt, c, field);
                break;
            case "cascade":
                Choice(p, "dir", CascadeDirs, c, field);
                break;
            case "aura":
                if (p["domain"] is { } auraDomain)
                {
                    Domain(auraDomain, c, field);
                }
                break;
            case "tether":
                Choice(p, "link", Links, c, field);
                break;
            case "aim":
                if (p["sequence"] is { } sequence && sequence is not JsonArray)
                {
                    c.Error($"'{field}.sequence' must be a list of bearings in degrees.");
                }
                break;
            case "detonate":
                Choice(p, "pick", DetonatePicks, c, field);
                break;
            case "mark":
                Choice(p, "at", MarkAt, c, field);
                break;
        }
    }

    private static void ShapeOf(JsonObject p, string param, ValidationContext c, string field)
    {
        if (JsonNum.StringOf(p[param]) is { } name && !Shapes.Has(name))
        {
            c.Error($"'{field}.{param}' names unknown shape '{name}'; known shapes are {string.Join(", ", Shapes.Names)}.");
        }
    }

    private static void Choice(JsonObject p, string param, string[] choices, ValidationContext c, string field)
    {
        if (p[param] is { } node && (JsonNum.StringOf(node) is not { } value || Array.IndexOf(choices, value) < 0))
        {
            c.Error($"'{field}.{param}' must be one of {string.Join(", ", choices)}, not '{node.ToJsonString()}'.");
        }
    }

    private static void Positive(JsonObject p, string param, ValidationContext c, string field)
    {
        if (p[param] is { } node && (!JsonNum.TryDouble(node, out var v) || v <= 0))
        {
            c.Error($"'{field}.{param}' must be a number above zero.");
        }
    }

    private static void Domain(JsonNode domain, ValidationContext c, string field)
    {
        List<Modifier>? modifiers;
        try
        {
            modifiers = domain.Deserialize<List<Modifier>>(ContentJson.Options);
        }
        catch (JsonException ex)
        {
            c.Error($"'{field}.domain' is not a list of modifiers: {ex.Message}");
            return;
        }
        c.Modifiers(modifiers, field + ".domain");
    }
}
