using System.Numerics;
using HollowWake.Core.Characters;
using HollowWake.Core.Components;
using HollowWake.Core.Crafting;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Items;
using HollowWake.Core.Maths;
using HollowWake.Core.Patterns;
using HollowWake.Core.Progression;
using HollowWake.Core.Sim;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>
/// Item contracts beyond the base roll: superior commons and overrolled magic, shared rolls, the affix pool law and
/// rarity floors, unique choice families, gear sockets, and the services stations sell.
/// </summary>
public class ItemContractTests
{
    private static (Simulation Sim, Entity Hero) Hero(string id)
    {
        var sim = ZoneBuilder.CreateArena(CoreContent.Registry, 23);
        var factory = sim.Service<CharacterFactory>();
        var sheet = factory.Create(sim, id, "acct", "Tamsin", "holdfast", sim.Service<ItemGenerator>());
        sheet.Position = new Vector3(8, 8, 0);
        return (sim, factory.Materialize(sim, sheet));
    }

    [Fact]
    public void OnlyCommonsMintSuperiorAndTheirRollLandsInTheTopWindow()
    {
        var gen = new ItemGenerator(CoreContent.Registry);
        var superior = 0;
        for (var seed = 1; seed <= 400; seed++)
        {
            var common = gen.Roll("short_sword", 10, "common", new Rng((ulong)seed));
            if (common.Superior)
            {
                superior++;
                Assert.True(common.BaseRoll >= 0.75 - 1e-9);
                Assert.StartsWith("Superior ", common.Name);
            }
            Assert.False(gen.Roll("short_sword", 10, "magic", new Rng((ulong)seed)).Superior);
        }
        Assert.InRange(superior, 5, 45);
    }

    [Fact]
    public void TheContractsDrawFromTheItemsOwnSeedSoTheSharedStreamIsUntouched()
    {
        // A common sword draws its base roll and its seed from the shared stream and nothing else, superior,
        // socketed or plain: the contracts draw from the seed.
        var gen = new ItemGenerator(CoreContent.Registry);
        var contracts = 0;
        for (var seed = 1; seed <= 200; seed++)
        {
            var shared = new Rng((ulong)seed);
            var item = gen.Roll("short_sword", 10, "common", shared);
            contracts += item.Superior || item.Sockets is not null ? 1 : 0;
            var expected = new Rng((ulong)seed);
            expected.NextDouble();
            expected.NextUInt64();
            Assert.Equal(expected.NextUInt64(), shared.NextUInt64());
        }
        Assert.True(contracts > 0);
    }

    [Fact]
    public void MagicMayOverrollATierAboveTheLevelGate()
    {
        var gen = new ItemGenerator(CoreContent.Registry);
        var vigorous = CoreContent.Registry.Get<AffixDef>("vigorous")!;
        var above = 0;
        for (var seed = 1; seed <= 3000; seed++)
        {
            // Level 12 reaches vigorous tier index 1 (level 12); index 0 (level 24) is above the gate.
            var ring = gen.Roll("ring", 12, "magic", new Rng((ulong)seed));
            foreach (var a in ring.Affixes)
            {
                if (a.Affix == "vigorous" && vigorous.Tiers[a.Tier].Level > ring.Level)
                {
                    above++;
                }
            }
            var rare = gen.Roll("ring", 12, "rare", new Rng((ulong)seed));
            Assert.DoesNotContain(rare.Affixes, x => CoreContent.Registry.Get<AffixDef>(x.Affix)!.Tiers[x.Tier].Level > rare.Level);
        }
        Assert.True(above > 0);
    }

    [Fact]
    public void RelicsRiseToTheirRarityFloorAndRollOnlyFromTheirPool()
    {
        var gen = new ItemGenerator(CoreContent.Registry);
        var pool = CoreContent.Registry.Get<ItemBaseDef>("relic_icon")!.AffixPool!;
        for (var seed = 1; seed <= 200; seed++)
        {
            var relic = gen.Roll("relic_icon", 20, "common", new Rng((ulong)seed));
            Assert.Equal("magic", relic.Rarity);
            Assert.All(relic.Affixes, a => Assert.Contains(a.Affix, pool));
        }
    }

    [Fact]
    public void LinesWithASharedRollRollTogether()
    {
        var gen = new ItemGenerator(CoreContent.Registry);
        var lines = CoreContent.Registry.Get<ItemBaseDef>("vestige_marrow")!.Implicits;
        for (var seed = 1; seed <= 20; seed++)
        {
            var stone = gen.Roll("vestige_marrow", 1, "common", new Rng((ulong)seed));
            var values = gen.Resolver.Lines(stone).Select(l => l.Value).ToList();
            var life = (values[0] - lines[0].Range![0]) / (lines[0].Range![1] - lines[0].Range![0]);
            var regen = (values[1] - lines[1].Range![0]) / (lines[1].Range![1] - lines[1].Range![0]);
            Assert.Equal(life, regen, 9);
        }
    }

