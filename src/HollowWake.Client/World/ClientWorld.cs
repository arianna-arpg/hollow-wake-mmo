using Godot;
using HollowWake.Core.Content;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
using HollowWake.Protocol;

namespace HollowWake.Client.World;

/// <summary>
/// The client's copy of the land and everything on it. Unmodified chunks are generated locally from
/// the seed the server sent, with the same code the server runs; modified chunks arrive on the wire.
/// Generation runs on worker threads nearest chunk first, and meshes are built on the main thread once
/// a chunk and its neighbours exist so borders meet. Entities are views driven by server snapshots.
/// </summary>
public partial class ClientWorld : Node3D
{
    private readonly Dictionary<ChunkCoord, Node3D> _chunkNodes = new();
    private readonly Dictionary<ChunkCoord, ChunkData> _chunkData = new();
    private readonly Dictionary<ChunkCoord, ChunkData> _received = new();
    private readonly Dictionary<ChunkCoord, Task<ChunkData>> _inFlight = new();
    private readonly HashSet<ChunkCoord> _failed = new();
    private readonly List<ChunkCoord> _wantedData = new();
    private readonly List<ChunkCoord> _wantedMesh = new();
    private readonly Dictionary<long, EntityView> _entities = new();
    private readonly ContentRegistry _content;
    private WorldGenerator? _generator;
    private Terrain? _terrain;
    private Node3D _chunkRoot = null!;
    private Node3D _entityRoot = null!;
    private ChunkCoord? _lastCenter;

    private int _viewChunks = 3;
    private DirectionalLight3D? _sun;
    private Godot.Environment? _environment;

    /// <summary>Chunks kept built around the player in each direction (the draw distance option); a change replans at once.</summary>
    public int ViewChunks
    {
        get => _viewChunks;
        set
        {
            if (_viewChunks != value)
            {
                _viewChunks = Math.Clamp(value, 1, 12);
                _lastCenter = null;
            }
        }
    }

    /// <summary>The sun casts shadows (the shadows option).</summary>
    public void SetShadows(bool on)
    {
        if (_sun is not null)
        {
            _sun.ShadowEnabled = on;
        }
    }

    /// <summary>The haze toward the horizon (the distance haze option).</summary>
    public void SetFog(bool on)
    {
        if (_environment is not null)
        {
            _environment.FogEnabled = on;
        }
    }

    /// <summary>Shows or hides the labels of props (the names over props option).</summary>
    public void SetPropLabels(bool on)
    {
        EntityView.PropLabels = on;
        foreach (var view in _entities.Values)
        {
            view.RefreshLabel();
        }
    }

    /// <summary>Chunk generations running at once on worker threads.</summary>
    public int Workers { get; set; } = Math.Clamp(System.Environment.ProcessorCount / 2, 1, 4);

    /// <summary>Chunk meshes built per frame on the main thread.</summary>
    public int BuildsPerFrame { get; set; } = 2;

    public WorldGenerator? Generator => _generator;
    public Terrain? Terrain => _terrain;
    public IReadOnlyDictionary<long, EntityView> Entities => _entities;
    public long SelfKey { get; set; }

    /// <summary>Chunks in view that still have no mesh.</summary>
    public int PendingChunks => _wantedMesh.Count;

