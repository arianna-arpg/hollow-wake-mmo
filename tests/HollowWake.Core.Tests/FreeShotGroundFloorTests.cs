using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Content;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Sim;
using HollowWake.Core.Patterns;
using HollowWake.Core.Weaves;
using HollowWake.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace HollowWake.Core.Tests;

public class FreeShotGroundFloorTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Contacts() =>
        new[] { "goblin_skirmisher", "goblin_slinger", "goblin_scavenger", "cave_gnasher", "reed_gnasher", "gnasher_whelp" }
        .SelectMany(body => new[] { -.125f, 0, .125f }.SelectMany(slope =>
            new[] { "firebolt", "piercing_arrow" }.SelectMany(preset =>
                new[] { false, true }.Select(beyond => new object[] { body, slope, preset, beyond }))));

    [Theory, MemberData(nameof(Contacts))]
    public void HumanoidFreeShotsStrikeShortBodiesBeforeAndBeyondCursor(string pattern, float slope, string preset, bool beyond)
    {
        var a = Arena(slope);
        var distance = beyond ? 8 : 4;
        var target = a.Body(10 + distance, 10, pattern: pattern);
        var t = a.Sim.World.Get<Transform>(target)!;
        Assert.Equal((float)a.Sim.Service<PatternFactory>().Resolve(pattern).Def.Height, t.Height);
        var originalHeight = t.Height;
        var originalRadius = t.Radius;
        var flights = Capture(a);
        Press(a, preset, beyond ? 1 : 6);
        a.Tick(150);
        var p = Assert.Single(flights);
        output.WriteLine($"{preset}/{pattern}, slope {slope}, beyond={beyond}, launch={p.Origin}, distance={p.Traveled:F3}");
        Assert.Equal(1.098f, p.Origin.Z, 4);
        Assert.True(p.Context.Target.IsNone);
        Assert.Equal(Vector3.Zero, p.Context.AimOffset);
        Assert.Contains(a.Events.OfType<DamageDealt>(), h => h.Source == a.Caster && h.Target == target && h.Total > 0);
        Assert.Equal(originalHeight, t.Height);
        Assert.Equal(originalRadius, t.Radius);
    }

    public static IEnumerable<object[]> EmptyShots() => new[] { -.125f, 0, .125f }
        .SelectMany(slope => new[] { "firebolt", "piercing_arrow" }.SelectMany(preset =>
            new[] { 1f, 3, 8 }.Select(cursor => new object[] { slope, preset, cursor })));

    [Theory, MemberData(nameof(EmptyShots))]
    public void HumanoidNearAndFarFreeShotsTravelFullRange(float slope, string preset, float cursor)
    {
        var a = Arena(slope);
        var flights = Capture(a);
        Press(a, preset, cursor);
        a.Tick(150);
        var p = Assert.Single(flights);
        output.WriteLine($"{preset}, slope {slope}, cursor {cursor}, distance={p.Traveled:F3}/{p.Range:F3}, end={p.Carrier}");
        Assert.True(p.Traveled >= p.Range);
        Assert.Equal(1.098f, p.Origin.Z, 4);
    }

    [Theory]
    [InlineData("goblin_skirmisher", -.125f)]
    [InlineData("goblin_skirmisher", 0)]
    [InlineData("goblin_skirmisher", .125f)]
    [InlineData("gnasher_whelp", -.125f)]
    [InlineData("gnasher_whelp", 0)]
    [InlineData("gnasher_whelp", .125f)]
    public void HoveredGlaivePiercesAndReachesItsReturn(string pattern, float slope)
    {
        var a = Arena(slope);
        var target = a.Body(13, 10, pattern: pattern);
        var stats = a.Sim.World.Get<Stats>(target)!;
        stats.Attributes.SetBase("life.max", 100000);
        stats.FillResources();
        var flights = Capture(a);
        Press(a, "stone_glaive", 3, target);
        a.Tick(150);
        var p = Assert.Single(flights);
        Assert.Equal(1.098f, p.Origin.Z, 4);
        Assert.Contains(a.Events.OfType<DamageDealt>(), h => h.Target == target && h.Total > 0);
        var turn = Assert.Single(a.Events.OfType<ProjectileTurned>());
        Assert.True(Vector2.Distance(new(10, 10), new(turn.Position.X, turn.Position.Y)) >= p.Range);
        Assert.Single(a.Events.OfType<ProjectileReturned>());
    }

    [Fact]
    public void ShippedFloorMatchesTheShortestAuthoredDamageBodyTop()
    {
        var content = DeliveryArena.Registry;
        var factory = new PatternFactory(content);
        var top = content.Each<PatternDef>().Select(p => factory.Resolve(p.Id))
            .Where(p => p.Components.ContainsKey("hit_body"))
            .Min(p => content.Get<HitBodyDef>(p.Components["hit_body"]["profile"]!.GetValue<string>())!
                .Volumes.Max(v => v.Up + v.HalfHeight) * p.Def.Height);
        Assert.Equal(top, content.Rules.Number("delivery.projectile_ground_floor", 0), 5);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GroundContactHooksKeepTheirExactGroundAnchor(bool gravity)
    {
        var a = Arena(0);
        var flights = Capture(a);
        var hook = gravity ? "on_land" : "on_end";
        var graph = "{\"type\":\"projectile\",\"aim\":\"point\",\"gravity\":" + (gravity ? "true" : "false") +
            ",\"speed\":10,\"range\":10,\"" + hook + "\":[{\"type\":\"projectile\",\"speed\":10,\"range\":5}]}";
        a.Run(graph, new Vector3(13, 10, 0), Vector2.UnitX);
        a.Tick(80);
        Assert.Equal(2, flights.Count);
        Assert.Null(flights[0].GroundFloor);
        Assert.Equal(0, flights[1].Origin.Z, 5);
        Assert.Equal(flights[1].Context.From, flights[1].Origin);
        Assert.Equal(0, flights[1].Velocity.Z);
        Assert.Null(flights[1].GroundFloor);
    }
    [Theory]
    [InlineData("-0.1", true)]
    [InlineData("1e100", true)]
    [InlineData("\"wrong\"", true)]
    [InlineData("0", false)]
    [InlineData("0.25", false)]
    public void GroundFloorRuleValidatesFiniteNonnegativeHeights(string json, bool invalid)
    {
        var content = DeliveryArena.Registry;
        ContentKinds.TryGet("rules", out var kind);
        var rule = new RulesDef { Id = "delivery", Values = new JsonObject { ["projectile_ground_floor"] = JsonNode.Parse(json) } };
        var report = new ValidationReport();
        kind!.Validate!(rule, new ValidationContext(content, report, "rules", "delivery", null));
        Assert.Equal(invalid, report.HasErrors);
    }
    [Theory]
    [InlineData(false, 4.4f)]
    [InlineData(true, 4.4f)]
    [InlineData(true, 20f)]
    public void ReturningPierceCountsOnlyTheSegmentActuallyFlown(bool chain, float range)
    {
        var a = Arena(0);
        var first = a.Body(12, 10);
        if (chain)
        {
            a.Body(14, 10);
            a.Body(17, 10, pattern: "gnasher_whelp");
        }
        a.Run("{\"type\":\"projectile\",\"speed\":15,\"range\":" + range.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ",\"radius\":0.1,\"pierce\":1,\"chains\":" + (chain ? "1" : "0") +
            ",\"return\":\"origin\",\"on_hit\":[{\"type\":\"event\",\"name\":\"leg_hit\"}]}",
            new(12, 10, 0), Vector2.UnitX, first);
        var p = a.Sim.World.Query<Projectile>().Single().C1;
        float? legStart = chain ? null : 0;
        var lastChains = p.Chains;
        for (var i = 0; i < 140 && !p.Ended; i++)
        {
            a.Tick();
            if (p.Chains != lastChains)
            {
                legStart = p.Odometer - p.Traveled;
                lastChains = p.Chains;
            }
            if (legStart is { } start)
            {
                Assert.Equal(start, p.Odometer - p.Traveled, 4);
                if (p.Returning)
                {
                    Assert.True(p.Odometer - start >= range);
                    Assert.Equal(chain ? 3 : 1, a.Struck("leg_hit").Distinct().Count());
                    return;
                }
            }
        }
        Assert.Fail("Expected a range-driven return after the piercing contacts.");
    }
    [Theory]
    [InlineData("gnasher_whelp", -.125f)]
    [InlineData("gnasher_whelp", 0)]
    [InlineData("gnasher_whelp", .125f)]
    [InlineData("reed_gnasher", 0)]
    public void FreeGlaiveKeepsDescendingAfterPiercingAnEarlierBody(string pattern, float slope)
    {
        var a = Arena(slope);
        var first = a.Body(12, 10, pattern: "goblin_skirmisher");
        var later = a.Body(15, 10, pattern: pattern);
        foreach (var body in new[] { first, later })
        {
            var stats = a.Sim.World.Get<Stats>(body)!;
            stats.Attributes.SetBase("life.max", 100000);
            stats.FillResources();
        }
        var flights = Capture(a);
        Press(a, "stone_glaive", 6);
        var outward = new HashSet<Entity>();
        a.Sim.Events.Subscribe<DamageDealt>(hit =>
        {
            if (flights.Count == 1 && !flights[0].Returning) outward.Add(hit.Target);
        });
        a.Tick(150);
        var p = Assert.Single(flights);
        output.WriteLine($"free glaive/{pattern}/{slope}: outward={string.Join(',', outward)}, raw={p.Velocity}");
        Assert.Contains(first, outward);
        Assert.Contains(later, outward);
        Assert.Single(a.Events.OfType<ProjectileReturned>());
    }

    [Theory]
    [InlineData(-.125f)]
    [InlineData(0)]
    [InlineData(.125f)]
    public void HoveredGlaiveStillReturnsWhenItsTargetMovesAside(float slope)
    {
        var a = Arena(slope);
        var target = a.Body(13, 10, pattern: "gnasher_whelp");
        var flights = Capture(a);
        Press(a, "stone_glaive", 3, target);
        for (var i = 0; i < 40 && flights.Count == 0; i++) a.Tick();
        var p = Assert.Single(flights);
        var body = a.Sim.World.Get<Transform>(target)!;
        body.Position += new Vector3(0, 4, 0);
        a.Sim.Spatial.Update(target, body.Position);
        a.Tick(150);
        Assert.DoesNotContain(a.Events.OfType<DamageDealt>(), hit => hit.Target == target);
        Assert.True(p.Traveled >= p.Range, $"Missed flight stopped at {p.Traveled} of {p.Range}");
        Assert.Single(a.Events.OfType<ProjectileTurned>());
        Assert.Single(a.Events.OfType<ProjectileReturned>());
    }
    private static DeliveryArena Arena(float slope)
    {
        var a = DeliveryArena.Create(seed: 481);
        var chunk = a.Sim.Terrain.GetChunk(new ChunkCoord(0, 0));
        for (var y = 0; y < chunk.Size; y++)
        for (var x = 0; x < chunk.Size; x++) chunk.Height[chunk.Index(x, y)] = slope * (x + .5f - 10);
        chunk.Modified = true;
        var t = a.Sim.World.Get<Transform>(a.Caster)!;
        t.Height = 1.8f;
        a.Sim.World.Add(a.Caster, new HitBody { Definition = a.Sim.Content.Get<HitBodyDef>(a.Sim.Content.Rules.Text("body.player_hit_profile", ""))! });
        HitBodies.InvalidateBounds(a.Sim);
        return a;
    }

    private static List<Projectile> Capture(DeliveryArena a)
    {
        var flights = new List<Projectile>();
        a.Sim.Events.Subscribe<EntitySpawned>(e => { if (a.Sim.World.Get<Projectile>(e.Entity) is { } p) flights.Add(p); });
        return flights;
    }

    private static void Press(DeliveryArena a, string preset, float distance, Entity? target = null)
    {
        var point = new Vector3(10 + distance, 10, a.Sim.Terrain.HeightAt(10 + distance, 10));
        var slot = a.Sim.World.Get<WeaveBook>(a.Caster)!.Add(new WeaveSpec { Preset = preset });
        Assert.True(a.Sim.Service<WeaveRuntime>().TryBegin(a.Sim, a.Caster, slot, target ?? Entity.None, point, Vector2.UnitX, out var reason), reason);
    }
}
