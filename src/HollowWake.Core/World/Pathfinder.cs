using System.Numerics;

namespace HollowWake.Core.World;

/// <summary>
/// Grid A* over terrain cells honoring step height and drop limits, with cell tags priced by a cost
/// function (roads cheap, bog dear). Node budget bounds the work for a server tick; failed searches
/// return the best partial path toward the goal so click-to-move still makes progress.
/// </summary>
public sealed class Pathfinder
{
    private readonly Terrain _terrain;

    public double StepUp { get; init; } = 0.6;
    public double MaxDrop { get; init; } = 3.0;
    public int MaxNodes { get; init; } = 4000;
    public Func<CellCoord, double> CellCost { get; init; } = _ => 1.0;

    public Pathfinder(Terrain terrain)
    {
        _terrain = terrain;
    }

    private static readonly (int dx, int dy, double cost)[] Neighbors =
    {
        (1, 0, 1), (-1, 0, 1), (0, 1, 1), (0, -1, 1),
        (1, 1, 1.4142), (-1, 1, 1.4142), (1, -1, 1.4142), (-1, -1, 1.4142),
    };

    public List<Vector3> FindPath(Vector3 from, Vector3 to)
    {
        var start = CellCoord.Of(from.X, from.Y);
        var goal = CellCoord.Of(to.X, to.Y);
        if (start == goal)
        {
            return new List<Vector3> { to };
        }

        var open = new PriorityQueue<(CellCoord cell, double z), double>();
        var gScore = new Dictionary<CellCoord, double> { [start] = 0 };
        var cameFrom = new Dictionary<CellCoord, CellCoord>();
        var heightAt = new Dictionary<CellCoord, double> { [start] = from.Z };
        open.Enqueue((start, from.Z), Heuristic(start, goal));
        var closed = new HashSet<CellCoord>();
        var best = start;
        var bestH = Heuristic(start, goal);
        var expanded = 0;

        while (open.Count > 0 && expanded < MaxNodes)
        {
            var (current, z) = open.Dequeue();
            if (!closed.Add(current))
            {
                continue;
            }
            expanded++;
            var h = Heuristic(current, goal);
            if (h < bestH)
            {
                bestH = h;
                best = current;
            }
            if (current == goal)
            {
                return Reconstruct(cameFrom, current, heightAt, to);
            }
            foreach (var (dx, dy, stepCost) in Neighbors)
            {
                var next = current.Offset(dx, dy);
                if (closed.Contains(next))
                {
                    continue;
                }
                if (dx != 0 && dy != 0)
                {
                    // No corner cutting past blocked cells.
                    if (!_terrain.CanStep(current, current.Offset(dx, 0), z, StepUp, MaxDrop) || !_terrain.CanStep(current, current.Offset(0, dy), z, StepUp, MaxDrop))
                    {
                        continue;
                    }
                }
                if (!_terrain.TryFindGround(next.X + 0.5, next.Y + 0.5, z, StepUp, out var ground, out _))
                {
                    continue;
                }
                if (!ground.Walkable || z - ground.Height > MaxDrop)
                {
                    continue;
                }
                if ((_terrain.FlagsAt(next) & CellFlags.Blocked) != 0)
                {
                    continue;
                }
                var tentative = gScore[current] + stepCost * CellCost(next) + Math.Abs(ground.Height - z) * 0.5;
                if (!gScore.TryGetValue(next, out var known) || tentative < known)
                {
                    gScore[next] = tentative;
                    cameFrom[next] = current;
                    heightAt[next] = ground.Height;
                    open.Enqueue((next, ground.Height), tentative + Heuristic(next, goal));
                }
            }
        }
        // Partial path toward the goal.
        return best == start ? new List<Vector3>() : Reconstruct(cameFrom, best, heightAt, null);
    }

    private static double Heuristic(CellCoord a, CellCoord b)
    {
        var dx = Math.Abs(a.X - b.X);
        var dy = Math.Abs(a.Y - b.Y);
        return Math.Max(dx, dy) + 0.4142 * Math.Min(dx, dy);
    }

    private static List<Vector3> Reconstruct(Dictionary<CellCoord, CellCoord> cameFrom, CellCoord end, Dictionary<CellCoord, double> heightAt, Vector3? exact)
    {
        var cells = new List<CellCoord> { end };
        while (cameFrom.TryGetValue(cells[^1], out var prev))
        {
            cells.Add(prev);
        }
        cells.Reverse();
        var path = new List<Vector3>(cells.Count);
        for (var i = 1; i < cells.Count; i++)
        {
            var c = cells[i];
            path.Add(new Vector3(c.X + 0.5f, c.Y + 0.5f, (float)heightAt[c]));
        }
        if (exact is { } e && path.Count > 0)
        {
            path[^1] = new Vector3(e.X, e.Y, path[^1].Z);
        }
        return path;
    }
}
