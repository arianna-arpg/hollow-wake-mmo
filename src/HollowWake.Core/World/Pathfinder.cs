using System.Numerics;

namespace HollowWake.Core.World;

/// <summary>How a search ended: no step toward the goal, a partial path toward it, or a path to it.</summary>
public enum PathResult
{
    None,
    Partial,
    Complete,
}

/// <summary>
/// Grid A* over terrain cells honoring step height and drop limits, with cell tags priced by a cost
/// function (roads cheap, bog dear). Node budget bounds the work for a server tick; failed searches
/// return the best partial path toward the goal so click-to-move still makes progress.
/// </summary>
/// <remarks>
/// The search keeps its open queue, node table and lookup between calls and clears them per search, so
/// planning allocates nothing in steady state. One pathfinder serves one simulation and runs one search
/// at a time on the simulation's thread.
/// </remarks>
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

    /// <summary>For each diagonal neighbor, the orthogonal steps (indices into the first four neighbors) it passes between.</summary>
    private static readonly (int x, int y)[] Corners = { (0, 2), (1, 2), (0, 3), (1, 3) };

    private struct Node
    {
        public CellCoord Cell;
        public double G;
        public double Z;
        public double Cost;
        public int Parent;
        public bool Closed;
    }

    /// <summary>What a cell offers a body arriving at a given height: a node to stand on, a corner to pass.</summary>
    private struct Probe
    {
        public float Height;
        public bool Node;
        public bool Corner;
    }

    // Search state, reused between searches. Queue entries carry the height they were reached at.
    private readonly PriorityQueue<(int Node, double Z), double> _open = new();
    private readonly Dictionary<CellCoord, int> _nodeOf = new();
    private readonly List<int> _trail = new();
    private Node[] _nodes = new Node[256];
    private int _count;

    public List<Vector3> FindPath(Vector3 from, Vector3 to)
    {
        var path = new List<Vector3>();
        FindPath(from, to, path, MaxNodes);
        return path;
    }

    /// <summary>
    /// Plans from <paramref name="from"/> toward <paramref name="to"/> into a caller-owned list (cleared
    /// first), expanding at most <paramref name="maxNodes"/> cells. Same paths as <see cref="FindPath(Vector3, Vector3)"/>
    /// for the same budget, without allocating.
    /// </summary>
    public PathResult FindPath(Vector3 from, Vector3 to, List<Vector3> into, int maxNodes)
    {
        into.Clear();
        var start = CellCoord.Of(from.X, from.Y);
        var goal = CellCoord.Of(to.X, to.Y);
        if (start == goal)
        {
            into.Add(to);
            return PathResult.Complete;
        }

        _open.Clear();
        _nodeOf.Clear();
        _count = 0;
        var startNode = AddNode(start, 0, from.Z, -1, 0);
        _open.Enqueue((startNode, from.Z), Heuristic(start, goal));
        var best = startNode;
        var bestH = Heuristic(start, goal);
        var expanded = 0;
        Span<Probe> steps = stackalloc Probe[4];

        while (_open.Count > 0 && expanded < maxNodes)
        {
            var (current, z) = _open.Dequeue();
            if (_nodes[current].Closed)
            {
                continue;
            }
            _nodes[current].Closed = true;
            expanded++;
            var cell = _nodes[current].Cell;
            var g = _nodes[current].G;
            var h = Heuristic(cell, goal);
            if (h < bestH)
            {
                bestH = h;
                best = current;
            }
            if (cell == goal)
            {
                Reconstruct(current, into, to);
                return PathResult.Complete;
            }
            // The four orthogonal cells serve as neighbors and as the corners diagonal moves pass.
            for (var i = 0; i < 4; i++)
            {
                steps[i] = ProbeCell(cell.Offset(Neighbors[i].dx, Neighbors[i].dy), z);
            }
            for (var n = 0; n < Neighbors.Length; n++)
            {
                var (dx, dy, stepCost) = Neighbors[n];
                var next = cell.Offset(dx, dy);
                var known = _nodeOf.TryGetValue(next, out var nextNode);
                if (known && _nodes[nextNode].Closed)
                {
                    continue;
                }
                float height;
                if (n < 4)
                {
                    if (!steps[n].Node)
                    {
                        continue;
                    }
                    height = steps[n].Height;
                }
                else
                {
                    // No corner cutting past blocked cells.
                    var (cx, cy) = Corners[n - 4];
                    if (!steps[cx].Corner || !steps[cy].Corner)
                    {
                        continue;
                    }
                    var probe = ProbeCell(next, z);
                    if (!probe.Node)
                    {
                        continue;
                    }
                    height = probe.Height;
                }
                var cost = known ? _nodes[nextNode].Cost : CellCost(next);
                var tentative = g + stepCost * cost + Math.Abs(height - z) * 0.5;
                if (!known)
                {
                    nextNode = AddNode(next, tentative, height, current, cost);
                    _open.Enqueue((nextNode, height), tentative + Heuristic(next, goal));
                }
                else if (tentative < _nodes[nextNode].G)
                {
                    ref var node = ref _nodes[nextNode];
                    node.G = tentative;
                    node.Z = height;
                    node.Parent = current;
                    _open.Enqueue((nextNode, height), tentative + Heuristic(next, goal));
                }
            }
        }
        // Partial path toward the goal.
        if (best == startNode)
        {
            return PathResult.None;
        }
        Reconstruct(best, into, null);
        return PathResult.Partial;
    }

    /// <summary>
    /// The ground a body at height z finds in a cell, judged as a cell to stand in (walkable, not blocked,
    /// within the drop) and as a corner to pass (the step rule of <see cref="Terrain.CanStep"/>).
    /// </summary>
    private Probe ProbeCell(CellCoord cell, double z)
    {
        if (!_terrain.TryFindGround(cell.X + 0.5, cell.Y + 0.5, z, StepUp, out var ground, out _, out var flags))
        {
            return default;
        }
        var blocked = (flags & CellFlags.Blocked) != 0;
        var drop = z - ground.Height;
        return new Probe
        {
            Height = ground.Height,
            Node = ground.Walkable && !(drop > MaxDrop) && !blocked,
            Corner = !(blocked && ground.Tags.Has("ground")) && drop <= MaxDrop,
        };
    }

    private int AddNode(CellCoord cell, double g, double z, int parent, double cost)
    {
        if (_count == _nodes.Length)
        {
            Array.Resize(ref _nodes, _nodes.Length * 2);
        }
        var index = _count++;
        _nodes[index] = new Node { Cell = cell, G = g, Z = z, Parent = parent, Cost = cost };
        _nodeOf[cell] = index;
        return index;
    }

    private static double Heuristic(CellCoord a, CellCoord b)
    {
        var dx = Math.Abs(a.X - b.X);
        var dy = Math.Abs(a.Y - b.Y);
        return Math.Max(dx, dy) + 0.4142 * Math.Min(dx, dy);
    }

    /// <summary>Writes the cell centers from the start (exclusive) to <paramref name="end"/>, the last replaced by the exact goal when given.</summary>
    private void Reconstruct(int end, List<Vector3> into, Vector3? exact)
    {
        _trail.Clear();
        for (var i = end; i >= 0; i = _nodes[i].Parent)
        {
            _trail.Add(i);
        }
        for (var k = _trail.Count - 2; k >= 0; k--)
        {
            var node = _nodes[_trail[k]];
            into.Add(new Vector3(node.Cell.X + 0.5f, node.Cell.Y + 0.5f, (float)node.Z));
        }
        if (exact is { } e && into.Count > 0)
        {
            into[^1] = new Vector3(e.X, e.Y, into[^1].Z);
        }
    }
}
