using System.Numerics;
using System.Text.Json;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Minions;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>A summoner, its foes and helpers to cast and watch, on the real content plus a few test weaves.</summary>
internal sealed class MinionArena
{
    private static readonly Lazy<ContentRegistry> Loaded = new(() =>
    {
        var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out var report);
        Assert.False(report.HasErrors, report.ToString());
        registry
            .With(new FactionDef { Id = "red", Relations = { ["blue"] = Stance.Hostile } })
            .With(new FactionDef { Id = "blue", Relations = { ["red"] = Stance.Hostile } })
            .With(Summon("t_pool", "{\"type\": \"summon\", \"pool\": [ {\"pattern\": \"remnant_shade\", \"weight\": 1}, {\"pattern\": \"sprig\", \"weight\": 1, \"tags\": [\"t_sprout\"]} ], \"count\": 8, \"max_active\": 8, \"duration\": 0, \"group\": \"t_pool\"}"))
            .With(Summon("t_cap", "{\"type\": \"summon\", \"pattern\": \"sprig\", \"count\": 1, \"max_active\": 2, \"duration\": 0, \"group\": \"t_cap\"}"))
            .With(Summon("t_shared_a", "{\"type\": \"summon\", \"pattern\": \"sprig\", \"count\": 2, \"max_active\": 3, \"duration\": 0, \"group\": \"t_a\", \"pool_group\": \"t_shared\"}"))
            .With(Summon("t_shared_b", "{\"type\": \"summon\", \"pattern\": \"remnant_shade\", \"count\": 2, \"max_active\": 3, \"duration\": 0, \"group\": \"t_b\", \"pool_group\": \"t_shared\"}"))
            .With(Summon("t_rival_a", "{\"type\": \"summon\", \"pattern\": \"sprig\", \"count\": 2, \"max_active\": 4, \"duration\": 0, \"group\": \"t_ra\", \"exclusive_group\": \"t_rivals\"}"))
            .With(Summon("t_rival_b", "{\"type\": \"summon\", \"pattern\": \"remnant_shade\", \"count\": 1, \"max_active\": 4, \"duration\": 0, \"group\": \"t_rb\", \"exclusive_group\": \"t_rivals\"}"))
            .With(Summon("t_shades", "{\"type\": \"summon\", \"pattern\": \"remnant_shade\", \"count\": 2, \"max_active\": 4, \"duration\": 0, \"group\": \"t_shades\"}"))
            .With(Summon("t_fury_shades", "{\"type\": \"summon\", \"pattern\": \"remnant_shade\", \"count\": 1, \"max_active\": 4, \"duration\": 0, \"group\": \"t_fury\"}", glyphs: "pack_fury"))
            .With(Summon("t_plain_shades", "{\"type\": \"summon\", \"pattern\": \"remnant_shade\", \"count\": 1, \"max_active\": 4, \"duration\": 0, \"group\": \"t_plain\", \"inherit\": false}"))
            .With(Summon("t_host_volley", "{\"type\": \"summon\", \"pattern\": \"sprig\", \"count\": 1, \"max_active\": 4, \"duration\": 0, \"group\": \"t_host\"}", "{\"type\": \"minion_cast\", \"weave\": \"shade_bolt\", \"at\": \"enemy\"}"))
            .With(Summon("t_contract", "{\"type\": \"summon\", \"pattern\": \"remnant_shade\", \"count\": 2, \"max_active\": 2, \"group\": \"t_court\", \"contract\": {\"reserve\": {\"mana\": 20}, \"respawn\": 3}}"))
            .With(Summon("t_presence", "{\"type\": \"summon\", \"pattern\": \"stone_warden\", \"count\": 1, \"max_active\": 1, \"duration\": 0, \"group\": \"t_apex\", \"presence\": [ {\"attribute\": \"damage\", \"form\": \"more\", \"value\": -0.2} ]}"))
            .With(Summon("t_waves", "{\"type\": \"summon\", \"pattern\": \"sprig\", \"count\": 1, \"max_active\": 6, \"duration\": 0, \"group\": \"t_waves\", \"waves\": {\"count\": 3, \"interval\": 0.5}}"))
            .With(Summon("t_born_hasted", "{\"type\": \"summon\", \"pattern\": \"sprig\", \"count\": 1, \"max_active\": 2, \"duration\": 0, \"group\": \"t_born\"}", glyphs: "fervent_birth"))
            .With(Summon("t_replenish", "{\"type\": \"summon\", \"pattern\": \"sprig\", \"count\": 1, \"max_active\": 2, \"duration\": 0, \"group\": \"t_fill\", \"replenish\": {\"interval\": 1}}"))
            .With(Summon("t_crew", "{\"type\": \"summon\", \"pattern\": \"remnant_shade\", \"count\": 1, \"max_active\": 2, \"duration\": 0, \"group\": \"t_crew\"}", glyphs: "resonant_key,crew_frenzy"))
            .With(Summon("t_crew_unkeyed", "{\"type\": \"summon\", \"pattern\": \"remnant_shade\", \"count\": 1, \"max_active\": 2, \"duration\": 0, \"group\": \"t_crew2\"}", glyphs: "crew_frenzy"))
            .With(new WeaveDef
            {
                Id = "t_consume",
                Form = "rite",
                Threads = { "umbra" },
                Effects = { Spec("{\"type\": \"consume_minion\", \"count\": 1, \"pick\": \"oldest\", \"effects\": [ {\"type\": \"event\", \"name\": \"t_consumed\", \"data\": {\"life\": \"consumed_life_max\"}} ]}") },
            });
        return registry;
    });

    public static ContentRegistry Registry => Loaded.Value;

    private static WeaveDef Summon(string id, string summon, string? extra = null, string? glyphs = null)
    {
        var def = new WeaveDef
        {
            Id = id,
            Form = "summon",
            Threads = { "umbra" },
            Glyphs = glyphs is null ? new List<GlyphRef>() : glyphs.Split(',').Select(g => (GlyphRef)g).ToList(),
            Effects = { Spec(summon) },
        };
        if (extra is not null)
        {
            def.Effects.Add(Spec(extra));
        }
        return def;
    }

    public static TypedSpec Spec(string json) => JsonSerializer.Deserialize<TypedSpec>(json, ContentJson.Options)!;

    public required Simulation Sim;
    public required Entity Owner;
    public readonly List<GameEvent> Events = new();

    public static MinionArena Create(ulong seed = 7, string owner = "loosed_kindler", Vector3? at = null)
    {
        var sim = ZoneBuilder.CreateArena(Registry, seed);
        var factory = sim.Service<PatternFactory>();
        var e = factory.Spawn(sim, owner, at ?? new Vector3(20, 20, 0), new SpawnOptions { Faction = "red" });
        sim.World.Remove<Behavior>(e);
        var arena = new MinionArena { Sim = sim, Owner = e };
        sim.Events.SubscribeAll(arena.Events.Add);
        return arena;
    }

    public Stats OwnerStats => Sim.World.Get<Stats>(Owner)!;

    public Transform OwnerAt => Sim.World.Get<Transform>(Owner)!;

    public MinionService Service => MinionService.Of(Sim);

    /// <summary>A foe of the summoner that stands still unless told otherwise.</summary>
    public Entity Foe(string pattern, Vector3 at, bool thinks = false)
    {
        var e = Sim.Service<PatternFactory>().Spawn(Sim, pattern, at, new SpawnOptions { Faction = "blue" });
        if (!thinks)
        {
            Sim.World.Remove<Behavior>(e);
        }
        return e;
    }

    public WeaveSlot Learn(string preset) => Sim.World.GetOrAdd<WeaveBook>(Owner).Add(new WeaveSpec { Preset = preset });

    /// <summary>Casts a slot through the runtime, clocks cleared and mana topped up first, and ticks until the cast resolves.</summary>
    public void Cast(WeaveSlot slot, Entity? target = null, Vector3? point = null)
    {
        var book = Sim.World.Get<WeaveBook>(Owner)!;
        book.ReadyAt.Clear();
        book.GlobalReadyAt = 0;
        OwnerStats.FillResources();
        var at = point ?? OwnerAt.Position;
        var runtime = Sim.Service<WeaveRuntime>();
        Assert.True(runtime.TryBegin(Sim, Owner, slot, target ?? Entity.None, at, Vector2.Zero, out var reason), reason);
        for (var i = 0; i < 100 && book.Casting is not null; i++)
        {
            Sim.Tick();
        }
        Assert.Null(book.Casting);
    }

    /// <summary>The owner's living minions, oldest first.</summary>
    public List<Entity> Minions(string? group = null) => Sim.World.Query<Minion>()
        .Where(m => m.C1.Owner == Owner && !Sim.World.Has<Dead>(m.Entity) && !Sim.World.IsDeferredDestroy(m.Entity) && (group is null || m.C1.Group == group))
        .OrderBy(m => m.C1.Sequence)
        .Select(m => m.Entity)
        .ToList();

    public Vector3 At(Entity e) => Sim.World.Get<Transform>(e)!.Position;

    public float Distance(Entity a, Entity b) => Sim.World.Get<Transform>(a)!.GroundDistanceTo(At(b));

    public void Move(Entity e, Vector3 to)
    {
        var t = Sim.World.Get<Transform>(e)!;
        t.Position = Sim.Terrain.SnapToGround(to, 3);
        Sim.Spatial.Update(e, t.Position);
    }

    public bool Hit(Entity source, Entity target) => Events.OfType<DamageDealt>().Any(d => d.Source == source && d.Target == target && d.Total > 0);
}

