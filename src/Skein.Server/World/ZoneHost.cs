using System.Numerics;
using Microsoft.Extensions.Logging;
using Skein.Core.Characters;
using Skein.Core.Components;
using Skein.Core.Content;
using Skein.Core.Definitions;
using Skein.Core.Maths;
using Skein.Core.Sim;
using Skein.Core.Territory;
using Skein.Core.World;
using Skein.Core.WorldGen;
using Skein.Protocol;
using Skein.Server.Config;
using Skein.Server.Persistence;
using Skein.Server.Sessions;

namespace Skein.Server.World;

/// <summary>
/// Owns every zone instance in this process, the shared generator and world state, player entry,
/// zone transfers, world events and saving. One process hosts many zones; a later gateway can
/// spread zones across processes behind the same host interface.
/// </summary>
public sealed class ZoneHost
{
    private readonly ServerConfig _config;
    private readonly ILoggerFactory _logs;
    private readonly ILogger _log;
    private readonly Dictionary<ZoneCoord, ZoneInstance> _zones = new();
    private readonly IWorldStore _worldStore;
    private readonly ICharacterStore _characters;
    private readonly PersistentChunkProvider _chunks;
    private double _nextEventAt;
    private double _elapsed;

    public ContentRegistry Content { get; }
    public WorldGenerator Generator { get; }
    public WorldState World { get; }
    public IReadOnlyDictionary<ZoneCoord, ZoneInstance> Zones => _zones;
    public double Elapsed => _elapsed;

    public ZoneHost(ServerConfig config, ContentRegistry content, ILoggerFactory logs, IWorldStore worldStore, ICharacterStore characters)
    {
        _config = config;
        _logs = logs;
        _log = logs.CreateLogger<ZoneHost>();
        _worldStore = worldStore;
        _characters = characters;
        Content = content;
        var seedText = config.Seed ?? content.Rules.Text("world.seed", "aureth");
        var overlays = new GenerationOverlays();
        Generator = new WorldGenerator(content, Rng.Hash64(seedText), overlays);
        World = new WorldState(overlays);
        _chunks = new PersistentChunkProvider(Generator, worldStore);
        Restore();
        _nextEventAt = config.WorldEventIntervalSeconds;
        _log.LogInformation("world seed '{Seed}' ({Hash}); start zone {Zone}", seedText, Generator.Seed, Generator.StartZone());
    }

    // ------------------------------------------------------------ zones

    public ZoneInstance GetOrCreate(ZoneCoord coord)
    {
        if (_zones.TryGetValue(coord, out var zone))
        {
            return zone;
        }
        var log = new LoggerSimLog(_logs.CreateLogger("zone." + coord.Key));
        var sim = ZoneBuilder.CreateZone(Content, Generator, World, coord, log, _config.TickRate, _chunks);
        zone = new ZoneInstance(coord, sim, _logs.CreateLogger<ZoneInstance>(), _config.SnapshotRate, _config.InterestRadius);
        zone.TransferRequested += (session, at) => Transfer(session, at);
        _zones[coord] = zone;
        _log.LogInformation("zone {Zone} '{Name}' loaded: {Report}", coord, zone.Info.Name, zone.LoadReport);
        return zone;
    }

    public void TickAll(double dt)
    {
        _elapsed += dt;
        World.WorldTime += dt;
        foreach (var zone in _zones.Values.ToArray())
        {
            zone.Tick();
        }
        if (_config.WorldEvents && _elapsed >= _nextEventAt)
        {
            _nextEventAt = _elapsed + _config.WorldEventIntervalSeconds;
            ScheduleRandomEvent();
        }
        ExpireEvents();
        UnloadIdleZones();
    }

    private void UnloadIdleZones()
    {
        foreach (var (coord, zone) in _zones.ToArray())
        {
            if (zone.Sessions.Count > 0 || zone.Sim.Clock.Time - zone.LastPopulatedAt < _config.ZoneUnloadSeconds)
            {
                continue;
            }
            if (coord == Generator.StartZone())
            {
                continue;
            }
            SaveChunks(zone);
            _zones.Remove(coord);
            _log.LogInformation("zone {Zone} unloaded after idling", coord);
        }
    }

