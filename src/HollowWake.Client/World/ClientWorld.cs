using Godot;
using HollowWake.Client.World.Appearance;
using HollowWake.Client.World.Logic;
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
    private AppearanceService? _appearance;
    private CardBaker? _baker;
    private DayCycle? _day;
    private SurfaceLook? _surfaces;
    private bool _shadows = true;
    private double _serverNow;
    private bool _hasServerNow;

    /// <summary>The appearance pipeline the views draw with, and the measurements the smoke reports.</summary>
    public AppearanceService? Appearance => _appearance;

    /// <summary>The surfaces content the terrain is coloured with.</summary>
    public SurfaceLook Surfaces => _surfaces ??= new SurfaceLook(_appearance?.Catalog.Surfaces ?? new HollowWake.Core.Definitions.SurfacesDef { Id = "default" });

    /// <summary>Triangles the drawn bodies hold, and how many are cards, for the measurement line.</summary>
    public (int Triangles, int Cards, int Bodies) BodyCounts
    {
        get
        {
            var triangles = 0;
            var cards = 0;
            var bodies = 0;
            foreach (var view in _entities.Values)
            {
                triangles += view.Triangles;
                if (view.Triangles > 0)
                {
                    bodies++;
                }
                if (view.IsCard)
                {
                    cards++;
                }
            }
            return (triangles, cards, bodies);
        }
    }

    /// <summary>The offscreen baker of card atlases, a child of the world.</summary>
    public CardBaker Baker => _baker ?? throw new InvalidOperationException("the world is not in the tree yet");

    /// <summary>Gives the views the appearance content and the card baker; called once the content is loaded.</summary>
    public void UseAppearance(AppearanceService service)
    {
        _appearance = service;
        _surfaces = new SurfaceLook(service.Catalog.Surfaces);
        TerrainMesh.ConfigureWater(_surfaces);
        _day = new DayCycle(service.Catalog.World);
        EntityView.Appearance = service;
        EntityView.PropLabelReach = service.Motion.Labels("prop_reach", 7);
        EntityView.HeroPosition = () => Self?.GlobalPosition;
        service.Shadows = _shadows;
    }

    /// <summary>What the camera is doing this frame, for the cards and the representation choice.</summary>
    public void ObserveCamera(bool isometric, float orthoSize, float yaw)
    {
        if (_appearance is null)
        {
            return;
        }
        _appearance.Isometric = isometric;
        _appearance.OrthoSize = orthoSize;
        _appearance.CameraYaw = yaw;
        CardNode.CameraYaw = yaw;
    }

    /// <summary>The server's clock, for the day cycle.</summary>
    public void ObserveServerTime(double serverNow)
    {
        _serverNow = serverNow;
        _hasServerNow = true;
    }

    /// <summary>
    /// The world's day as a message reported it (docs/design/networking-and-persistence.md): enter_world carries the
    /// day's length and the fraction, the self state the fraction alone; the server time is the client's estimate
    /// of the server clock as the message arrived. Entering a world forgets the day a previous server said, so a
    /// server that predates the fields leaves the clock folding. Needs the appearance content, given at startup.
    /// </summary>
    public void ObserveDay(double? dayLengthSeconds, double? timeOfDay, double atServerTime, bool entering = false)
    {
        if (_day is null)
        {
            return;
        }
        if (entering)
        {
            _day.ForgetServer();
        }
        _day.ObserveServer(dayLengthSeconds, timeOfDay, atServerTime);
    }

    /// <summary>Whether the sky follows the day the server reported rather than the folded zone clock, for the smoke's evidence.</summary>
    public bool HasServerDay => _day?.Server is not null;

    private float? _dayOverride;

    /// <summary>A held fraction of the day for previews (the developer options), or null for the server's clock.</summary>
    public float? DayOverride
    {
        get => _dayOverride;
        set
        {
            _dayOverride = value;
            _lastDayFraction = -1;
        }
    }

    /// <summary>The fraction of the day the sky shows now, for the smoke and the overlay.</summary>
    public float TimeOfDay => _dayOverride ?? (_day is null || !_hasServerNow ? 0.5f : _day.TimeOfDay(_serverNow));

    /// <summary>A blow landed: the struck body flinches and flashes, the striker's swing comes through.</summary>
    public void CombatHit(CombatEventMsg c)
    {
        if (c.Kind == "damage" && _entities.TryGetValue(c.Target, out var target))
        {
            target.Hit(c.Amount, _entities.TryGetValue(c.Source, out var source) ? source.GlobalPosition : null);
        }
        if (c.Kind is "damage" or "evade" or "block" && _entities.TryGetValue(c.Source, out var striker))
        {
            striker.Struck();
        }
    }

    /// <summary>The self's worn gear changed: its body shows it.</summary>
    public void RefreshSelfEquipment() => Self?.RefreshEquipment();

    /// <summary>The sound cue hooks, raised by the bodies' moments; no asset plays until one is registered.</summary>
    public AudioCues Cues => _cues ?? throw new InvalidOperationException("the world is not in the tree yet");
    private AudioCues? _cues;

    /// <summary>The first surface tag of the cell under a Godot point (grass, sand, stone ...), or null off the built world.</summary>
    public string? SurfaceAt(Vector3 godotPoint)
    {
        if (_terrain is null)
        {
            return null;
        }
        var core = Coords.ToCore(godotPoint);
        var coord = _terrain.Layout.ChunkOf(core.X, core.Y);
        if (!_chunkData.TryGetValue(coord, out var chunk))
        {
            return null;
        }
        var origin = _terrain.Layout.ChunkOrigin(coord);
        var lx = (int)MathF.Floor(core.X) - origin.X;
        var ly = (int)MathF.Floor(core.Y) - origin.Y;
        if (lx < 0 || ly < 0 || lx >= chunk.Size || ly >= chunk.Size)
        {
            return null;
        }
        var index = chunk.Index(lx, ly);
        if ((chunk.Flags[index] & CellFlags.Water) != 0)
        {
            return "water";
        }
        var tags = chunk.TagsAt(index);
        return tags.Items.Count > 0 ? tags.Items[0] : null;
    }

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

    private float _foliageDensity = 1f;

    /// <summary>How many tufts the chunks grow, as a multiplier (the foliage density option); a change rebuilds the chunks in view.</summary>
    public float FoliageDensity
    {
        get => _foliageDensity;
        set
        {
            var clamped = Math.Clamp(value, 0f, 3f);
            if (MathF.Abs(clamped - _foliageDensity) < 0.001f)
            {
                return;
            }
            _foliageDensity = clamped;
            foreach (var (coord, node) in _chunkNodes.ToList())
            {
                node.QueueFree();
                _chunkNodes.Remove(coord);
            }
            _lastCenter = null;
        }
    }

    /// <summary>The sun casts shadows (the shadows option).</summary>
    public void SetShadows(bool on)
    {
        _shadows = on;
        if (_sun is not null)
        {
            _sun.ShadowEnabled = on;
        }
        if (_appearance is not null)
        {
            _appearance.Shadows = on;
        }
        foreach (var view in _entities.Values)
        {
            view.SetShadows(on);
        }
    }

    /// <summary>The haze toward the horizon (the distance haze option).</summary>
    public void SetFog(bool on)
    {
        _fogOn = on;
        _lastDayFraction = -1;
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

    public (int Completed, int Total) Readiness(double x, double y, int radius)
    {
        var total = (radius * 2 + 1) * (radius * 2 + 1);
        if (_terrain is null) return (0, total);
        var center = _terrain.Layout.ChunkOf(x, y);
        var completed = 0;
        for (var dy = -radius; dy <= radius; dy++)
        for (var dx = -radius; dx <= radius; dx++)
            if (_chunkNodes.ContainsKey(new ChunkCoord(center.X + dx, center.Y + dy))) completed++;
        return (completed, total);
    }

    public void RetryPreparation()
    {
        _failed.Clear();
        _lastCenter = null;
    }

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
        _baker = new CardBaker { Name = "CardBaker" };
        AddChild(_baker);
        _cues = new AudioCues();
        AddChild(_cues);
        EntityView.Cue = (cue, variant, at) => _cues.Play(cue, variant, at);
        EntityView.SurfaceAt = SurfaceAt;
        // One shadow split reaches the isometric camera's whole view at a fixed height and costs one pass over the casters.
        var sun = new DirectionalLight3D { Name = "Sun", LightEnergy = 1.2f, ShadowEnabled = true, DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal, DirectionalShadowMaxDistance = 110f, ShadowBias = 0.06f };
        sun.RotationDegrees = new Vector3(-55, 35, 0);
        AddChild(sun);
        _sun = sun;
        _sky = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(0.45f, 0.58f, 0.78f),
            SkyHorizonColor = new Color(0.75f, 0.8f, 0.88f),
            GroundBottomColor = new Color(0.2f, 0.18f, 0.16f),
            GroundHorizonColor = new Color(0.6f, 0.58f, 0.54f),
            SunAngleMax = 12f,
            SunCurve = 0.1f,
        };
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = _sky },
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.6f, 0.62f, 0.7f),
            AmbientLightEnergy = 0.55f,
            // A light haze toward the horizon; the isometric camera sits 120 units out, so anything denser whites out the ground.
            FogEnabled = true,
            FogLightColor = new Color(0.62f, 0.7f, 0.82f),
            FogDensity = 0.0007f,
            FogSkyAffect = 0.35f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            TonemapWhite = 4f,
        };
        AddChild(new WorldEnvironment { Name = "Environment", Environment = env });
        _environment = env;
    }

    private ProceduralSkyMaterial? _sky;
    private float _lastDayFraction = -1;

    public override void _Process(double delta)
    {
        if (_day is null || (!_hasServerNow && _dayOverride is null) || _environment is null || _sun is null || _sky is null)
        {
            return;
        }
        // the sky, sun and haze follow the server's clock through the content's day
        var fraction = _dayOverride ?? _day.TimeOfDay(_serverNow);
        if (MathF.Abs(fraction - _lastDayFraction) < 0.0005f)
        {
            return;
        }
        _lastDayFraction = fraction;
        var sky = _day.At(fraction);
        static Color C(HollowWake.Client.UI.Logic.Rgba c) => new(c.R, c.G, c.B);
        _sun.LightColor = C(sky.Sun);
        _sun.LightEnergy = sky.SunEnergy;
        _sun.Visible = sky.SunPitch > 0.5f;
        // the sun stands at its height over the bearing the day gives it: a Godot rotation about x is the pitch, about y the bearing
        _sun.RotationDegrees = new Vector3(-MathF.Max(2f, sky.SunPitch), sky.SunYaw, 0);
        _environment.AmbientLightColor = C(sky.Ambient);
        _environment.AmbientLightEnergy = sky.AmbientEnergy;
        _environment.FogLightColor = C(sky.Fog);
        _environment.FogDensity = _fogOn ? sky.FogDensity : 0f;
        _sky.SkyTopColor = C(sky.Sky);
        _sky.SkyHorizonColor = C(sky.Horizon);
        _sky.GroundHorizonColor = C(sky.Horizon).Darkened(0.2f);
        _sky.GroundBottomColor = C(sky.Fog).Darkened(0.6f);
    }

    private bool _fogOn = true;

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
            // Register already-generated neighbours too: pure cursor sampling needs their border
            // heights even before their meshes are built. Never request a missing/failed neighbour.
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var neighbour = new ChunkCoord(c.X + dx, c.Y + dy);
                if (_chunkData.ContainsKey(neighbour)) _terrain!.GetChunk(neighbour);
            }
            var chunk = _terrain!.GetChunk(c);
            var node = TerrainMesh.Build(chunk, _terrain.Layout, Surfaces, Lookup);
            if (_appearance is not null && Foliage.Build(chunk, _terrain.Layout, _appearance.Catalog.World.Foliage.Tufts, _foliageDensity) is { } tufts)
            {
                node.AddChild(tufts);
            }
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
            // A telegraph's strike landed: its mark fades out and frees itself.
            if (view.IsTelegraph)
            {
                view.Dying();
            }
            else
            {
                view.QueueFree();
            }
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

    /// <summary>First visible base or walkable layer hit, using loaded data without generation.</summary>
    public System.Numerics.Vector3? RayToGround(Vector3 origin, Vector3 direction, float maxDistance = 400f) =>
        _terrain is null ? null : Logic.GroundRay.Pick(_terrain, Coords.ToCore(origin), Coords.ToCore(direction),
            coord => _chunkNodes.ContainsKey(coord), maxDistance);
    /// <summary>
    /// The entity whose drawn body a ray passes through, ignoring the player's own. Creatures and interactables win
    /// over plain props, so a canopy in front of a hob does not take the hover from it; among overlapping bodies the
    /// one the ray passes closest to the middle of wins, nearer ones on a tie. The browser client's EntityLayer.pick.
    /// </summary>
    public EntityView? PickEntity(Vector3 origin, Vector3 direction, float maxDistance = 400f)
    {
        EntityView? best = null;
        var bestScore = float.MaxValue;
        EntityView? bestProp = null;
        var bestPropScore = float.MaxValue;
        foreach (var view in _entities.Values)
        {
            if (view.Key == SelfKey || view.PickSize is not { } size)
            {
                continue;
            }
            var center = view.GlobalPosition + new Vector3(0, size.Height * 0.5f, 0);
            var along = (center - origin).Dot(direction);
            if (along < 0 || along > maxDistance)
            {
                continue;
            }
            var off = (center - (origin + direction * along)).Length();
            var reach = Mathf.Max(size.Radius, 0.5f) + size.Height * 0.35f;
            if (off > reach)
            {
                continue;
            }
            var score = off / reach + along * 1e-4f;
            if (view.State.Resources.Count > 0 || view.State.Interactable is not null)
            {
                if (score < bestScore)
                {
                    bestScore = score;
                    best = view;
                }
            }
            else if (score < bestPropScore)
            {
                bestPropScore = score;
                bestProp = view;
            }
        }
        return best ?? bestProp;
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
