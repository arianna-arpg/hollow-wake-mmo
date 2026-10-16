using Skein.Core.Components;
using Skein.Core.Definitions;
using Skein.Core.Ecs;
using Skein.Core.Sim;

namespace Skein.Core.Items;

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
        if (fromInventory && (inventory is null || !inventory.Remove(item)))
        {
            reason = "not_in_bag";
            return false;
        }
        // Clear the slot and any slots this item occupies, returning displaced items to the bag.
        var toClear = new List<string> { slot };
        toClear.AddRange(b.Occupies);
        foreach (var s in toClear)
        {
            if (equipment.OccupiedBy.TryGetValue(s, out var bySlot))
            {
                Unequip(sim, wearer, bySlot, out _);
            }
            if (equipment.BySlot.ContainsKey(s))
            {
                Unequip(sim, wearer, s, out _);
            }
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
        equipment.BySlot.Remove(slot);
        foreach (var (occupied, by) in equipment.OccupiedBy.ToList())
        {
            if (by == slot)
            {
                equipment.OccupiedBy.Remove(occupied);
            }
        }
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
