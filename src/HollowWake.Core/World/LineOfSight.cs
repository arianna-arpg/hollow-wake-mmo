using System.Numerics;

namespace HollowWake.Core.World;

/// <summary>
/// Rays across the terrain: whether a straight line between two points is clear of walls (structure cells
/// that cannot be walked) and of ground rising above it. Deliveries stop at what blocks a line unless
/// they phase. The walk visits each cell the segment crosses once, in order, and allocates nothing.
/// </summary>
public static class LineOfSight
{
    /// <summary>True when a cell is a wall: part of a structure that cannot be walked.</summary>
    public static bool IsWall(Terrain terrain, CellCoord cell) =>
        (terrain.FlagsAt(cell) & (CellFlags.Structure | CellFlags.Blocked)) == (CellFlags.Structure | CellFlags.Blocked);

    /// <summary>
    /// True when nothing stands between two points: no wall lies across the line and the ground stays
    /// below it. The cells the points stand in are not tested for ground, so a line between two bodies'
    /// centers is not refused by the slope under their own feet.
    /// </summary>
    public static bool Clear(Terrain terrain, Vector3 from, Vector3 to) => Blocker(terrain, from, to, out _) is null;

    /// <summary>
    /// The first cell that blocks the line from <paramref name="from"/> to <paramref name="to"/>, or null
    /// when it is clear; <paramref name="t"/> is how far along the line, from zero to one, the blocking cell
    /// begins.
    /// </summary>
    public static CellCoord? Blocker(Terrain terrain, Vector3 from, Vector3 to, out float t)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var cx = (int)MathF.Floor(from.X);
        var cy = (int)MathF.Floor(from.Y);
        var endX = (int)MathF.Floor(to.X);
        var endY = (int)MathF.Floor(to.Y);
        var stepX = Math.Sign(dx);
        var stepY = Math.Sign(dy);
        var deltaX = dx != 0 ? 1f / MathF.Abs(dx) : float.PositiveInfinity;
        var deltaY = dy != 0 ? 1f / MathF.Abs(dy) : float.PositiveInfinity;
        var maxX = dx != 0 ? (stepX > 0 ? cx + 1 - from.X : from.X - cx) * deltaX : float.PositiveInfinity;
        var maxY = dy != 0 ? (stepY > 0 ? cy + 1 - from.Y : from.Y - cy) * deltaY : float.PositiveInfinity;
        var entered = 0f;
        var cells = Math.Abs(endX - cx) + Math.Abs(endY - cy) + 1;
        for (var i = 0; i < cells; i++)
        {
            var cell = new CellCoord(cx, cy);
            var leaves = MathF.Min(MathF.Min(maxX, maxY), 1f);
            var first = i == 0;
            var last = cx == endX && cy == endY;
            if (!first && IsWall(terrain, cell))
            {
                t = entered;
                return cell;
            }
            if (!first && !last)
            {
                var mid = (entered + leaves) * 0.5f;
                var x = from.X + dx * mid;
                var y = from.Y + dy * mid;
                if (terrain.HeightAt(x, y) > from.Z + (to.Z - from.Z) * mid)
                {
                    t = entered;
                    return cell;
                }
            }
            if (last)
            {
                break;
            }
            entered = leaves;
            if (maxX < maxY)
            {
                cx += stepX;
                maxX += deltaX;
            }
            else
            {
                cy += stepY;
                maxY += deltaY;
            }
        }
        t = 1;
        return null;
    }
}
