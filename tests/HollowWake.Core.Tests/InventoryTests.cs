using System.Numerics;
using HollowWake.Core.Characters;
using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Items;
using HollowWake.Core.Maths;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>The bag as a board of cells: footprints, placement, first fit, swaps, masks, equipment and persistence.</summary>
public class InventoryTests
{
    private static (Simulation Sim, Entity Hero, CharacterSheet Sheet) Hero(string calling = "holdfast", string id = "inv")
    {
        var sim = ZoneBuilder.CreateArena(CoreContent.Registry, 11);
        var factory = sim.Service<CharacterFactory>();
        var sheet = factory.Create(sim, id, "acct", "Tamsin", calling, sim.Service<ItemGenerator>());
        sheet.Position = new Vector3(8, 8, 0);
        return (sim, factory.Materialize(sim, sheet), sheet);
    }

    private static ItemInstance Roll(Simulation sim, string baseId, int seed = 1) => sim.Service<ItemGenerator>().Roll(baseId, 3, "common", new Rng((ulong)seed));

    [Fact]
    public void FootprintsComeFromTheBaseAndFirstFitScansRowByRow()
    {
        var (sim, hero, _) = Hero();
        var inv = sim.World.Get<Inventory>(hero)!;
        Assert.Equal(12 * 5, inv.Capacity);
        var sword = Roll(sim, "short_sword");
        Assert.Equal((1, 3), inv.FootprintOf(sword));
        Assert.True(inv.TryAdd(sword));
        Assert.True(inv.Bag.TryGetPlacement(sword, out var placed));
        Assert.Equal((1, 3), (placed.Width, placed.Height));
        // The floss from the calling took the first cell, so the sword sits beside it.
        Assert.Equal(sword, inv.Bag.At(sword.X!.Value, sword.Y!.Value + 2));
        Assert.True(inv.Bag.FindSpace(2, 2, out var x, out var y));
        Assert.True(inv.Bag.Fits(x, y, 2, 2));
    }

    [Fact]
    public void ABoardNeverOverlapsAndAMaskSwitchesCellsOff()
    {
        var board = new Board(4, 3, new[] { "XXXX", "X..X", "XXXX" });
        Assert.Equal(10, board.UsableCells);
        var a = new ItemInstance { Base = "short_sword", Uid = 1 };
        var b = new ItemInstance { Base = "short_sword", Uid = 2 };
        Assert.False(board.Place(a, 1, 0, 1, 3));
        Assert.True(board.Place(a, 0, 0, 1, 3));
        Assert.False(board.Place(b, 0, 1, 1, 1));
        Assert.True(board.FindSpace(1, 3, out var x, out var y));
        Assert.Equal((3, 0), (x, y));
        Assert.Equal(new[] { "XXXX", "X..X", "XXXX" }, board.MaskRows());
        Assert.True(board.Remove(a));
        Assert.Null(a.X);
        Assert.Equal(10, board.FreeCells);
    }

    [Fact]
    public void MovingOntoAnotherItemSwapsThemAndNothingOverlaps()
    {
        var (sim, hero, _) = Hero();
        var inv = sim.World.Get<Inventory>(hero)!;
        var service = sim.Service<InventoryService>();
        var sword = Roll(sim, "short_sword", 2);
        var helm = Roll(sim, "iron_helm", 3);
        Assert.True(inv.TryAddAt(sword, 6, 0));
        Assert.True(inv.TryAddAt(helm, 8, 0));
        var moved = new List<ItemMoved>();
        sim.Events.Subscribe<ItemMoved>(moved.Add);

        // To free cells.
        Assert.True(service.Move(sim, hero, sword.Uid, InventoryService.Bag, 6, 2, null, out var reason), reason);
        Assert.Equal((6, 2), (sword.X, sword.Y));
        // Onto the helm: the two trade places.
        Assert.True(service.Move(sim, hero, sword.Uid, InventoryService.Bag, 8, 0, null, out reason), reason);
        Assert.Equal((8, 0), (sword.X, sword.Y));
        Assert.Equal((6, 2), (helm.X, helm.Y));
        Assert.Equal(2, moved.Count);
        // Out of bounds and onto two items at once are refused, and nothing moves.
        Assert.False(service.Move(sim, hero, sword.Uid, InventoryService.Bag, 11, 4, null, out reason));
        Assert.Equal("out_of_bounds", reason);
        var cap = Roll(sim, "leather_cap", 4);
        Assert.True(inv.TryAddAt(cap, 9, 1));
        Assert.False(service.Move(sim, hero, helm.Uid, InventoryService.Bag, 8, 0, null, out reason));
        Assert.Equal("occupied", reason);
        Assert.Equal((6, 2), (helm.X, helm.Y));
        AssertNoOverlap(inv);
    }

