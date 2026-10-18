using HollowWake.Core.Components;
using HollowWake.Core.Crafting;
using HollowWake.Core.Ecs;
using HollowWake.Core.Intents;
using HollowWake.Core.Items;
using HollowWake.Core.Loot;

namespace HollowWake.Core.Systems.Intents;

/// <summary>Picking up, equipping, unequipping and dropping items, and crafting: a character's bag and gear, so players only.</summary>
public static class ItemIntentHandlers
{
    public static void Register(IntentHandlers handlers)
    {
        handlers.Register<PickUpIntent>(PickUp, playerOnly: true);
        handlers.Register<EquipIntent>(Equip, playerOnly: true);
        handlers.Register<UnequipIntent>(Unequip, playerOnly: true);
        handlers.Register<DropItemIntent>(DropItem, playerOnly: true);
        handlers.Register<CraftStartIntent>(CraftStart, playerOnly: true);
        handlers.Register<CraftSubmitIntent>(CraftSubmit, playerOnly: true);
        handlers.Register<MoveItemIntent>(MoveItem, playerOnly: true);
        handlers.Register<SocketStoneIntent>(SocketStone, playerOnly: true);
        handlers.Register<UnsocketStoneIntent>(UnsocketStone, playerOnly: true);
        handlers.Register<LearnFormIntent>(LearnForm, playerOnly: true);
        handlers.Register<UnlearnFormIntent>(UnlearnForm, playerOnly: true);
        handlers.Register<BuyIntent>(Buy, playerOnly: true);
        handlers.Register<SellIntent>(Sell, playerOnly: true);
        handlers.Register<BuyBackIntent>(BuyBack, playerOnly: true);
        handlers.Register<BuyPageIntent>(BuyPage, playerOnly: true);
        handlers.Register<StationServiceIntent>(StationService, playerOnly: true);
    }

    private static void BuyPage(in IntentContext ctx, BuyPageIntent b)
    {
        if (!ctx.Sim.TryService<ContainerService>(out var containers))
        {
            ctx.Reject("no_stash");
            return;
        }
        if (!containers.Reachable(ctx.Sim, ctx.Entity, ContainerService.PageId(b.Stash, 1), out _))
        {
            ctx.Reject("out_of_reach");
            return;
        }
        if (!containers.BuyPage(ctx.Sim, ctx.Entity, b.Stash, out var reason))
        {
            ctx.Reject(reason);
        }
    }

    private static void StationService(in IntentContext ctx, StationServiceIntent s)
    {
        if (!StationServices.Perform(ctx.Sim, ctx.Entity, s.Station, s.Service, new StationServices.Target(s.Item, s.Slot, s.Node), out var reason))
        {
            ctx.Reject(reason);
        }
    }

    private static void Buy(in IntentContext ctx, BuyIntent b)
    {
        if (!ctx.Sim.Service<VendorService>().Buy(ctx.Sim, ctx.Entity, b.Vendor, b.Stock, b.Count, out var reason))
        {
            ctx.Reject(reason);
        }
    }

    private static void Sell(in IntentContext ctx, SellIntent s)
    {
        if (!ctx.Sim.Service<VendorService>().Sell(ctx.Sim, ctx.Entity, s.Vendor, s.ItemUid, s.Count, out var reason))
        {
            ctx.Reject(reason);
        }
    }

    private static void BuyBack(in IntentContext ctx, BuyBackIntent b)
    {
        if (!ctx.Sim.Service<VendorService>().BuyBack(ctx.Sim, ctx.Entity, b.Vendor, b.ItemUid, out var reason))
        {
            ctx.Reject(reason);
        }
    }

    private static void SocketStone(in IntentContext ctx, SocketStoneIntent s)
    {
        var done = s.Host is { } host
            ? GearSockets.Socket(ctx.Sim, ctx.Entity, s.StoneUid, host, s.Socket, out var reason)
            : ctx.Sim.Service<RackService>().Socket(ctx.Sim, ctx.Entity, s.StoneUid, s.Slot, s.Socket, out reason);
        if (!done)
        {
            ctx.Reject(reason);
        }
    }

    private static void UnsocketStone(in IntentContext ctx, UnsocketStoneIntent u)
    {
        var done = u.Host is { } host
            ? GearSockets.Unsocket(ctx.Sim, ctx.Entity, host, u.Socket, u.X, u.Y, out var reason)
            : ctx.Sim.Service<RackService>().Unsocket(ctx.Sim, ctx.Entity, u.Slot, u.Socket, u.X, u.Y, out reason);
        if (!done)
        {
            ctx.Reject(reason);
        }
    }

    private static void LearnForm(in IntentContext ctx, LearnFormIntent l)
    {
        if (!ctx.Sim.Service<RackService>().Learn(ctx.Sim, ctx.Entity, l.ItemUid, l.Slot, out var reason))
        {
            ctx.Reject(reason);
        }
    }

    private static void UnlearnForm(in IntentContext ctx, UnlearnFormIntent u)
    {
        if (!ctx.Sim.Service<RackService>().Unlearn(ctx.Sim, ctx.Entity, u.Slot, u.X, u.Y, out var reason))
        {
            ctx.Reject(reason);
        }
    }

    /// <summary>Moves an item to a board cell, an equipment slot or the world.</summary>
    private static void MoveItem(in IntentContext ctx, MoveItemIntent mi)
    {
        if (!ctx.Sim.Service<InventoryService>().Move(ctx.Sim, ctx.Entity, mi.ItemUid, mi.Container, mi.X, mi.Y, mi.Slot, out var reason))
        {
            ctx.Reject(reason);
        }
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
