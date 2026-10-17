using System.Collections;
using System.Numerics;
using HollowWake.Core.Ecs;

namespace HollowWake.Core.World;

/// <summary>
/// Uniform grid index of entity positions for radius and area queries: targeting, interest management,
/// perception. Rebuilt or updated each tick by the movement system.
/// </summary>
/// <remarks>
/// Each grid cell keeps its entities with their positions, in the order they entered the cell, so a
/// query reads positions where it finds the entities. An entity remembers its cell and its place there.
/// </remarks>
public sealed class SpatialGrid
{
    private sealed class Cell
    {
        public readonly (int, int) Key;
        public Entry[] Items = new Entry[4];
        public int Count;
        /// <summary>Bumped when an entity enters or leaves the cell; moving inside it does not.</summary>
        public int Version;

        public Cell((int, int) key)
        {
            Key = key;
        }

        public int Add(Entity e, Vector3 position)
        {
            if (Count == Items.Length)
            {
                Array.Resize(ref Items, Items.Length * 2);
            }
            Items[Count] = new Entry(e, position);
            Version++;
            return Count++;
        }
    }

    private readonly record struct Entry(Entity Entity, Vector3 Position);

    private readonly record struct Slot(Cell Cell, int Index);

    private readonly Dictionary<(int, int), Cell> _cells = new();
    private readonly Dictionary<Entity, Slot> _where = new();
    private readonly float _cellSize;

    public SpatialGrid(float cellSize = 8f)
    {
        _cellSize = cellSize;
    }

    public int Count => _where.Count;

    private (int, int) KeyOf(Vector3 p) => ((int)Math.Floor(p.X / _cellSize), (int)Math.Floor(p.Y / _cellSize));

    private Cell CellAt((int, int) key)
    {
        if (!_cells.TryGetValue(key, out var cell))
        {
            cell = new Cell(key);
            _cells[key] = cell;
        }
        return cell;
    }

    public void Update(Entity e, Vector3 position)
    {
        var key = KeyOf(position);
        if (_where.TryGetValue(e, out var slot))
        {
            if (slot.Cell.Key == key)
            {
                slot.Cell.Items[slot.Index] = new Entry(e, position);
                return;
            }
            Leave(slot);
            var cell = CellAt(key);
            _where[e] = new Slot(cell, cell.Add(e, position));
            return;
        }
        var target = CellAt(key);
        _where[e] = new Slot(target, target.Add(e, position));
    }

    public void Remove(Entity e)
    {
        if (_where.TryGetValue(e, out var slot))
        {
            Leave(slot);
            _where.Remove(e);
        }
    }

    /// <summary>Takes an entity out of its cell, closing the gap so the others keep their order.</summary>
    private void Leave(Slot slot)
    {
        var cell = slot.Cell;
        for (var i = slot.Index + 1; i < cell.Count; i++)
        {
            var moved = cell.Items[i];
            cell.Items[i - 1] = moved;
            _where[moved.Entity] = new Slot(cell, i - 1);
        }
        cell.Count--;
        cell.Items[cell.Count] = default;
        cell.Version++;
    }

    public bool TryGetPosition(Entity e, out Vector3 position)
    {
        if (_where.TryGetValue(e, out var slot))
        {
            position = slot.Cell.Items[slot.Index].Position;
            return true;
        }
        position = default;
        return false;
    }

    /// <summary>
    /// Entities within radius of a point, measured on the ground plane with a height tolerance.
    /// Enumerates the grid in place without copying or allocating. Moving an entity within its cell
    /// while enumerating is fine; a caller that adds or removes entities, or moves them between cells,
    /// must materialize the results first (for example with <c>ToList()</c>), or the enumeration throws.
    /// </summary>
    public Query Within(Vector3 center, float radius, float heightTolerance = 4f) => new(this, center, radius, heightTolerance);

    public IEnumerable<Entity> All => _where.Keys;

    /// <summary>A radius query; enumerate it with <c>foreach</c> or LINQ.</summary>
    public readonly struct Query : IEnumerable<(Entity Entity, Vector3 Position, float Distance)>
    {
        private readonly SpatialGrid _grid;
        private readonly Vector3 _center;
        private readonly float _radius;
        private readonly float _heightTolerance;

        internal Query(SpatialGrid grid, Vector3 center, float radius, float heightTolerance)
        {
            _grid = grid;
            _center = center;
            _radius = radius;
            _heightTolerance = heightTolerance;
        }

        public Enumerator GetEnumerator() => new(_grid, _center, _radius, _heightTolerance);

        IEnumerator<(Entity Entity, Vector3 Position, float Distance)> IEnumerable<(Entity Entity, Vector3 Position, float Distance)>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>Walks the cells overlapping the query's square row by row, yielding entities inside the circle.</summary>
    public struct Enumerator : IEnumerator<(Entity Entity, Vector3 Position, float Distance)>
    {
        private readonly SpatialGrid _grid;
        private readonly Vector3 _center;
        private readonly float _r2;
        private readonly float _heightTolerance;
        private readonly int _minX;
        private readonly int _maxX;
        private readonly int _maxY;
        private int _cx;
        private int _cy;
        private Cell? _cell;
        private int _index;
        private int _version;
        private (Entity Entity, Vector3 Position, float Distance) _current;

        internal Enumerator(SpatialGrid grid, Vector3 center, float radius, float heightTolerance)
        {
            _grid = grid;
            _center = center;
            _r2 = radius * radius;
            _heightTolerance = heightTolerance;
            var size = grid._cellSize;
            _minX = (int)Math.Floor((center.X - radius) / size);
            _maxX = (int)Math.Floor((center.X + radius) / size);
            _maxY = (int)Math.Floor((center.Y + radius) / size);
            _cx = _minX - 1;
            _cy = (int)Math.Floor((center.Y - radius) / size);
            _cell = null;
            _index = 0;
            _version = 0;
            _current = default;
        }

        public readonly (Entity Entity, Vector3 Position, float Distance) Current => _current;

        readonly object IEnumerator.Current => _current;

        public bool MoveNext()
        {
            while (true)
            {
                if (_cell is { } cell)
                {
                    if (cell.Version != _version)
                    {
                        throw new InvalidOperationException("The spatial grid changed while a query was enumerating it; materialize the query first.");
                    }
                    while (_index < cell.Count)
                    {
                        var entry = cell.Items[_index++];
                        var p = entry.Position;
                        var dx = p.X - _center.X;
                        var dy = p.Y - _center.Y;
                        var d2 = dx * dx + dy * dy;
                        if (d2 <= _r2 && Math.Abs(p.Z - _center.Z) <= _heightTolerance)
                        {
                            _current = (entry.Entity, p, MathF.Sqrt(d2));
                            return true;
                        }
                    }
                    _cell = null;
                }
                if (++_cx > _maxX)
                {
                    _cx = _minX;
                    _cy++;
                }
                if (_cy > _maxY)
                {
                    return false;
                }
                if (_grid._cells.TryGetValue((_cx, _cy), out var next) && next.Count > 0)
                {
                    _cell = next;
                    _index = 0;
                    _version = next.Version;
                }
            }
        }

        public void Reset() => throw new NotSupportedException();

        public readonly void Dispose()
        {
        }
    }
}
