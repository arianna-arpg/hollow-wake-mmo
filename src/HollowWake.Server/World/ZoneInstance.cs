using System.Numerics;
using Microsoft.Extensions.Logging;
using HollowWake.Core.Components;
using HollowWake.Core.Crafting;
using HollowWake.Core.Ecs;
using HollowWake.Core.Interaction;
using HollowWake.Core.Items;
using HollowWake.Core.Sim;
using HollowWake.Core.Systems;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
using HollowWake.Protocol;
using HollowWake.Server.Sessions;

namespace HollowWake.Server.World;

/// <summary>
/// One running zone: its simulation, the sessions inside it, and the translation of simulation
/// events into messages for the players who should hear about them. Interest is a radius around
/// each player; entities enter and leave a session's known set as they come and go.
/// </summary>
public sealed class ZoneInstance
{
    private readonly ILogger _log;
    private readonly List<GameEvent> _pending = new();
    private readonly double _snapshotInterval;
    private readonly float _interestRadius;

    public ZoneCoord Coord { get; }
    public Simulation Sim { get; }
    public ZoneInfo Info { get; }
    public List<Session> Sessions { get; } = new();
    public double LastPopulatedAt { get; private set; }
    public ZoneLoadReport LoadReport { get; }

    /// <summary>Raised when a player walks out of the zone or asks to travel; the host moves them.</summary>
    public event Action<Session, Vector3>? TransferRequested;

    public ZoneInstance(ZoneCoord coord, Simulation sim, ILogger log, int snapshotRate, double interestRadius)
    {
        Coord = coord;
        Sim = sim;
        _log = log;
        Info = sim.Generator!.Zone(coord);
        _snapshotInterval = 1.0 / Math.Max(1, snapshotRate);
        _interestRadius = (float)interestRadius;
        LoadReport = ZoneBuilder.LoadZone(sim);
        LastPopulatedAt = sim.Clock.Time;
        sim.Events.SubscribeAll(e => _pending.Add(e));
    }

    public string Key => Coord.Key;

    public bool Contains(Vector3 position) => Sim.Layout.ZoneOf(position.X, position.Y) == Coord;

    public void Tick()
    {
        Sim.Tick();
        if (Sessions.Count > 0)
        {
            LastPopulatedAt = Sim.Clock.Time;
        }
        DispatchEvents();
        foreach (var session in Sessions.ToArray())
        {
            if (session.Entity.IsNone || !Sim.World.IsAlive(session.Entity))
            {
                continue;
            }
            var t = Sim.World.Get<Transform>(session.Entity);
            if (t is not null && !Contains(t.Position))
            {
                TransferRequested?.Invoke(session, t.Position);
                continue;
            }
            if (Sim.Clock.Time - session.LastSnapshotAt >= _snapshotInterval)
            {
                session.LastSnapshotAt = Sim.Clock.Time;
                SendSnapshot(session);
            }
            if (Sim.Clock.Time - session.LastSelfAt >= 0.5)
            {
                session.LastSelfAt = Sim.Clock.Time;
                session.Send(Snapshots.Self(Sim, session.Entity));
            }
        }
    }

    private void SendSnapshot(Session session)
    {
        if (!Sim.World.TryGet<Transform>(session.Entity, out var me))
        {
            return;
        }
        var visible = new HashSet<long>();
        var states = new List<EntityState>();
        foreach (var (e, _, _) in Sim.Spatial.Within(me.Position, _interestRadius, 64f))
        {
            if (!Sim.World.IsAlive(e))
            {
                continue;
            }
            if (Sim.World.Has<Spawner>(e))
            {
                continue;
            }
            visible.Add(e.Key);
            var state = Snapshots.Capture(Sim, e);
            if (session.Known.Add(e.Key))
            {
                session.Send(new EntitySpawnMsg(state));
            }
            else
            {
                states.Add(state);
            }
        }
        foreach (var key in session.Known.ToList())
        {
            if (!visible.Contains(key))
            {
                session.Known.Remove(key);
                session.Send(new EntityDespawnMsg(key));
            }
        }
        if (states.Count > 0)
        {
            session.Send(new EntityUpdateMsg(states, Sim.Clock.Time));
        }
    }