    [Fact]
    public void AUniquePicksFromItsChoiceFamiliesWithoutRepeats()
    {
        var gen = new ItemGenerator(CoreContent.Registry);
        var seen = new HashSet<string>();
        for (var seed = 1; seed <= 30; seed++)
        {
            var item = gen.RollUnique("wellmother_reliquae", 12, new Rng((ulong)seed));
            Assert.Equal(2, item.UniqueChoices!.Count);
            Assert.NotEqual(item.UniqueChoices[0].Option, item.UniqueChoices[1].Option);
            seen.Add(item.UniqueChoices[0].Option);
            // The base's implicit, the unique's line and two picked lines.
            Assert.Equal(4, gen.Resolver.Lines(item).Count());
            var copy = item.Clone(1000 + seed);
            Assert.Equal(item.UniqueChoices[1].Option, copy.UniqueChoices![1].Option);
        }
        Assert.True(seen.Count >= 3);
    }

    [Fact]
    public void AChassisStoneKeepsTheRowsItRolledAndTheWeaveFoldsThem()
    {
        var gen = new ItemGenerator(CoreContent.Registry);
        var rolled = new HashSet<string>();
        ItemInstance? wide = null;
        ItemInstance? narrow = null;
        for (var seed = 1; seed <= 80; seed++)
        {
            var stone = gen.Roll("glyph_stone", 5, "common", new Rng((ulong)seed), new ItemGenerator.PayloadRequest { Glyph = "weavers_chassis" });
            var rows = stone.Payload!.Rows!;
            Assert.Equal(2, rows.Count);
            rolled.Add(rows["edge"] + "/" + rows["reach"]);
            Assert.Equal(rows, stone.Clone(10_000 + seed).Payload!.Rows);
            if (rows["reach"] == "wide")
            {
                wide ??= stone;
            }
            else if (rows["reach"] == "far" && rows["edge"] == "heavy")
            {
                narrow ??= stone;
            }
        }
        Assert.True(rolled.Count >= 4);

        // A bolt has no area: the stone that rolled wide sits dormant with the reason; a far, heavy one folds both rows.
        var compiler = new Weaves.WeaveCompiler(CoreContent.Registry, Effects.EffectRegistry.CreateDefault());
        var dormant = compiler.Compile(new WeaveSpec { Form = "bolt", Threads = { "ember" }, Glyphs = { WeaveItems.RefOf(wide!) } }, Weaves.GlyphFit.Dormant);
        Assert.Contains(dormant.Dormant, d => d.Glyph.Id == "weavers_chassis");
        var folded = compiler.Compile(new WeaveSpec { Form = "bolt", Threads = { "ember" }, Glyphs = { WeaveItems.RefOf(narrow!) } }, Weaves.GlyphFit.Dormant);
        Assert.Empty(folded.Dormant);
        Assert.Contains(folded.Modifiers, m => m.Attribute == "range" && m.Form == Attributes.ModifierForm.Increased);
        Assert.Contains(folded.Modifiers, m => m.Attribute == "damage" && m.Form == Attributes.ModifierForm.More && Math.Abs(m.Value - 0.15) < 1e-9);
        // A copy granted without rows takes the first row of each axis.
        var granted = compiler.Compile(new WeaveSpec { Form = "bolt", Threads = { "ember" }, Glyphs = { "weavers_chassis" } });
        Assert.Contains(granted.Modifiers, m => m.Attribute == "crit.chance");
    }

    [Fact]
    public void GearMintsSocketsByRarityUpToItsClassCap()
    {
        var gen = new ItemGenerator(CoreContent.Registry);
        var socketed = 0;
        for (var seed = 1; seed <= 300; seed++)
        {
            var chest = gen.Roll("iron_cuirass", 10, "rare", new Rng((ulong)seed));
            var count = chest.Sockets?.Count ?? 0;
            Assert.InRange(count, 0, 2);
            socketed += count > 0 ? 1 : 0;
            var ring = gen.Roll("ring", 10, "rare", new Rng((ulong)seed));
            Assert.Null(ring.Sockets);
            Assert.Null(gen.Roll("thread_floss", 10, "common", new Rng((ulong)seed)).Sockets);
        }
        Assert.True(socketed > 0);
    }

    [Fact]
    public void ASocketedVestigeLendsItsLinesToTheWornHostAndComesBackToTheBag()
    {
        var (sim, hero) = Hero("ic1");
        var gen = sim.Service<ItemGenerator>();
        var inv = sim.World.Get<Inventory>(hero)!;
        var stats = sim.World.Get<Stats>(hero)!;
        var cuirass = gen.Roll("leather_jerkin", 3, "common", new Rng(31));
        cuirass.Sockets = new List<ItemInstance?> { null };
        var vestige = gen.Roll("vestige_ember", 3, "common", new Rng(32));
        var glyph = gen.Roll("glyph_stone", 3, "common", new Rng(33));
        Assert.True(inv.TryAdd(cuirass));
        Assert.True(inv.TryAdd(vestige));
        Assert.True(inv.TryAdd(glyph));
        Assert.True(sim.Service<EquipmentService>().Equip(sim, hero, cuirass, null, fromInventory: true, out var reason), reason);
        var resist = stats.Get("resist.ember");

        Assert.False(GearSockets.Socket(sim, hero, glyph.Uid, cuirass.Uid, 0, out reason));
        Assert.Equal("not_socketable", reason);
        Assert.True(GearSockets.Socket(sim, hero, vestige.Uid, cuirass.Uid, 0, out reason), reason);
        Assert.False(inv.Bag.Contains(vestige));
        Assert.True(stats.Get("resist.ember") > resist);

        Assert.True(GearSockets.Unsocket(sim, hero, cuirass.Uid, 0, null, null, out reason), reason);
        Assert.True(inv.Bag.Contains(vestige));
        Assert.Equal(resist, stats.Get("resist.ember"), 9);
        Assert.False(GearSockets.Unsocket(sim, hero, cuirass.Uid, 0, null, null, out reason));
        Assert.Equal("empty_socket", reason);
    }

