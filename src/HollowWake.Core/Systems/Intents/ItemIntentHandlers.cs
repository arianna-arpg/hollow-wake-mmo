using HollowWake.Core.Components;
using HollowWake.Core.Crafting;
using HollowWake.Core.Ecs;
using HollowWake.Core.Intents;
using HollowWake.Core.Items;
using HollowWake.Core.Loot;

namespace HollowWake.Core.Systems.Intents;

/// <summary>Picking up, equipping, unequipping and dropping items, and crafting.</summary>
public static class ItemIntentHandlers
{
    public static void Register(IntentHandlers handlers)
    {
        handlers.Register<PickUpIntent>(PickUp);
        handlers.Register<EquipIntent>(Equip);
        handlers.Register<UnequipIntent>(Unequip);
        handlers.Register<DropItemIntent>(DropItem);
        handlers.Register<CraftStartIntent>(CraftStart);
        handlers.Register<CraftSubmitIntent>(CraftSubmit);
    }

    private static void PickUp(in IntentContext ctx, PickUpIntent p)
    {
        if (!ctx.Sim.Service<LootService>().PickUp(ctx.Sim, ctx.Entity, p.Drop, out var reason))
        {
            ctx.Reject(reason);
        }
    }

    private static void Equip(in IntentContext ctx, EquipIntent eq)
    {
        var inv = ctx.Sim.World.Get<Inventory>(ctx.Entity);
        var item = inv?.Find(eq.ItemUid);
        if (item is null)
        {
            ctx.Reject("no_item");
            return;
        }
        if (!ctx.Sim.Service<EquipmentService>().Equip(ctx.Sim, ctx.Entity, item, eq.Slot, fromInventory: true, out var reason))
        {
            ctx.Reject(reason);
        }
    }

    private static void Unequip(in IntentContext ctx, UnequipIntent un)
    {
        if (!ctx.Sim.Service<EquipmentService>().Unequip(ctx.Sim, ctx.Entity, un.Slot, out var reason))
        {
            ctx.Reject(reason);
        }
    }

    private static void DropItem(in IntentContext ctx, DropItemIntent di)
    {
        var inv = ctx.Sim.World.Get<Inventory>(ctx.Entity);
        var item = inv?.Find(di.ItemUid);
        var transform = ctx.Transform;
        if (item is null || inv is null || transform is null)
        {
            ctx.Reject("no_item");
            return;
        }
        inv.Remove(item);
        ctx.Sim.Service<LootService>().Drop(ctx.Sim, item, transform.Position, Entity.None, ctx.Entity);
    }

    private static void CraftStart(in IntentContext ctx, CraftStartIntent cs)
    {
        if (ctx.Sim.Service<CraftingService>().Start(ctx.Sim, ctx.Entity, cs.RecipeId, cs.Station, out var reason) is null)
        {
            ctx.Reject(reason);
        }
    }

    private static void CraftSubmit(in IntentContext ctx, CraftSubmitIntent csub)
    {
        if (ctx.Sim.Service<CraftingService>().Submit(ctx.Sim, ctx.Entity, csub.RecipeId, csub.Input, out var reason) is null)
        {
            ctx.Reject(reason);
        }
    }
}
