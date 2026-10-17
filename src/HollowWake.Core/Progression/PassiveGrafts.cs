using System.Globalization;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Items;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Progression;

/// <summary>
/// Passive grafts (row H6). A node, or an option picked on a choice node, may grant a glyph; its owner binds the
/// graft to one weave slot, and it is grafted onto that seat the way slot grafts are: a modifier on the attribute
/// family <c>glyph.slot{n}.{glyph}</c> (the value is the glyph's level) under source <c>graft:{source}</c>, which the
/// weave runtime reads when it arms the book. A socketed copy of the glyph wins over the graft and a graft that
/// misfits goes dormant, as the runtime decides. Refunding the node or the pick takes the graft and its binding away.
/// </summary>
public static class PassiveGrafts
{
    /// <summary>A graft the hero owns: its source key and the glyph it grants.</summary>
    public readonly record struct Owned(string Source, string Node, string? Option, PassiveGlyphGrant Glyph);

    public static string SourceOf(string node, string? option) => option is null ? node : node + ":" + option;

    /// <summary>Every graft the hero owns: from allocated nodes, then from the options picked on them.</summary>
    public static List<Owned> Of(Simulation sim, Entity hero)
    {
        var owned = new List<Owned>();
        if (!sim.World.TryGet<Stats>(hero, out var stats))
        {
            return owned;
        }
        var state = sim.World.Get<PassiveState>(hero);
        foreach (var nodeId in stats.Passives)
        {
            if (sim.Content.Get<PassiveNodeDef>(nodeId) is not { } node)
            {
                continue;
            }
            if (node.Glyph is { } glyph)
            {
                owned.Add(new Owned(nodeId, nodeId, null, glyph));
            }
            if (node.Choice is { } choice && state is not null && state.Picks.TryGetValue(nodeId, out var picks))
            {
                var options = PassiveChoices.OptionsOf(sim.Content, choice.Group);
                foreach (var pick in picks)
                {
                    foreach (var option in options)
                    {
                        if (option.Id == pick && option.Glyph is { } granted)
                        {
                            owned.Add(new Owned(SourceOf(nodeId, pick), nodeId, pick, granted));
                        }
                    }
                }
            }
        }
        return owned;
    }

    /// <summary>Binds a graft the hero owns to a weave slot (a slot below zero unbinds it), or says why not.</summary>
    public static bool Bind(Simulation sim, Entity hero, string source, int slot, out string reason)
    {
        var owned = Of(sim, hero);
        if (owned.FindIndex(o => o.Source == source) < 0)
        {
            reason = "no_graft";
            return false;
        }
        var state = sim.World.GetOrAdd<PassiveState>(hero);
        if (slot >= 0 && (!sim.World.TryGet<WeaveBook>(hero, out var book) || slot >= book.Slots.Count))
        {
            reason = "no_slot";
            return false;
        }
        if (!LoadoutGate.Allows(sim, hero, out reason))
        {
            return false;
        }
        if (slot < 0)
        {
            state.Grafts.Remove(source);
        }
        else
        {
            state.Grafts[source] = slot;
        }
        Apply(sim, hero);
        reason = "";
        return true;
    }

    /// <summary>
    /// Puts the bound grafts on the hero as <c>glyph.slot{n}.{glyph}</c> modifiers, dropping bindings of grafts the
    /// hero no longer owns, and has the weave book re-armed so the seats read them.
    /// </summary>
    public static void Apply(Simulation sim, Entity hero)
    {
        if (!sim.World.TryGet<Stats>(hero, out var stats) || !sim.World.TryGet<PassiveState>(hero, out var state))
        {
            return;
        }
        foreach (var source in state.GraftSources)
        {
            stats.Attributes.RemoveSource(source);
        }
        var changed = state.GraftSources.Count > 0;
        state.GraftSources.Clear();
        if (state.Grafts.Count > 0)
        {
            var owned = Of(sim, hero);
            List<string>? stale = null;
            foreach (var (source, slot) in state.Grafts)
            {
                var index = owned.FindIndex(o => o.Source == source);
                if (index < 0)
                {
                    (stale ??= new List<string>()).Add(source);
                    continue;
                }
                var glyph = owned[index].Glyph;
                var modifierSource = "graft:" + source;
                stats.Attributes.Add(new Modifier
                {
                    Attribute = "glyph.slot" + (slot + 1).ToString(CultureInfo.InvariantCulture) + "." + glyph.Id,
                    Form = ModifierForm.Flat,
                    Value = Math.Max(1, glyph.Level),
                    Source = modifierSource,
                });
                state.GraftSources.Add(modifierSource);
                changed = true;
            }
            if (stale is not null)
            {
                foreach (var source in stale)
                {
                    state.Grafts.Remove(source);
                }
            }
        }
        if (changed && sim.World.TryGet<WeaveBook>(hero, out var book))
        {
            // Re-armed now rather than at the next cast, so the seats (and what the interface shows of them) carry the graft.
            book.Changed();
            if (sim.TryService<Weaves.WeaveRuntime>(out var runtime))
            {
                runtime.EnsureArmed(sim, hero, book, stats);
            }
        }
    }

    /// <summary>Forgets the bindings of a node's grafts (its own and its picks'), as a refund does.</summary>
    public static void Forget(PassiveState state, string node)
    {
        List<string>? gone = null;
        foreach (var source in state.Grafts.Keys)
        {
            if (source == node || (source.Length > node.Length && source[node.Length] == ':' && source.StartsWith(node, StringComparison.Ordinal)))
            {
                (gone ??= new List<string>()).Add(source);
            }
        }
        if (gone is not null)
        {
            foreach (var source in gone)
            {
                state.Grafts.Remove(source);
            }
        }
    }
}
