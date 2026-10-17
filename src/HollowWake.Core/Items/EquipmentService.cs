using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Items;

/// <summary>Equips and unequips items and keeps the wearer's modifier stack and weapon bases current.</summary>
public sealed class EquipmentService
{
    private readonly ItemResolver _resolver;

    public EquipmentService(ItemResolver resolver)
    {
        _resolver = resolver;
    }

    public bool MeetsRequirements(Stats stats, ItemInstance item, out string? failing)
    {
        var b = _resolver.BaseOf(item);
        foreach (var (attr, required) in b.Requirements)
        {
            var have = attr == "level" ? stats.Level : stats.Get(attr);
            if (have < required)
            {
                failing = attr;
                return false;
            }
        }
        failing = null;
        return true;
    }

    public bool Equip(Simulation sim, Entity wearer, ItemInstance item, string? slotOverride, bool fromInventory, out string reason)
    {
        reason = "";
        if (!sim.World.TryGet<Stats>(wearer, out var stats))
        {
            reason = "no_stats";
            return false;
        }
        var equipment = sim.World.GetOrAdd<Equipment>(wearer);
        var b = _resolver.BaseOf(item);
        var slot = slotOverride ?? b.Slot;
        if (slot is null)
        {
            reason = "not_equippable";
            return false;
        }
        if (sim.Content.TryGet<SlotDef>(slot, out var slotDef) && slotDef is not null && !slotDef.Accepts.IsEmpty && !_resolver.TagsOf(item).HasAny(slotDef.Accepts))
        {
            reason = "wrong_slot";
            return false;
        }
        if (!MeetsRequirements(stats, item, out var failing))
        {
            reason = "requires:" + failing;
            return false;
        }
        var inventory = sim.World.Get<Inventory>(wearer);
        int? fromX = item.X, fromY = item.Y;
        var fromOverflow = inventory is not null && inventory.Overflow.Contains(item);
        if (fromInventory && (inventory is null || !inventory.Remove(item)))
        {
            reason = "not_in_bag";
            return false;
        }
        // The slot and any slots this item also fills give up what they hold. Displaced items go to the bag,
        // which by now includes the cells the moved item left; if one finds no room nothing changes at all.
        var displaced = Displaced(equipment, slot, b);
        if (inventory is not null && displaced.Count > 0)
        {
            var stowed = new List<ItemInstance>(displaced.Count);
            foreach (var (_, d) in displaced)
            {
                if (!inventory.TryAdd(d))
                {
                    foreach (var s in stowed)
                    {
                        inventory.Remove(s);
                    }
                    if (fromInventory)
                    {
                        Restore(inventory, item, fromX, fromY, fromOverflow);
                    }
                    reason = "bag_full";
                    return false;
                }
                stowed.Add(d);
            }
        }
        foreach (var (s, _) in displaced)
        {
            TakeOffSlot(equipment, s);
        }
        equipment.BySlot[slot] = item;
        foreach (var s in b.Occupies)
        {
            equipment.OccupiedBy[s] = slot;
        }
        Recalculate(sim, wearer);
        sim.Events.Publish(new ItemEquipped(wearer, item.Uid, slot) { Tick = sim.Clock.Tick });
        return true;
    }

    /// <summary>The items (and the slots they are worn in) that equipping into <paramref name="slot"/> would displace.</summary>
    private static List<(string Slot, ItemInstance Item)> Displaced(Equipment equipment, string slot, ItemBaseDef b)
    {
        var result = new List<(string, ItemInstance)>();
        void Add(string s)
        {
            if (equipment.BySlot.TryGetValue(s, out var worn) && !result.Exists(r => ReferenceEquals(r.Item2, worn)))
            {
                result.Add((s, worn));
            }
        }
        foreach (var s in b.Occupies.Prepend(slot))
        {
            if (equipment.OccupiedBy.TryGetValue(s, out var bySlot))
            {
                Add(bySlot);
            }
            Add(s);
        }
        return result;
    }

    private static void TakeOffSlot(Equipment equipment, string slot)
    {
        equipment.BySlot.Remove(slot);
        foreach (var (occupied, by) in equipment.OccupiedBy.ToList())
        {
            if (by == slot)
            {
                equipment.OccupiedBy.Remove(occupied);
            }
        }
    }

    private static void Restore(Inventory inventory, ItemInstance item, int? x, int? y, bool overflow)
    {
        if (!overflow && x is { } px && y is { } py && inventory.TryAddAt(item, px, py))
        {
            return;
        }
        if (!inventory.TryAdd(item))
        {
            inventory.Overflow.Add(item);
        }
    }