/// <summary>Summons: pools, capacities, pool and exclusive groups, inheritance, minion AI, orders and casts.</summary>
public class MinionTests
{
    [Fact]
    public void PoolsRollEachBodyAndTheSameSeedRollsTheSame()
    {
        static List<string> Roll(ulong seed)
        {
            var arena = MinionArena.Create(seed);
            arena.Cast(arena.Learn("t_pool"));
            return arena.Minions("t_pool").Select(e => arena.Sim.World.Get<Identity>(e)!.Pattern!).ToList();
        }
        var first = Roll(11);
        Assert.Equal(8, first.Count);
        Assert.Contains("remnant_shade", first);
        Assert.Contains("sprig", first);
        Assert.Equal(first, Roll(11));
        // Pool entry tags ride the bodies they choose, beside the tags every minion wears.
        var arena = MinionArena.Create(11);
        arena.Cast(arena.Learn("t_pool"));
        foreach (var e in arena.Minions("t_pool"))
        {
            var identity = arena.Sim.World.Get<Identity>(e)!;
            Assert.True(identity.Tags.Has("minion"));
            Assert.Equal(identity.Pattern == "sprig", identity.Tags.Has("t_sprout"));
        }
    }

    [Fact]
    public void TheOldestBodyMakesRoomPastTheCap()
    {
        var arena = MinionArena.Create();
        var slot = arena.Learn("t_cap");
        arena.Cast(slot);
        arena.Cast(slot);
        var firstTwo = arena.Minions("t_cap");
        Assert.Equal(2, firstTwo.Count);
        arena.Cast(slot);
        var now = arena.Minions("t_cap");
        Assert.Equal(2, now.Count);
        Assert.DoesNotContain(firstTwo[0], now);
        Assert.Contains(firstTwo[1], now);
        // The replaced body was dismissed, not killed.
        Assert.DoesNotContain(arena.Events.OfType<EntityDied>(), d => d.Entity == firstTwo[0]);
        Assert.Contains(arena.Events.OfType<EntityDespawned>(), d => d.Entity == firstTwo[0]);
    }

