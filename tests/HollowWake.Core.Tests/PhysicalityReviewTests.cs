using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Movement;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Systems;
using HollowWake.Core.Tags;
using HollowWake.Core.World;
using Xunit;

namespace HollowWake.Core.Tests;

public class PhysicalityReviewTests
{
    [Fact]
    public void GeometryMassIsTheSameAttributeForQueriesExpressionsAndLinksAfterResize()
    {
        var arena = DeliveryArena.Create();
        var body = arena.Body(20, 20, pattern: "goblin_sapper");
        var stats = arena.Sim.World.Get<Stats>(body)!;
        var transform = arena.Sim.World.Get<Transform>(body)!;
        var profile = arena.Sim.World.Get<PhysicalBody>(body)!;
        var p = profile.Profile;
        var basis = p.Width * p.Depth * p.Height * p.Fill * p.Heft * profile.Density;
        stats.Attributes.Add(new Modifier { Attribute = "mass_copy", Form = ModifierForm.Link, From = "mass", Value = 1 });
        void Verify(double expected)
        {
            Assert.Equal(expected, stats.Get("mass"), 3);
            Assert.Equal(expected, Expr.Parse("mass").Evaluate(stats), 3);
            Assert.Equal(expected, stats.Get("mass_copy"), 3);
            Assert.Equal(expected, Physicality.Mass(arena.Sim, body), 3);
        }
        Verify(basis);
        transform.Radius *= 2;
        transform.Height *= 3;
        Verify(basis * 12);
        stats.Attributes.SetBase("mass", 37);
        Verify(37);
        transform.Height /= 3;
        Verify(37);
        stats.Attributes.ClearBase("mass");
        Verify(basis * 4);
        stats.Attributes.Add(new Modifier { Attribute = "mass", Value = 10 });
        stats.Attributes.Add(new Modifier { Attribute = "mass", Form = ModifierForm.Increased, Value = .5 });
        stats.Attributes.Add(new Modifier { Attribute = "mass", Form = ModifierForm.More, Value = .2 });
        Verify((basis * 4 + 10) * 1.5 * 1.2);
    }

    [Fact]
    public void CollapsedLiveEnvelopeUsesTheMassFloorRatherThanTheLegacyDefault()
    {
        var arena = DeliveryArena.Create();
        var body = arena.Body(20, 20, pattern: "goblin_sapper");
        arena.Sim.World.Get<Transform>(body)!.Height = 0;
        Assert.Equal(.1, Physicality.Mass(arena.Sim, body));
        Assert.Equal(.1, arena.Sim.World.Get<Stats>(body)!.Get("mass"));
    }

    [Fact]
    public void AuthoredMassBaseOverridesGeometryFromBirth()
    {
        var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
        var parent = content.Require<PatternDef>("goblin_sapper");
        content.With(new PatternDef { Id = "weighted_sapper", Extends = parent.Id, Radius = parent.Radius, Height = parent.Height,
            Components = { ["stats"] = new JsonObject { ["attributes"] = new JsonObject { ["mass"] = 37 } } } });
        var sim = ZoneBuilder.CreateArena(content);
        var entity = sim.Service<PatternFactory>().Spawn(sim, "weighted_sapper", new Vector3(20, 20, 0));
        Assert.Equal(37, sim.World.Get<Stats>(entity)!.Get("mass"));
        Assert.Equal(37, Physicality.Mass(sim, entity));
    }

    [Theory]
    [InlineData("goblin_sapper")]
    [InlineData("boar")]
    public void OwnerTagConditionsAndQueryScopesHaveTheSameMeaningWithOrWithoutProfiles(string pattern)
    {
        var arena = DeliveryArena.Create();
        var body = arena.Body(20, 20, pattern: pattern);
        var stats = arena.Sim.World.Get<Stats>(body)!;
        var basis = stats.Get("mass");
        stats.Attributes.Add(new Modifier { Attribute = "mass", Value = 5, Condition = Expr.Parse("has('test.heavy')") });
        stats.Attributes.Add(new Modifier { Attribute = "mass", Value = 100, Scope = TagSet.Of("query.test") });
        Assert.Equal(basis, Physicality.Mass(arena.Sim, body));
        stats.Tags = stats.Tags.With("test.heavy");
        Assert.Equal(basis + 5, Physicality.Mass(arena.Sim, body), 3);
        Assert.Equal(basis + 5, Expr.Parse("mass").Evaluate(stats), 3);
        Assert.Equal(basis + 105, stats.Get("mass", TagSet.Of("query.test")), 3);
        stats.Tags = stats.Tags.Without("test.heavy");
        Assert.Equal(basis, stats.Get("mass"), 3);
    }