    // ------------------------------------------------------------ players

    public void Enter(Session session, CharacterSheet sheet)
    {
        var position = sheet.Position;
        var coord = Generator.Layout.ZoneOf(position.X, position.Y);
        if (sheet.X == 0 && sheet.Y == 0 && sheet.Z == 0)
        {
            coord = Generator.StartZone();
            var (sx, sy) = Generator.StartPoint(coord);
            position = new Vector3((float)sx, (float)sy, 0);
            sheet.Position = position;
        }
        var zone = GetOrCreate(coord);
        session.Character = sheet;
        var factory = zone.Sim.Service<CharacterFactory>();
        session.Entity = factory.Materialize(zone.Sim, sheet);
        zone.Add(session);
        session.State = SessionState.InWorld;
        var t = zone.Sim.World.Get<Transform>(session.Entity)!;
        session.Send(new EnterWorldMsg(sheet.Id, session.Entity.Key, zone.Key, t.Position.X, t.Position.Y, t.Position.Z, WorldInfo(), zone.Sim.Clock.Time, _config.TickRate));
        session.Send(new ZoneChangedMsg(zone.Key, zone.Info.Name, zone.Info.Level, zone.Info.Safe));
        foreach (var chunk in zone.ModifiedChunks())
        {
            session.Send(Snapshots.Chunk(chunk));
        }
        _log.LogInformation("{Session} entered {Zone} at {Pos}", session, zone.Coord, t.Position);
    }

    public void Leave(Session session, bool save = true)
    {
        var zone = session.Zone;
        if (zone is null)
        {
            return;
        }
        if (save && session.Character is not null && !session.Entity.IsNone)
        {
            zone.Sim.Service<CharacterFactory>().Capture(zone.Sim, session.Entity, session.Character);
            _characters.Save(session.Character);
        }
        zone.Remove(session);
        if (session.State == SessionState.InWorld)
        {
            session.State = SessionState.Authenticated;
        }
    }

    /// <summary>Moves a player to the zone containing a point, carrying their entity state across.</summary>
    public void Transfer(Session session, Vector3 to)
    {
        var from = session.Zone;
        if (from is null || session.Character is null)
        {
            return;
        }
        from.Sim.Service<CharacterFactory>().Capture(from.Sim, session.Entity, session.Character);
        session.Character.Position = to;
        var target = Generator.Layout.ZoneOf(to.X, to.Y);
        if (target == from.Coord)
        {
            // Travel inside the same zone: just move the body.
            if (from.Sim.World.TryGet<Transform>(session.Entity, out var t))
            {
                t.Position = from.Sim.Terrain.SnapToGround(to, 4);
                from.Sim.Spatial.Update(session.Entity, t.Position);
                from.Sim.World.Get<Motion>(session.Entity)?.Stop();
            }
            return;
        }
        from.Remove(session);
        var zone = GetOrCreate(target);
        session.Entity = zone.Sim.Service<CharacterFactory>().Materialize(zone.Sim, session.Character);
        foreach (var key in session.Known.ToList())
        {
            session.Send(new EntityDespawnMsg(key));
        }
        zone.Add(session);
        var pos = zone.Sim.World.Get<Transform>(session.Entity)!.Position;
        session.Send(new EnterWorldMsg(session.Character.Id, session.Entity.Key, zone.Key, pos.X, pos.Y, pos.Z, WorldInfo(), zone.Sim.Clock.Time, _config.TickRate));
        session.Send(new ZoneChangedMsg(zone.Key, zone.Info.Name, zone.Info.Level, zone.Info.Safe));
        foreach (var chunk in zone.ModifiedChunks())
        {
            session.Send(Snapshots.Chunk(chunk));
        }
        _log.LogInformation("{Session} crossed from {From} to {To}", session, from.Coord, zone.Coord);
    }