    private void DispatchEvents()
    {
        if (_pending.Count == 0)
        {
            return;
        }
        var batch = _pending.ToArray();
        _pending.Clear();
        foreach (var evt in batch)
        {
            switch (evt)
            {
                case DamageDealt d:
                    Broadcast(new CombatEventMsg(d.Evaded ? "evade" : d.Blocked ? "block" : "damage", d.Source.Key, d.Target.Key, Math.Round(d.Total, 1), d.Crit, d.Tags.Items.ToList(), null), d.Source, d.Target);
                    break;
                case Healed h:
                    Broadcast(new CombatEventMsg("heal", h.Source.Key, h.Target.Key, Math.Round(h.Amount, 1), false, new List<string> { h.Resource }, null), h.Source, h.Target);
                    break;
                case StatusApplied s:
                    Broadcast(new CombatEventMsg("status", s.Source.Key, s.Target.Key, s.Stacks, false, new List<string>(), s.Status), s.Source, s.Target);
                    break;
                case EntityDied died:
                    Broadcast(new CombatEventMsg("death", died.Killer.Key, died.Entity.Key, 0, false, died.Tags.Items.ToList(), null), died.Killer, died.Entity);
                    break;
                case LevelUp lu:
                    ToOwner(lu.Entity, new NoticeMsg($"You reach level {lu.Level}.", "levelup"));
                    break;
                case QuestAccepted qa:
                    ToOwner(qa.Entity, new NoticeMsg("Quest accepted.", "quest"));
                    break;
                case QuestCompleted qc:
                    ToOwner(qc.Entity, new NoticeMsg("Quest complete. Return to the giver.", "quest"));
                    break;
                case QuestTurnedIn qt:
                    ToOwner(qt.Entity, new NoticeMsg("Quest turned in.", "quest"));
                    break;
                case PoiDiscovered pd:
                    ToOwner(pd.Entity, new NoticeMsg($"Discovered {pd.Name}.", "discovery"));
                    break;
                case ItemAcquired ia:
                    ToOwner(ia.Entity, new NoticeMsg($"Picked up {ia.Base} x{ia.Count}.", "loot"));
                    break;
                case IntentRejected ir:
                    ToOwner(ir.Entity, new IntentRejectedMsg(ir.Intent, ir.Reason));
                    break;
                case QuestsOffered qo:
                {
                    var quests = Sim.Service<Core.Quests.QuestService>();
                    var offers = qo.Offers.Select(id => quests.Find(Sim, id)).Where(q => q is not null).Select(q => Snapshots.QuestOffer(q!)).ToList();
                    var turnIns = qo.TurnIns.Select(id => quests.Find(Sim, id)).Where(q => q is not null).Select(q => Snapshots.QuestOffer(q!)).ToList();
                    ToOwner(qo.Entity, new QuestOffersMsg(qo.Giver.Key, offers, turnIns));
                    break;
                }
                case CraftChallengeIssued cc:
                    ToOwner(cc.Crafter, new CraftChallengeMsg(cc.Recipe, cc.Challenge.Type, cc.Challenge.Data.ToJsonString(), cc.Challenge.TimeLimit));
                    break;
                case CraftResolved cr:
                {
                    ItemInfo? item = null;
                    if (cr.Success && Sim.World.TryGet<Inventory>(cr.Crafter, out var inv))
                    {
                        var last = inv.Items.OrderByDescending(i => i.Uid).FirstOrDefault();
                        if (last is not null)
                        {
                            item = Snapshots.Item(Sim.Service<ItemResolver>(), last);
                        }
                    }
                    ToOwner(cr.Crafter, new CraftResultMsg(cr.Recipe, cr.Success, cr.Score, cr.Quality, cr.Reason, item));
                    break;
                }
                case TravelRequested tr:
                {
                    var session = Sessions.FirstOrDefault(s => s.Entity == tr.Entity);
                    if (session is not null)
                    {
                        TransferRequested?.Invoke(session, new Vector3((float)tr.X, (float)tr.Y, 0));
                    }
                    break;
                }
                case FountBound fb:
                    BroadcastAll(new NoticeMsg($"{Sim.World.Get<Identity>(fb.Entity)?.Name} has bound {fb.Fount.Split('@')[0]}.", "territory"));
                    break;
                case Notice n:
                    ToOwner(n.To, new NoticeMsg(n.Text, "info"));
                    break;
                case ChatMessage cm:
                    BroadcastAll(new ChatBroadcastMsg(Sim.World.Get<Identity>(cm.From)?.Name ?? "?", cm.Channel, cm.Text));
                    break;
            }
        }
    }

    private void Broadcast(Message message, Entity a, Entity b)
    {
        foreach (var s in Sessions)
        {
            if (s.Known.Contains(a.Key) || s.Known.Contains(b.Key) || s.Entity == a || s.Entity == b)
            {
                s.Send(message);
            }
        }
    }

    public void BroadcastAll(Message message)
    {
        foreach (var s in Sessions)
        {
            s.Send(message);
        }
    }

    private void ToOwner(Entity e, Message message)
    {
        foreach (var s in Sessions)
        {
            if (s.Entity == e)
            {
                s.Send(message);
                return;
            }
        }
    }

    public void Add(Session session)
    {
        Sessions.Add(session);
        session.Zone = this;
        session.Known.Clear();
        session.LastSnapshotAt = double.NegativeInfinity;
        session.LastSelfAt = double.NegativeInfinity;
        LastPopulatedAt = Sim.Clock.Time;
    }

    public void Remove(Session session)
    {
        Sessions.Remove(session);
        if (session.Zone == this)
        {
            session.Zone = null;
        }
        if (!session.Entity.IsNone && Sim.World.IsAlive(session.Entity))
        {
            LifetimeSystem.Despawn(Sim, session.Entity);
            Sim.World.Flush();
        }
        session.Entity = Entity.None;
        session.Known.Clear();
    }

    public IEnumerable<ChunkData> ModifiedChunks() => Sim.Terrain.LoadedChunks.Where(c => c.Modified);
}
