using System.Numerics;
using System.Text.Json;
using HollowWake.Core.Effects;

// Golden samples from combat predicates, including modifier extremes and tiny anchor neighborhoods.
// Exclude a small boundary band because finite polygons approximate curves.
var fixtures = new List<object>();
foreach (var name in Shapes.Names)
{
    var shape = Shapes.Get(name);
    foreach (var hollow in new[] { 0f, .219f, .985f })
        Add(shape, new(3.27f, 121, 1.135f, hollow));
    if (shape.HasAngle)
        foreach (var angle in new[] { -45f, 0f, 360f, 450f, 720f })
        foreach (var hollow in new[] { 0f, .219f }) Add(shape, new(3.27f, angle, 1.135f, hollow));
    if (shape.HasWidth)
        foreach (var width in new[] { -0.25f, 0f, 16f }) Add(shape, new(3.27f, 450, width, 0));
    if (shape.CenterAllowanceSquared > 0)
        foreach (var radius in new[] { .06f, 3.27f })
        foreach (var hollow in new[] { 0f, .01f, .219f }) Add(shape, new(radius, 90, 0, hollow));
}
void Add(Shape shape, ShapeSize size)
{
    var points = new List<object>();
    var bound = Math.Max(.2f, size.Radius * Math.Max(1, 2 * shape.Curvature) + Math.Abs(size.Width));
    var step = bound / 12.5f;
    for (var x = -bound + .13f * step; x <= bound; x += step)
    for (var y = -bound + .21f * step; y <= bound; y += step)
        Sample(x, y, Math.Min(.03f, size.Radius / 200));
    // The regular grid can miss the core's 0.1-unit central angular allowance.
    foreach (var x in new[] { -.12f, -.08f, -.04f, .005f, .04f, .08f, .12f, -1f, 1f })
    foreach (var y in new[] { .005f, .02f, -.02f, .08f, .2f }) Sample(x, y, .002f);
    void Sample(float x, float y, float band)
    {
        var inside = shape.Contains(size, new Vector2(x, y), Vector2.UnitX, 0);
        if (new[] { -band, 0, band }.Any(dx => new[] { -band, 0, band }.Any(dy => shape.Contains(size, new(x + dx, y + dy), Vector2.UnitX, 0) != inside))) return;
        points.Add(new { x, y, inside });
    }
    fixtures.Add(new { shape = shape.Name, center_allowance = MathF.Sqrt(shape.CenterAllowanceSquared),
        drawable = (!shape.HasWidth || size.Width > 0) && (!shape.HasAngle || size.Angle > 0),
        size = new { radius = size.Radius, angle = size.Angle * MathF.PI / 180,
        width = size.Width, inner = size.Inner * size.Radius, curvature = shape.Curvature }, points });
}
var path = Path.GetFullPath(args.Length > 0 ? args[0] : "tests/fixtures/cast-forecasts/shapes.json");
Directory.CreateDirectory(Path.GetDirectoryName(path)!);
File.WriteAllText(path, JsonSerializer.Serialize(fixtures));
Console.WriteLine($"Wrote {fixtures.Count} fixtures from combat predicates.");
