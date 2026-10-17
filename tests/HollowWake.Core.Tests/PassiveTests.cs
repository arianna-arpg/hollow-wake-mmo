using System.Numerics;
using HollowWake.Core.Characters;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Items;
using HollowWake.Core.Patterns;
using HollowWake.Core.Progression;
using HollowWake.Core.Sim;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>Choice nodes and their deals, realms with their own currencies, and refunds at a font.</summary>
public class PassiveTests
{
    /// <summary>The core content plus small test clusters hanging off the Kindler start.</summary>
    private static readonly Lazy<ContentRegistry> Registry = new(() =>
    {
        var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out var report);
        Assert.False(report.HasErrors, report.ToString());
        registry.With(new PassiveChoiceDef
        {
            Id = "t_capped",
            Max = 1,
            Unique = ChoiceUnique.None,
            Options = { new PassiveOptionDef { Id = "a", Modifiers = { new Attributes.Modifier { Attribute = "might", Value = 1 } } }, new PassiveOptionDef { Id = "b" } },
        });
        PassiveNodeDef Node(string id, string group) => new() { Id = id, Links = { "start_kindler" }, Choice = new PassiveChoiceRef { Group = group } };
        registry.With(Node("t_each_a", "doctrine_of_war")).With(Node("t_each_b", "doctrine_of_war"));
        registry.With(Node("t_first_a", "seam_vow")).With(Node("t_first_b", "seam_vow"));
        registry.With(Node("t_sole_a", "waymark")).With(Node("t_sole_b", "waymark"));
        registry.With(Node("t_two", "wellmother_vows"));
        registry.With(Node("t_cap_a", "t_capped")).With(Node("t_cap_b", "t_capped"));
        return registry;
    });

    private static (Simulation Sim, Entity Hero, CharacterSheet Sheet) Hero(string calling = "kindler", string id = "pas", int level = 20)
    {
        var sim = ZoneBuilder.CreateArena(Registry.Value, 21);
        var factory = sim.Service<CharacterFactory>();
        var sheet = factory.Create(sim, id, "acct", "Ilse", calling, sim.Service<ItemGenerator>());
        sheet.Level = level;
        sheet.Position = new Vector3(8, 8, 0);
        return (sim, factory.Materialize(sim, sheet), sheet);
    }

    [Fact]
    public void AChoiceNodeNeedsAnOptionAndItsPickAppliesModifiers()
    {
        var (sim, hero, _) = Hero();
        var progression = sim.Service<ProgressionService>();
        var stats = sim.World.Get<Stats>(hero)!;
        var might = stats.Get("might");
        var points = progression.Points(sim, hero, ProgressionService.MainCurrency);
        Assert.False(progression.Allocate(sim, hero, "inner_a", null, out var reason));
        Assert.Equal("needs_option", reason);
        Assert.False(progression.Allocate(sim, hero, "inner_a", "luck", out reason));
        Assert.Equal("unknown_option", reason);
        Assert.True(progression.Allocate(sim, hero, "inner_a", "might", out reason), reason);
        Assert.Equal(might + 4, stats.Get("might"));
        Assert.Equal(new List<string> { "might" }, sim.World.Get<PassiveState>(hero)!.Picks["inner_a"]);
        Assert.Equal(points - 1, progression.Points(sim, hero, ProgressionService.MainCurrency));
        // A one-pick node has no pick left; an ordinary node takes no option.
        Assert.False(progression.Allocate(sim, hero, "inner_a", "will", out reason));
        Assert.Equal("no_picks", reason);
        Assert.False(progression.Allocate(sim, hero, "kd1", "might", out reason));
        Assert.Equal("not_a_choice", reason);
    }

    [Fact]
    public void PickLimitsAndUniquenessPerNodeAndPerCharacter()
    {
        var (sim, hero, _) = Hero(id: "pas2");
        var progression = sim.Service<ProgressionService>();
        // Two picks at one node, each option once there.
        Assert.True(progression.Allocate(sim, hero, "t_two", "tending", out var reason), reason);
        Assert.False(progression.Allocate(sim, hero, "t_two", "tending", out reason));
        Assert.Equal("already_picked", reason);
        Assert.True(progression.Allocate(sim, hero, "t_two", "depth", out reason), reason);
        Assert.False(progression.Allocate(sim, hero, "t_two", "patience", out reason));
        Assert.Equal("no_picks", reason);
        // Doctrines are once per character, across every node that deals them.
        Assert.True(progression.Allocate(sim, hero, "t_each_a", "iron", out reason), reason);
        Assert.False(progression.Allocate(sim, hero, "t_each_b", "iron", out reason));
        Assert.Equal("already_picked", reason);
        Assert.True(progression.Allocate(sim, hero, "t_each_b", "vigil", out reason), reason);
        // A group with a cap of one pick across the character.
        Assert.True(progression.Allocate(sim, hero, "t_cap_a", "a", out reason), reason);
        Assert.False(progression.Allocate(sim, hero, "t_cap_b", "b", out reason));
        Assert.Equal("group_full", reason);
    }

    [Fact]
    public void FirstDealsShutOutTheClusterAndSoleSiblingsDealNothing()
    {
        var (sim, hero, _) = Hero(id: "pas3");
        var progression = sim.Service<ProgressionService>();
        Assert.True(progression.Allocate(sim, hero, "t_first_a", "ash", out var reason), reason);
        Assert.False(progression.Allocate(sim, hero, "t_first_b", "tide", out reason));
        Assert.Equal("exclusive", reason);

        Assert.True(progression.Allocate(sim, hero, "t_sole_a", "long_road", out reason), reason);
        Assert.False(progression.Allocate(sim, hero, "t_sole_b", "second_wind", out reason));
        Assert.Equal("deals_nothing", reason);
        Assert.True(progression.Allocate(sim, hero, "t_sole_b", null, out reason), reason);
        Assert.False(sim.World.Get<PassiveState>(hero)!.Picks.ContainsKey("t_sole_b"));
    }

    [Fact]
    public void RealmsOpenOnTheirUnlockWithTheirOwnCurrencyAndRoots()
    {
        var (sim, hero, _) = Hero(id: "pas4");
        var progression = sim.Service<ProgressionService>();
        var stats = sim.World.Get<Stats>(hero)!;
        var main = progression.Points(sim, hero, ProgressionService.MainCurrency);
        Assert.False(progression.Allocate(sim, hero, "dv1", null, out var reason));
        Assert.Equal("realm_sealed", reason);

        sim.World.Get<QuestLog>(hero)!.Completed.Add("the_wellmother");
        progression.RefreshRealms(sim, hero);
        Assert.True(progression.RealmOpen(sim, hero, "devotion"));
        Assert.Contains("devotion_root", stats.Passives);
        Assert.Contains("devotion_root", progression.Anchors(sim, hero));
        Assert.Equal(0, progression.Points(sim, hero, "devotion"));
        Assert.False(progression.Allocate(sim, hero, "dv1", null, out reason));
        Assert.Equal("no_points", reason);

        progression.GrantPoints(sim, hero, "devotion", 2);
        Assert.True(progression.Allocate(sim, hero, "dv1", null, out reason), reason);
        Assert.Equal(1, progression.Points(sim, hero, "devotion"));
        Assert.Equal(main, progression.Points(sim, hero, ProgressionService.MainCurrency));
        // Tree adjacency inside the realm: dv3 hangs from dv1 or dv2.
        Assert.True(progression.Allocate(sim, hero, "dv3", "patience", out reason), reason);
    }

    [Fact]
    public void RefundsNeedAFontKeepTheTreeConnectedAndPayFromTheWallet()
    {
        var (sim, hero, _) = Hero(id: "pas5");
        var progression = sim.Service<ProgressionService>();
        var stats = sim.World.Get<Stats>(hero)!;
        var inv = sim.World.Get<Inventory>(hero)!;
        Assert.True(progression.Allocate(sim, hero, "kd1", null, out var reason), reason);
        Assert.True(progression.Allocate(sim, hero, "kd2", null, out reason), reason);
        Assert.True(progression.Allocate(sim, hero, "inner_b", "will", out reason), reason);
        var points = progression.Points(sim, hero, ProgressionService.MainCurrency);

        Assert.False(progression.RefundAtFont(sim, hero, "kd2", out reason));
        Assert.Equal("no_font", reason);
        sim.Service<PatternFactory>().Spawn(sim, "passive_font", new Vector3(9.5f, 8, 0));
        Assert.NotNull(Interaction.Fonts.InReach(sim, hero));

        Assert.False(progression.RefundAtFont(sim, hero, "kd2", out reason));
        Assert.StartsWith("cannot_afford", reason);
        inv.AddCurrency("shards", 500);
        Assert.False(progression.RefundAtFont(sim, hero, "kd1", out reason));
        Assert.Equal("would_disconnect", reason);
        Assert.False(progression.RefundAtFont(sim, hero, "start_kindler", out reason));
        Assert.Equal("anchor", reason);

        var cost = progression.RefundCost(sim, hero, sim.Content.Get<PassiveNodeDef>("kd2")!)["shards"];
        Assert.True(progression.RefundAtFont(sim, hero, "kd2", out reason), reason);
        Assert.DoesNotContain("kd2", stats.Passives);
        Assert.Equal(points + 1, progression.Points(sim, hero, ProgressionService.MainCurrency));
        Assert.Equal(500 - cost, inv.CurrencyOf("shards"));

        // A choice node's picks go with it.
        var will = stats.Get("will");
        Assert.True(progression.RefundAtFont(sim, hero, "inner_b", out reason), reason);
        Assert.Equal(will - 4, stats.Get("will"));
        Assert.False(sim.World.Get<PassiveState>(hero)!.Picks.ContainsKey("inner_b"));
    }

    [Fact]
    public void AVocationOpensOnlyToItsCallingOnceItsOathIsFound()
    {
        var (sim, hero, _) = Hero(calling: "holdfast", id: "pas7");
        var progression = sim.Service<ProgressionService>();
        Assert.False(progression.RealmOpen(sim, hero, "warden"));
        sim.World.Get<QuestLog>(hero)!.Flags.Add("warden_oath");
        progression.RefreshRealms(sim, hero);
        Assert.True(progression.RealmOpen(sim, hero, "warden"));
        Assert.Contains("warden_root", sim.World.Get<Stats>(hero)!.Passives);
        Assert.Contains("warden_root", progression.Anchors(sim, hero));

        // Its nodes spend vocation points, which only grants give.
        Assert.False(progression.Allocate(sim, hero, "wd1", null, out var reason));
        Assert.Equal("no_points", reason);
        progression.GrantPoints(sim, hero, "vocation", 2);
        Assert.True(progression.Allocate(sim, hero, "wd1", null, out reason), reason);
        Assert.Equal(1, progression.Points(sim, hero, "vocation"));

        // A hero of another calling who found the same oath never opens it, and is told whose it is.
        var (other, kindler, _) = Hero(calling: "kindler", id: "pas8");
        var theirs = other.Service<ProgressionService>();
        other.World.Get<QuestLog>(kindler)!.Flags.Add("warden_oath");
        theirs.RefreshRealms(other, kindler);
        Assert.False(theirs.RealmOpen(other, kindler, "warden"));
        Assert.Contains("Holdfast", theirs.SealedReason(other, kindler, other.Content.Get<PassiveRealmDef>("warden")!));
        Assert.False(theirs.Allocate(other, kindler, "warden_root", null, out reason));
    }

    [Fact]
    public void APassiveGraftIsBoundToOneWeaveSlotAndLeavesWithItsNode()
    {
        var (sim, hero, sheet) = Hero(calling: "holdfast", id: "pas10");
        var progression = sim.Service<ProgressionService>();
        var stats = sim.World.Get<Stats>(hero)!;
        var book = sim.World.Get<WeaveBook>(hero)!;
        sim.World.Get<QuestLog>(hero)!.Flags.Add("warden_oath");
        progression.RefreshRealms(sim, hero);
        progression.GrantPoints(sim, hero, "vocation", 3);
        Assert.True(progression.Allocate(sim, hero, "wd1", null, out var reason), reason);
        Assert.False(PassiveGrafts.Bind(sim, hero, "wd3", 0, out reason));
        Assert.Equal("no_graft", reason);
        Assert.True(progression.Allocate(sim, hero, "wd3", null, out reason), reason);
        Assert.Equal("anchor", Assert.Single(PassiveGrafts.Of(sim, hero)).Glyph.Id);

        // Bound to the first seat, the graft is on the seat as if socketed; moving it moves it.
        Assert.False(PassiveGrafts.Bind(sim, hero, "wd3", book.Slots.Count, out reason));
        Assert.Equal("no_slot", reason);
        Assert.True(PassiveGrafts.Bind(sim, hero, "wd3", 0, out reason), reason);
        Assert.Equal(1, stats.Get("glyph.slot1.anchor"));
        var runtime = sim.Service<Weaves.WeaveRuntime>();
        Assert.Contains("glyph.anchor", runtime.Resolve(sim, book.Slots[0], out _)!.Tags);
        Assert.True(PassiveGrafts.Bind(sim, hero, "wd3", 1, out reason), reason);
        Assert.Equal(0, stats.Get("glyph.slot1.anchor"));
        Assert.Equal(1, stats.Get("glyph.slot2.anchor"));

        // The binding saves with the character.
        var factory = sim.Service<CharacterFactory>();
        factory.Capture(sim, hero, sheet);
        Assert.Equal(1, sheet.PassiveGrafts["wd3"]);
        var again = factory.Materialize(sim, sheet);
        Assert.Equal(1, sim.World.Get<Stats>(again)!.Get("glyph.slot2.anchor"));

        // Refunding the node takes the graft and its binding away.
        sim.Service<PatternFactory>().Spawn(sim, "passive_font", new Vector3(9.5f, 8, 0));
        sim.World.Get<Inventory>(hero)!.AddCurrency("shards", 500);
        Assert.True(progression.RefundAtFont(sim, hero, "wd3", out reason), reason);
        Assert.Equal(0, stats.Get("glyph.slot2.anchor"));
        Assert.Empty(sim.World.Get<PassiveState>(hero)!.Grafts);
        Assert.DoesNotContain("glyph.anchor", runtime.Resolve(sim, book.Slots[1], out _)!.Tags);
    }

    [Fact]
    public void AnotherCallingsStartIsBoughtAsTravelAndNeverAnchors()
    {
        var (sim, hero, _) = Hero(calling: "holdfast", id: "pas9", level: 5);
        var progression = sim.Service<ProgressionService>();
        var stats = sim.World.Get<Stats>(hero)!;
        Assert.False(progression.Allocate(sim, hero, "start_holdfast", null, out var reason));
        Assert.Equal("already", reason);
        Assert.True(progression.Allocate(sim, hero, "inner_a", "might", out reason), reason);
        var points = progression.Points(sim, hero, ProgressionService.MainCurrency);
        Assert.True(progression.Allocate(sim, hero, "start_kindler", null, out reason), reason);
        Assert.Equal(points - 1, progression.Points(sim, hero, ProgressionService.MainCurrency));
        Assert.DoesNotContain("start_kindler", progression.Anchors(sim, hero));

        // A travel start refunds like any node, and nothing hangs from it but itself.
        sim.Service<PatternFactory>().Spawn(sim, "passive_font", new Vector3(9.5f, 8, 0));
        sim.World.Get<Inventory>(hero)!.AddCurrency("shards", 500);
        Assert.True(progression.RefundAtFont(sim, hero, "start_kindler", out reason), reason);
        Assert.DoesNotContain("start_kindler", stats.Passives);
    }

    [Fact]
    public void PicksSurviveASaveAndOldNodesGetTheirDefault()
    {
        var (sim, hero, sheet) = Hero(id: "pas6");
        var progression = sim.Service<ProgressionService>();
        Assert.True(progression.Allocate(sim, hero, "inner_a", "grace", out var reason), reason);
        var factory = sim.Service<CharacterFactory>();
        factory.Capture(sim, hero, sheet);
        var json = System.Text.Json.JsonSerializer.Serialize(sheet, ContentJson.Options);
        var loaded = System.Text.Json.JsonSerializer.Deserialize<CharacterSheet>(json, ContentJson.Options)!;
        var again = factory.Materialize(sim, loaded);
        Assert.Equal(new List<string> { "grace" }, sim.World.Get<PassiveState>(again)!.Picks["inner_a"]);
        Assert.Equal(sim.World.Get<Stats>(hero)!.Get("grace"), sim.World.Get<Stats>(again)!.Get("grace"));

        // A save from before inner_b dealt choices gets the attribute the node used to give.
        loaded.Passives.Add("inner_b");
        loaded.PassivePicks.Remove("inner_b");
        var old = factory.Materialize(sim, loaded);
        Assert.Equal(new List<string> { "will" }, sim.World.Get<PassiveState>(old)!.Picks["inner_b"]);
    }
}