    [Fact]
    public void EquippingFromTheBagSwapsTheWornItemIntoTheBagOrRefusesWithoutRoom()
    {
        var (sim, hero, _) = Hero();
        var inv = sim.World.Get<Inventory>(hero)!;
        var eq = sim.World.Get<Equipment>(hero)!;
        var service = sim.Service<InventoryService>();
        var worn = eq.In("main_hand")!;
        var axe = Roll(sim, "hand_axe", 5);
        Assert.True(inv.TryAdd(axe));
        Assert.True(service.Move(sim, hero, axe.Uid, InventoryService.Equipment, 0, 0, "main_hand", out var reason), reason);
        Assert.Same(axe, eq.In("main_hand"));
        Assert.Contains(worn, inv.Items);
        AssertNoOverlap(inv);

        // Fill every cell, then try to swap the worn axe for a bow: the axe and the buckler would need room the bag lacks.
        var bow = Roll(sim, "bow", 6);
        Assert.True(inv.TryAdd(bow));
        var filler = 10;
        while (inv.TryAdd(Roll(sim, "ring", filler++)))
        {
        }
        Assert.Equal(0, inv.FreeSlots);
        Assert.False(service.Move(sim, hero, bow.Uid, InventoryService.Equipment, 0, 0, "main_hand", out reason));
        Assert.Equal("bag_full", reason);
        Assert.Same(axe, eq.In("main_hand"));
        Assert.Contains(bow, inv.Items);
        Assert.NotNull(eq.In("off_hand"));
        AssertNoOverlap(inv);
    }

    [Fact]
    public void UnequippingToACellAndDroppingToTheWorld()
    {
        var (sim, hero, _) = Hero();
        var inv = sim.World.Get<Inventory>(hero)!;
        var eq = sim.World.Get<Equipment>(hero)!;
        var service = sim.Service<InventoryService>();
        var chest = eq.In("chest")!;
        Assert.True(service.Move(sim, hero, chest.Uid, InventoryService.Bag, 10, 2, null, out var reason), reason);
        Assert.Null(eq.In("chest"));
        Assert.Equal((10, 2), (chest.X, chest.Y));
        Assert.True(service.Move(sim, hero, chest.Uid, InventoryService.World, 0, 0, null, out reason), reason);
        Assert.DoesNotContain(chest, inv.Items);
        Assert.Contains(sim.World.Query<LootDrop>(), d => d.C1.Item == chest);
        Assert.False(service.Move(sim, hero, chest.Uid, InventoryService.Bag, 0, 0, null, out reason));
        Assert.Equal("no_item", reason);
    }

    [Fact]
    public void PositionsSurviveASaveAndItemsWithoutRoomAreKeptNotLost()
    {
        var (sim, hero, sheet) = Hero(id: "persist");
        var inv = sim.World.Get<Inventory>(hero)!;
        var sword = Roll(sim, "short_sword", 7);
        Assert.True(inv.TryAddAt(sword, 5, 1));
        var factory = sim.Service<CharacterFactory>();
        factory.Capture(sim, hero, sheet);
        var json = System.Text.Json.JsonSerializer.Serialize(sheet, Content.ContentJson.Options);
        var loaded = System.Text.Json.JsonSerializer.Deserialize<CharacterSheet>(json, Content.ContentJson.Options)!;
        var again = factory.Materialize(sim, loaded);
        var bag = sim.World.Get<Inventory>(again)!;
        var back = bag.Find(sword.Uid)!;
        Assert.Equal((5, 1), (back.X, back.Y));

        // A save holding more than the board can take keeps the rest without a cell.
        var crowded = new CharacterSheet { Id = "crowd", AccountId = "acct", Name = "Crowd", Calling = "holdfast" };
        for (var i = 0; i < 25; i++)
        {
            crowded.Bag.Add(Roll(sim, "iron_cuirass", 100 + i));
        }
        var crowd = sim.World.Get<Inventory>(factory.Materialize(sim, crowded))!;
        Assert.Equal(25, crowd.Items.Count());
        Assert.NotEmpty(crowd.Overflow);
        AssertNoOverlap(crowd);
    }

    // ------------------------------------------------------------ the weave rack

