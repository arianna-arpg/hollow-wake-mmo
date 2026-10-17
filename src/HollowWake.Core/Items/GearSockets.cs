using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Items;

/// <summary>
/// Gear sockets (row I7). A gear item minted with sockets (<see cref="ItemInstance.Sockets"/>) holds socketable stones,
/// items whose tags meet rule <c>inventory.sockets.accepts</c>; a socketed stone lends its lines to the host, so they
/// are the wearer's while the host is worn. Stones go in from the bag and come out to the bag, and nothing is lost:
/// a socket that holds a stone gives it back to the bag before another goes in, and a stone without room stays put.
/// A bench adds sockets up to the cap of the item's class.
/// </summary>
public static class GearSockets
{
    /// <summary>The item tags a socket takes, from rule <c>inventory.sockets.accepts</c>.</summary>
    public static TagSet Accepts(ContentRegistry content)
    {
        var tags = content.Rules.Strings("inventory.sockets.accepts");
        return tags.Count == 0 ? TagSet.Of("vestige") : TagSet.From(tags);
    }

    public static bool IsSocketable(Simulation sim, ItemInstance item) =>
        sim.Service<ItemResolver>().TagsOf(item).HasAny(Accepts(sim.Content));

    /// <summary>A host the hero has in the bag or wears, and whether it is worn.</summary>
    private static bool FindHost(Simulation sim, Entity hero, long uid, out ItemInstance host, out bool worn)
    {
        host = null!;
        worn = false;
        if (!sim.Service<InventoryService>().Locate(sim, hero, uid, out var found, out var location) || found is null)
        {
            return false;
        }
        if (location.Container is not (InventoryService.Bag or InventoryService.Equipment))
        {
            return false;
        }
        host = found;
        worn = location.Container == InventoryService.Equipment;
        return true;
    }

    /// <summary>Sockets a stone from the bag into a socket of a gear item in the bag or worn.</summary>
    public static bool Socket(Simulation sim, Entity hero, long stoneUid, long hostUid, int socket, out string reason)
    {
        if (!FindHost(sim, hero, hostUid, out var host, out var worn))
        {
            reason = "no_host";
            return false;
        }
        if (host.Sockets is not { } sockets || sockets.Count == 0)
        {
            reason = "no_sockets";
            return false;
        }
        if (socket < 0 || socket >= sockets.Count)
        {
            reason = "no_socket";
            return false;
        }
        if (!sim.World.TryGet<Inventory>(hero, out var inv) || inv.Find(stoneUid) is not { } stone || ReferenceEquals(stone, host))
        {
            reason = "no_item";
            return false;
        }
        if (!IsSocketable(sim, stone))
        {
            reason = "not_socketable";
            return false;
        }
        if (sim.World.TryGet<Stats>(hero, out var stats) && !sim.Service<EquipmentService>().MeetsRequirements(stats, stone, out var failing))
        {
            reason = "requires:" + failing;
            return false;
        }
        int? sx = stone.X, sy = stone.Y;
        var fromOverflow = inv.Overflow.Contains(stone);
        inv.Remove(stone);
        if (sockets[socket] is { } previous && !RackService.Stow(inv, new List<ItemInstance> { previous }, sx, sy))
        {
            RackService.Restore(inv, stone, sx, sy, fromOverflow);
            reason = "bag_full";
            return false;
        }
        if (sockets[socket] is { } old)
        {
            sim.Events.Publish(new GearSocketChanged(hero, host.Uid, socket, old.Uid, false) { Tick = sim.Clock.Tick });
        }
        sockets[socket] = stone;
        stone.X = null;
        stone.Y = null;
        if (worn)
        {
            sim.Service<EquipmentService>().Recalculate(sim, hero);
        }
        sim.Events.Publish(new GearSocketChanged(hero, host.Uid, socket, stone.Uid, true) { Tick = sim.Clock.Tick });
        reason = "";
        return true;
    }

    /// <summary>Takes a stone out of a gear item's socket into the bag, at a cell when one is given.</summary>
    public static bool Unsocket(Simulation sim, Entity hero, long hostUid, int socket, int? x, int? y, out string reason)
    {
        if (!FindHost(sim, hero, hostUid, out var host, out var worn))
        {
            reason = "no_host";
            return false;
        }
        if (host.Sockets is not { } sockets || socket < 0 || socket >= sockets.Count || sockets[socket] is not { } stone)
        {
            reason = "empty_socket";
            return false;
        }
        if (!sim.World.TryGet<Inventory>(hero, out var inv) || !RackService.Stow(inv, new List<ItemInstance> { stone }, x, y))
        {
            reason = "bag_full";
            return false;
        }
        sockets[socket] = null;
        if (worn)
        {
            sim.Service<EquipmentService>().Recalculate(sim, hero);
        }
        sim.Events.Publish(new GearSocketChanged(hero, host.Uid, socket, stone.Uid, false) { Tick = sim.Clock.Tick });
        reason = "";
        return true;
    }

    /// <summary>How many sockets the item could hold at most: the cap of its class.</summary>
    public static int CapOf(Simulation sim, ItemInstance item) =>
        sim.Content.TryGet<ItemBaseDef>(item.Base, out var b) && b is not null && b.Slot is not null && b.Stack <= 1 ? ItemGenerator.SocketCap(sim.Content, b.Class) : 0;

    /// <summary>Adds an empty socket to a gear item, up to its class cap (a bench service).</summary>
    public static bool AddSocket(Simulation sim, ItemInstance item, out string reason)
    {
        var cap = CapOf(sim, item);
        var have = item.Sockets?.Count ?? 0;
        if (cap <= 0)
        {
            reason = "no_sockets";
            return false;
        }
        if (have >= cap)
        {
            reason = "socket_cap";
            return false;
        }
        (item.Sockets ??= new List<ItemInstance?>()).Add(null);
        reason = "";
        return true;
    }
}