    [Fact]
    public void PoolGroupsShareOneCapacityAndOwnersCanRaiseIt()
    {
        var arena = MinionArena.Create();
        var a = arena.Learn("t_shared_a");
        var b = arena.Learn("t_shared_b");
        arena.Cast(a);
        arena.Cast(b);
        // Two of each wanted, three fit the shared pool: the oldest sprig made room.
        Assert.Equal(3, arena.Minions().Count);
        Assert.Single(arena.Minions("t_a"));
        Assert.Equal(2, arena.Minions("t_b").Count);
        // minion.max scoped by the pool tag raises the pool's capacity.
        arena.OwnerStats.Attributes.Add(new Modifier { Attribute = "minion.max", Form = ModifierForm.Flat, Value = 2, Scope = TagSet.Of("pool.t_shared"), Source = "test" });
        arena.Cast(a);
        Assert.Equal(5, arena.Minions().Count);
        Assert.Equal(3, arena.Minions("t_a").Count);
    }

    [Fact]
    public void AnExclusiveSummonDismissesItsRivals()
    {
        var arena = MinionArena.Create();
        arena.Cast(arena.Learn("t_rival_a"));
        Assert.Equal(2, arena.Minions("t_ra").Count);
        arena.Cast(arena.Learn("t_rival_b"));
        Assert.Empty(arena.Minions("t_ra"));
        Assert.Single(arena.Minions("t_rb"));
    }

