using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Characters;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Dialogue;
using HollowWake.Core.Ecs;
using HollowWake.Core.Interaction;
using HollowWake.Core.Items;
using HollowWake.Core.Maths;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>Conversations run by the server, and trade at the server's prices.</summary>
public class DialogueTests
{
    private static readonly Lazy<ContentRegistry> Registry = new(() =>
    {
        var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out var report);
        Assert.False(report.HasErrors, report.ToString());
        // A conversation whose choice runs an effect type rather than a dialogue action.
        registry.With(new DialogueDef
        {
            Id = "t_teacher",
            For = { "t_teacher" },
            Start = "start",
            Nodes =
            {
                ["start"] = new DialogueNodeDef
                {
                    Line = "Learn something, {hero}.",
                    Choices =
                    {
                        new DialogueChoiceDef { Id = "learn", Text = "Teach me.", Effects = { new TypedSpec { Type = "grant_xp", Params = new JsonObject { ["amount"] = 40 } } }, End = true },
                        new DialogueChoiceDef { Id = "rich", Text = "I am rich.", Enabled = Expressions.Expr.Parse("wallet.shards >= 1000"), Reason = "You are not." },
                    },
                },
            },
        });
        return registry;
    });

    private static (Simulation Sim, Entity Hero, Entity Speaker) Talk(string pattern = "quartermaster", string id = "dlg")
    {
        var sim = ZoneBuilder.CreateArena(Registry.Value, 31);
        var factory = sim.Service<CharacterFactory>();
        var sheet = factory.Create(sim, id, "acct", "Odile", "holdfast", sim.Service<ItemGenerator>());
        sheet.Position = new Vector3(10, 10, 0);
        var hero = factory.Materialize(sim, sheet);
        var speaker = sim.Service<PatternFactory>().Spawn(sim, pattern, new Vector3(11.5f, 10, 0));
        return (sim, hero, speaker);
    }

    private static DialogueView View(Simulation sim, Entity hero) => sim.Service<DialogueService>().View(sim, hero)!;

    [Fact]
    public void TalkingOpensTheBoundConversationAndItsGatesChooseTheChoices()
    {
        var (sim, hero, quartermaster) = Talk();
        var shown = new List<DialogueShown>();
        sim.Events.Subscribe<DialogueShown>(shown.Add);
        Assert.True(sim.Service<InteractionService>().Interact(sim, hero, quartermaster, out var reason), reason);
        Assert.Equal("greet", Assert.Single(shown).Node);
        var view = View(sim, hero);
        Assert.Equal("Quartermaster", view.Speaker);
        Assert.Contains("Odile", view.Line);
        // A Holdfast is also asked about the wardens, the oath that opens their vocation.
        Assert.Equal(new[] { "work", "trade", "wardens", "bye" }, view.Choices.Select(c => c.Id));
        Assert.Contains("met_quartermaster", sim.World.Get<QuestLog>(hero)!.Flags);

        var dialogues = sim.Service<DialogueService>();
        Assert.True(dialogues.Choose(sim, hero, "quartermaster", "work", "greet", out reason), reason);
        Assert.Contains(View(sim, hero).Choices, c => c.Id == "take_hobs");
        Assert.DoesNotContain(View(sim, hero).Choices, c => c.Id == "hobs_done");
        Assert.False(dialogues.Choose(sim, hero, "quartermaster", "hobs_done", null, out reason));
        Assert.Equal("unknown_choice", reason);
        Assert.False(dialogues.Choose(sim, hero, "quartermaster", "take_hobs", "greet", out reason));
        Assert.Equal("stale", reason);

        Assert.True(dialogues.Choose(sim, hero, "quartermaster", "take_hobs", "work", out reason), reason);
        Assert.True(sim.World.Get<QuestLog>(hero)!.Quests.ContainsKey("first_threads"));
        Assert.Equal("hobs_taken", View(sim, hero).Node);
        Assert.True(dialogues.Choose(sim, hero, "quartermaster", "back", null, out reason), reason);
        Assert.True(dialogues.Choose(sim, hero, "quartermaster", "work", null, out reason), reason);
        var now = View(sim, hero).Choices.Select(c => c.Id).ToList();
        Assert.DoesNotContain("take_hobs", now);
        Assert.Contains("hobs_working", now);

        // A choice offered but closed says why; leaving ends the conversation.
        Assert.True(dialogues.Choose(sim, hero, "quartermaster", "back", null, out reason), reason);
        Assert.True(dialogues.Choose(sim, hero, "quartermaster", "bye", null, out reason), reason);
        Assert.Null(dialogues.View(sim, hero));
    }

    [Fact]
    public void EffectsRunThroughTheEffectRegistryAndClosedChoicesSayWhy()
    {
        var (sim, hero, teacher) = Talk("hob", "dlg2");
        var dialogues = sim.Service<DialogueService>();
        Assert.True(dialogues.Open(sim, hero, teacher, sim.Content.Get<DialogueDef>("t_teacher")!, out var reason), reason);
        var rich = View(sim, hero).Choices.Single(c => c.Id == "rich");
        Assert.False(rich.Enabled);
        Assert.Equal("You are not.", rich.Reason);
        Assert.False(dialogues.Choose(sim, hero, "t_teacher", "rich", null, out reason));
        Assert.Equal("not_enabled", reason);
        var xp = sim.World.Get<Stats>(hero)!.Xp;
        var ended = new List<DialogueEnded>();
        sim.Events.Subscribe<DialogueEnded>(ended.Add);
        Assert.True(dialogues.Choose(sim, hero, "t_teacher", "learn", null, out reason), reason);
        Assert.True(sim.World.Get<Stats>(hero)!.Xp > xp);
        Assert.Equal("ended", Assert.Single(ended).Why);
    }

    [Fact]
    public void ALineOpensTheVendorAndTradeHappensAtTheServersPrices()
    {
        var (sim, hero, quartermaster) = Talk(id: "dlg3");
        var dialogues = sim.Service<DialogueService>();
        var vendors = sim.Service<VendorService>();
        var inv = sim.World.Get<Inventory>(hero)!;
        Assert.True(sim.Service<InteractionService>().Interact(sim, hero, quartermaster, out var reason), reason);
        var opened = new List<VendorOpened>();
        sim.Events.Subscribe<VendorOpened>(opened.Add);
        Assert.True(dialogues.Choose(sim, hero, "quartermaster", "trade", null, out reason), reason);
        Assert.Equal("quartermaster", Assert.Single(opened).Id);
        Assert.Equal("trading", View(sim, hero).Node);

        // Nothing is bought without the coin, and nothing is paid without the room.
        var offers = vendors.Offers(sim, hero);
        var floss = offers.Single(o => o.Offer.Id == "floss");
        Assert.False(floss.Enabled);
        Assert.False(vendors.Buy(sim, hero, quartermaster, "floss", 1, out reason));
        Assert.Equal("cannot_afford:shards", reason);
        inv.AddCurrency("shards", 1000);
        var price = floss.Offer.Price["shards"];
        var flossBefore = inv.CountOf(i => i.Base == "thread_floss");
        Assert.True(vendors.Buy(sim, hero, quartermaster, "floss", 2, out reason), reason);
        Assert.Equal(flossBefore + 10, inv.CountOf(i => i.Base == "thread_floss"));
        Assert.Equal(1000 - 2 * price, inv.CurrencyOf("shards"));

        // Limited stock runs out; an entry closed by its gate is not for sale.
        Assert.True(vendors.Buy(sim, hero, quartermaster, "stone_ring", 2, out reason), reason);
        Assert.False(vendors.Buy(sim, hero, quartermaster, "stone_ring", 1, out reason));
        Assert.Equal("sold_out", reason);
        Assert.Equal(2, inv.Items.Count(i => i.Payload?.Glyph == "ring"));
        Assert.False(vendors.Buy(sim, hero, quartermaster, "codex_bolt", 1, out reason));
        Assert.Equal("not_for_sale", reason);

        // Selling pays the quoted price; buying back returns the same item at what was paid.
        var sword = sim.Service<ItemGenerator>().Roll("short_sword", 3, "magic", new Rng(4));
        Assert.True(inv.TryAdd(sword));
        var quote = vendors.SellPrice(sim, hero, sim.Content.Get<VendorDef>("quartermaster")!, sword)!["shards"];
        var shards = inv.CurrencyOf("shards");
        Assert.True(vendors.Sell(sim, hero, quartermaster, sword.Uid, null, out reason), reason);
        Assert.Null(inv.Find(sword.Uid));
        Assert.Equal(shards + quote, inv.CurrencyOf("shards"));
        Assert.True(vendors.BuyBack(sim, hero, quartermaster, sword.Uid, out reason), reason);
        Assert.Same(sword, inv.Find(sword.Uid));
        Assert.Equal(shards, inv.CurrencyOf("shards"));
        // A vendor nobody opened cannot be traded with.
        Assert.False(vendors.Buy(sim, hero, hero, "floss", 1, out reason));
        Assert.Equal("not_trading", reason);
    }

    [Fact]
    public void WalkingAwayClosesTheConversationAndTheVendor()
    {
        var (sim, hero, quartermaster) = Talk(id: "dlg4");
        Assert.True(sim.Service<InteractionService>().Interact(sim, hero, quartermaster, out var reason), reason);
        Assert.True(sim.Service<DialogueService>().Choose(sim, hero, "quartermaster", "trade", null, out reason), reason);
        var ended = new List<DialogueEnded>();
        var closed = new List<VendorClosed>();
        sim.Events.Subscribe<DialogueEnded>(ended.Add);
        sim.Events.Subscribe<VendorClosed>(closed.Add);
        var t = sim.World.Get<Transform>(hero)!;
        t.Position = new Vector3(40, 40, 0);
        sim.Spatial.Update(hero, t.Position);
        sim.Run(15);
        Assert.Equal("departed", Assert.Single(ended).Why);
        Assert.Equal("departed", Assert.Single(closed).Why);
        Assert.Null(sim.World.Get<Panes>(hero)!.Conversation);
        Assert.Null(sim.World.Get<Panes>(hero)!.Vendor);
    }

    [Fact]
    public void AVillagerGreetsKinDifferently()
    {
        var (sim, hero, villager) = Talk("villager", "dlg5");
        var dialogues = sim.Service<DialogueService>();
        Assert.Equal("marcher", dialogues.For(sim, hero, villager)!.Id);
        sim.World.Get<QuestLog>(hero)!.Completed.Add("the_wellmother");
        Assert.Equal("marcher_kin", dialogues.For(sim, hero, villager)!.Id);
    }
}