    /// <summary>Chunks with a mesh in the scene.</summary>
    public int BuiltChunks => _chunkNodes.Count;

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
        _sun = sun;
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.55f, 0.66f, 0.8f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.6f, 0.62f, 0.7f),
            AmbientLightEnergy = 0.55f,
            // A light haze toward the horizon; the isometric camera sits 120 units out, so anything denser whites out the ground.
            FogEnabled = true,
            FogLightColor = new Color(0.62f, 0.7f, 0.82f),
            FogDensity = 0.0007f,
        };
        AddChild(new WorldEnvironment { Name = "Environment", Environment = env });
        _environment = env;
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
        _terrain = new Terrain(layout, new ClientChunkProvider(_generator, _received, _chunkData));
        ClearChunks();
        _received.Clear();
        _lastCenter = null;
    }

    public void ReceiveChunk(ChunkDataMsg msg)
    {
        var coord = new ChunkCoord(msg.X, msg.Y);
        var chunk = new ChunkData(coord, msg.Size) { GenVersion = msg.GenVersion, Modified = true };
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
        // The wire carries what the simulation can change; biomes and the water level come from local generation.
        var local = _chunkData.GetValueOrDefault(coord) ?? _generator?.GenerateChunk(coord);
        if (local is not null)
        {
            chunk.BiomeTable.Clear();
            chunk.BiomeTable.AddRange(local.BiomeTable);
            Array.Copy(local.BiomeIndex, chunk.BiomeIndex, Math.Min(local.BiomeIndex.Length, chunk.BiomeIndex.Length));
            chunk.WaterLevel = local.WaterLevel;
        }
        _received[coord] = chunk;
        _chunkData[coord] = chunk;
        _terrain?.Unload(coord);
        // Rebuild this chunk and the neighbours whose edges blend into it.
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                var c = new ChunkCoord(coord.X + dx, coord.Y + dy);
                var built = _chunkNodes.Remove(c, out var node);
                if (built)
                {
                    node!.QueueFree();
                }
                if ((built || c == coord) && !_wantedMesh.Contains(c))
                {
                    _wantedMesh.Add(c);
                }
            }
        }
    }

    private void ClearChunks()
    {
        foreach (var n in _chunkNodes.Values)
        {
            n.QueueFree();
        }
        _chunkNodes.Clear();
        _chunkData.Clear();
        _inFlight.Clear();
        _failed.Clear();
        _wantedData.Clear();
        _wantedMesh.Clear();
        _terrain?.Clear();
    }

    /// <summary>Keeps chunks generated and built around a world position, a few per frame, nearest first.</summary>
    public void Stream(System.Numerics.Vector3 around)
    {
        if (_terrain is null || _generator is null)
        {
            return;
        }
        var center = _terrain.Layout.ChunkOf(around.X, around.Y);
        if (_lastCenter != center)
        {
            _lastCenter = center;
            Replan(center);
        }
        StartGeneration();
        CollectGenerated();
        BuildMeshes();
    }

    private void Replan(ChunkCoord center)
    {
        _wantedData.Clear();
        _wantedMesh.Clear();
        // Data is wanted one chunk further out than meshes, so every built chunk knows its neighbours.
        var reach = ViewChunks + 1;
        for (var dy = -reach; dy <= reach; dy++)
        {
            for (var dx = -reach; dx <= reach; dx++)
            {
                var c = new ChunkCoord(center.X + dx, center.Y + dy);
                if (!_chunkData.ContainsKey(c))
                {
                    _wantedData.Add(c);
                }
                if (Math.Abs(dx) <= ViewChunks && Math.Abs(dy) <= ViewChunks && !_chunkNodes.ContainsKey(c))
                {
                    _wantedMesh.Add(c);
                }
            }
        }
        int Dist(ChunkCoord c)
        {
            var dx = c.X - center.X;
            var dy = c.Y - center.Y;
            return dx * dx + dy * dy;
        }
        _wantedData.Sort((a, b) => Dist(a).CompareTo(Dist(b)));
        _wantedMesh.Sort((a, b) => Dist(a).CompareTo(Dist(b)));
        static int Ring(ChunkCoord c, ChunkCoord center) => Math.Max(Math.Abs(c.X - center.X), Math.Abs(c.Y - center.Y));
        foreach (var (c, node) in _chunkNodes.ToList())
        {
            if (Ring(c, center) > ViewChunks + 2)
            {
                node.QueueFree();
                _chunkNodes.Remove(c);
            }
        }
        foreach (var c in _chunkData.Keys.ToList())
        {
            if (Ring(c, center) > ViewChunks + 3)
            {
                _chunkData.Remove(c);
                _terrain!.Unload(c);
            }
        }
    }

    private void StartGeneration()
    {
        foreach (var c in _wantedData)
        {
            if (_inFlight.Count >= Workers)
            {
                return;
            }
            if (_chunkData.ContainsKey(c) || _inFlight.ContainsKey(c) || _failed.Contains(c))
            {
                continue;
            }
            if (_received.TryGetValue(c, out var received))
            {
                _chunkData[c] = received;
                continue;
            }
            var coord = c;
            var generator = _generator!;
            _inFlight[c] = Task.Run(() => generator.GenerateChunk(coord));
        }
    }

    private void CollectGenerated()
    {
        if (_inFlight.Count == 0)
        {
            return;
        }
        List<ChunkCoord>? done = null;
        foreach (var (c, task) in _inFlight)
        {
            if (task.IsCompleted)
            {
                (done ??= new List<ChunkCoord>()).Add(c);
            }
        }
        if (done is null)
        {
            return;
        }
        foreach (var c in done)
        {
            var task = _inFlight[c];
            _inFlight.Remove(c);
            _wantedData.Remove(c);
            if (task.IsCompletedSuccessfully)
            {
                // A chunk the server sent meanwhile wins over the local generation.
                if (!_received.ContainsKey(c))
                {
                    _chunkData[c] = task.Result;
                }
            }
            else
            {
                _failed.Add(c);
                GD.PushError($"chunk {c.X},{c.Y} failed to generate: {task.Exception?.GetBaseException().Message}");
            }
        }
    }

    private void BuildMeshes()
    {
        var budget = BuildsPerFrame;
        for (var i = 0; i < _wantedMesh.Count && budget > 0;)
        {
            var c = _wantedMesh[i];
            if (_chunkNodes.ContainsKey(c))
            {
                _wantedMesh.RemoveAt(i);
                continue;
            }
            if (!_chunkData.ContainsKey(c) || !NeighboursReady(c))
            {
                i++;
                continue;
            }
            var chunk = _terrain!.GetChunk(c);
            var node = TerrainMesh.Build(chunk, _terrain.Layout, _content, Lookup);
            _chunkRoot.AddChild(node);
            _chunkNodes[c] = node;
            _wantedMesh.RemoveAt(i);
            budget--;
        }
    }

    private bool NeighboursReady(ChunkCoord c)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                var n = new ChunkCoord(c.X + dx, c.Y + dy);
                if (!_chunkData.ContainsKey(n) && !_failed.Contains(n))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private ChunkData? Lookup(ChunkCoord c) => _chunkData.GetValueOrDefault(c);

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

    /// <summary>
    /// Applies the fields a delta carries to a known entity. A delta for an entity this client does not know
    /// is ignored: the server's next refresh brings its full state.
    /// </summary>
    public void ApplyDelta(EntityDelta delta)
    {
        if (_entities.TryGetValue(delta.Key, out var view))
        {
            view.Apply(delta.ApplyTo(view.State), snap: false);
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

    /// <summary>
    /// Marches a camera ray against the terrain height field and returns the ground point in simulation
    /// space. Only built chunks count: nothing else is visible, and sampling elsewhere would generate
    /// chunks on the main thread.
    /// </summary>
    public System.Numerics.Vector3? RayToGround(Vector3 origin, Vector3 direction, float maxDistance = 400f)
    {
        if (_terrain is null)
        {
            return null;
        }
        var terrain = _terrain;
        bool Below(Vector3 point)
        {
            var core = Coords.ToCore(point);
            return _chunkNodes.ContainsKey(terrain.Layout.ChunkOf(core.X, core.Y)) && core.Z <= terrain.HeightAt(core.X, core.Y);
        }
        var step = 0.5f;
        var last = origin;
        for (var d = 0f; d < maxDistance; d += step)
        {
            var p = origin + direction * d;
            if (Below(p))
            {
                // Refine between last and p.
                var lo = last;
                var hi = p;
                for (var i = 0; i < 6; i++)
                {
                    var mid = (lo + hi) * 0.5f;
                    if (Below(mid))
                    {
                        hi = mid;
                    }
                    else
                    {
                        lo = mid;
                    }
                }
                var hc = Coords.ToCore(hi);
                return new System.Numerics.Vector3(hc.X, hc.Y, terrain.HeightAt(hc.X, hc.Y));
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

/// <summary>Chunks received from the server win, then chunks generated ahead of time; anything else is generated on the spot.</summary>
public sealed class ClientChunkProvider : IChunkProvider
{
    private readonly WorldGenerator _generator;
    private readonly Dictionary<ChunkCoord, ChunkData> _received;
    private readonly Dictionary<ChunkCoord, ChunkData> _generated;

    public ClientChunkProvider(WorldGenerator generator, Dictionary<ChunkCoord, ChunkData> received, Dictionary<ChunkCoord, ChunkData> generated)
    {
        _generator = generator;
        _received = received;
        _generated = generated;
    }

    public ChunkData Load(ChunkCoord coord)
    {
        if (_received.TryGetValue(coord, out var received))
        {
            return received;
        }
        if (_generated.TryGetValue(coord, out var generated))
        {
            return generated;
        }
        var chunk = _generator.GenerateChunk(coord);
        _generated[coord] = chunk;
        return chunk;
    }
}