    /// <summary>Takes a worn item off into a cell of the bag, which must have room for it there.</summary>
    public bool UnequipTo(Simulation sim, Entity wearer, string slot, int x, int y, out string reason)
    {
        reason = "";
        if (!sim.World.TryGet<Equipment>(wearer, out var equipment) || !equipment.BySlot.TryGetValue(slot, out var item))
        {
            reason = "empty";
            return false;
        }
        if (!sim.World.TryGet<Inventory>(wearer, out var inventory) || !inventory.TryAddAt(item, x, y))
        {
            reason = "no_room";
            return false;
        }
        TakeOffSlot(equipment, slot);
        Recalculate(sim, wearer);
        return true;
    }

    /// <summary>Takes a worn item off without putting it anywhere; the caller decides where it goes.</summary>
    public ItemInstance? TakeOff(Simulation sim, Entity wearer, string slot)
    {
        if (!sim.World.TryGet<Equipment>(wearer, out var equipment) || !equipment.BySlot.TryGetValue(slot, out var item))
        {
            return null;
        }
        TakeOffSlot(equipment, slot);
        Recalculate(sim, wearer);
        return item;
    }

    public bool Equip(Simulation sim, Entity wearer, ItemInstance item, string? slotOverride, bool fromInventory) => Equip(sim, wearer, item, slotOverride, fromInventory, out _);

    public bool Unequip(Simulation sim, Entity wearer, string slot, out string reason)
    {
        reason = "";
        if (!sim.World.TryGet<Equipment>(wearer, out var equipment) || !equipment.BySlot.TryGetValue(slot, out var item))
        {
            reason = "empty";
            return false;
        }
        var inventory = sim.World.Get<Inventory>(wearer);
        if (inventory is not null && !inventory.TryAdd(item))
        {
            reason = "bag_full";
            return false;
        }
        TakeOffSlot(equipment, slot);
        Recalculate(sim, wearer);
        return true;
    }

    /// <summary>Rebuilds every item-sourced modifier and the weapon bases on the wearer.</summary>
    public void Recalculate(Simulation sim, Entity wearer)
    {
        if (!sim.World.TryGet<Stats>(wearer, out var stats))
        {
            return;
        }
        var equipment = sim.World.Get<Equipment>(wearer);
        foreach (var source in stats.Attributes.AllModifiers.Select(m => m.Source).Where(s => s.StartsWith("item:", StringComparison.Ordinal)).Distinct().ToList())
        {
            stats.Attributes.RemoveSource(source);
        }
        foreach (var attr in stats.Attributes.BaseOverrides.Select(kv => kv.Key).Where(k => k.StartsWith("weapon.", StringComparison.Ordinal) || k.StartsWith("offhand.", StringComparison.Ordinal) || k is "armor" or "evasion" or "ward.max.local").ToList())
        {
            stats.Attributes.ClearBase(attr);
        }
        if (equipment is null)
        {
            stats.Attributes.Touch();
            return;
        }
        double armor = 0, evasion = 0, ward = 0;
        foreach (var (slot, item) in equipment.BySlot)
        {
            stats.Attributes.AddRange(_resolver.WearerModifiers(item));
            var local = _resolver.LocalAttributes(item);
            var scope = Expressions.EmptyScope.Instance;
            armor += ItemResolver.LocalValue(local, "armor");
            evasion += ItemResolver.LocalValue(local, "evasion");
            ward += ItemResolver.LocalValue(local, "ward");
            var prefix = slot == "off_hand" ? "offhand." : slot == "main_hand" ? "weapon." : null;
            if (prefix is not null)
            {
                foreach (var attr in local.BaseOverrides.Select(kv => kv.Key).Concat(local.ModifiedAttributes).Distinct())
                {
                    if (attr.StartsWith("weapon.", StringComparison.Ordinal))
                    {
                        stats.Attributes.SetBase(prefix + attr[7..], ItemResolver.LocalValue(local, attr));
                    }
                }
            }
        }
        if (armor > 0)
        {
            stats.Attributes.Add(new Attributes.Modifier { Attribute = "armor", Value = armor, Source = "item:gear" });
        }
        if (evasion > 0)
        {
            stats.Attributes.Add(new Attributes.Modifier { Attribute = "evasion", Value = evasion, Source = "item:gear" });
        }
        if (ward > 0)
        {
            stats.Attributes.Add(new Attributes.Modifier { Attribute = "ward.max", Value = ward, Source = "item:gear" });
        }
        stats.Attributes.Touch();
        sim.World.Get<WeaveBook>(wearer)?.InvalidateCompiled();
    }
}
