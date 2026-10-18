using System.Text.Json;
using System.Numerics;
using HollowWake.Core.Effects;
using HollowWake.Client.UI.Logic;
using HollowWake.Protocol;
using Xunit;
namespace HollowWake.Client.Tests;
public class CastForecastTests
{
    private static CastForecastInfo Cast(long id = 1) => new() { CastId = id, Source = 12, StartedAt = 1, NextEmissionAt = 3 };
    private static CastForecastMsg Frame(long sequence, double time, params CastForecastInfo[] casts) => new("zone", sequence, time, time + .5, casts.ToList(), 0);
    [Fact]
    public void Full_baselines_repair_interest_and_terminals_cannot_resurrect()
    {
        var t = new CastForecastTimeline(); t.Reset("zone");
        Assert.True(t.Receive(Frame(1, 1, Cast()))); Assert.Single(t.Active);
        t.Lifecycle(new(1, 1, "interrupt", 12, 0, "", "", "cast", new(), 1.2, 1, 2, 0, 0, 0, 0, 0, 0, false, ""));
        Assert.Empty(t.Active);
        Assert.True(t.Receive(Frame(2, 1.1, Cast()))); Assert.Empty(t.Active);
        Assert.True(t.Receive(Frame(3, 1.3))); Assert.Empty(t.Active);
        Assert.False(t.Receive(Frame(2, 1.1, Cast())));
        Assert.True(t.Receive(Frame(4, 2, Cast(2)))); Assert.Single(t.Active);
        t.RemoveEntity(12); Assert.Empty(t.Active);
        t.Reset("next-zone"); Assert.False(t.Receive(Frame(5, 2.2, Cast(2))));
    }
    [Fact]
    public void Unchanged_references_require_the_exact_revision_and_full_records_repair_loss()
    {
        var t = new CastForecastTimeline(); t.Reset("zone"); t.Receive(Frame(1, 1, Cast()));
        t.Receive(Frame(2, 1.1) with { Retained = new() { new(1, 1) } });
        Assert.Single(t.Active); Assert.Equal(0, t.Omitted);
        // Revision 3 changed geometry but was not received. Never refresh revision 1 as if current.
        t.Receive(Frame(4, 1.3) with { Retained = new() { new(1, 3) } });
        Assert.Empty(t.Active); Assert.Equal(1, t.Omitted);
        t.Receive(Frame(5, 1.4, Cast())); Assert.Single(t.Active); Assert.Equal(0, t.Omitted);
        t.Receive(Frame(6, 1.5)); Assert.Empty(t.Active);
        t.Receive(Frame(7, 1.6) with { Retained = new() { new(1, 5) } });
        Assert.Empty(t.Active); Assert.Equal(1, t.Omitted);
    }
    [Fact]
    public void Clocks_describe_readiness_without_releasing_and_staleness_is_explicit()
    {
        var t = new CastForecastTimeline(); t.Reset("zone"); t.Receive(Frame(1, 1, Cast()));
        Assert.False(t.Stale(1.5)); Assert.True(t.Stale(1.51)); Assert.Single(t.Active);
        Assert.Equal(.5, CastForecastTimeline.Progress(Cast(), 2));
        Assert.Equal(.5, CastForecastTimeline.Progress(Cast() with { Mode = "channel", NextEmissionStartedAt = 5, NextEmissionAt = 7 }, 6));
        Assert.Equal("waiting", CastForecastTimeline.Caption(Cast(), 4, false));
        Assert.Equal("ready", CastForecastTimeline.Caption(Cast() with { ReadyAt = 2 }, 4, false));
        Assert.Equal("channel", CastForecastTimeline.Caption(Cast() with { Mode = "channel" }, 4, false));
        Assert.Equal("stale", CastForecastTimeline.Caption(Cast(), 4, true));
    }
    [Fact]
    public void Unknown_shapes_do_not_become_circles_and_bad_geometry_is_declined()
    {
        var r = new CastForecastRegion { Shape = "modded", Size = new() { Radius = 2 }, HeightTolerance = 4 };
        Assert.False(CastForecastTimeline.Usable(r));
        Assert.True(CastForecastTimeline.Usable(r with { Shape = "circle" }));
        Assert.False(CastForecastTimeline.Usable(r with { Shape = "circle", Heading = float.NaN }));
    }
    [Fact]
    public void Large_modified_areas_increase_precision_or_decline_at_the_budget()
    {
        var r = new CastForecastRegion { Shape = "circle", Size = new() { Radius = 50 }, HeightTolerance = 4 };
        var segments = CastForecastTimeline.SegmentsFor(r, 64, 256, .025);
        Assert.InRange(segments, 65, 256);
        Assert.True(50 * (1 - Math.Cos(Math.PI / segments)) <= .025);
        Assert.Equal(0, CastForecastTimeline.SegmentsFor(r with { Size = new() { Radius = 100000 } }, 64, 256, .025));
    }
    [Fact]
    public void Every_registered_shape_matches_combat_samples_including_thin_hollows()
    {
        var root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "tests/fixtures"))) root = Directory.GetParent(root)!.FullName;
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/cast-forecasts/shapes.json")));
        var names = json.RootElement.EnumerateArray().Select(item => item.GetProperty("shape").GetString()!).Distinct().Order().ToArray();
        Assert.Equal(Shapes.Names.Order(), names);
        Assert.Equal(ShapeOutline.Names.Order(), names);
        foreach (var item in json.RootElement.EnumerateArray())
        {
            var size = item.GetProperty("size");
            var r = new CastForecastRegion { Shape = item.GetProperty("shape").GetString()!, Size = new()
            { Radius = size.GetProperty("radius").GetSingle(), Angle = size.GetProperty("angle").GetSingle(),
              Width = size.GetProperty("width").GetSingle(), Inner = size.GetProperty("inner").GetSingle(), Curvature = size.GetProperty("curvature").GetSingle() } };
            Assert.True(CastForecastTimeline.Usable(r));
            var outline = CastForecastTimeline.Outline(r, 64);
            var combatSize = new ShapeSize(r.Size.Radius!.Value, r.Size.Angle!.Value * 180 / MathF.PI,
                r.Size.Width!.Value, r.Size.Inner!.Value / r.Size.Radius.Value);
            foreach (var p in item.GetProperty("points").EnumerateArray())
            {
                Assert.Equal(p.GetProperty("inside").GetBoolean(), Shapes.Get(r.Shape).Contains(combatSize,
                    new Vector2(p.GetProperty("x").GetSingle(), p.GetProperty("y").GetSingle()), Vector2.UnitX, 0));
                Assert.True(p.GetProperty("inside").GetBoolean() == ShapeOutline.Inside(outline, p.GetProperty("x").GetSingle(), p.GetProperty("y").GetSingle()), $"{r.Shape}, inner {r.Size.Inner}: {p}");
            }
        }
    }
}
