using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using HollowWake.Core.Attributes;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Weaves.Probes;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Weaves;

/// <summary>
/// A pure test of a weave being compiled: true when the weave has the mechanism. It reads the weave's
/// patched graph, timing, bases and modifiers through <see cref="WeaveProbe"/> and the optional parameter
/// after the colon of a name such as <c>affliction:bleeding</c>. Probes run at compile time only and never
/// name weave ids.
/// </summary>
public delegate bool MechanismProbe(WeaveProbe weave, string? parameter);

/// <summary>What a probe accepts after the colon of its name.</summary>
public sealed class ProbeParameter
{
    private readonly Func<string, ContentRegistry?, string?> _check;

    private ProbeParameter(string description, Func<string, ContentRegistry?, string?> check)
    {
        Description = description;
        _check = check;
    }

    public string Description { get; }

    /// <summary>The probe takes no parameter.</summary>
    public static readonly ProbeParameter None = new("none", static (p, _) => $"takes no parameter, got '{p}'");

    /// <summary>Any text.</summary>
    public static readonly ProbeParameter Any = new("any text", static (_, _) => null);

    /// <summary>The id of a status.</summary>
    public static readonly ProbeParameter Status = new("a status id", static (p, content) =>
        content is null || content.Has<StatusDef>(p) ? null : $"names unknown status '{p}'");

    /// <summary>One of a fixed set of words.</summary>
    public static ProbeParameter OneOf(params string[] words) => new(string.Join(", ", words), (p, _) =>
        Array.IndexOf(words, p) >= 0 ? null : $"takes one of {string.Join(", ", words)}, not '{p}'");

    /// <summary>Null when the parameter is acceptable, else why not.</summary>
    public string? Check(string parameter, ContentRegistry? content) => _check(parameter, content);
}

/// <summary>A registered mechanism probe.</summary>
public sealed record MechanismInfo(string Name, MechanismProbe Probe, ProbeParameter Parameter, string Description);

/// <summary>
/// The open registry of mechanism probes. A weave's mechanisms are the ones its form and glyphs declare
/// plus every probe that holds on the compiled weave, so a glyph that levies a cooldown or adds a
/// projectile lifts the gates of glyphs that need one. Names may carry a parameter after a colon, passed
/// to the probe; an unknown probe or parameter fails validation. Each package registers its probes in its
/// own file under <c>Weaves/Probes/</c>.
/// </summary>
public static class MechanismRegistry
{
    private static readonly ConcurrentDictionary<string, MechanismInfo> Probes = new(StringComparer.Ordinal);

    static MechanismRegistry()
    {
        RegisterBuiltins();
        CastingProbes.Register();
        DeliveryProbes.Register();
        MinionProbes.Register();
        CombatProbes.Register();
    }

    public static void Register(string name, MechanismProbe probe, ProbeParameter? parameter = null, string description = "")
    {
        var key = TagSet.Normalize(name);
        Probes[key] = new MechanismInfo(key, probe, parameter ?? ProbeParameter.None, description);
    }

    public static bool TryGet(string name, out MechanismInfo info) => Probes.TryGetValue(TagSet.Normalize(name), out info!);

    public static IEnumerable<MechanismInfo> All => Probes.Values.OrderBy(p => p.Name, StringComparer.Ordinal);

    /// <summary>Splits <c>name:parameter</c>; the parameter is null without a colon.</summary>
    public static (string Name, string? Parameter) Split(string mechanism)
    {
        var colon = mechanism.IndexOf(':');
        return colon < 0 ? (mechanism, null) : (mechanism[..colon], mechanism[(colon + 1)..]);
    }

    /// <summary>
    /// Null when a mechanism a glyph requires can be checked: a plain name (declared mechanisms are open)
    /// or a registered probe with an acceptable parameter. Otherwise why it cannot.
    /// </summary>
    public static string? Validate(string mechanism, ContentRegistry? content)
    {
        var (name, parameter) = Split(mechanism);
        if (parameter is null)
        {
            return null;
        }
        if (!TryGet(name, out var info))
        {
            return $"'{mechanism}' names no mechanism probe '{name}'";
        }
        return info.Parameter.Check(parameter, content) is { } problem ? $"'{mechanism}': {name} {problem}" : null;
    }

    /// <summary>Every probe that holds on the weave when asked without a parameter, in name order.</summary>
    internal static List<string> Held(WeaveProbe weave)
    {
        var held = new List<string>();
        foreach (var info in All)
        {
            if (info.Probe(weave, null))
            {
                held.Add(info.Name);
            }
        }
        return held;
    }

    /// <summary>Whether a parameterized probe holds; false for names that are not probes.</summary>
    public static bool Holds(WeaveProbe weave, string mechanism)
    {
        var (name, parameter) = Split(mechanism);
        return TryGet(name, out var info) && info.Probe(weave, parameter);
    }

    // ------------------------------------------------------------ the built-in probes

