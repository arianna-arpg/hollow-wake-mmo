using System.Numerics;
using HollowWake.Protocol;

namespace HollowWake.Client.World.Logic;

/// <summary>Server-volume outlines in core coordinates. No prediction, picking, animation or gameplay.</summary>
public static class HitBodyLines
{
    private static readonly Dictionary<string, Func<float, Vector2>> Profiles = new(StringComparer.Ordinal)
    {
        ["ellipsoid"] = a => new(MathF.Cos(a), MathF.Sin(a)),
        ["cylinder"] = a => new(MathF.Cos(a) >= -0.000001f ? 1 : -1, MathF.Sin(a)),
    };
    public static bool Supported(HitBodyInfo body) => body.Volumes.All(v => Profiles.ContainsKey(v.Shape));

    public static List<Vector3> Build(HitBodyInfo body, float radius, float height, int segments = 32)
    {
        var lines = new List<Vector3>();
        if (!Supported(body)) return lines;
        foreach (var v in body.Volumes)
        {
            var c = new Vector3(v.Forward * radius, v.Left * radius, v.Up * height);
            var r = v.Radius * radius; var h = v.HalfHeight * height;
            for (var plane = 0; plane < 3; plane++)
            for (var i = 0; i < segments; i++)
            {
                Vector3 Point(int k)
                {
                    var a = k * MathF.Tau / segments;
                    if (plane == 0) return c + new Vector3(MathF.Cos(a) * r, MathF.Sin(a) * r, 0);
                    var p = Profiles[v.Shape](a);
                    return c + (plane == 1 ? new Vector3(p.X * r, 0, p.Y * h) : new Vector3(0, p.X * r, p.Y * h));
                }
                lines.Add(Point(i)); lines.Add(Point(i + 1));
            }
        }
        return lines;
    }
    public static HitBodyInfo Legacy { get; } = new("", 0, new() { new("cylinder", 0, 0, .5f, 1, .5f) });
}