    public WorldInfo WorldInfo()
    {
        var overlays = World.Overlays.All.Select(o => new OverlayInfo(o.Id, o.Global,
            o.Regions.Select(r => new[] { r.X, r.Y }).ToList(),
            o.Zones.Select(z => new[] { z.X, z.Y }).ToList(),
            o.Circle is { } c ? new[] { c.X, c.Y, c.Radius } : null,
            new Dictionary<string, double>(o.Params), o.Priority)).ToList();
        return new WorldInfo(Generator.Seed, Generator.Layout.ChunkSize, Generator.Layout.ZoneChunks, Generator.Layout.RegionZones, overlays, ContentHash());
    }

    private string? _contentHash;

    public string ContentHash()
    {
        if (_contentHash is not null)
        {
            return _contentHash;
        }
        var h = Rng.Hash64(string.Join("|", Content.KindNames.OrderBy(k => k).Select(k => k + ":" + Content.CountOfKind(k))));
        _contentHash = h.ToString("x16");
        return _contentHash;
    }

    // ------------------------------------------------------------ world events

    private void ScheduleRandomEvent()
    {
        var defs = Content.Each<WorldEventDef>().ToList();
        if (defs.Count == 0 || World.Events.Count >= _config.MaxActiveWorldEvents)
        {
            return;
        }
        var rng = new Rng(Rng.Combine(Generator.Seed, (ulong)_elapsed));
        var regions = _zones.Values.Select(z => Generator.Layout.RegionOf(z.Coord)).Distinct().ToList();
        if (regions.Count == 0)
        {
            regions.Add(Generator.Layout.RegionOf(Generator.StartZone()));
        }
        var region = rng.Pick(regions);
        var idx = rng.PickWeighted(defs.Select(d => d.Weight).ToList());
        if (idx < 0)
        {
            return;
        }
        StartEvent(defs[idx], region, rng);
    }

    public ActiveWorldEvent StartEvent(WorldEventDef def, RegionCoord region, Rng rng)
    {
        var duration = def.Duration?.Evaluate(new Core.Expressions.DictScope(rng)) ?? 900;
        var evt = new ActiveWorldEvent { Id = $"{def.Id}@{region.Key}@{(long)_elapsed}", Definition = def.Id, StartedAt = _elapsed, EndsAt = _elapsed + duration };
        evt.Regions.Add(region);
        World.Events.Add(evt);
        if (def.Generation.Count > 0)
        {
            World.Overlays.Add(new GenerationOverlay { Id = "event:" + evt.Id, Regions = { region }, Params = new Dictionary<string, double>(def.Generation), Priority = 5 });
        }
        var regionInfo = Generator.Region(region);
        foreach (var zone in _zones.Values)
        {
            zone.BroadcastAll(new NoticeMsg($"{def.DisplayName} begins over {regionInfo.Name}.", "event"));
        }
        _log.LogInformation("world event {Event} started over {Region} for {Duration}s", def.Id, region, duration);
        return evt;
    }

    private void ExpireEvents()
    {
        foreach (var evt in World.Events.ToArray())
        {
            if (_elapsed < evt.EndsAt)
            {
                continue;
            }
            World.Events.Remove(evt);
            World.Overlays.Remove("event:" + evt.Id);
            foreach (var zone in _zones.Values)
            {
                zone.BroadcastAll(new NoticeMsg($"{Content.Get<WorldEventDef>(evt.Definition)?.DisplayName ?? evt.Definition} has passed.", "event"));
            }
        }
    }

    // ------------------------------------------------------------ persistence

