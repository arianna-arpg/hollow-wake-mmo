using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Loot;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Items;

/// <summary>
/// Moves a hero's items between the places they are kept: the bag board, the equipment doll, the world, and
/// the boards that register themselves here (containers, the weave rack). One move intent is one call; the
/// server decides whether it can happen and what it displaces, and the hero's states show the result.
/// </summary>
public sealed class InventoryService
{
    public const string Bag = "bag";
    public const string Equipment = "equipment";
    public const string World = "world";
    public const string Overflow = "overflow";

    /// <summary>Where an item is now.</summary>
    public readonly record struct Location(string Container, string? Slot);

    /// <summary>
    /// A place items can be moved into or out of beyond the bag, the doll and the world: containers and the rack
    /// register here. <see cref="Find"/> locates an item it holds; <see cref="Take"/> removes it;
    /// <see cref="Put"/> accepts one at a cell.
    /// </summary>
    public interface IItemPlace
    {
        /// <summary>Whether this place answers to a container id.</summary>
        bool Handles(string container);

        bool Find(Simulation sim, Entity hero, long uid, out ItemInstance? item, out Location location);

        /// <summary>Takes an item out, or says why it cannot leave.</summary>
        bool Take(Simulation sim, Entity hero, ItemInstance item, Location from, out string reason);

        /// <summary>Takes an item in at a cell, or says why not; on refusal nothing changed.</summary>
        bool Put(Simulation sim, Entity hero, ItemInstance item, string container, int x, int y, out string reason);

        /// <summary>Puts an item back where <see cref="Take"/> took it from, after a move that could not finish.</summary>
        void Return(Simulation sim, Entity hero, ItemInstance item, Location from);
    }

    private readonly List<IItemPlace> _places = new();

    public void Register(IItemPlace place) => _places.Add(place);

    /// <summary>A hero's bag as rule <c>inventory.bag</c> shapes it (width, height, an optional mask), with footprints from content.</summary>
    public static Inventory CreateBag(Content.ContentRegistry content, IFootprints? footprints)
    {
        var rules = content.Rules;
        var mask = rules.Strings("inventory.bag.mask");
        return new Inventory((int)rules.Number("inventory.bag.width", 12), (int)rules.Number("inventory.bag.height", 5), mask.Count == 0 ? null : mask)
        {
            Footprints = footprints,
        };
    }

    /// <summary>
    /// Puts saved items back on a board: each at its saved cell when it still fits there, else in the first space
    /// that fits it, else among the items without a cell. Nothing merges and nothing is lost.
    /// </summary>
    public static void PlaceSaved(Inventory inv, IEnumerable<ItemInstance> items)
    {
        var pending = new List<ItemInstance>();
        foreach (var item in items)
        {
            var (w, h) = inv.FootprintOf(item);
            if (item.X is { } x && item.Y is { } y && inv.Bag.Place(item, x, y, w, h))
            {
                continue;
            }
            item.X = null;
            item.Y = null;
            pending.Add(item);
        }
        // Items without a saved cell go after every item that kept its own, so none is pushed out by a newcomer.
        foreach (var item in pending)
        {
            var (w, h) = inv.FootprintOf(item);
            if (!(inv.Bag.FindSpace(w, h, out var x, out var y) && inv.Bag.Place(item, x, y, w, h)))
            {
                inv.Overflow.Add(item);
            }
        }
    }

