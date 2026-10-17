using HollowWake.Core.Definitions;

namespace HollowWake.Core.Weaves.Probes;

/// <summary>
/// Mechanism probes about how a weave is cast: cast modes, triggers, toggles, strike timing, releases,
/// charge spends, repeats and slot behaviors. Registered through <see cref="MechanismRegistry.Register"/>
/// when the registry starts. The trigger permit (<c>trigger_permit</c>) is a declared mechanism, not a probe:
/// a glyph that lifts the cast-time limit on triggered casts declares it.
/// </summary>
internal static class CastingProbes
{
    public static void Register()
    {
        MechanismRegistry.Register("triggered", static (w, _) => Triggered(w), description: "the weave casts itself when an event happens to its caster");
        MechanismRegistry.Register("toggle", static (w, _) => w.Form.Toggle is not null, description: "the weave is pressed on and off");
        MechanismRegistry.Register("overcharge", static (w, _) => w.Form.CastMode == CastMode.Overcharge, description: "an overcharge that banks stages");
        MechanismRegistry.Register("timing", static (w, _) => w.Form.Timing is not null, description: "a cast bar that rewards a timed second press");
        MechanismRegistry.Register("release", static (w, _) => w.Form.CastMode is CastMode.Charge or CastMode.Overcharge || w.Form.Channel?.Release is not null || w.Form.Guard?.Bash is not null, description: "a payload that fires when a hold ends: a charge-up, an overcharge, a channel's release or a guard's bash");
        MechanismRegistry.Register("ramps", static (w, _) => w.Form.Channel is { Ramps.Count: > 0 }, description: "a channel whose numbers grow with held time");
        MechanismRegistry.Register("spends", static (w, _) => Spends(w), description: "the weave spends charges when it resolves");
        MechanismRegistry.Register("repeats", static (w, _) => w.Raises("repeat.count"), description: "the weave repeats after it resolves");
        MechanismRegistry.Register("convert", static (w, _) => w.Form.Convert is not null || Any(w, static g => g.Convert is not null), description: "the slot casts another weave while a condition holds");
        MechanismRegistry.Register("meta", static (w, _) => w.Form.MetaAction is not null || Any(w, static g => g.MetaAction is not null), description: "the slot has a second ability");
    }

    private static bool Triggered(WeaveProbe w) => w.Form.Trigger is not null || Any(w, static g => g.Trigger is not null);

    private static bool Spends(WeaveProbe w) => w.Form.Spend is not null || Any(w, static g => g.Spend is not null);

    private static bool Any(WeaveProbe w, Func<GlyphDef, bool> test)
    {
        foreach (var g in w.Glyphs)
        {
            if (test(g))
            {
                return true;
            }
        }
        return false;
    }
}
