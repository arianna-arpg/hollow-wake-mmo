using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Items;

/// <summary>
/// Whether a hero owns the weave they ask a slot to hold (question Q1): the codex learned into that seat teaches
/// the spec's preset or form at the spec's level, the spec's glyphs are the stones socketed there in socket order
/// (ids, levels and rows), and every Thread the spec weaves is one the hero is attuned to. The weave runtime asks
/// while rule <c>casting.require_ownership</c> holds; monsters, tools and tests compose freely.
/// </summary>
public sealed class WeaveOwnership : IWeaveOwnership
{
    public bool Allows(Simulation sim, Entity hero, int slot, WeaveSpec spec, out string reason)
    {
        var rackService = sim.Service<RackService>();
        rackService.Reconcile(sim, hero);
        var rack = sim.World.Get<Rack>(hero);
        if (rack is null || slot < 0 || slot >= rack.Seats.Count || rack.Seats[slot].Codex?.Payload is not { } payload)
        {
            reason = "not_learned";
            return false;
        }
        var seat = rack.Seats[slot];
        if (!RackService.Teaches(sim, payload, spec))
        {
            reason = "not_learned";
            return false;
        }
        if (spec.Level != Math.Max(1, payload.Level))
        {
            reason = "level_not_owned";
            return false;
        }
        var owned = WeaveItems.SpecOf(seat)!.Glyphs;
        if (owned.Count != spec.Glyphs.Count)
        {
            reason = "glyph_not_owned";
            return false;
        }
        for (var i = 0; i < owned.Count; i++)
        {
            if (!SameGlyph(owned[i], spec.Glyphs[i]))
            {
                reason = "glyph_not_owned";
                return false;
            }
        }
        var attuned = Attuned(sim, hero);
        foreach (var thread in spec.Threads)
        {
            if (!attuned.Contains(thread))
            {
                reason = "not_attuned:" + thread;
                return false;
            }
        }
        reason = "";
        return true;
    }

    private static bool SameGlyph(GlyphRef owned, GlyphRef asked)
    {
        if (owned.Id != asked.Id || (owned.Level ?? 1) != (asked.Level ?? owned.Level ?? 1) || owned.Rows.Count != asked.Rows.Count)
        {
            return false;
        }
        foreach (var (axis, row) in owned.Rows)
        {
            if (!asked.Rows.TryGetValue(axis, out var other) || other != row)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// The Threads a hero may weave: their calling's, then any Thread an attribute <c>attune.{thread}</c> above zero
    /// grants (from passives, gear or holdings), in content order.
    /// </summary>
    public static List<string> Attuned(Simulation sim, Entity hero)
    {
        var threads = new List<string>();
        if (sim.World.TryGet<Identity>(hero, out var identity))
        {
            foreach (var tag in identity.Tags)
            {
                if (tag.StartsWith("calling.", StringComparison.Ordinal) && sim.Content.Get<CallingDef>(tag["calling.".Length..]) is { } calling)
                {
                    foreach (var t in calling.Threads)
                    {
                        if (!threads.Contains(t))
                        {
                            threads.Add(t);
                        }
                    }
                }
            }
        }
        if (sim.World.TryGet<Stats>(hero, out var stats))
        {
            foreach (var thread in sim.Content.Each<ThreadDef>())
            {
                var attribute = "attune." + thread.Id;
                if (!threads.Contains(thread.Id) && sim.Content.Attributes.Contains(attribute) && stats.Get(attribute) > 0)
                {
                    threads.Add(thread.Id);
                }
            }
        }
        return threads;
    }
}
