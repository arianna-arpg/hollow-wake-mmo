using System.Numerics;
using System.Text.Json;
using HollowWake.Core.Effects;

// Golden samples from combat predicates. Exclude points within .03 units of a boundary because finite
// polygons approximate curves. Include narrow hollows to catch cosmetic clamping in forecast adapters.
var fixtures = new List<object>();
foreach (var name in Shapes.Names)
foreach (var hollow in new[] { 0f, .219f, .985f })
{
    var shape = Shapes.Get(name);
    var size = new ShapeSize(3.27f, 121, 1.135f, hollow);
    var points = new List<object>();
    for (var x = -5.87f; x <= 6; x += .47f)
    for (var y = -5.83f; y <= 6; y += .47f)
    {
        var inside = shape.Contains(size, new Vector2(x, y), Vector2.UnitX, 0);
        if (new[] { -.03f, 0, .03f }.Any(dx => new[] { -.03f, 0, .03f }.Any(dy => shape.Contains(size, new(x + dx, y + dy), Vector2.UnitX, 0) != inside))) continue;
        points.Add(new { x, y, inside });
    }
    fixtures.Add(new { shape = name, size = new { radius = size.Radius, angle = size.Angle * MathF.PI / 180,
        width = size.Width, inner = size.Inner * size.Radius, curvature = shape.Curvature }, points });
}
var path = Path.GetFullPath(args.Length > 0 ? args[0] : "tests/fixtures/cast-forecasts/shapes.json");
Directory.CreateDirectory(Path.GetDirectoryName(path)!);
File.WriteAllText(path, JsonSerializer.Serialize(fixtures));
Console.WriteLine($"Wrote {fixtures.Count} fixtures from combat predicates.");