    private static ItemInstance Codex(Simulation sim, string form, string rarity = "common", int seed = 1, string? weave = null) =>
        sim.Service<ItemGenerator>().Roll("weave_codex", 1, rarity, new Rng((ulong)seed), new ItemGenerator.PayloadRequest { Form = weave is null ? form : null, Weave = weave });

    private static ItemInstance Stone(Simulation sim, string glyph, int seed = 1, int level = 1) =>
        sim.Service<ItemGenerator>().Roll("glyph_stone", 1, "common", new Rng((ulong)seed), new ItemGenerator.PayloadRequest { Glyph = glyph, Level = level });

    [Fact]
    public void ACallingStartsWithItsWeavesLearnedAsCodices()
    {
        var (sim, hero, sheet) = Hero("kindler", "rack1");
        var rack = sim.World.Get<Rack>(hero)!;
        var book = sim.World.Get<WeaveBook>(hero)!;
        var calling = sim.Content.Get<Definitions.CallingDef>("kindler")!;
        Assert.Equal(calling.Weaves.Count, sheet.Rack.Count);
        for (var i = 0; i < calling.Weaves.Count; i++)
        {
            Assert.Equal(calling.Weaves[i].Preset, rack.Seats[i].Codex!.Payload!.Weave);
            Assert.Equal(calling.Weaves[i].ToString(), book.Slots[i].Key);
            Assert.NotNull(sim.Service<WeaveRuntime>().Resolve(sim, book.Slots[i], out _));
            Assert.Same(book.Slots[i], rack.Seats[i].Bound);
        }
        Assert.Equal("Flame Bolt Codex", rack.Seats[0].Codex!.Name);
    }

    [Fact]
    public void StonesSocketIntoWeaveSlotsAndAMisfitStaysDormantWithItsReason()
    {
        var (sim, hero, _) = Hero("kindler", "rack2");
        var inv = sim.World.Get<Inventory>(hero)!;
        var book = sim.World.Get<WeaveBook>(hero)!;
        var service = sim.Service<RackService>();
        var bolt = Codex(sim, "bolt", seed: 2);
        var fork = Stone(sim, "fork", 3);
        Assert.True(inv.TryAdd(bolt) && inv.TryAdd(fork));

        Assert.True(service.Learn(sim, hero, bolt.Uid, 4, out var reason), reason);
        Assert.DoesNotContain(bolt, inv.Items);
        Assert.Equal("bolt", book.Slots[4].Spec.Form);
        Assert.Equal(new List<string> { "ember" }, book.Slots[4].Spec.Threads);
        Assert.True(service.Socket(sim, hero, fork.Uid, 4, 0, out reason), reason);
        Assert.Equal("fork", Assert.Single(book.Slots[4].Spec.Glyphs).Id);
        Assert.Empty(book.Slots[4].Dormant);
        Assert.Contains(book.Slots[4].Compiled!.GlyphRefs, g => g.Id == "fork");

        // A stone whose gate fails stays socketed, dormant, with its reason; the weave still compiles without it.
        var second = Stone(sim, "fork", 4);
        Assert.True(inv.TryAdd(second));
        Assert.True(service.Socket(sim, hero, second.Uid, 1, 0, out reason), reason);
        var nova = book.Slots[1];
        Assert.NotNull(nova.Compiled);
        var dormant = Assert.Single(nova.Dormant);
        Assert.Equal("fork", dormant.Ref.Id);
        Assert.Contains("projectile", dormant.Reason);

        // A socket with no codex, a socket past the codex's count and a missing stone are refused.
        var third = Stone(sim, "ring", 5);
        Assert.True(inv.TryAdd(third));
        Assert.False(service.Socket(sim, hero, third.Uid, 6, 0, out reason));
        Assert.Equal("not_learned", reason);
        Assert.False(service.Socket(sim, hero, third.Uid, 4, 1, out reason));
        Assert.Equal("no_socket", reason);
        Assert.False(service.Socket(sim, hero, 999999, 4, 0, out reason));
        Assert.Equal("no_item", reason);
    }

