using System.Numerics;
using System.Text.Json;
using HollowWake.Core.Characters;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Ecs;
using HollowWake.Core.Items;
using HollowWake.Core.Maths;
using HollowWake.Core.Patterns;
using HollowWake.Core.Progression;
using HollowWake.Core.Sim;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>Boards beyond the bag: the reliquary whose seated relics are active, its unlock rungs, stash pages and their owners.</summary>
public class ContainerTests
{
    private static (Simulation Sim, Entity Hero, CharacterSheet Sheet) Hero(string id, ContainerSave? vault = null)
    {
        var sim = ZoneBuilder.CreateArena(CoreContent.Registry, 17);
        var factory = sim.Service<CharacterFactory>();
        var sheet = factory.Create(sim, id, "acct", "Tamsin", "holdfast", sim.Service<ItemGenerator>());
        sheet.Position = new Vector3(8, 8, 0);
        sheet.Vault = vault;
        return (sim, factory.Materialize(sim, sheet), sheet);
    }

    private static ItemInstance Give(Simulation sim, Entity hero, string baseId, int seed = 1, string rarity = "magic")
    {
        var item = sim.Service<ItemGenerator>().Roll(baseId, 12, rarity, new Rng((ulong)seed));
        Assert.True(sim.World.Get<Inventory>(hero)!.TryAdd(item));
        return item;
    }

    private static bool Move(Simulation sim, Entity hero, ItemInstance item, string container, int x, int y, out string reason) =>
        sim.Service<InventoryService>().Move(sim, hero, item.Uid, container, x, y, null, out reason);

    [Fact]
    public void SeatedRelicsApplyUnderTheContainerAndLeaveWithTheRelic()
    {
        var (sim, hero, _) = Hero("ct1");
        var stats = sim.World.Get<Stats>(hero)!;
        var relic = Give(sim, hero, "relic_shard");
        Assert.Equal("magic", relic.Rarity);
        var life = stats.Get("life.max");
        var xp = stats.Get("xp.gain");

        // In the bag a relic does nothing.
        Assert.Equal(life, stats.Get("life.max"));
        Assert.True(Move(sim, hero, relic, "reliquary", 0, 0, out var reason), reason);
        Assert.True(stats.Get("life.max") > life);
        Assert.Contains(stats.Attributes.AllModifiers, m => m.Source == "container:reliquary");
        // One cell occupied: the experience price is one step.
        Assert.Equal(1, stats.Gauge("container.reliquary.cells"));
        Assert.Equal(xp - 0.01, stats.Get("xp.gain"), 6);

        // Back to the bag: nothing of it stays.
        Assert.True(Move(sim, hero, relic, InventoryService.Bag, 11, 4, out reason), reason);
        Assert.Equal(life, stats.Get("life.max"));
        Assert.Equal(xp, stats.Get("xp.gain"), 6);
        Assert.DoesNotContain(stats.Attributes.AllModifiers, m => m.Source == "container:reliquary");
    }

    [Fact]
    public void TheReliquaryTakesOnlyRelicsOnUnlockedCells()
    {
        var (sim, hero, _) = Hero("ct2");
        var sword = Give(sim, hero, "short_sword", 2, "common");
        Assert.False(Move(sim, hero, sword, "reliquary", 0, 0, out var reason));
        Assert.Equal("not_accepted", reason);
        Assert.NotNull(sword.X);

        var relic = Give(sim, hero, "relic_shard", 3);
        // The second rung is locked at level 1: its cells are masked off.
        Assert.False(Move(sim, hero, relic, "reliquary", 2, 0, out reason));
        Assert.Equal("no_room", reason);
        Assert.True(sim.World.Get<Inventory>(hero)!.Bag.Contains(relic));
    }

    [Fact]
    public void RungsUnlockAsTheHeroLevelsAndShowOnTheBoard()
    {
        var (sim, hero, _) = Hero("ct3");
        var containers = sim.World.Get<Containers>(hero)!;
        Assert.Equal(4, containers.Boards["reliquary"].UsableCells);
        var stats = sim.World.Get<Stats>(hero)!;
        sim.Tick();
        stats.Level = 10;
        sim.Events.Publish(new LevelUp(hero, 10));
        sim.Tick();
        Assert.Equal(8, containers.Boards["reliquary"].UsableCells);
        Assert.Equal(new[] { "XXXX..", "XXXX..", "......", "......" }, containers.Boards["reliquary"].MaskRows());
    }

    [Fact]
    public void AUniqueCopyLimitHoldsPerBoard()
    {
        var (sim, hero, _) = Hero("ct4");
        sim.World.Get<Stats>(hero)!.Level = 10;
        sim.Service<ContainerService>().Refresh(sim, hero);
        var gen = sim.Service<ItemGenerator>();
        var inv = sim.World.Get<Inventory>(hero)!;
        var first = gen.RollUnique("wellmother_reliquae", 12, new Rng(5));
        var second = gen.RollUnique("wellmother_reliquae", 12, new Rng(6));
        Assert.True(inv.TryAdd(first));
        Assert.True(inv.TryAdd(second));
        Assert.True(Move(sim, hero, first, "reliquary", 0, 0, out var reason), reason);
        Assert.False(Move(sim, hero, second, "reliquary", 2, 0, out reason));
        Assert.Equal("copy_limit", reason);
    }

