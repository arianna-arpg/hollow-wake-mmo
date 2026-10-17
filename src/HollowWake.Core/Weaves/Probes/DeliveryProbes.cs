using System.Text.Json.Nodes;
using HollowWake.Core.Content;
using HollowWake.Core.Effects;

namespace HollowWake.Core.Weaves.Probes;

/// <summary>
/// Mechanism probes about how a weave reaches its targets: trajectories, forks and returns, placements,
/// shapes and movement deliveries. Registered through <see cref="MechanismRegistry.Register"/> when the
/// registry starts.
/// </summary>
internal static class DeliveryProbes
{
    /// <summary>
    /// The trajectory axes that bend or pace a flight, as projectile parameters; each defaults to the weave
    /// attribute <c>projectile.{axis}</c>. Spin turns only the hit shape, so it is not among them.
    /// </summary>
    private static readonly string[] Axes = { "homing", "guide", "erratic", "spiral", "orbit", "wave", "accel", "bounce", "recurve", "converge" };

    public static void Register()
    {
        MechanismRegistry.Register("returning", static (w, _) => Returning(w), description: "projectiles that turn back to their origin or their caster");
        MechanismRegistry.Register("homing", static (w, _) => Axis(w, "homing"), description: "projectiles that home on bodies");
        MechanismRegistry.Register("trajectory", static (w, _) => Trajectory(w), description: "projectiles that fly anything but straight: any trajectory axis");
        MechanismRegistry.Register("chain", static (w, _) => w.Has("chain") || (w.Has("projectile") && w.Raises("projectile.chain")), description: "hits that leap to other bodies, or projectiles that chain");
        MechanismRegistry.Register("shape", static (w, p) => Shaped(w, p), ProbeParameter.OneOf(Shapes.Names.ToArray()), "deliveries drawn as a shape other than a circle, or as the named shape");
        MechanismRegistry.Register("fire", static (w, p) => Fires(w, p), ProbeParameter.OneOf("fan", "volley", "ring", "salvo"), "projectiles fired in a style other than a fan, or in the named style");
        MechanismRegistry.Register("cascade", static (w, _) => w.Has("cascade") || w.Raises("area.cascade"), description: "placements that repeat stepping away");
        MechanismRegistry.Register("storm", static (w, _) => w.Has("scatter"), description: "strikes scattered over a disc");
        MechanismRegistry.Register("telegraph", static (w, _) => Telegraphed(w), description: "placements marked before they land: delayed fields, telegraphed strikes");
        MechanismRegistry.Register("shove", static (w, _) => Shoves(w), description: "knockbacks, pulls and buffets that shove bodies");
    }

    private static string? Text(JsonObject node, string param) => JsonNum.StringOf(node[param]);

    private static bool Returning(WeaveProbe w)
    {
        foreach (var node in w.Nodes("projectile"))
        {
            if (Text(node, "return") is { } mode && mode != "none")
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The weave raises the axis's attribute, or one of its projectiles sets the axis to something other than zero.</summary>
    private static bool Axis(WeaveProbe w, string axis)
    {
        if (!w.Has("projectile"))
        {
            return false;
        }
        if (w.Raises("projectile." + axis))
        {
            return true;
        }
        foreach (var node in w.Nodes("projectile"))
        {
            if (node[axis] is { } value && !(JsonNum.TryDouble(value, out var v) && v == 0))
            {
                return true;
            }
        }
        return false;
    }

    private static bool Trajectory(WeaveProbe w)
    {
        foreach (var axis in Axes)
        {
            if (Axis(w, axis))
            {
                return true;
            }
        }
        return false;
    }

    private static bool Shaped(WeaveProbe w, string? shape)
    {
        foreach (var node in w.Nodes())
        {
            var type = WeaveGraph.TypeOf(node);
            if (type is not ("area" or "field" or "projectile"))
            {
                continue;
            }
            var name = Text(node, "shape") ?? "circle";
            if (shape is null ? name != "circle" : name == shape || (shape == "sector" && name == "cone") || (shape == "cone" && name == "sector"))
            {
                return true;
            }
        }
        return false;
    }

    private static bool Fires(WeaveProbe w, string? style)
    {
        foreach (var node in w.Nodes("projectile"))
        {
            var fire = Text(node, "fire") ?? "fan";
            if (style is null ? fire != "fan" : fire == style)
            {
                return true;
            }
        }
        return false;
    }

    private static bool Telegraphed(WeaveProbe w)
    {
        foreach (var node in w.Nodes("field"))
        {
            if (node["delay"] is { } delay && !(JsonNum.TryDouble(delay, out var d) && d <= 0))
            {
                return true;
            }
        }
        foreach (var node in w.Nodes("scatter"))
        {
            if (node["telegraph"] is { } telegraph && !(JsonNum.TryDouble(telegraph, out var t) && t <= 0))
            {
                return true;
            }
        }
        return false;
    }

    private static bool Shoves(WeaveProbe w)
    {
        foreach (var node in w.Nodes("move"))
        {
            if (Text(node, "kind") is "knockback" or "pull" or "buffet")
            {
                return true;
            }
        }
        return false;
    }
}