    [Fact]
    public void ABetterCopySwapsInPlaceAndKeepsItsStones()
    {
        var (sim, hero, _) = Hero("kindler", "rack3");
        var inv = sim.World.Get<Inventory>(hero)!;
        var rack = sim.World.Get<Rack>(hero)!;
        var service = sim.Service<RackService>();
        var common = Codex(sim, "bolt", "common", 6);
        var magic = Codex(sim, "bolt", "magic", 7);
        var fork = Stone(sim, "fork", 8);
        Assert.True(inv.TryAdd(common) && inv.TryAdd(magic) && inv.TryAdd(fork));
        Assert.True(service.Learn(sim, hero, common.Uid, 5, out var reason), reason);
        Assert.True(service.Socket(sim, hero, fork.Uid, 5, 0, out reason), reason);
        Assert.Equal(1, WeaveItems.SocketsOf(sim.Content, common));
        Assert.Equal(2, WeaveItems.SocketsOf(sim.Content, magic));
        var magicCell = (magic.X, magic.Y);

        Assert.True(service.Learn(sim, hero, magic.Uid, 5, out reason), reason);
        Assert.Same(magic, rack.Seats[5].Codex);
        Assert.Same(fork, rack.Seats[5].Stones[0]);
        Assert.Contains(common, inv.Items);
        Assert.Equal(magicCell, (common.X, common.Y));

        // The stone comes out into a chosen cell; unlearning returns the codex.
        Assert.True(inv.Bag.Fits(11, 4, 1, 1));
        Assert.True(service.Unsocket(sim, hero, 5, 0, 11, 4, out reason), reason);
        Assert.Equal((11, 4), (fork.X, fork.Y));
        Assert.True(service.Unlearn(sim, hero, 5, null, null, out reason), reason);
        Assert.Contains(magic, inv.Items);
        Assert.Null(rack.Seats[5].Codex);
        Assert.Null(sim.World.Get<WeaveBook>(hero)!.Slots[5].Spec.Form);
    }

    [Fact]
    public void AStoneCardKnowsHowItFitsEverySeat()
    {
        var (sim, hero, _) = Hero("kindler", "rack4");
        var service = sim.Service<RackService>();
        var ring = Stone(sim, "ring", 9);
        // Ember bolt has no area, the storm nova has one; a full seat is full.
        Assert.Equal("misfit", service.FitOf(sim, hero, ring, 0).Fit);
        Assert.Equal("fits", service.FitOf(sim, hero, ring, 1).Fit);
        var other = Stone(sim, "ring", 10);
        Assert.True(sim.World.Get<Inventory>(hero)!.TryAdd(other));
        Assert.True(service.Socket(sim, hero, other.Uid, 1, 0, out var reason), reason);
        Assert.Equal("full", service.FitOf(sim, hero, ring, 1).Fit);
    }

    [Fact]
    public void OwnershipChecksTheSeatsCodexStonesLevelAndThreads()
    {
        var (sim, hero, _) = Hero("kindler", "rack5");
        var inv = sim.World.Get<Inventory>(hero)!;
        var service = sim.Service<RackService>();
        var ownership = sim.Service<Weaves.IWeaveOwnership>();
        var bolt = Codex(sim, "bolt", seed: 11);
        var fork = Stone(sim, "fork", 12);
        Assert.True(inv.TryAdd(bolt) && inv.TryAdd(fork));
        Assert.True(service.Learn(sim, hero, bolt.Uid, 4, out var reason), reason);
        Assert.True(service.Socket(sim, hero, fork.Uid, 4, 0, out reason), reason);
        var owned = new Definitions.WeaveSpec { Form = "bolt", Threads = new List<string> { "storm" }, Glyphs = new List<Definitions.GlyphRef> { "fork@1" }, Level = 1 };
        Assert.True(ownership.Allows(sim, hero, 4, owned, out reason), reason);
        Assert.False(ownership.Allows(sim, hero, 4, owned with { Threads = new List<string> { "void" } }, out reason));
        Assert.Equal("not_attuned:void", reason);
        Assert.False(ownership.Allows(sim, hero, 4, owned with { Glyphs = new List<Definitions.GlyphRef> { "fork@2" } }, out reason));
        Assert.Equal("glyph_not_owned", reason);
        Assert.False(ownership.Allows(sim, hero, 4, owned with { Form = "nova" }, out reason));
        Assert.Equal("not_learned", reason);
        Assert.False(ownership.Allows(sim, hero, 6, owned, out reason));
        Assert.Equal("not_learned", reason);
        Assert.True(ownership.Allows(sim, hero, 0, new Definitions.WeaveSpec { Preset = "ember_bolt" }, out reason), reason);
    }