    [Fact]
    public void StashPagesAreReachedAtAChestAndBoughtWithShards()
    {
        var (sim, hero, _) = Hero("ct5");
        var service = sim.Service<ContainerService>();
        var inv = sim.World.Get<Inventory>(hero)!;
        var helm = Give(sim, hero, "iron_helm", 4, "common");
        Assert.False(Move(sim, hero, helm, "personal_stash:1", 0, 0, out var reason));
        Assert.Equal("out_of_reach", reason);

        sim.Service<PatternFactory>().Spawn(sim, "stash_chest", new Vector3(9.5f, 8, 0));
        Assert.True(Move(sim, hero, helm, "personal_stash:1", 3, 2, out reason), reason);
        Assert.Equal((3, 2), (helm.X, helm.Y));
        Assert.False(inv.Bag.Contains(helm));

        // Page two is not owned until it is bought: 100 * 2 * 2 shards.
        Assert.False(Move(sim, hero, helm, "personal_stash:2", 0, 0, out reason));
        Assert.Equal("page_locked", reason);
        Assert.False(service.BuyPage(sim, hero, "personal_stash", out reason));
        Assert.StartsWith("cannot_afford", reason);
        inv.AddCurrency("shards", 1000);
        Assert.True(service.BuyPage(sim, hero, "personal_stash", out reason), reason);
        Assert.Equal(600, inv.CurrencyOf("shards"));
        Assert.True(Move(sim, hero, helm, "personal_stash:2", 0, 0, out reason), reason);
    }

    [Fact]
    public void BoardsSaveWithTheirOwnersAndTheVaultTravelsAcrossCharacters()
    {
        var (sim, hero, sheet) = Hero("ct6", new ContainerSave());
        sim.Service<PatternFactory>().Spawn(sim, "stash_chest", new Vector3(9.5f, 8, 0));
        var relic = Give(sim, hero, "relic_icon", 7);
        var helm = Give(sim, hero, "iron_helm", 8, "common");
        Assert.True(Move(sim, hero, relic, "reliquary", 0, 0, out var reason), reason);
        Assert.True(Move(sim, hero, helm, "personal_stash:1", 5, 5, out reason), reason);
        var factory = sim.Service<CharacterFactory>();
        factory.Capture(sim, hero, sheet);

        // The character's file holds its stash; the reliquary is the account's and rides on the vault.
        var json = JsonSerializer.Serialize(sheet, ContentJson.Options);
        Assert.DoesNotContain("reliquary", json);
        Assert.True(sheet.Containers.Boards.ContainsKey("personal_stash:1"));
        Assert.True(sheet.Vault!.Boards.ContainsKey("reliquary"));
        var loaded = JsonSerializer.Deserialize<CharacterSheet>(json, ContentJson.Options)!;
        loaded.Vault = JsonSerializer.Deserialize<ContainerSave>(JsonSerializer.Serialize(sheet.Vault, ContentJson.Options), ContentJson.Options);
        var again = factory.Materialize(sim, loaded);
        var boards = sim.World.Get<Containers>(again)!;
        Assert.Equal(helm.Uid, boards.Boards["personal_stash:1"].At(5, 5)!.Uid);
        Assert.Equal(relic.Uid, boards.Boards["reliquary"].At(0, 0)!.Uid);
        Assert.Contains(sim.World.Get<Stats>(again)!.Attributes.AllModifiers, m => m.Source == "container:reliquary");

        // Another character of the account sees the reliquary but not the first one's stash.
        var other = factory.Create(sim, "ct6b", "acct", "Odile", "holdfast", sim.Service<ItemGenerator>());
        other.Position = new Vector3(8, 8, 0);
        other.Vault = sheet.Vault;
        var second = factory.Materialize(sim, other);
        var theirs = sim.World.Get<Containers>(second)!;
        Assert.Equal(relic.Uid, theirs.Boards["reliquary"].At(0, 0)!.Uid);
        Assert.Empty(theirs.Boards["personal_stash:1"].Placed);
    }

    [Fact]
    public void ItemsOfBoardsContentNoLongerHasAreNeverLost()
    {
        var gen = new ItemGenerator(CoreContent.Registry, 900);
        var mine = gen.Roll("iron_helm", 5, "common", new Rng(61));
        var ours = gen.Roll("relic_shard", 5, "magic", new Rng(62));
        var waiting = gen.Roll("iron_helm", 5, "common", new Rng(63));
        var vault = new ContainerSave();
        vault.Boards["old_vault_box"] = new List<ItemInstance> { ours };
        var (sim, hero, sheet) = Hero("ct8", vault);
        var service = sim.Service<ContainerService>();
        var character = new ContainerSave();
        character.Boards["old_box"] = new List<ItemInstance> { mine };
        character.Boards["personal_stash:3"] = new List<ItemInstance> { waiting };
        service.Load(sim, hero, character, vault);

        // The character's item comes back in the bag's overflow; the account's waits for its board.
        Assert.Contains(mine, sim.World.Get<Inventory>(hero)!.Overflow);
        var account = service.Capture(sim, hero, "account");
        Assert.Equal(ours.Uid, Assert.Single(account.Boards["old_vault_box"]).Uid);
        // A page not owned keeps its items for when it is bought.
        Assert.Equal(waiting.Uid, Assert.Single(service.Capture(sim, hero, "character").Boards["personal_stash:3"]).Uid);
    }

    [Fact]
    public void WithoutAnAccountSaveTheAccountBoardsAreNeverWritten()
    {
        var (sim, hero, sheet) = Hero("ct7");
        var relic = Give(sim, hero, "relic_shard", 9);
        Assert.True(Move(sim, hero, relic, "reliquary", 0, 0, out var reason), reason);
        sim.Service<CharacterFactory>().Capture(sim, hero, sheet);
        Assert.Null(sheet.Vault);
    }
}
