using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Intents;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Systems.Intents;

/// <summary>Casting from a slot, cancelling, binding a weave to a slot, and the held-input intents of the cast modes.</summary>
public static class WeaveIntentHandlers
{
    public static void Register(IntentHandlers handlers)
    {
        handlers.Register<UseWeaveIntent>(UseWeave);
        handlers.Register<CancelCastIntent>(CancelCast);
        handlers.Register<SetWeaveIntent>(SetWeave);
        // Held input: accepted and ignored until the cast modes that read them land.
        handlers.Register<ReleaseWeaveIntent>(static (in IntentContext _, ReleaseWeaveIntent _) => { });
        handlers.Register<PressWeaveIntent>(static (in IntentContext _, PressWeaveIntent _) => { });
        handlers.Register<AimIntent>(static (in IntentContext _, AimIntent _) => { });
        handlers.Register<UseMetaIntent>(static (in IntentContext _, UseMetaIntent _) => { });
        handlers.Register<SwapWeaveSlotsIntent>(static (in IntentContext _, SwapWeaveSlotsIntent _) => { });
    }

    private static void UseWeave(in IntentContext ctx, UseWeaveIntent u)
    {
        var sim = ctx.Sim;
        if (!sim.World.TryGet<WeaveBook>(ctx.Entity, out var book) || u.Slot < 0 || u.Slot >= book.Slots.Count)
        {
            ctx.Reject("no_slot");
            return;
        }
        var target = sim.World.IsAlive(u.Target) ? u.Target : Entity.None;
        if (!sim.Service<WeaveRuntime>().TryBegin(sim, ctx.Entity, book.Slots[u.Slot], target, u.Point, u.Direction, out var reason))
        {
            ctx.Reject(reason);
        }
    }

    private static void CancelCast(in IntentContext ctx, CancelCastIntent _) => ctx.Sim.Service<WeaveRuntime>().Interrupt(ctx.Sim, ctx.Entity);

    /// <summary>
    /// Binds a weave to a slot. Misfit glyphs stay socketed but dormant, with their reasons on the slot; only a
    /// weave that cannot compile at all is refused. While rule <c>casting.require_ownership</c> holds, the
    /// hero must own what the spec names, as the registered <see cref="IWeaveOwnership"/> decides (a missing
    /// service is permission).
    /// </summary>
    private static void SetWeave(in IntentContext ctx, SetWeaveIntent sw)
    {
        var sim = ctx.Sim;
        var book = sim.World.GetOrAdd<WeaveBook>(ctx.Entity);
        var compiler = sim.Service<WeaveCompiler>();
        if (!compiler.TryCompile(sw.Spec, out var compiled, out var error, GlyphFit.Dormant))
        {
            ctx.Reject("invalid:" + error);
            return;
        }
        var maxSlots = (int)sim.Content.Rules.Number("weaves.max_slots", 8);
        if (sw.Slot < 0 || sw.Slot >= maxSlots)
        {
            ctx.Reject("bad_slot");
            return;
        }
        if (sim.Content.Rules.Flag("casting.require_ownership", false) && sim.TryService<IWeaveOwnership>(out var ownership)
            && !ownership.Allows(sim, ctx.Entity, sw.Slot, sw.Spec, out var refusal))
        {
            ctx.Reject(refusal);
            return;
        }
        while (book.Slots.Count <= sw.Slot)
        {
            book.Slots.Add(new WeaveSlot { Spec = new Definitions.WeaveSpec { Preset = null, Form = null } });
        }
        book.Slots[sw.Slot] = new WeaveSlot { Spec = sw.Spec, Compiled = compiled, Dormant = compiled!.Dormant };
    }
}