    private void Restore()
    {
        var save = _worldStore.Load();
        if (save is null)
        {
            return;
        }
        foreach (var (id, h) in save.Holdings)
        {
            var holding = new Holding { Id = id, Name = h.Name, OwnerCharacterId = h.Owner, Faction = h.Faction, CreatedAt = h.CreatedAt, Influence = h.Influence };
            foreach (var f in h.Founts)
            {
                holding.Founts.Add(f);
            }
            foreach (var d in h.Decrees)
            {
                holding.Decrees.Add(d);
            }
            holding.Structures.AddRange(h.Structures);
            foreach (var (c, v) in h.Treasury)
            {
                holding.Treasury[c] = v;
            }
            World.Holdings[id] = holding;
        }
        foreach (var e in save.Events)
        {
            var evt = new ActiveWorldEvent { Id = e.Id, Definition = e.Definition, StartedAt = 0, EndsAt = e.EndsAt };
            foreach (var r in e.Regions)
            {
                evt.Regions.Add(new RegionCoord(r[0], r[1]));
            }
            World.Events.Add(evt);
            if (Content.Get<WorldEventDef>(e.Definition) is { } def && def.Generation.Count > 0)
            {
                World.Overlays.Add(new GenerationOverlay { Id = "event:" + evt.Id, Regions = new HashSet<RegionCoord>(evt.Regions), Params = new Dictionary<string, double>(def.Generation), Priority = 5 });
            }
        }
        World.WorldTime = save.WorldTime;
        _elapsed = save.WorldTime;
        ItemUidSeed = save.NextItemUid;
    }

    public long ItemUidSeed { get; private set; } = 1;

    public void SaveAll()
    {
        var save = new WorldSave { WorldTime = World.WorldTime, NextItemUid = NextItemUid() };
        foreach (var (id, h) in World.Holdings)
        {
            save.Holdings[id] = new HoldingSave
            {
                Id = id,
                Name = h.Name,
                Owner = h.OwnerCharacterId,
                Faction = h.Faction,
                Founts = h.Founts.ToList(),
                Decrees = h.Decrees.ToList(),
                Structures = h.Structures.ToList(),
                Treasury = new Dictionary<string, double>(h.Treasury),
                Influence = h.Influence,
                CreatedAt = h.CreatedAt,
            };
        }
        foreach (var e in World.Events)
        {
            save.Events.Add(new EventSave { Id = e.Id, Definition = e.Definition, EndsAt = e.EndsAt, Regions = e.Regions.Select(r => new[] { r.X, r.Y }).ToList() });
        }
        _worldStore.Save(save);
        foreach (var zone in _zones.Values)
        {
            SaveChunks(zone);
            foreach (var session in zone.Sessions)
            {
                if (session.Character is not null && !session.Entity.IsNone && zone.Sim.World.IsAlive(session.Entity))
                {
                    zone.Sim.Service<CharacterFactory>().Capture(zone.Sim, session.Entity, session.Character);
                    _characters.Save(session.Character);
                }
            }
        }
    }

    private long NextItemUid()
    {
        long max = ItemUidSeed;
        foreach (var zone in _zones.Values)
        {
            max = Math.Max(max, zone.Sim.Service<Core.Items.ItemGenerator>().NextUid());
        }
        return max;
    }

    private void SaveChunks(ZoneInstance zone)
    {
        foreach (var chunk in zone.ModifiedChunks())
        {
            _worldStore.SaveChunk(chunk);
        }
    }
}

/// <summary>Chunks come from saved modifications when they exist, otherwise from the generator.</summary>
public sealed class PersistentChunkProvider : IChunkProvider
{
    private readonly WorldGenerator _generator;
    private readonly IWorldStore _store;

    public PersistentChunkProvider(WorldGenerator generator, IWorldStore store)
    {
        _generator = generator;
        _store = store;
    }

    public ChunkData Load(ChunkCoord coord) => _store.LoadChunk(coord) ?? _generator.GenerateChunk(coord);
}

/// <summary>Routes simulation diagnostics into the server's logger.</summary>
public sealed class LoggerSimLog : ISimLog
{
    private readonly ILogger _log;

    public LoggerSimLog(ILogger log)
    {
        _log = log;
    }

    public void Info(string message) => _log.LogInformation("{Message}", message);
    public void Warn(string message) => _log.LogWarning("{Message}", message);
    public void Error(string message, Exception? exception = null) => _log.LogError(exception, "{Message}", message);
}
