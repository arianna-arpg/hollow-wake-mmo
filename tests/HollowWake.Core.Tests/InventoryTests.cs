using System.Numerics;
using HollowWake.Core.Characters;
using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Items;
using HollowWake.Core.Maths;
using HollowWake.Core.Sim;
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
