using Godot;
using Skein.Core.Content;
using Skein.Core.World;
using Skein.Core.WorldGen;
using Skein.Protocol;

namespace Skein.Client.World;

/// <summary>
/// The client's copy of the land and everything on it. Unmodified chunks are generated locally from
/// the seed the server sent, with the same code the server runs; modified chunks arrive on the wire.
/// Entities are views driven by server snapshots.
/// </summary>
public partial class ClientWorld : Node3D
{
    private readonly Dictionary<ChunkCoord, Node3D> _chunkNodes = new();
    private readonly Dictionary<long, EntityView> _entities = new();
    private readonly Dictionary<ChunkCoord, ChunkData> _received = new();
    private readonly Queue<ChunkCoord> _toBuild = new();
    private readonly HashSet<ChunkCoord> _queued = new();
    private readonly ContentRegistry _content;
    private WorldGenerator? _generator;
    private Terrain? _terrain;
    private Node3D _chunkRoot = null!;
    private Node3D _entityRoot = null!;
    private ChunkCoord? _lastCenter;

    public int ViewChunks { get; set; } = 3;
    public WorldGenerator? Generator => _generator;
    public Terrain? Terrain => _terrain;
    public IReadOnlyDictionary<long, EntityView> Entities => _entities;
    public long SelfKey { get; set; }

    public ClientWorld(ContentRegistry content)
    {
        _content = content;
    }