    private static void RegisterBuiltins()
    {
        Register("cooldown", static (w, _) => w.HasCooldown, description: "the weave has a cooldown: the form's, or one a glyph levies through cooldown.added");
        Register("strikes", static (w, _) => w.Tags.Has("attack") && w.Has("damage"), description: "an attack that deals damage");
        Register("projectile", static (w, _) => w.Has("projectile"), description: "the graph fires projectiles");
        Register("flight", static (w, p) => Flight(w, p), ProbeParameter.OneOf("children", "spends"), "projectiles in flight: children when they split, spends when a hit can end them");
        Register("area", static (w, _) => Area(w), description: "the graph strikes areas sized by a radius: circles, cones and fields (a line's reach is its range, not an area)");
        Register("surface", static (w, p) => Surface(w, p), ProbeParameter.OneOf("standing"), "the graph leaves a field; standing when it stays put");
        Register("summon", static (w, _) => w.Has("summon"), description: "the graph summons");
        Register("minion", static (w, _) => w.Has("summon"), description: "the weave makes minions");
        Register("construct", static (w, p) => Construct(w, p), ProbeParameter.OneOf("massed"), "the graph places constructs; massed when more than one");
        Register("periodic_construct", static (w, _) => Constructs(w).Any(n => n.ContainsKey("interval") || n.ContainsKey("pulse")), description: "a construct that acts on an interval");
        Register("engagement", static (w, _) => w.Has("aura") || w.Nodes("summon").Any(n => n.ContainsKey("contract")) || IsGuard(w) || w.Form.Toggle is not null, description: "a toggle, a contract or a held guard");
        Register("displaces", static (w, _) => w.Has("move"), description: "the graph moves bodies");
        Register("channel", static (w, _) => w.Form.CastMode == CastMode.Channel, description: "a channeled cast");
        Register("held", static (w, _) => w.Form.CastMode is CastMode.Charge or CastMode.Overcharge || (w.Form.CastMode == CastMode.Channel && w.Form.Channel?.Release is not null), description: "a held cast that releases: a charge-up, an overcharge or a channel with a release");
        Register("guard", static (w, _) => IsGuard(w), description: "a guard stance");
        Register("uses", static (w, _) => HasUses(w), description: "a bank of use charges");
        Register("bankless", static (w, _) => !HasUses(w), description: "no bank of use charges");
        Register("affliction", static (w, p) => Affliction(w, p), ProbeParameter.Status, "the weave can inflict an ailment, or the named status");
        Register("status", static (w, p) => AppliesStatus(w, p), ProbeParameter.Status, "the weave applies a status whose strength can scale, or the named status");
        Register("landing", static (w, _) => w.Nodes("projectile").Any(n => WeaveProbe.Flag(n, "gravity")) || w.Nodes().Any(n => n.ContainsKey("on_land")), description: "the delivery lands: lobbed projectiles, leaps, delayed placements");
    }

    private static bool IsGuard(WeaveProbe w) => w.Form.CastMode == CastMode.Guard || w.Tags.Has("guard");

    /// <summary>A bank of uses from the form or an active glyph, or one a modifier raises through <c>uses.max</c>.</summary>
    private static bool HasUses(WeaveProbe w)
    {
        if (w.Form.Uses is not null || w.Raises("uses.max"))
        {
            return true;
        }
        foreach (var g in w.Glyphs)
        {
            if (g.Uses is not null)
            {
                return true;
            }
        }
        return false;
    }

    private static bool Flight(WeaveProbe w, string? parameter)
    {
        if (!w.Has("projectile"))
        {
            return false;
        }
        return parameter switch
        {
            "children" => w.Raises("projectile.forks") || w.Nodes("projectile").Any(p => NestedProjectile(w, p)),
            "spends" => !w.Raises("projectile.rehit"),
            _ => true,
        };
    }

    private static bool NestedProjectile(WeaveProbe w, JsonObject projectile)
    {
        foreach (var kv in projectile)
        {
            if (kv.Value is JsonArray list && list.OfType<JsonObject>().Any(n => w.ContainsType(n, "projectile")))
            {
                return true;
            }
        }
        return false;
    }

    private static bool Area(WeaveProbe w)
    {
        if (w.Has("field"))
        {
            return true;
        }
        foreach (var node in w.Nodes("area"))
        {
            if ((JsonNum.StringOf(node["shape"]) ?? "circle") != "line")
            {
                return true;
            }
        }
        return false;
    }

    private static bool Surface(WeaveProbe w, string? parameter) => parameter switch
    {
        "standing" => w.Nodes("field").Any(n => !WeaveProbe.Flag(n, "follow")),
        _ => w.Has("field"),
    };

    private static IEnumerable<JsonObject> Constructs(WeaveProbe w) => w.Nodes("deploy").Concat(w.Nodes("spawn"));

    private static bool Construct(WeaveProbe w, string? parameter) => parameter switch
    {
        "massed" => Constructs(w).Any(n => n["count"] is { } count && !(JsonNum.TryDouble(count, out var c) && c <= 1)),
        _ => Constructs(w).Any(),
    };

    private static bool Affliction(WeaveProbe w, string? status)
    {
        foreach (var id in w.StatusesInflicted())
        {
            if (status is null ? w.Content.Get<StatusDef>(id)?.Tags.Has("ailment") ?? false : id == status)
            {
                return true;
            }
        }
        return false;
    }

