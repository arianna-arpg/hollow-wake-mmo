using System.Numerics;
using System.Text.Json.Serialization;
using Skein.Core.Components;
using Skein.Core.Definitions;
using Skein.Core.Ecs;
using Skein.Core.Items;
using Skein.Core.Progression;
using Skein.Core.Sim;
using Skein.Core.Tags;

namespace Skein.Core.Characters;

/// <summary>A quest's saved state.</summary>
public sealed record SavedQuest(string QuestId, QuestState State, Dictionary<string, int> Counts, double StartedAt);

/// <summary>
/// Everything that persists about a character: identity, calling, progress, passives, weave loadout,
/// bag, gear, quests, position. Pure data; the server stores it, the factory turns it into an entity
/// and back.
/// </summary>
public sealed class CharacterSheet
{
    public required string Id { get; init; }
    public required string AccountId { get; init; }
    public required string Name { get; set; }
    public required string Calling { get; init; }
    public int Level { get; set; } = 1;
    public double Xp { get; set; }
    public List<string> Passives { get; set; } = new();
    public List<WeaveSpec> Weaves { get; set; } = new();
    public List<ItemInstance> Bag { get; set; } = new();
    public int BagCapacity { get; set; } = 40;
    public Dictionary<string, ItemInstance> Equipment { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, double> Currency { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, SavedQuest> Quests { get; set; } = new(StringComparer.Ordinal);
    public List<string> CompletedQuests { get; set; } = new();
    public List<string> Discovered { get; set; } = new();
    public Dictionary<string, double> Reputation { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, double> Resources { get; set; } = new(StringComparer.Ordinal);
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public string? Look { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastPlayedAt { get; set; } = DateTimeOffset.UtcNow;
    public double PlayedSeconds { get; set; }

    [JsonIgnore]
    public Vector3 Position
    {
        get => new((float)X, (float)Y, (float)Z);
        set
        {
            X = value.X;
            Y = value.Y;
            Z = value.Z;
        }
    }
}

/// <summary>Creates new characters from callings and moves characters between sheets and entities.</summary>
public sealed class CharacterFactory
{
    public CharacterSheet Create(Simulation sim, string id, string accountId, string name, string callingId, ItemGenerator items)
    {
        var calling = sim.Content.Require<CallingDef>(callingId);
        var sheet = new CharacterSheet { Id = id, AccountId = accountId, Name = name, Calling = callingId, Look = calling.Look };
        sheet.Weaves.AddRange(calling.Weaves);
        if (calling.StartNode is not null)
        {
            sheet.Passives.Add(calling.StartNode);
        }
        sheet.Passives.AddRange(calling.Passives);
        var rng = new Maths.Rng(id);
        foreach (var grant in calling.Items)
        {
            var item = grant.Unique is not null ? items.RollUnique(grant.Unique, grant.Level ?? 1, rng) : items.Roll(grant.Item, grant.Level ?? 1, grant.Rarity ?? "common", rng);
            item.Count = Math.Clamp(grant.Count, 1, Math.Max(1, item.Stack));
            var b = items.Resolver.BaseOf(item);
            if (b.Slot is not null && !sheet.Equipment.ContainsKey(b.Slot))
            {
                sheet.Equipment[b.Slot] = item;
            }
            else
            {
                sheet.Bag.Add(item);
            }
        }
        if (sim.Generator is not null)
        {
            var (x, y) = sim.Generator.StartPoint(sim.Generator.StartZone());
            sheet.X = x;
            sheet.Y = y;
        }
        return sheet;
    }

    /// <summary>Builds the player entity from a sheet inside a simulation.</summary>
    public Entity Materialize(Simulation sim, CharacterSheet sheet)
    {
        var calling = sim.Content.Get<CallingDef>(sheet.Calling);
        var e = sim.World.Create();
        var position = sim.Terrain.SnapToGround(sheet.Position, 3);
        sim.World.Add(e, new Transform { Position = position, Radius = 0.4f, Height = 1.8f });
        sim.World.Add(e, new Identity
        {
            Name = sheet.Name,
            Tags = TagSet.Of("player", "character", "calling." + sheet.Calling),
            Faction = "players",
            IsPlayer = true,
            CharacterId = sheet.Id,
            AccountId = sheet.AccountId,
            Look = sheet.Look ?? calling?.Look ?? "player",
        });
        sim.World.Add(e, new Player { AccountId = sheet.AccountId, CharacterId = sheet.Id });
        sim.World.Add(e, new Motion());
        sim.World.Add(e, new Statuses());
        var stats = sim.World.Add(e, new Stats(sim.Content));
        stats.UseRng(sim.RngFor(e, "player"));
        stats.Level = Math.Max(1, sheet.Level);
        stats.Xp = sheet.Xp;
        stats.Tags = TagSet.Of("player", "character", "calling." + sheet.Calling, "faction.players");
        if (calling is not null)
        {
            foreach (var (attr, value) in calling.Attributes)
            {
                stats.Attributes.SetBase(attr, value);
            }
            stats.Attributes.AddRange(calling.Modifiers, "calling:" + calling.Id);
        }
        stats.Passives.AddRange(sheet.Passives.Distinct(StringComparer.Ordinal));
        stats.PassivePointsSpent = stats.Passives.Sum(id => sim.Content.Get<PassiveNodeDef>(id) is { } n && n.Kind != PassiveKind.Start ? n.Cost : 0);
        sim.Service<ProgressionService>().ApplyAllocated(sim, stats);

        var book = sim.World.Add(e, new WeaveBook());
        foreach (var w in sheet.Weaves)
        {
            book.Add(w);
        }
        var inventory = sim.World.Add(e, new Inventory(sheet.BagCapacity));
        foreach (var item in sheet.Bag)
        {
            inventory.TryAdd(item);
        }
        foreach (var (currency, amount) in sheet.Currency)
        {
            inventory.AddCurrency(currency, amount);
        }
        sim.World.Add(e, new Equipment());
        var equip = sim.Service<EquipmentService>();
        foreach (var (slot, item) in sheet.Equipment)
        {
            equip.Equip(sim, e, item, slot, fromInventory: false);
        }
        equip.Recalculate(sim, e);

        var log = sim.World.Add(e, new QuestLog());
        foreach (var (id, q) in sheet.Quests)
        {
            var progress = new QuestProgress { QuestId = id, State = q.State, StartedAt = q.StartedAt };
            foreach (var (k, v) in q.Counts)
            {
                progress.Counts[k] = v;
            }
            progress.Generated = sim.Service<Quests.QuestService>().Find(sim, id);
            log.Quests[id] = progress;
        }
        foreach (var c in sheet.CompletedQuests)
        {
            log.Completed.Add(c);
        }
        foreach (var d in sheet.Discovered)
        {
            log.Discovered.Add(d);
        }
        foreach (var (f, r) in sheet.Reputation)
        {
            log.Reputation[f] = r;
        }

        stats.FillResources();
        foreach (var (res, value) in sheet.Resources)
        {
            stats.SetResource(res, value);
        }
        if (stats.Resource("life") <= 0)
        {
            stats.SetResource("life", stats.ResourceMax("life"));
        }
        sim.Spatial.Update(e, position);
        sim.Events.Publish(new EntitySpawned(e, null, position) { Tick = sim.Clock.Tick });
        return e;
    }

    /// <summary>Writes the entity's state back into the sheet.</summary>
    public void Capture(Simulation sim, Entity e, CharacterSheet sheet)
    {
        if (sim.World.TryGet<Transform>(e, out var t))
        {
            sheet.Position = t.Position;
        }
        if (sim.World.TryGet<Stats>(e, out var stats))
        {
            sheet.Level = stats.Level;
            sheet.Xp = stats.Xp;
            sheet.Passives = stats.Passives.ToList();
            sheet.Resources = new Dictionary<string, double>(stats.Resources, StringComparer.Ordinal);
        }
        if (sim.World.TryGet<WeaveBook>(e, out var book))
        {
            sheet.Weaves = book.Slots.Select(s => s.Spec).ToList();
        }
        if (sim.World.TryGet<Inventory>(e, out var inv))
        {
            sheet.Bag = inv.Items.ToList();
            sheet.BagCapacity = inv.Capacity;
            sheet.Currency = new Dictionary<string, double>(inv.Currency, StringComparer.Ordinal);
        }
        if (sim.World.TryGet<Equipment>(e, out var eq))
        {
            sheet.Equipment = new Dictionary<string, ItemInstance>(eq.BySlot, StringComparer.Ordinal);
        }
        if (sim.World.TryGet<QuestLog>(e, out var log))
        {
            sheet.Quests = log.Quests.ToDictionary(kv => kv.Key, kv => new SavedQuest(kv.Key, kv.Value.State, new Dictionary<string, int>(kv.Value.Counts), kv.Value.StartedAt), StringComparer.Ordinal);
            sheet.CompletedQuests = log.Completed.ToList();
            sheet.Discovered = log.Discovered.ToList();
            sheet.Reputation = new Dictionary<string, double>(log.Reputation, StringComparer.Ordinal);
        }
        sheet.LastPlayedAt = DateTimeOffset.UtcNow;
    }
}