    [Fact]
    public void MinionsInheritTheirOwnersMinionAttributesWithTheSummonsTags()
    {
        var arena = MinionArena.Create();
        var owner = arena.OwnerStats;
        owner.Attributes.Add(new Modifier { Attribute = "minion.damage", Form = ModifierForm.Increased, Value = 0.5, Source = "test" });
        // Scoped to undead bodies: a shade inherits it, a sprig does not.
        owner.Attributes.Add(new Modifier { Attribute = "minion.life", Form = ModifierForm.Increased, Value = 0.4, Scope = TagSet.Of("undead"), Source = "test" });
        arena.Cast(arena.Learn("t_pool"));
        var source = "owner:" + arena.Owner.Key;
        foreach (var e in arena.Minions("t_pool"))
        {
            var stats = arena.Sim.World.Get<Stats>(e)!;
            var damage = Assert.Single(stats.Attributes.ModifiersFor("damage"), m => m.Source == source);
            Assert.Equal(ModifierForm.More, damage.Form);
            Assert.Equal(0.5, damage.Value, 9);
            var undead = arena.Sim.World.Get<Identity>(e)!.Tags.Has("undead");
            Assert.Equal(undead, stats.Attributes.ModifiersFor("life.max").Any(m => m.Source == source && Math.Abs(m.Value - 0.4) < 1e-9));
            // Born at the inherited maximum.
            Assert.Equal(stats.ResourceMax("life"), stats.Resource("life"), 6);
        }
    }

    [Fact]
    public void TheSummoningWeavesGlyphsJoinTheFoldAndChangesReachLivingMinions()
    {
        var arena = MinionArena.Create();
        arena.OwnerStats.Attributes.Add(new Modifier { Attribute = "minion.damage", Form = ModifierForm.Increased, Value = 0.5, Source = "test" });
        arena.Cast(arena.Learn("t_fury_shades"));
        var fury = arena.Minions("t_fury").Single();
        arena.Cast(arena.Learn("t_plain_shades"));
        var plain = arena.Minions("t_plain").Single();
        var source = "owner:" + arena.Owner.Key;
        double Inherited(Entity e) => arena.Sim.World.Get<Stats>(e)!.Attributes.ModifiersFor("damage").Where(m => m.Source == source).Sum(m => m.Value);
        // Pack Fury's 30% joins the owner's 50% in one increased layer.
        Assert.Equal(0.8, Inherited(fury), 9);
        Assert.Equal(0, Inherited(plain), 9);
        // The owner invests more: within the refold interval the living minion carries it.
        arena.OwnerStats.Attributes.Add(new Modifier { Attribute = "minion.damage", Form = ModifierForm.Increased, Value = 0.2, Source = "test2" });
        arena.Sim.RunSeconds(1);
        Assert.Equal(1.0, Inherited(fury), 9);
        // A hit from the inheriting minion lands harder than the same hit from one that inherits nothing.
        var a = arena.Sim.World.Get<Stats>(fury)!;
        var b = arena.Sim.World.Get<Stats>(plain)!;
        Assert.Equal(2.0, a.Attributes.Apply("damage", 1, a, TagSet.Of("attack")) / b.Attributes.Apply("damage", 1, b, TagSet.Of("attack")), 6);
    }

