namespace HollowWake.Core.Definitions;

/// <summary>Client-only normalized cosmetic geometry. It never describes or tests a hit region.</summary>
public static class CastVisualPrimitives
{
    private static readonly Dictionary<string, Func<CastVisualPhase, IReadOnlyList<float[]>>> Builders = new(StringComparer.Ordinal)
    {
        ["segments"] = p => p.Segments,
        ["ring"] = p => Radial(p, ring: true),
        ["rays"] = p => Radial(p, ring: false),
        ["corners"] = Corners,
    };

    public static IEnumerable<string> Kinds => Builders.Keys.Order(StringComparer.Ordinal);
    public static void Register(string kind, Func<CastVisualPhase, IReadOnlyList<float[]>> builder) => Builders[kind] = builder;
    public static bool Contains(string kind) => Builders.ContainsKey(kind);
    public static IReadOnlyList<float[]> Build(CastVisualPhase phase) => Builders.TryGetValue(phase.Primitive, out var build)
        ? build(phase) : Array.Empty<float[]>();

    private static IReadOnlyList<float[]> Radial(CastVisualPhase p, bool ring)
    {
        var lines = new List<float[]>();
        var arc = p.Arc * Math.PI / 180;
        for (var i = 0; i < p.Pieces; i++)
        {
            var a = -arc / 2 + arc * i / p.Pieces;
            var b = ring ? -arc / 2 + arc * (i + 1) / p.Pieces : a;
            var inner = ring ? 1 : 0.35;
            lines.Add(new[] { (float)(Math.Cos(a) * inner), (float)(Math.Sin(a) * inner), 0f,
                (float)Math.Cos(b), (float)Math.Sin(b), 0f });
        }
        return lines;
    }

    private static IReadOnlyList<float[]> Corners(CastVisualPhase p)
    {
        var lines = new List<float[]>();
        for (var i = 0; i < p.Pieces; i++)
        {
            var a = Math.PI * 2 * i / p.Pieces;
            var c = Math.Cos(a); var s = Math.Sin(a);
            lines.Add(new[] { (float)(c + s * 0.18), (float)(s - c * 0.18), 0f,
                (float)(c - s * 0.18), (float)(s + c * 0.18), 0f });
        }
        return lines;
    }
}