    [Fact]
    public void AFontRaisesStonesMergesCodicesAndResetsPassivesForShards()
    {
        var (sim, hero) = Hero("ic2");
        var gen = sim.Service<ItemGenerator>();
        var inv = sim.World.Get<Inventory>(hero)!;
        var font = sim.Service<PatternFactory>().Spawn(sim, "passive_font", new Vector3(9.5f, 8, 0));
        var stone = gen.Roll("glyph_stone", 3, "common", new Rng(41));
        Assert.True(inv.TryAdd(stone));
        var level = stone.Payload!.Level;

        // Only at the station whose pane is open.
        Assert.False(StationServices.Perform(sim, hero, font, "raise_stone", new StationServices.Target(stone.Uid, null, null), out var reason));
        Assert.Equal("no_station", reason);
        Assert.True(sim.Service<Interaction.InteractionService>().Interact(sim, hero, font, out reason), reason);
        var quotes = StationServices.For(sim, hero, font);
        var quote = Assert.Single(quotes, q => q.Def.Id == "raise_stone" && q.Target.Item == stone.Uid);
        Assert.False(quote.Enabled);
        Assert.False(StationServices.Perform(sim, hero, font, "raise_stone", new StationServices.Target(stone.Uid, null, null), out reason));
        Assert.StartsWith("cannot_afford", reason);

        inv.AddCurrency("shards", 5000);
        var price = quote.Price["shards"];
        Assert.True(StationServices.Perform(sim, hero, font, "raise_stone", new StationServices.Target(stone.Uid, null, null), out reason), reason);
        Assert.Equal(level + 1, stone.Payload.Level);
        Assert.Equal(5000 - price, inv.CurrencyOf("shards"));

        // Two copies of a codex become one of the next rarity.
        var codexA = gen.Roll("weave_codex", 3, "common", new Rng(42), new ItemGenerator.PayloadRequest { Form = "bolt" });
        var codexB = gen.Roll("weave_codex", 3, "common", new Rng(43), new ItemGenerator.PayloadRequest { Form = "bolt" });
        Assert.True(inv.TryAdd(codexA));
        Assert.True(inv.TryAdd(codexB));
        Assert.Contains(StationServices.For(sim, hero, font), q => q.Def.Id == "merge_codices" && q.Enabled);
        Assert.True(StationServices.Perform(sim, hero, font, "merge_codices", new StationServices.Target(codexA.Uid, null, null), out reason), reason);
        Assert.Equal("magic", codexA.Rarity);
        Assert.Null(inv.Find(codexB.Uid));

        // A reset returns what was bought.
        var progression = sim.Service<ProgressionService>();
        Assert.False(StationServices.Perform(sim, hero, font, "reset_passives", default, out reason));
        Assert.Equal("nothing_to_reset", reason);
        sim.World.Get<Stats>(hero)!.Level = 5;
        Assert.True(progression.Allocate(sim, hero, "hf1", null, out reason), reason);
        Assert.True(StationServices.Perform(sim, hero, font, "reset_passives", default, out reason), reason);
        Assert.DoesNotContain("hf1", sim.World.Get<Stats>(hero)!.Passives);
    }

    [Fact]
    public void AnAnvilChiselsSocketsUpToTheCap()
    {
        var (sim, hero) = Hero("ic3");
        var gen = sim.Service<ItemGenerator>();
        var inv = sim.World.Get<Inventory>(hero)!;
        inv.AddCurrency("shards", 5000);
        var anvil = sim.Service<PatternFactory>().Spawn(sim, "anvil", new Vector3(9.5f, 8, 0));
        Assert.True(sim.Service<Interaction.InteractionService>().Interact(sim, hero, anvil, out var reason), reason);
        var helm = gen.Roll("iron_helm", 3, "common", new Rng(51));
        helm.Sockets = null;
        Assert.True(inv.TryAdd(helm));
        var target = new StationServices.Target(helm.Uid, null, null);
        Assert.True(StationServices.Perform(sim, hero, anvil, "chisel_socket", target, out reason), reason);
        Assert.Single(helm.Sockets!);
        // A helm is armor, whose cap is one.
        Assert.False(StationServices.Perform(sim, hero, anvil, "chisel_socket", target, out reason));
        Assert.Equal("socket_cap", reason);
    }
}
