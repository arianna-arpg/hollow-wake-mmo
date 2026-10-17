using System.Numerics;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Items;

namespace HollowWake.Core.Components;

/// <summary>A weave bound to a slot, with its compiled form cached until the spec or owner changes.</summary>
public sealed class WeaveSlot
{
    public required WeaveSpec Spec;
    public Weaves.CompiledWeave? Compiled;
    public string Key => Spec.ToString();
}

public sealed class CastState
{
    public required WeaveSlot Slot;
    public Entity Target = Entity.None;
    public Vector3 Point;
    public Vector2 Direction;
    public double StartedAt;
    public double Duration;
    public double NextChannelAt;
    public bool Channeling;
    public int Pulses;
}

/// <summary>The weaves an entity knows and can use, their cooldowns, and what it is casting now.</summary>
public sealed class WeaveBook : IComponent
{
    public List<WeaveSlot> Slots { get; } = new();
    public Dictionary<string, double> ReadyAt { get; } = new(StringComparer.Ordinal);
    public CastState? Casting;
    public double GlobalReadyAt;

    public WeaveSlot Add(WeaveSpec spec)
    {
        var slot = new WeaveSlot { Spec = spec };
        Slots.Add(slot);
        return slot;
    }

    public WeaveSlot? Find(string key) => Slots.FirstOrDefault(s => s.Key == key || s.Spec.Preset == key || s.Spec.Name == key);

    public bool IsReady(WeaveSlot slot, double now) => !ReadyAt.TryGetValue(slot.Key, out var t) || t <= now;

    public void InvalidateCompiled()
    {
        foreach (var s in Slots)
        {
            s.Compiled = null;
        }
    }
}

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

/// <summary>Data-driven AI state.</summary>
public sealed class Behavior : IComponent
{
    public required string BehaviorId;
    public string State = "";
    public double NextThinkAt;
    public Entity Target = Entity.None;
    public Vector3 Home;
    public Dictionary<string, double> Memory = new(StringComparer.Ordinal);
    public double StateEnteredAt;
    public bool Entered;
}

public enum QuestState
{
    Offered,
    Active,
    Complete,
    TurnedIn,
    Failed,
}

public sealed class QuestProgress
{
    public required string QuestId;
    public QuestState State = QuestState.Active;
    public Dictionary<string, int> Counts = new(StringComparer.Ordinal);
    public double StartedAt;
    public QuestDef? Generated;
}

public sealed class QuestLog : IComponent
{
    public Dictionary<string, QuestProgress> Quests { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Completed { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Discovered { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, double> Reputation { get; } = new(StringComparer.Ordinal);
}

/// <summary>A player-controlled entity and its pending intents.</summary>
public sealed class Player : IComponent
{
    public required string AccountId;
    public required string CharacterId;
    public Queue<Intents.IIntent> Intents { get; } = new();
    public double LastInputAt;
    public double RespawnAt = double.NegativeInfinity;
}

/// <summary>Summons and companions belong to someone and time out.</summary>
public sealed class Minion : IComponent
{
    public Entity Owner = Entity.None;
    public double ExpiresAt = double.PositiveInfinity;
    public string? Group;
}