    [Theory]
    [InlineData("knockback")]
    [InlineData("buffet")]
    [InlineData("pull")]
    public void TinyVictimsRespectFinalCeilingEvenWithExtremeForceModifiers(string kind)
    {
        var arena = DeliveryArena.Create();
        var body = arena.Body(30, 10, pattern: "goblin_scavenger");
        arena.Sim.World.Get<Stats>(body)!.Attributes.SetBase("mass", 1);
        var source = arena.Sim.World.Get<Stats>(arena.Caster)!;
        source.Attributes.SetBase("mass", 100000);
        source.Attributes.SetBase("shove.authority", 1000);
        var origin = arena.Sim.World.Get<Transform>(body)!.Position;
        arena.Run($$"""{"type":"move","kind":"{{kind}}","distance":4}""", origin, target: body);
        arena.Tick(60);
        var travel = Vector3.Distance(origin, arena.Sim.World.Get<Transform>(body)!.Position);
        var ceiling = arena.Sim.Content.Rules.Number("physicality.shove_scale_max", 0);
        Assert.InRange(travel, 4 * (float)ceiling - .2f, 4 * (float)ceiling + .01f);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AHighSpeedTinyBodyCannotSkipAPlacedOrTerrainWall(bool terrain)
    {
        var arena = DeliveryArena.Create();
        var body = arena.Body(30, 10.5f, pattern: "goblin_scavenger");
        arena.Sim.World.Get<Stats>(body)!.Attributes.SetBase("mass", 1);
        Block(arena, new CellCoord(32, 10), terrain);
        arena.Run("""{"type":"move","kind":"knockback","distance":4}""", new Vector3(30, 10.5f, 0), target: body);
        arena.Tick(60);
        Assert.InRange(arena.Sim.World.Get<Transform>(body)!.Position.X, 31.5f, 31.99999f);
        Assert.Single(arena.Of<BodyCollided>(), e => e.Entity == body);
        Assert.Equal(Vector3.Zero, arena.Sim.World.Get<Motion>(body)!.Impulse);
    }

    [Fact]
    public void FastDiagonalNudgeChecksEvenAShortCrossedCellInterval()
    {
        var arena = DeliveryArena.Create();
        var body = arena.Body(30.01f, 10.99f);
        var t = arena.Sim.World.Get<Transform>(body)!;
        // The line enters (30,11) only briefly, then crosses into (31,11).
        Block(arena, new CellCoord(30, 11), false);
        DisplacementSystem.Nudge(arena.Sim, body, t, new Vector2(20, .3f));
        Assert.True(t.Position.X < 31, $"passed a crossed blocker at {t.Position}");
    }

    [Theory]
    [InlineData(80, false)]
    [InlineData(160, false)]
    [InlineData(80, true)]
    public void FieldPullUsesCanonicalWeightAndRespectsImmovable(double mass, bool immovable)
    {
        var arena = DeliveryArena.Create();
        var body = arena.Body(28, 10);
        var stats = arena.Sim.World.Get<Stats>(body)!;
        stats.Attributes.SetBase("mass", mass);
        if (immovable) stats.Flags.Add("immovable");
        arena.Run("""{"type":"field","radius":12,"duration":2,"interval":1,"pull":2}""", new Vector3(20, 10, 0));
        arena.Tick(10);
        var travel = 28 - arena.Sim.World.Get<Transform>(body)!.Position.X;
        Assert.InRange(travel, immovable ? 0 : (float)(80 / mass) - .15f, immovable ? 0 : (float)(80 / mass) + .15f);
    }

    [Fact]
    public void GoblinkinStanceIsReciprocalWithEveryExistingFaction()
    {
        var sim = DeliveryArena.Create().Sim;
        foreach (var faction in sim.Content.Each<FactionDef>())
            Assert.Equal(HollowWake.Core.Combat.Relations.StanceBetween(sim, "goblinkin", faction.Id),
                HollowWake.Core.Combat.Relations.StanceBetween(sim, faction.Id, "goblinkin"));
    }

    private static void Block(DeliveryArena arena, CellCoord cell, bool terrain)
    {
        if (terrain)
        {
            var (chunk, i) = arena.Sim.Terrain.Locate(cell);
            chunk.Flags[i] = CellFlags.Structure | CellFlags.Blocked;
        }
        else arena.Sim.Service<BlockedCells>().Block(cell, arena.Sim.World.Create());
    }
}
