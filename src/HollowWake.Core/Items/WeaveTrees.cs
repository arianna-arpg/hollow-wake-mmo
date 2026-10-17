using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Items;

/// <summary>
/// Buying nodes of a weave's own tree for a hero's weave slot. A purchase appends the node to the slot's spec
/// (<c>WeaveSpec.Tree</c>) with the semantics of a set-weave: the spec must compile, ownership must allow it while
/// rule <c>casting.require_ownership</c> holds, and the loadout must be free to change. The compiler decides whether
/// the node counts (linked, not excluded, within its ranks and the points of the weave's level); a node that would
/// not is refused with the compiler's reason, and nothing changes. The slot keeps its cooldown and state, and the
/// rack keeps the tree with the seat while it still teaches the same weave.
/// </summary>
public static class WeaveTrees
{
    public static bool Buy(Simulation sim, Entity hero, int slotIndex, string node, out string reason)
    {
        if (!sim.World.TryGet<WeaveBook>(hero, out var book) || slotIndex < 0 || slotIndex >= book.Slots.Count)
        {
            reason = "no_slot";
            return false;
        }
        var slot = book.Slots[slotIndex];
        var current = slot.Spec;
        if (current.Preset is null && current.Form is null)
        {
            reason = "no_weave";
            return false;
        }
        var runtime = sim.Service<WeaveRuntime>();
        var before = runtime.Resolve(sim, slot, out _);
        if (before?.Form.Tree is null)
        {
            reason = "no_tree";
            return false;
        }
        var tree = new List<string>(current.Tree) { node };
        var spec = current with { Tree = tree };
        if (!sim.Service<WeaveCompiler>().TryCompile(spec, out var compiled, out var error, GlyphFit.Dormant, slot.Grafts) || compiled is null)
        {
            reason = "invalid:" + error;
            return false;
        }
        // The node must count: one more dormant entry for it means the compiler would not take it, and says why.
        if (DormantOf(compiled, node, out var why) > DormantOf(before, node, out _))
        {
            reason = why!;
            return false;
        }
        if (sim.Content.Rules.Flag("casting.require_ownership", false) && sim.TryService<IWeaveOwnership>(out var ownership)
            && !ownership.Allows(sim, hero, slotIndex, spec, out reason))
        {
            return false;
        }
        if (!runtime.CanChangeLoadout(sim, hero, out reason))
        {
            return false;
        }
        if (book.Casting is { } cast && ReferenceEquals(cast.Slot, slot))
        {
            runtime.Interrupt(sim, hero);
        }
        slot.Spec = spec;
        slot.Compiled = null;
        book.Changed();
        sim.Events.Publish(new WeaveTreeNodeBought(hero, slotIndex, slot.Key, node) { Tick = sim.Clock.Tick });
        reason = "";
        return true;
    }

    /// <summary>How many of a weave's dormant tree entries name the node, and the reason of the last of them.</summary>
    private static int DormantOf(CompiledWeave weave, string node, out string? reason)
    {
        var count = 0;
        reason = null;
        foreach (var dormant in weave.DormantNodes)
        {
            if (dormant.Id == node)
            {
                count++;
                reason = dormant.Reason;
            }
        }
        return count;
    }
}