    /// <summary>Finds an item the hero has anywhere this service knows.</summary>
    public bool Locate(Simulation sim, Entity hero, long uid, out ItemInstance? item, out Location location)
    {
        item = null;
        location = default;
        if (sim.World.TryGet<Inventory>(hero, out var inv))
        {
            foreach (var p in inv.Bag.Placed)
            {
                if (p.Item.Uid == uid)
                {
                    item = p.Item;
                    location = new Location(Bag, null);
                    return true;
                }
            }
            foreach (var o in inv.Overflow)
            {
                if (o.Uid == uid)
                {
                    item = o;
                    location = new Location(Overflow, null);
                    return true;
                }
            }
        }
        if (sim.World.TryGet<Components.Equipment>(hero, out var eq))
        {
            foreach (var (slot, worn) in eq.BySlot)
            {
                if (worn.Uid == uid)
                {
                    item = worn;
                    location = new Location(Equipment, slot);
                    return true;
                }
            }
        }
        foreach (var place in _places)
        {
            if (place.Find(sim, hero, uid, out item, out location))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Moves an item to <c>bag</c> or a registered container at a cell, to <c>equipment</c> in a slot, or to the
    /// <c>world</c>. On the bag, an item already under the target is swapped into the moved item's old place
    /// (or the first space that fits it); with nowhere to go the move is refused and nothing changes.
    /// </summary>
    public bool Move(Simulation sim, Entity hero, long uid, string container, int x, int y, string? slot, out string reason)
    {
        if (!sim.World.TryGet<Inventory>(hero, out var inv))
        {
            reason = "no_bag";
            return false;
        }
        if (!Locate(sim, hero, uid, out var item, out var from) || item is null)
        {
            reason = "no_item";
            return false;
        }
        bool moved;
        switch (container)
        {
            case Bag:
                moved = ToBag(sim, hero, inv, item, from, x, y, out reason);
                break;
            case Equipment:
                moved = ToEquipment(sim, hero, inv, item, from, slot, out reason);
                break;
            case World:
                moved = ToWorld(sim, hero, inv, item, from, out reason);
                break;
            default:
                moved = ToPlace(sim, hero, inv, item, from, container, x, y, out reason);
                break;
        }
        if (moved)
        {
            sim.Events.Publish(new ItemMoved(hero, uid, from.Container, container) { Tick = sim.Clock.Tick });
        }
        return moved;
    }

    private bool ToBag(Simulation sim, Entity hero, Inventory inv, ItemInstance item, Location from, int x, int y, out string reason)
    {
        var (w, h) = inv.FootprintOf(item);
        if (!InBag(inv.Bag, x, y, w, h))
        {
            reason = "out_of_bounds";
            return false;
        }
        if (from.Container == Bag)
        {
            return Shift(inv, item, x, y, w, h, out reason);
        }
        // From anywhere else the target cells must be free: there is no place to swap an occupant into.
        if (!inv.Bag.Fits(x, y, w, h))
        {
            reason = "occupied";
            return false;
        }
        switch (from.Container)
        {
            case Overflow:
                inv.Overflow.Remove(item);
                inv.Bag.Place(item, x, y, w, h);
                reason = "";
                return true;
            case Equipment:
                return sim.Service<EquipmentService>().UnequipTo(sim, hero, from.Slot!, x, y, out reason);
            default:
                if (!TakeFrom(sim, hero, item, from, out _, out reason))
                {
                    return false;
                }
                inv.Bag.Place(item, x, y, w, h);
                return true;
        }
    }

    /// <summary>Takes an item out of the registered place holding it, or says why it cannot leave.</summary>
    private bool TakeFrom(Simulation sim, Entity hero, ItemInstance item, Location from, out IItemPlace? place, out string reason)
    {
        place = PlaceOf(from.Container);
        if (place is null)
        {
            reason = "no_item";
            return false;
        }
        if (!place.Take(sim, hero, item, from, out reason))
        {
            reason = ReasonOr(reason, "locked");
            return false;
        }
        return true;
    }

    /// <summary>A move inside the bag: to free cells, or a swap with the one item under the target.</summary>
    private static bool Shift(Inventory inv, ItemInstance item, int x, int y, int w, int h, out string reason)
    {
        reason = "";
        if (item.X == x && item.Y == y)
        {
            return true;
        }
        if (inv.Bag.Fits(x, y, w, h, ignoring: item))
        {
            inv.Bag.Remove(item);
            inv.Bag.Place(item, x, y, w, h);
            return true;
        }
        var under = new List<ItemInstance>(2);
        inv.Bag.Overlapping(x, y, w, h, item, under);
        if (under.Count != 1)
        {
            reason = "occupied";
            return false;
        }
        var other = under[0];
        var (ow, oh) = inv.FootprintOf(other);
        int fromX = item.X ?? 0, fromY = item.Y ?? 0, otherX = other.X ?? 0, otherY = other.Y ?? 0;
        inv.Bag.Remove(item);
        inv.Bag.Remove(other);
        if (inv.Bag.Place(item, x, y, w, h))
        {
            if (inv.Bag.Place(other, fromX, fromY, ow, oh))
            {
                return true;
            }
            if (inv.Bag.FindSpace(ow, oh, out var fx, out var fy) && inv.Bag.Place(other, fx, fy, ow, oh))
            {
                return true;
            }
            inv.Bag.Remove(item);
        }
        inv.Bag.Place(item, fromX, fromY, w, h);
        inv.Bag.Place(other, otherX, otherY, ow, oh);
        reason = "no_room";
        return false;
    }

    private bool ToEquipment(Simulation sim, Entity hero, Inventory inv, ItemInstance item, Location from, string? slot, out string reason)
    {
        if (slot is null)
        {
            reason = "no_slot";
            return false;
        }
        var equipment = sim.Service<EquipmentService>();
        switch (from.Container)
        {
            case Bag or Overflow:
                return equipment.Equip(sim, hero, item, slot, fromInventory: true, out reason);
            case Equipment when from.Slot == slot:
                reason = "";
                return true;
            case Equipment:
            {
                // Between two slots of the doll: through the bag, so whatever the target slot held finds room.
                if (!equipment.Unequip(sim, hero, from.Slot!, out reason))
                {
                    return false;
                }
                if (equipment.Equip(sim, hero, item, slot, fromInventory: true, out reason))
                {
                    return true;
                }
                equipment.Equip(sim, hero, item, from.Slot, fromInventory: true, out _);
                return false;
            }
            default:
            {
                if (!TakeFrom(sim, hero, item, from, out var place, out reason))
                {
                    return false;
                }
                if (equipment.Equip(sim, hero, item, slot, fromInventory: false, out reason))
                {
                    return true;
                }
                place!.Return(sim, hero, item, from);
                return false;
            }
        }
    }

    private bool ToWorld(Simulation sim, Entity hero, Inventory inv, ItemInstance item, Location from, out string reason)
    {
        reason = "";
        var at = sim.World.Get<Transform>(hero)?.Position;
        if (at is null)
        {
            reason = "nowhere";
            return false;
        }
        if (item.Locked)
        {
            reason = "locked";
            return false;
        }
        switch (from.Container)
        {
            case Bag or Overflow:
                inv.Remove(item);
                break;
            case Equipment:
                if (sim.Service<EquipmentService>().TakeOff(sim, hero, from.Slot!) is null)
                {
                    reason = "no_item";
                    return false;
                }
                break;
            default:
                if (!TakeFrom(sim, hero, item, from, out _, out reason))
                {
                    return false;
                }
                break;
        }
        sim.Service<LootService>().Drop(sim, item, at.Value, Entity.None, hero);
        return true;
    }

    private bool ToPlace(Simulation sim, Entity hero, Inventory inv, ItemInstance item, Location from, string container, int x, int y, out string reason)
    {
        var target = PlaceOf(container);
        if (target is null)
        {
            reason = "unknown_container";
            return false;
        }
        // Out of where it is first, then in; if the target refuses, the item goes back where it was.
        int? fromX = item.X, fromY = item.Y;
        switch (from.Container)
        {
            case Bag or Overflow:
                inv.Remove(item);
                break;
            case Equipment:
                if (sim.Service<EquipmentService>().TakeOff(sim, hero, from.Slot!) is null)
                {
                    reason = "no_item";
                    return false;
                }
                break;
            default:
                if (!TakeFrom(sim, hero, item, from, out _, out reason))
                {
                    return false;
                }
                break;
        }
        if (target.Put(sim, hero, item, container, x, y, out reason))
        {
            return true;
        }
        switch (from.Container)
        {
            case Bag:
                if (fromX is not { } bx || fromY is not { } by || !inv.TryAddAt(item, bx, by))
                {
                    if (!inv.TryAdd(item))
                    {
                        inv.Overflow.Add(item);
                    }
                }
                break;
            case Overflow:
                inv.Overflow.Add(item);
                break;
            case Equipment:
                sim.Service<EquipmentService>().Equip(sim, hero, item, from.Slot, fromInventory: false, out _);
                break;
            default:
                PlaceOf(from.Container)?.Return(sim, hero, item, from);
                break;
        }
        return false;
    }

    private IItemPlace? PlaceOf(string container)
    {
        foreach (var place in _places)
        {
            if (place.Handles(container))
            {
                return place;
            }
        }
        return null;
    }

    private static bool InBag(Board board, int x, int y, int w, int h) => x >= 0 && y >= 0 && x + w <= board.Width && y + h <= board.Height;

    private static string ReasonOr(string reason, string fallback) => string.IsNullOrEmpty(reason) ? fallback : reason;
}
