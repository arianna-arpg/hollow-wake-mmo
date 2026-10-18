using System.Numerics;
using System.Text.Json.Serialization;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Items;
using HollowWake.Core.Progression;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Characters;

/// <summary>A quest's saved state.</summary>
public sealed record SavedQuest(string QuestId, QuestState State, Dictionary<string, int> Counts, double StartedAt);

/// <summary>A rack seat's saved state: the codex learned there, the stones by socket, and a form codex's Threads.</summary>
public sealed class SavedSeat
{
    public int Slot { get; set; }
    public ItemInstance? Codex { get; set; }
    public List<ItemInstance?> Stones { get; set; } = new();
    public List<string> Threads { get; set; } = new();
}

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
    /// <summary>The weave slots as last bound. Seats of <see cref="Rack"/> rebuild theirs from what they hold.</summary>
    public List<WeaveSpec> Weaves { get; set; } = new();
    /// <summary>The weave rack: codices learned and stones socketed, seat by seat.</summary>
    public List<SavedSeat> Rack { get; set; } = new();
    /// <summary>Zero for a save from before the rack, whose weaves get codices once when it loads.</summary>
    public int RackVersion { get; set; }
    /// <summary>Everything in the bag; each item keeps its cell on the board (<see cref="ItemInstance.X"/>, <see cref="ItemInstance.Y"/>).</summary>
    public List<ItemInstance> Bag { get; set; } = new();
    /// <summary>The bag's usable cells when saved. The bag's shape comes from rule <c>inventory.bag</c>; this is a record, not a setting.</summary>
    public int BagCapacity { get; set; } = 40;
    public Dictionary<string, ItemInstance> Equipment { get; set; } = new(StringComparer.Ordinal);
    /// <summary>The character's own container boards and stash pages, by board id, and the stash pages bought.</summary>
    public ContainerSave Containers { get; set; } = new();
    /// <summary>
    /// The account's boards (the reliquary, the account stash) while this character plays. The store fills it from the
    /// account when the character loads and writes it back to the account when the character saves; the character's
    /// own file never holds it. Null means no account save came with the sheet, and the account's boards stay untouched.
    /// </summary>
    [JsonIgnore]
    public ContainerSave? Vault { get; set; }
    public Dictionary<string, double> Currency { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, SavedQuest> Quests { get; set; } = new(StringComparer.Ordinal);
    public List<string> CompletedQuests { get; set; } = new();
    public List<string> Discovered { get; set; } = new();
    public Dictionary<string, double> Reputation { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Story flags set by conversations and rewards.</summary>
    public List<string> Flags { get; set; } = new();
    /// <summary>Options picked on choice nodes, by node.</summary>
    public Dictionary<string, List<string>> PassivePicks { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Passive points granted per currency by rewards.</summary>
    public Dictionary<string, int> PassiveGranted { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Realms opened.</summary>
    public List<string> OpenRealms { get; set; } = new();
    /// <summary>Refunds made.</summary>
    public int PassiveRefunds { get; set; }
    /// <summary>Passive grafts bound to weave slots: the source (a node, or node:option) to the slot index.</summary>
    public Dictionary<string, int> PassiveGrafts { get; set; } = new(StringComparer.Ordinal);
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
        // The calling's weaves arrive as codices learned into the rack, with a stone for every glyph a composed
        // weave names, so the hero owns every weave they start with.
        sheet.Rack.AddRange(SeatsFor(sim, calling.Weaves, items, rng));
        sheet.RackVersion = 1;
        if (sim.Generator is not null)
        {
            var (x, y) = sim.Generator.StartPoint(sim.Generator.StartZone());
            sheet.X = x;
            sheet.Y = y;
        }
        return sheet;
    }

    /// <summary>
    /// Rack seats that teach these weaves: a codex per weave (a preset's codex teaches it whole, a composed
    /// weave's codex its form) at the weave's level, its Threads, and a stone per glyph of a composed weave.
    /// </summary>
    public static List<SavedSeat> SeatsFor(Simulation sim, IReadOnlyList<WeaveSpec> weaves, ItemGenerator items, Maths.Rng rng)
    {
        var seats = new List<SavedSeat>(weaves.Count);
        var codexBase = WeaveItems.CodexBase(sim.Content);
        var stoneBase = WeaveItems.StoneBase(sim.Content);
        if (!sim.Content.Has<ItemBaseDef>(codexBase))
        {
            return seats;
        }
        for (var i = 0; i < weaves.Count; i++)
        {
            var spec = weaves[i];
            if (spec.Preset is null && spec.Form is null)
            {
                continue;
            }
            var codex = items.Roll(codexBase, 1, RarityWithSockets(sim, spec.Preset is null ? spec.Glyphs.Count : 0), rng,
                new ItemGenerator.PayloadRequest { Weave = spec.Preset, Form = spec.Preset is null ? spec.Form : null, Level = Math.Max(1, spec.Level) });
            var seat = new SavedSeat { Slot = i, Codex = codex };
            if (spec.Preset is null)
            {
                seat.Threads.AddRange(spec.Threads);
            }
            foreach (var glyph in spec.Glyphs)
            {
                if (!sim.Content.Has<ItemBaseDef>(stoneBase) || !sim.Content.Has<GlyphDef>(glyph.Id))
                {
                    continue;
                }
                var stone = items.Roll(stoneBase, 1, "common", rng, new ItemGenerator.PayloadRequest { Glyph = glyph.Id, Level = glyph.Level });
                if (glyph.Rows.Count > 0)
                {
                    stone.Payload!.Rows = new Dictionary<string, string>(glyph.Rows, StringComparer.Ordinal);
                }
                seat.Stones.Add(stone);
            }
            seats.Add(seat);
        }
        return seats;
    }

    /// <summary>The lowest item rarity whose codex has at least <paramref name="sockets"/> sockets (rule <c>inventory.codex_sockets</c>).</summary>
    private static string RarityWithSockets(Simulation sim, int sockets)
    {
        string? best = null;
        foreach (var rarity in sim.Content.Each<ItemRarityDef>().OrderBy(r => r.Rank))
        {
            best = rarity.Id;
            if (sim.Content.Rules.Number("inventory.codex_sockets." + rarity.Id, 1) >= sockets)
            {
                return rarity.Id;
            }
        }
        return best ?? "common";
    }

    /// <summary>Builds the player entity from a sheet inside a simulation.</summary>
    public Entity Materialize(Simulation sim, CharacterSheet sheet)
    {
        var calling = sim.Content.Get<CallingDef>(sheet.Calling);
        var e = sim.World.Create();
        var position = sim.Terrain.SnapToGround(sheet.Position, 3);
        sim.World.Add(e, new Transform { Position = position, Radius = 0.4f, Height = (float)sim.Content.Rules.Number("physicality.reference_height", 1.8) });
        Combat.HitBodies.InvalidateBounds(sim);
        if (sim.Content.Get<HitBodyDef>(sim.Content.Rules.Text("body.player_hit_profile", "")) is { } anatomy)
            sim.World.Add(e, new Combat.HitBody { Definition = anatomy });
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
        var passives = sim.World.Add(e, new PassiveState { Refunds = sheet.PassiveRefunds });
        foreach (var (node, picks) in sheet.PassivePicks)
        {
            passives.Picks[node] = new List<string>(picks);
        }
        foreach (var (currency, points) in sheet.PassiveGranted)
        {
            passives.Granted[currency] = points;
        }
        foreach (var realm in sheet.OpenRealms)
        {
            passives.OpenRealms.Add(realm);
        }
        var progression = sim.Service<ProgressionService>();
        progression.ApplyAllocated(sim, stats, passives);
        progression.RecountSpent(sim, e, stats, passives);

        var book = sim.World.Add(e, new WeaveBook());
        foreach (var w in sheet.Weaves)
        {
            book.Add(w);
        }
        // A save from before the rack gets codices (and stones) for the weaves it had, so it keeps them as owned.
        if (sheet.RackVersion == 0)
        {
            if (sheet.Rack.Count == 0 && sheet.Weaves.Count > 0)
            {
                sheet.Rack.AddRange(SeatsFor(sim, sheet.Weaves, sim.Service<ItemGenerator>(), new Maths.Rng(sheet.Id + ":rack")));
            }
            sheet.RackVersion = 1;
        }
        var rack = sim.World.Add(e, new Rack());
        if (sim.TryService<RackService>(out var rackService))
        {
            foreach (var saved in sheet.Rack)
            {
                var seat = rack.SeatAt(saved.Slot);
                seat.Codex = saved.Codex;
                seat.Stones.AddRange(saved.Stones);
                seat.Threads.AddRange(saved.Threads);
                if (seat.Codex is not null)
                {
                    rackService.Rebuild(sim, e, saved.Slot);
                }
            }
        }
        // Grafts the passives grant go back on the seats they were bound to.
        foreach (var (source, slot) in sheet.PassiveGrafts)
        {
            passives.Grafts[source] = slot;
        }
        Progression.PassiveGrafts.Apply(sim, e);
        // The bag's shape comes from the rules, not the save: a saved item keeps its cell while it still fits there.
        var inventory = sim.World.Add(e, InventoryService.CreateBag(sim.Content, sim.Service<ItemResolver>()));
        InventoryService.PlaceSaved(inventory, sheet.Bag);
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
        foreach (var flag in sheet.Flags)
        {
            log.Flags.Add(flag);
        }
        // Realms whose unlocks read quests and flags open once those are back.
        progression.RefreshRealms(sim, e);
        // Container rungs read the same; seated relics apply before resources fill.
        if (sim.TryService<ContainerService>(out var containers))
        {
            containers.Load(sim, e, sheet.Containers, sheet.Vault);
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
            if (sim.World.TryGet<PassiveState>(e, out var passives))
            {
                sheet.PassivePicks = passives.Picks.ToDictionary(kv => kv.Key, kv => new List<string>(kv.Value), StringComparer.Ordinal);
                sheet.PassiveGranted = new Dictionary<string, int>(passives.Granted, StringComparer.Ordinal);
                sheet.OpenRealms = passives.OpenRealms.ToList();
                sheet.PassiveRefunds = passives.Refunds;
                sheet.PassiveGrafts = new Dictionary<string, int>(passives.Grafts, StringComparer.Ordinal);
            }
            sheet.Resources = new Dictionary<string, double>(stats.Resources, StringComparer.Ordinal);
        }
        if (sim.World.TryGet<WeaveBook>(e, out var book))
        {
            sim.Service<RackService>().Reconcile(sim, e);
            sheet.Weaves = book.Slots.Select(s => s.Spec).ToList();
        }
        if (sim.World.TryGet<Rack>(e, out var rack))
        {
            sheet.Rack = new List<SavedSeat>();
            for (var i = 0; i < rack.Seats.Count; i++)
            {
                var seat = rack.Seats[i];
                if (!seat.IsEmpty)
                {
                    sheet.Rack.Add(new SavedSeat { Slot = i, Codex = seat.Codex, Stones = new List<ItemInstance?>(seat.Stones), Threads = new List<string>(seat.Threads) });
                }
            }
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
        if (sim.World.TryGet<Containers>(e, out var boards) && sim.TryService<ContainerService>(out var containers))
        {
            sheet.Containers = containers.Capture(sim, e, "character");
            if (boards.AccountLoaded)
            {
                sheet.Vault = containers.Capture(sim, e, "account");
            }
        }
        if (sim.World.TryGet<QuestLog>(e, out var log))
        {
            sheet.Quests = log.Quests.ToDictionary(kv => kv.Key, kv => new SavedQuest(kv.Key, kv.Value.State, new Dictionary<string, int>(kv.Value.Counts), kv.Value.StartedAt), StringComparer.Ordinal);
            sheet.CompletedQuests = log.Completed.ToList();
            sheet.Discovered = log.Discovered.ToList();
            sheet.Reputation = new Dictionary<string, double>(log.Reputation, StringComparer.Ordinal);
            sheet.Flags = log.Flags.ToList();
        }
        sheet.LastPlayedAt = DateTimeOffset.UtcNow;
    }
}
