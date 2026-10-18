using System.Numerics;
using System.Text.Json;
using HollowWake.Client.UI.Logic;
using HollowWake.Core.Definitions;
using HollowWake.Protocol;
using Xunit;

namespace HollowWake.Client.Tests;

public class CastForecastGeometryTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private static JsonElement Fixture()
    {
        var root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "tests/fixtures"))) root = Directory.GetParent(root)!.FullName;
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/cast-forecasts/slabs.json")));
        return document.RootElement.Clone();
    }
    public static IEnumerable<object[]> GeometryCases() => Fixture().GetProperty("geometry").EnumerateArray()
        .Select(c => new object[] { c.GetProperty("name").GetString()!, c });
    public static IEnumerable<object[]> CacheCases() => Fixture().GetProperty("cache").EnumerateArray()
        .Select(c => new object[] { c.GetProperty("name").GetString()!, c });
    private static float[] Endpoints((Vector3 A, Vector3 B) segment) =>
        new[] { segment.A.X, segment.A.Y, segment.A.Z, segment.B.X, segment.B.Y, segment.B.Z };
    private static void SameEndpoints(string name, IEnumerable<float[]> expected, IEnumerable<float[]> actual)
    {
        var want = expected.ToArray(); var got = actual.ToArray();
        Assert.True(want.Length == got.Length, $"{name}: expected {want.Length} segments, got {got.Length}");
        for (var i = 0; i < want.Length; i++)
            for (var axis = 0; axis < 6; axis++)
                Assert.True(Math.Abs(want[i][axis] - got[i][axis]) < 0.000001f,
                    $"{name}: segment {i} coordinate {axis}: expected {want[i][axis]}, got {got[i][axis]}");
    }

    [Theory]
    [MemberData(nameof(GeometryCases))]
    public void Renderer_segments_match_the_paired_slab_fixture(string name, JsonElement sample)
    {
        var region = sample.GetProperty("region").Deserialize<CastForecastRegion>(Json)!;
        var style = Fixture().GetProperty("style").Deserialize<CastForecastStyleDef>(Json)!;
        var actual = CastForecastGeometry.Build(region, sample.GetProperty("committed").GetBoolean(), style);
        var expected = sample.GetProperty("expected_segments").EnumerateArray()
            .Select(segment => segment.EnumerateArray().Select(n => n.GetSingle()).ToArray());
        SameEndpoints(name, expected, actual.Select(Endpoints));
    }

    [Theory]
    [MemberData(nameof(CacheCases))]
    public void Renderer_reuse_matches_the_paired_fixture(string name, JsonElement sample)
    {
        var before = sample.GetProperty("before").Deserialize<CastForecastRegion>(Json);
        var after = sample.GetProperty("after").Deserialize<CastForecastRegion>(Json);
        var oldCommitted = sample.GetProperty("before_committed").GetBoolean();
        var committed = sample.GetProperty("after_committed").GetBoolean();
        var reuse = sample.GetProperty("reuse").GetBoolean();
        Assert.True(reuse == CastForecastGeometry.SameMesh(before, oldCommitted, after, committed), name);
        if (reuse && before is not null && after is not null)
        {
            var style = Fixture().GetProperty("style").Deserialize<CastForecastStyleDef>(Json)!;
            SameEndpoints(name, CastForecastGeometry.Build(before, oldCommitted, style).Select(Endpoints),
                CastForecastGeometry.Build(after, committed, style).Select(Endpoints));
        }
    }
}
