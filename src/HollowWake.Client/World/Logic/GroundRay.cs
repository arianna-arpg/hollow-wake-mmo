using System.Numerics;
using HollowWake.Core.World;

namespace HollowWake.Client.World.Logic;

/// <summary>A camera ray against visible, loaded base terrain and walkable layer tops.</summary>
public static class GroundRay
{
    public static Vector3? Pick(Terrain terrain, Vector3 origin, Vector3 direction,
        Func<ChunkCoord, bool> visible, float maxDistance = 400)
    {
        bool Height(Vector3 point, out float height)
        {
            height = 0;
            return visible(terrain.Layout.ChunkOf(point.X, point.Y)) && terrain.TryHeightAt(point.X, point.Y, out height);
        }
        const float step = .5f;
        var previous = origin;
        for (var from = 0f; from < maxDistance; from += step)
        {
            var to = MathF.Min(maxDistance, from + step);
            var next = origin + direction * to;
            var best = float.PositiveInfinity;
            Vector3? hit = null;
            // A layer is a flat cell surface, not a solid height field. Intersect its top plane and
            // require the intersection to lie inside that cell. Rays already below a deck stay below it.
            if (direction.Z < -1e-6f)
            {
                for (var y = (int)MathF.Floor(MathF.Min(previous.Y, next.Y)); y <= (int)MathF.Floor(MathF.Max(previous.Y, next.Y)); y++)
                for (var x = (int)MathF.Floor(MathF.Min(previous.X, next.X)); x <= (int)MathF.Floor(MathF.Max(previous.X, next.X)); x++)
                {
                    var cell = new CellCoord(x, y);
                    var coord = terrain.Layout.ChunkOf(cell);
                    if (!visible(coord) || !terrain.TryGetLoaded(coord, out var chunk) ||
                        !chunk.Layers.TryGetValue(terrain.Layout.LocalIndex(cell), out var layers)) continue;
                    foreach (var surface in layers)
                    {
                        if (!surface.Walkable || surface.Tags.Has("ground")) continue;
                        var distance = (surface.Height - origin.Z) / direction.Z;
                        if (distance < from || distance > to || distance >= best) continue;
                        var at = origin + direction * distance;
                        if (MathF.Floor(at.X) != x || MathF.Floor(at.Y) != y) continue;
                        best = distance;
                        hit = at with { Z = surface.Height };
                    }
                }
            }
            if (Height(next, out var baseHeight) && next.Z <= baseHeight)
            {
                var lo = from;
                var hi = to;
                for (var i = 0; i < 6; i++)
                {
                    var mid = (lo + hi) * .5f;
                    var at = origin + direction * mid;
                    if (Height(at, out var h) && at.Z <= h) hi = mid;
                    else lo = mid;
                }
                if (hi < best)
                {
                    var at = origin + direction * hi;
                    if (Height(at, out var h)) hit = at with { Z = h };
                }
            }
            if (hit is not null) return hit;
            previous = next;
        }
        return null;
    }
}