    public override void _Ready()
    {
        _chunkRoot = new Node3D { Name = "Chunks" };
        _entityRoot = new Node3D { Name = "Entities" };
        AddChild(_chunkRoot);
        AddChild(_entityRoot);
        var sun = new DirectionalLight3D { Name = "Sun", LightEnergy = 1.2f, ShadowEnabled = true };
        sun.RotationDegrees = new Vector3(-55, 35, 0);
        AddChild(sun);
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.55f, 0.66f, 0.8f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.6f, 0.62f, 0.7f),
            AmbientLightEnergy = 0.9f,
            FogEnabled = true,
            FogLightColor = new Color(0.6f, 0.7f, 0.82f),
            FogDensity = 0.004f,
        };
        AddChild(new WorldEnvironment { Name = "Environment", Environment = env });
    }

    public void ApplyWorldInfo(WorldInfo info)
    {
        var overlays = new GenerationOverlays();
        foreach (var o in info.Overlays)
        {
            var overlay = new GenerationOverlay
            {
                Id = o.Id,
                Global = o.Global,
                Circle = o.Circle is { Length: 3 } c ? (c[0], c[1], c[2]) : null,
                Params = new Dictionary<string, double>(o.Params),
                Priority = o.Priority,
            };
            foreach (var r in o.Regions)
            {
                overlay.Regions.Add(new RegionCoord(r[0], r[1]));
            }
            foreach (var z in o.Zones)
            {
                overlay.Zones.Add(new ZoneCoord(z[0], z[1]));
            }
            overlays.Add(overlay);
        }
        var layout = new WorldLayout { ChunkSize = info.ChunkSize, ZoneChunks = info.ZoneChunks, RegionZones = info.RegionZones };
        _generator = new WorldGenerator(_content, info.Seed, overlays, layout);
        _terrain = new Terrain(layout, new ClientChunkProvider(_generator, _received));
        ClearChunks();
        _lastCenter = null;
    }

    public void ReceiveChunk(ChunkDataMsg msg)
    {
        var chunk = new ChunkData(new ChunkCoord(msg.X, msg.Y), msg.Size) { GenVersion = msg.GenVersion, Modified = true };
        Array.Copy(msg.Height, chunk.Height, Math.Min(msg.Height.Length, chunk.Height.Length));
        for (var i = 0; i < Math.Min(msg.Flags.Length, chunk.Flags.Length); i++)
        {
            chunk.Flags[i] = (CellFlags)msg.Flags[i];
        }
        chunk.TagTable.Clear();
        foreach (var t in msg.Tags)
        {
            chunk.TagTable.Add(Core.Tags.TagSet.Parse(t));
        }
        if (chunk.TagTable.Count == 0)
        {
            chunk.TagTable.Add(Core.Tags.TagSet.Empty);
        }
        Array.Copy(msg.SurfaceTag, chunk.SurfaceTag, Math.Min(msg.SurfaceTag.Length, chunk.SurfaceTag.Length));
        foreach (var l in msg.Layers)
        {
            chunk.AddLayer(l.Index, new Surface(l.Height, l.Walkable, Core.Tags.TagSet.Parse(l.Tags)));
        }
        _received[chunk.Coord] = chunk;
        _terrain?.Unload(chunk.Coord);
        if (_chunkNodes.Remove(chunk.Coord, out var node))
        {
            node.QueueFree();
        }
        Enqueue(chunk.Coord);
    }

    private void ClearChunks()
    {
        foreach (var n in _chunkNodes.Values)
        {
            n.QueueFree();
        }
        _chunkNodes.Clear();
        _toBuild.Clear();
        _queued.Clear();
        _terrain?.Clear();
    }

    private void Enqueue(ChunkCoord c)
    {
        if (_queued.Add(c))
        {
            _toBuild.Enqueue(c);
        }
    }

    /// <summary>Keeps chunks built around a world position and builds a few per frame.</summary>
    public void Stream(System.Numerics.Vector3 around)
    {
        if (_terrain is null)
        {
            return;
        }
        var center = _terrain.Layout.ChunkOf(around.X, around.Y);
        if (_lastCenter != center)
        {
            _lastCenter = center;
            for (var dy = -ViewChunks; dy <= ViewChunks; dy++)
            {
                for (var dx = -ViewChunks; dx <= ViewChunks; dx++)
                {
                    var c = new ChunkCoord(center.X + dx, center.Y + dy);
                    if (!_chunkNodes.ContainsKey(c))
                    {
                        Enqueue(c);
                    }
                }
            }
            foreach (var (c, node) in _chunkNodes.ToList())
            {
                if (Math.Abs(c.X - center.X) > ViewChunks + 2 || Math.Abs(c.Y - center.Y) > ViewChunks + 2)
                {
                    node.QueueFree();
                    _chunkNodes.Remove(c);
                    _terrain.Unload(c);
                }
            }
        }
        var budget = 2;
        while (budget-- > 0 && _toBuild.Count > 0)
        {
            var c = _toBuild.Dequeue();
            _queued.Remove(c);
            if (_chunkNodes.ContainsKey(c))
            {
                continue;
            }
            var chunk = _terrain.GetChunk(c);
            var node = TerrainMesh.Build(chunk, _terrain.Layout, _content);
            _chunkRoot.AddChild(node);
            _chunkNodes[c] = node;
        }
    }

    // ------------------------------------------------------------ entities

    public EntityView Spawn(EntityState state)
    {
        if (_entities.TryGetValue(state.Key, out var existing))
        {
            existing.Apply(state, snap: true);
            return existing;
        }
        var view = new EntityView(state, state.Key == SelfKey);
        _entityRoot.AddChild(view);
        _entities[state.Key] = view;
        view.Apply(state, snap: true);
        return view;
    }

    public void Update(EntityState state)
    {
        if (_entities.TryGetValue(state.Key, out var view))
        {
            view.Apply(state, snap: false);
        }
        else
        {
            Spawn(state);
        }
    }

    public void Despawn(long key)
    {
        if (_entities.Remove(key, out var view))
        {
            view.QueueFree();
        }
    }

    public void ClearEntities()
    {
        foreach (var v in _entities.Values)
        {
            v.QueueFree();
        }
        _entities.Clear();
    }

    public EntityView? Self => _entities.GetValueOrDefault(SelfKey);

    /// <summary>Ground height at a world position, or zero before the world exists.</summary>
    public float HeightAt(float x, float y) => _terrain?.HeightAt(x, y) ?? 0f;

    /// <summary>Marches a camera ray against the terrain height field and returns the ground point in simulation space.</summary>
    public System.Numerics.Vector3? RayToGround(Vector3 origin, Vector3 direction, float maxDistance = 400f)
    {
        if (_terrain is null)
        {
            return null;
        }
        var step = 0.5f;
        var last = origin;
        for (var d = 0f; d < maxDistance; d += step)
        {
            var p = origin + direction * d;
            var core = Coords.ToCore(p);
            var h = _terrain.HeightAt(core.X, core.Y);
            if (core.Z <= h)
            {
                // Refine between last and p.
                var lo = last;
                var hi = p;
                for (var i = 0; i < 6; i++)
                {
                    var mid = (lo + hi) * 0.5f;
                    var mc = Coords.ToCore(mid);
                    if (mc.Z <= _terrain.HeightAt(mc.X, mc.Y))
                    {
                        hi = mid;
                    }
                    else
                    {
                        lo = mid;
                    }
                }
                var hc = Coords.ToCore(hi);
                return new System.Numerics.Vector3(hc.X, hc.Y, _terrain.HeightAt(hc.X, hc.Y));
            }
            last = p;
        }
        return null;
    }

    /// <summary>The nearest entity view to a ray, if any lies within its radius of the ray.</summary>
    public EntityView? PickEntity(Vector3 origin, Vector3 direction, float maxDistance = 400f)
    {
        EntityView? best = null;
        var bestDist = float.MaxValue;
        foreach (var view in _entities.Values)
        {
            if (view.Key == SelfKey)
            {
                continue;
            }
            var center = view.GlobalPosition + new Vector3(0, view.State.Height * 0.5f, 0);
            var to = center - origin;
            var along = to.Dot(direction);
            if (along < 0 || along > maxDistance)
            {
                continue;
            }
            var closest = origin + direction * along;
            var dist = (center - closest).Length();
            var reach = Mathf.Max(view.State.Radius, 0.5f) + view.State.Height * 0.35f;
            if (dist <= reach && along < bestDist)
            {
                bestDist = along;
                best = view;
            }
        }
        return best;
    }
}

/// <summary>Chunks received from the server win; everything else is generated locally.</summary>
public sealed class ClientChunkProvider : IChunkProvider
{
    private readonly WorldGenerator _generator;
    private readonly Dictionary<ChunkCoord, ChunkData> _received;

    public ClientChunkProvider(WorldGenerator generator, Dictionary<ChunkCoord, ChunkData> received)
    {
        _generator = generator;
        _received = received;
    }

    public ChunkData Load(ChunkCoord coord) => _received.TryGetValue(coord, out var c) ? c : _generator.GenerateChunk(coord);
}
