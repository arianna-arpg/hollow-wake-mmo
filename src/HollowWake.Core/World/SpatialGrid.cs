using System.Numerics;
using HollowWake.Core.Ecs;

namespace HollowWake.Core.World;

/// <summary>
/// Uniform grid index of entity positions for radius and area queries: targeting, interest management,
/// perception. Rebuilt or updated each tick by the movement system.
/// </summary>
public sealed class SpatialGrid
{
    private readonly Dictionary<(int, int), List<Entity>> _cells = new();
    private readonly Dictionary<Entity, (int, int)> _where = new();
    private readonly Dictionary<Entity, Vector3> _positions = new();
    private readonly float _cellSize;

    public SpatialGrid(float cellSize = 8f)
    {
        _cellSize = cellSize;
    }

    public int Count => _where.Count;

    private (int, int) KeyOf(Vector3 p) => ((int)Math.Floor(p.X / _cellSize), (int)Math.Floor(p.Y / _cellSize));

    public void Update(Entity e, Vector3 position)
    {
        var key = KeyOf(position);
        if (_where.TryGetValue(e, out var old))
        {
            if (old != key)
            {
                _cells[old].Remove(e);
                if (!_cells.TryGetValue(key, out var list))
                {
                    list = new List<Entity>();
                    _cells[key] = list;
                }
                list.Add(e);
                _where[e] = key;
            }
        }
        else
        {
            if (!_cells.TryGetValue(key, out var list))
            {
                list = new List<Entity>();
                _cells[key] = list;
            }
            list.Add(e);
            _where[e] = key;
        }
        _positions[e] = position;
    }

    public void Remove(Entity e)
    {
        if (_where.TryGetValue(e, out var key))
        {
            _cells[key].Remove(e);
            _where.Remove(e);
            _positions.Remove(e);
        }
    }

    public bool TryGetPosition(Entity e, out Vector3 position) => _positions.TryGetValue(e, out position);

    /// <summary>Entities within radius of a point, measured on the ground plane with a height tolerance.</summary>
    public IEnumerable<(Entity Entity, Vector3 Position, float Distance)> Within(Vector3 center, float radius, float heightTolerance = 4f)
    {
        var r2 = radius * radius;
        var minX = (int)Math.Floor((center.X - radius) / _cellSize);
        var maxX = (int)Math.Floor((center.X + radius) / _cellSize);
        var minY = (int)Math.Floor((center.Y - radius) / _cellSize);
        var maxY = (int)Math.Floor((center.Y + radius) / _cellSize);
        for (var cy = minY; cy <= maxY; cy++)
        {
            for (var cx = minX; cx <= maxX; cx++)
            {
                if (!_cells.TryGetValue((cx, cy), out var list))
                {
                    continue;
                }
                foreach (var e in list.ToArray())
                {
                    var p = _positions[e];
                    var dx = p.X - center.X;
                    var dy = p.Y - center.Y;
                    var d2 = dx * dx + dy * dy;
                    if (d2 <= r2 && Math.Abs(p.Z - center.Z) <= heightTolerance)
                    {
                        yield return (e, p, MathF.Sqrt(d2));
                    }
                }
            }
        }
    }

    public IEnumerable<Entity> All => _where.Keys;
}