    [Fact]
    public void MinionsFollowTheirOwner()
    {
        var arena = MinionArena.Create();
        arena.Cast(arena.Learn("t_shades"));
        var minions = arena.Minions("t_shades");
        Assert.Equal(2, minions.Count);
        arena.Move(arena.Owner, new Vector3(34, 20, 0));
        arena.Sim.RunSeconds(5);
        foreach (var m in minions)
        {
            Assert.True(arena.Distance(m, arena.Owner) < 4, $"minion stayed {arena.Distance(m, arena.Owner)} away");
        }
    }

    [Fact]
    public void MinionsTakeUpWhatTheirOwnerFights()
    {
        var arena = MinionArena.Create();
        arena.Cast(arena.Learn("t_shades"));
        var minions = arena.Minions("t_shades");
        // A foe beyond the minions' own notice, within the owner's reach.
        var foe = arena.Foe("hob", new Vector3(31, 20, 0));
        arena.Sim.RunSeconds(1);
        Assert.All(minions, m => Assert.False(arena.Hit(m, foe)));
        var bolt = arena.Sim.World.Get<WeaveBook>(arena.Owner)!.Find("cinder_spit")!;
        arena.Cast(bolt, foe, arena.At(foe));
        arena.Sim.RunSeconds(6);
        Assert.Contains(minions, m => arena.Hit(m, foe));
    }

    [Fact]
    public void MinionsLedPastTheirLeashHeelBackToTheOwner()
    {
        var arena = MinionArena.Create();
        arena.Cast(arena.Learn("t_shades"));
        var minion = arena.Minions("t_shades").First();
        var foe = arena.Foe("hob", new Vector3(52, 20, 0));
        var behavior = arena.Sim.World.Get<Behavior>(minion)!;
        behavior.Target = foe;
        var farthest = 0f;
        for (var i = 0; i < 20 * 8; i++)
        {
            arena.Sim.Tick();
            farthest = Math.Max(farthest, arena.Distance(minion, arena.Owner));
        }
        Assert.False(arena.Hit(minion, foe), "the minion chased past its leash");
        Assert.InRange(farthest, 10f, 19f);
        Assert.True(arena.Distance(minion, arena.Owner) < 5, $"the minion did not heel: {arena.Distance(minion, arena.Owner)}");
    }

    [Fact]
    public void MinionsOnAnAssaultOrderGoPastTheLeashAndKillWhatHoldsTheMark()
    {
        var arena = MinionArena.Create();
        arena.Cast(arena.Learn("t_shades"));
        var minions = arena.Minions("t_shades");
        var foe = arena.Foe("hob", new Vector3(40, 20, 0));
        arena.Cast(arena.Learn("command_assault"), point: arena.At(foe) + new Vector3(1, 0, 0));
        var order = Assert.Single(arena.Events.OfType<MinionsCommanded>());
        Assert.Equal(2, order.Heeded);
        Assert.Equal(foe, order.Target);
        Assert.All(minions, m => Assert.NotNull(arena.Sim.World.Get<Behavior>(m)!.Order));
        arena.Sim.RunSeconds(6);
        Assert.Contains(minions, m => arena.Hit(m, foe));
    }

    [Fact]
    public void DisobedientMinionsDefyOrders()
    {
        var arena = MinionArena.Create();
        arena.Cast(arena.Learn("t_shades"));
        var minions = arena.Minions("t_shades");
        foreach (var m in minions)
        {
            arena.Sim.World.Get<Stats>(m)!.Attributes.SetBase("obedience", 0);
        }
        arena.Cast(arena.Learn("command_hold"), point: arena.OwnerAt.Position + new Vector3(8, 0, 0));
        var order = Assert.Single(arena.Events.OfType<MinionsCommanded>());
        Assert.Equal(0, order.Heeded);
        Assert.Equal(2, order.Defied);
        Assert.All(minions, m => Assert.Null(arena.Sim.World.Get<Behavior>(m)!.Order));
    }

