namespace HollowWake.Core.Items;

/// <summary>One rolled affix: which family, which tier, and the 0..1 roll per line. Values derive at read time.</summary>
public sealed class AffixRoll
{
    public required string Affix { get; init; }
    public int Tier { get; init; }
    public List<double> Rolls { get; init; } = new();
    public bool Crafted { get; init; }
}

/// <summary>
/// An item as it exists in the world or a bag. Everything numeric is derived from the base, tier, rarity,
/// rolls and content at read time, so saves are tiny and a rebalance retunes every existing item.
/// </summary>
public sealed class ItemInstance
{
    public long Uid { get; set; }
    public required string Base { get; init; }
    public int Level { get; init; } = 1;
    public int Tier { get; init; } = 1;
    public string Rarity { get; set; } = "common";
    public string Name { get; set; } = "";
    /// <summary>0..1 across the rarity's base-value window.</summary>
    public double BaseRoll { get; init; } = 0.5;
    public List<double> ImplicitRolls { get; init; } = new();
    public List<AffixRoll> Affixes { get; init; } = new();
    public string? Unique { get; init; }
    public List<double> UniqueRolls { get; init; } = new();
    public int Count { get; set; } = 1;
    public int Stack { get; init; } = 1;
    /// <summary>Crafting quality 0..1; scales local values through the items.quality rule.</summary>
    public double Quality { get; set; }
    public ulong Seed { get; init; }
    public string? Crafter { get; set; }
    public bool Locked { get; set; }

    /// <summary>The cell of the item's top-left corner on the board holding it; null when it lies on none.</summary>
    public int? X { get; set; }
    public int? Y { get; set; }

    /// <summary>What a glyph stone or a codex holds: the glyph or the weave it teaches, at a level.</summary>
    public ItemPayload? Payload { get; set; }

    /// <summary>Stones socketed into this item, by socket index; null where a socket is empty. Null for items without sockets.</summary>
    public List<ItemInstance?>? Sockets { get; set; }

    public bool CanStackWith(ItemInstance other) =>
        Stack > 1 && other.Base == Base && Affixes.Count == 0 && other.Affixes.Count == 0 && Unique is null && other.Unique is null && Count < Stack
        && Payload is null && other.Payload is null;

    public ItemInstance Clone(long uid) => new()
    {
        Uid = uid,
        Base = Base,
        Level = Level,
        Tier = Tier,
        Rarity = Rarity,
        Name = Name,
        BaseRoll = BaseRoll,
        ImplicitRolls = new List<double>(ImplicitRolls),
        Affixes = Affixes.Select(a => new AffixRoll { Affix = a.Affix, Tier = a.Tier, Rolls = new List<double>(a.Rolls), Crafted = a.Crafted }).ToList(),
        Unique = Unique,
        UniqueRolls = new List<double>(UniqueRolls),
        Count = Count,
        Stack = Stack,
        Quality = Quality,
        Seed = Seed,
        Crafter = Crafter,
        Locked = Locked,
        Payload = Payload?.Clone(),
        // A copy has the same sockets, empty: stones are items of their own and are never duplicated.
        Sockets = Sockets is null ? null : new List<ItemInstance?>(new ItemInstance?[Sockets.Count]),
    };

    public override string ToString() => $"{Name} ({Base} L{Level} {Rarity})";
}

/// <summary>
/// What a weave item holds. A glyph stone holds a glyph at a level with the rows it rolled; a codex teaches a
/// form (composed with threads the hero chooses) or a whole preset weave, at a level.
/// </summary>
public sealed class ItemPayload
{
    public string? Glyph { get; set; }
    public string? Form { get; set; }
    public string? Weave { get; set; }
    public int Level { get; set; } = 1;
    /// <summary>A glyph stone's rolled row per axis.</summary>
    public Dictionary<string, string>? Rows { get; set; }

    public ItemPayload Clone() => new()
    {
        Glyph = Glyph,
        Form = Form,
        Weave = Weave,
        Level = Level,
        Rows = Rows is null ? null : new Dictionary<string, string>(Rows, StringComparer.Ordinal),
    };
}
