using HollowWake.Core.Ecs;
using HollowWake.Core.Items;

namespace HollowWake.Core.Components;

public sealed class Inventory : IComponent
{
    public List<ItemInstance?> Slots { get; }
    public Dictionary<string, double> Currency { get; } = new(StringComparer.Ordinal);

    public Inventory(int capacity = 40)
    {
        Slots = new List<ItemInstance?>(capacity);
        for (var i = 0; i < capacity; i++)
        {
            Slots.Add(null);
        }
    }

    public int Capacity => Slots.Count;
    public int FreeSlots => Slots.Count(s => s is null);
    public IEnumerable<ItemInstance> Items => Slots.Where(s => s is not null)!;

    public bool TryAdd(ItemInstance item)
    {
        if (item.Stack > 1 || item.Count > 1)
        {
            foreach (var existing in Items)
            {
                if (existing.CanStackWith(item))
                {
                    var room = existing.Stack - existing.Count;
                    var moved = Math.Min(room, item.Count);
                    existing.Count += moved;
                    item.Count -= moved;
                    if (item.Count <= 0)
                    {
                        return true;
                    }
                }
            }
        }
        var i = Slots.IndexOf(null);
        if (i < 0)
        {
            return false;
        }
        Slots[i] = item;
        return true;
    }

    public bool Remove(ItemInstance item)
    {
        var i = Slots.IndexOf(item);
        if (i < 0)
        {
            return false;
        }
        Slots[i] = null;
        return true;
    }

    public ItemInstance? Find(long uid) => Items.FirstOrDefault(i => i.Uid == uid);

    public int CountOf(Func<ItemInstance, bool> predicate) => Items.Where(predicate).Sum(i => i.Count);

    public double CurrencyOf(string id) => Currency.TryGetValue(id, out var v) ? v : 0;

    public void AddCurrency(string id, double amount)
    {
        Currency[id] = CurrencyOf(id) + amount;
        if (Currency[id] <= 0)
        {
            Currency.Remove(id);
        }
    }
}

public sealed class Equipment : IComponent
{
    public Dictionary<string, ItemInstance> BySlot { get; } = new(StringComparer.Ordinal);
    /// <summary>Slots occupied by an item equipped elsewhere (a two-handed weapon's off hand).</summary>
    public Dictionary<string, string> OccupiedBy { get; } = new(StringComparer.Ordinal);

    public ItemInstance? In(string slot) => BySlot.TryGetValue(slot, out var i) ? i : null;
    public IEnumerable<ItemInstance> All => BySlot.Values;
}