    private static bool AppliesStatus(WeaveProbe w, string? status)
    {
        foreach (var id in w.StatusesApplied())
        {
            if (status is not null)
            {
                if (id == status)
                {
                    return true;
                }
                continue;
            }
            if (w.Content.Get<StatusDef>(id) is { } def && (def.Tick is not null || (def.ScalesWithPotency && def.Modifiers.Count > 0)))
            {
                return true;
            }
        }
        return false;
    }
}

/// <summary>
/// What a mechanism probe reads: the weave being compiled as it stands with its active glyphs, including
/// its patched and hooked effect graph, its form's timing, its local bases and its modifiers.
/// </summary>
public sealed class WeaveProbe
{
    public required ContentRegistry Content { get; init; }
    public required FormDef Form { get; init; }
    public required IReadOnlyList<ThreadDef> Threads { get; init; }
    public required TagSet Tags { get; init; }
    /// <summary>Mechanisms the form and the active glyphs declare.</summary>
    public required TagSet Declared { get; init; }
    public required IReadOnlyList<Modifier> Modifiers { get; init; }
    public required IReadOnlyDictionary<string, double> Bases { get; init; }
    public required WeaveGraph Graph { get; init; }
    /// <summary>The active glyphs, in socket order: probes about what glyphs grant (a use bank, a trigger) read them.</summary>
    public IReadOnlyList<GlyphDef> Glyphs { get; init; } = Array.Empty<GlyphDef>();

    public IEnumerable<JsonObject> Nodes() => Graph.Nodes();
    public IEnumerable<JsonObject> Nodes(string type) => Graph.Nodes(type);
    public bool Has(string type) => Graph.Has(type);

    /// <summary>Whether a node is an effect of the type.</summary>
    public bool ContainsType(JsonObject node, string type) => WeaveGraph.TypeOf(node) == type;

    public double Base(string attribute) => Bases.TryGetValue(attribute, out var v) ? v : 0;

    /// <summary>
    /// True when the weave itself gives an attribute a positive value: a positive base, or a modifier among
    /// the weave's own (preset and glyph modifiers) that adds to it, flat positive or by formula.
    /// </summary>
    public bool Raises(string attribute)
    {
        if (Base(attribute) > 0)
        {
            return true;
        }
        foreach (var m in Modifiers)
        {
            if (m.Attribute != attribute)
            {
                continue;
            }
            if (m.ValueExpr is not null || m.Form is ModifierForm.Link || (m.Form is ModifierForm.Flat or ModifierForm.Override && m.Value > 0))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>A cooldown from the form's expression (not a constant zero) or levied through <c>cooldown.added</c>.</summary>
    public bool HasCooldown => (Form.Cooldown is { } cd && (!cd.IsConstant || cd.ConstantValue > 0)) || Raises("cooldown.added");

    /// <summary>Statuses the graph's <c>apply_status</c> nodes apply, with <c>$thread_status</c> resolved through the weave's threads.</summary>
    public IEnumerable<string> StatusesApplied()
    {
        foreach (var node in Graph.Nodes("apply_status"))
        {
            var status = JsonNum.StringOf(node["status"]) ?? "$thread_status";
            if (status != "$thread_status")
            {
                yield return status;
                continue;
            }
            var threads = ThreadsOf(node);
            if (threads.Count > 0 && Content.Get<ThreadDef>(threads[0])?.Status is { } fromThread)
            {
                yield return fromThread;
            }
        }
    }

    /// <summary>Statuses the weave can inflict: those it applies plus the ailments of the Threads its damage nodes deal.</summary>
    public IEnumerable<string> StatusesInflicted()
    {
        foreach (var s in StatusesApplied())
        {
            yield return s;
        }
        foreach (var node in Graph.Nodes("damage"))
        {
            foreach (var t in ThreadsOf(node))
            {
                if (Content.Get<ThreadDef>(t)?.Status is { } status)
                {
                    yield return status;
                }
            }
        }
    }

    /// <summary>A node's <c>threads</c> parameter resolved as the effect compiler resolves it.</summary>
    public IReadOnlyList<string> ThreadsOf(JsonObject node)
    {
        var weaveThreads = Threads.Select(t => t.Id).ToList();
        switch (node["threads"])
        {
            case null:
                return weaveThreads;
            case JsonValue v when v.TryGetValue<string>(out var s):
                return s switch
                {
                    "$threads" => weaveThreads,
                    "$thread" => weaveThreads.Take(1).ToList(),
                    _ => new[] { s },
                };
            case JsonArray arr:
                return arr.Select(n => JsonNum.StringOf(n) ?? "").Where(s => s.Length > 0).SelectMany(s => s == "$threads" ? weaveThreads : new List<string> { s }).ToList();
            default:
                return weaveThreads;
        }
    }

    /// <summary>A boolean parameter of a node, false when absent.</summary>
    public static bool Flag(JsonObject node, string param) => JsonNum.BoolOf(node[param], false);
}