    [Fact]
    public void PlayerCommandsArriveAsEventsAndUnknownCommandsAreLogged()
    {
        var arena = MinionArena.Create();
        var log = new ListSimLog();
        arena.Sim.Log = log;
        arena.Cast(arena.Learn("t_shades"));
        var minions = arena.Minions("t_shades");
        var mark = arena.OwnerAt.Position + new Vector3(6, 0, 0);
        arena.Sim.Events.Publish(new MinionCommandRequested(arena.Owner, "hold", Entity.None, mark, "t_shades"));
        Assert.All(minions, m => Assert.Equal("hold", arena.Sim.World.Get<Behavior>(m)!.Order?.Command));
        arena.Sim.RunSeconds(4);
        Assert.All(minions, m => Assert.True(arena.Sim.World.Get<Transform>(m)!.GroundDistanceTo(mark) < 2.5f, "the minion did not hold the mark"));
        arena.Sim.Events.Publish(new MinionCommandRequested(arena.Owner, "dance", Entity.None, mark, null));
        Assert.Contains(log.Lines, l => l.Contains("dance"));
    }

    [Fact]
    public void RecallBringsMobileMinionsToARingAroundTheOwner()
    {
        var arena = MinionArena.Create();
        arena.Cast(arena.Learn("t_shades"));
        var minions = arena.Minions("t_shades");
        foreach (var m in minions)
        {
            arena.Move(m, arena.OwnerAt.Position + new Vector3(-15, 8, 0));
        }
        arena.Cast(arena.Learn("command_recall"));
        var radius = arena.Sim.Content.Rules.Number("minions.recall_radius", 1.5);
        Assert.All(minions, m => Assert.InRange(arena.Distance(m, arena.Owner), radius - 0.2, radius + 0.2));
        Assert.All(minions, m => Assert.Equal("recall", arena.Sim.World.Get<Behavior>(m)!.Order?.Command));
    }

    [Fact]
    public void MinionCastIsScopedToTheHostWeavesOwnMinions()
    {
        var arena = MinionArena.Create();
        arena.Cast(arena.Learn("t_shades"));
        var host = arena.Learn("t_host_volley");
        var foe = arena.Foe("hob", arena.OwnerAt.Position + new Vector3(6, 0, 0));
        arena.Cast(host);
        // Its first cast raised a sprig that then cast at the foe; the shades of another weave did not.
        var ordered = arena.Events.OfType<MinionOrdered>().ToList();
        Assert.Single(ordered);
        Assert.Equal("t_host", arena.Sim.World.Get<Minion>(ordered[0].Minion)!.Group);
        // The volley from the command form addresses every minion that has a foe in reach.
        arena.Events.Clear();
        arena.Cast(arena.Learn("remnant_volley"));
        Assert.Equal(3, arena.Events.OfType<MinionOrdered>().Count());
        Assert.Equal(3, arena.Events.OfType<EntitySpawned>().Count(s => s.Pattern == "projectile"));
        Assert.Empty(arena.Events.OfType<WeaveCast>().Where(w => w.Caster != arena.Owner));
        _ = foe;
    }

    [Fact]
    public void ConsumingAMinionRunsItsEffectsAndKillsItWithoutCredit()
    {
        var arena = MinionArena.Create();
        arena.Cast(arena.Learn("t_shades"));
        var oldest = arena.Minions("t_shades").First();
        var lifeMax = arena.Sim.World.Get<Stats>(oldest)!.ResourceMax("life");
        arena.Cast(arena.Learn("t_consume"));
        var consumed = Assert.Single(arena.Events.OfType<CustomEvent>(), e => e.Name == "t_consumed");
        Assert.Equal(lifeMax, consumed.Data["life"], 6);
        var died = Assert.Single(arena.Events.OfType<EntityDied>());
        Assert.Equal(oldest, died.Entity);
        Assert.True(died.Killer.IsNone);
        Assert.Single(arena.Minions("t_shades"));
    }
}
