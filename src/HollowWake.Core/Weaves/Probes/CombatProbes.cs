using HollowWake.Core.Content;
using HollowWake.Core.Definitions;

namespace HollowWake.Core.Weaves.Probes;

/// <summary>
/// Mechanism probes about what a weave's hits do: statuses and their banks, charges, leech and procs.
/// Registered through <see cref="MechanismRegistry.Register"/> when the registry starts.
/// </summary>
internal static class CombatProbes
{
    public static void Register()
    {
        MechanismRegistry.Register("charges", static (w, p) => Nodes(w, "charge", "id", p), ProbeParameter.Any, "the graph gains, spends or drains charges, or charges of the named kind");
        MechanismRegistry.Register("rupture", static (w, p) => Nodes(w, "rupture", "status", p), ProbeParameter.Status, "the graph ruptures statuses, or the named status");
        MechanismRegistry.Register("consumes", static (w, p) => Nodes(w, "consume_status", "status", p), ProbeParameter.Status, "the graph consumes statuses, or the named status");
        MechanismRegistry.Register("contagion", static (w, _) => w.Has("spread_status") || w.Has("move_status"), description: "the graph spreads or moves statuses between bodies");
        MechanismRegistry.Register("leech", static (w, _) => Leeches(w), description: "the weave's hits leech: a damage node with leech, or the weave raises a leech attribute");
        MechanismRegistry.Register("bank", static (w, p) => Banks(w, p), ProbeParameter.Status, "the weave applies a status that banks damage, or the named one");
    }

    /// <summary>Whether the graph has a node of the type, and, with a parameter, one whose field names it.</summary>
    private static bool Nodes(WeaveProbe w, string type, string field, string? parameter)
    {
        foreach (var node in w.Nodes(type))
        {
            if (parameter is null || JsonNum.StringOf(node[field]) == parameter)
            {
                return true;
            }
        }
        return false;
    }

    private static bool Leeches(WeaveProbe w)
    {
        foreach (var node in w.Nodes("damage"))
        {
            if (node["leech"] is not null)
            {
                return true;
            }
        }
        foreach (var m in w.Modifiers)
        {
            if (m.Attribute.StartsWith("leech.", StringComparison.Ordinal) && w.Raises(m.Attribute))
            {
                return true;
            }
        }
        return false;
    }

    private static bool Banks(WeaveProbe w, string? status)
    {
        foreach (var id in w.StatusesApplied())
        {
            if (status is not null && id != status)
            {
                continue;
            }
            if (w.Content.Get<StatusDef>(id)?.Bank is not null)
            {
                return true;
            }
        }
        return false;
    }
}