    [Fact]
    public void WithOwnershipOnASetWeaveMustNameWhatTheSeatHolds()
    {
        Assert.True(CoreContent.Registry.Rules.Flag("casting.require_ownership", false));
        var (sim, hero, _) = Hero("kindler", "rack7");
        var player = sim.World.Get<Player>(hero)!;
        var rejected = new List<Systems.IntentRejected>();
        sim.Events.Subscribe<Systems.IntentRejected>(rejected.Add);
        // Seat 0 holds the ember bolt codex: binding it again (with an attuned Thread) is fine; a weave it does not teach is not.
        player.Intents.Enqueue(new Intents.SetWeaveIntent(0, new Definitions.WeaveSpec { Preset = "ember_bolt", Threads = new List<string> { "storm" } }));
        player.Intents.Enqueue(new Intents.SetWeaveIntent(1, new Definitions.WeaveSpec { Preset = "ember_bolt" }));
        sim.Tick();
        Assert.Equal("not_learned", Assert.Single(rejected).Reason);
        var book = sim.World.Get<WeaveBook>(hero)!;
        Assert.Equal(new List<string> { "storm" }, book.Slots[0].Spec.Threads);
        Assert.Equal("storm_nova", book.Slots[1].Spec.Preset);
        // The seat adopts the rebound slot and its Threads.
        sim.Service<RackService>().Reconcile(sim, hero);
        var seat = sim.World.Get<Rack>(hero)!.Seats[0];
        Assert.Same(book.Slots[0], seat.Bound);
        Assert.Equal(new List<string> { "storm" }, seat.Threads);
    }

    [Fact]
    public void TheRackSurvivesASaveAndFollowsReorderedSlots()
    {
        var (sim, hero, sheet) = Hero("kindler", "rack6");
        var inv = sim.World.Get<Inventory>(hero)!;
        var service = sim.Service<RackService>();
        var fork = Stone(sim, "fork", 13);
        Assert.True(inv.TryAdd(fork));
        Assert.True(service.Socket(sim, hero, fork.Uid, 0, 0, out var reason), reason);
        // Reordering the book (as swapping weave slots does) carries the seats along.
        var book = sim.World.Get<WeaveBook>(hero)!;
        (book.Slots[0], book.Slots[2]) = (book.Slots[2], book.Slots[0]);
        service.Reconcile(sim, hero);
        var rack = sim.World.Get<Rack>(hero)!;
        Assert.Same(fork, rack.Seats[2].Stones[0]);
        Assert.Equal("ember_bolt", rack.Seats[2].Codex!.Payload!.Weave);

        var factory = sim.Service<CharacterFactory>();
        factory.Capture(sim, hero, sheet);
        var json = System.Text.Json.JsonSerializer.Serialize(sheet, Content.ContentJson.Options);
        var loaded = System.Text.Json.JsonSerializer.Deserialize<CharacterSheet>(json, Content.ContentJson.Options)!;
        var again = factory.Materialize(sim, loaded);
        var rebuilt = sim.World.Get<WeaveBook>(again)!;
        Assert.Equal("ember_bolt", rebuilt.Slots[2].Spec.Preset);
        Assert.Equal(fork.Uid, sim.World.Get<Rack>(again)!.Seats[2].Stones[0]!.Uid);
        Assert.Contains(rebuilt.Slots[2].Spec.Glyphs, g => g.Id == "fork");
    }

    [Fact]
    public void DropsLeanTowardWhatTheDropperWeaves()
    {
        var sim = ZoneBuilder.CreateArena(CoreContent.Registry, 5);
        var gen = sim.Service<ItemGenerator>();
        int Forks(IReadOnlyCollection<string>? lean)
        {
            var forks = 0;
            for (var i = 0; i < 300; i++)
            {
                foreach (var r in gen.RollLoot("weave_items", 5, new Rng((ulong)(1000 + i)), lean: lean))
                {
                    if (r.Item?.Payload?.Glyph == "fork")
                    {
                        forks++;
                    }
                }
            }
            return forks;
        }
        var plain = Forks(null);
        var leaned = Forks(new HashSet<string> { "fork" });
        Assert.True(leaned > plain * 2, $"fork stones: {leaned} with the lean against {plain} without");
    }

    private static void AssertNoOverlap(Inventory inv)
    {
        var covered = new HashSet<(int, int)>();
        foreach (var p in inv.Bag.Placed)
        {
            Assert.Equal((p.X, p.Y), (p.Item.X, p.Item.Y));
            for (var dy = 0; dy < p.Height; dy++)
            {
                for (var dx = 0; dx < p.Width; dx++)
                {
                    Assert.True(covered.Add((p.X + dx, p.Y + dy)), $"{p.Item.Name} overlaps at {p.X + dx},{p.Y + dy}");
                    Assert.Same(p.Item, inv.Bag.At(p.X + dx, p.Y + dy));
                }
            }
        }
    }
}
