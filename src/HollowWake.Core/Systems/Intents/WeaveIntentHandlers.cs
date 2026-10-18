using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Intents;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Systems.Intents;

/// <summary>
/// Casting from a slot, cancelling, binding a weave to a slot, the held-input intents of the cast modes
/// (release, a second press, the live aim), meta actions and swapping rack seats. Held input that finds
/// nothing held is ignored rather than refused: a client may let go of a key just after its cast ended.
/// Any caster sends them; binding a weave changes a character's loadout and is a player's only.
/// </summary>
public static class WeaveIntentHandlers
{
    public static void Register(IntentHandlers handlers)
    {
        handlers.Register<UseWeaveIntent>(UseWeave);
        handlers.Register<CancelCastIntent>(CancelCast);
        handlers.Register<SetWeaveIntent>(SetWeave, playerOnly: true);
        handlers.Register<ReleaseWeaveIntent>(Release);
        handlers.Register<PressWeaveIntent>(Press);
        handlers.Register<AimIntent>(Aim);
        handlers.Register<UseMetaIntent>(UseMeta);
        handlers.Register<SwapWeaveSlotsIntent>(Swap);
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
    /// service is permission). Changing the loadout follows rule <c>casting.swap_requires</c>.
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
        var runtime = sim.Service<WeaveRuntime>();
        if (!runtime.CanChangeLoadout(sim, ctx.Entity, out var busy))
        {
            ctx.Reject(busy);
            return;
        }
        if (sw.Slot < book.Slots.Count && book.Casting is { } cast && ReferenceEquals(cast.Slot, book.Slots[sw.Slot]))
        {
            runtime.Interrupt(sim, ctx.Entity);
        }
        book.Set(sw.Slot, new WeaveSlot { Spec = sw.Spec, Compiled = compiled, Dormant = compiled!.Dormant });
    }

    private static void Release(in IntentContext ctx, ReleaseWeaveIntent r) => ctx.Sim.Service<WeaveRuntime>().Release(ctx.Sim, ctx.Entity, r.Slot);

    private static void Press(in IntentContext ctx, PressWeaveIntent p) => ctx.Sim.Service<WeaveRuntime>().Press(ctx.Sim, ctx.Entity, p.Slot);

    private static void Aim(in IntentContext ctx, AimIntent a) => ctx.Sim.Service<WeaveRuntime>().Aim(ctx.Sim, ctx.Entity, a.Point, a.Direction);

    private static void UseMeta(in IntentContext ctx, UseMetaIntent m)
    {
        if (!ctx.Sim.Service<WeaveRuntime>().UseMeta(ctx.Sim, ctx.Entity, m.Slot, out var reason))
        {
            ctx.Reject(reason);
        }
    }

    private static void Swap(in IntentContext ctx, SwapWeaveSlotsIntent s)
    {
        if (!ctx.Sim.Service<WeaveRuntime>().Swap(ctx.Sim, ctx.Entity, s.A, s.B, out var reason))
        {
            ctx.Reject(reason);
        }
    }
}
