using System.Numerics;
using Microsoft.Extensions.Logging;
using HollowWake.Core.Characters;
using HollowWake.Core.Components;
using HollowWake.Core.Crafting;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Intents;
using HollowWake.Core.Items;
using HollowWake.Core.Sim;
using HollowWake.Protocol;
using HollowWake.Server.Config;
using HollowWake.Server.Net;
using HollowWake.Server.Persistence;
using HollowWake.Server.World;

namespace HollowWake.Server.Sessions;

/// <summary>
/// The front door: connections become sessions, messages become authentication, character
/// management or intents. Everything runs on the world thread; the transport inbox feeds it.
/// </summary>
public sealed class SessionManager
{
    private readonly ServerConfig _config;
    private readonly ILogger _log;
    private readonly ZoneHost _zones;
    private readonly IAccountStore _accounts;
    private readonly ICharacterStore _characters;
    private readonly Dictionary<long, Session> _sessions = new();
    private readonly SnapshotSettings _snapshots;

    public IReadOnlyCollection<Session> Sessions => _sessions.Values;

    public SessionManager(ServerConfig config, ILogger<SessionManager> log, ZoneHost zones, IAccountStore accounts, ICharacterStore characters)
    {
        _config = config;
        _log = log;
        _zones = zones;
        _accounts = accounts;
        _characters = characters;
        _snapshots = config.Snapshots.Normalized();
    }

    public void Handle(TransportEvent evt)
    {
        switch (evt.Kind)
        {
            case TransportEventKind.Connected:
            {
                if (_sessions.Count >= _config.MaxPlayers)
                {
                    evt.Connection.Send(MessageSerializer.Encode(new DisconnectMsg("server full")));
                    evt.Connection.Close("server full");
                    return;
                }
                var session = new Session(evt.Connection);
                session.View.Settings = _snapshots;
                _sessions[session.Id] = session;
                _log.LogInformation("{Session} connected", session);
                break;
            }
            case TransportEventKind.Received:
            {
                if (!_sessions.TryGetValue(evt.Connection.Id, out var session))
                {
                    return;
                }
                Message? message;
                try
                {
                    message = MessageSerializer.Decode(evt.Payload!);
                }
                catch (Exception ex)
                {
                    _log.LogWarning("{Session} sent an unreadable message: {Error}", session, ex.Message);
                    session.Error("bad_message", "Could not read message.");
                    return;
                }
                if (message is null)
                {
                    return;
                }
                try
                {
                    Dispatch(session, message);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "{Session} message {Type} failed", session, message.GetType().Name);
                    session.Error("internal", "That could not be done.");
                }
                break;
            }
            case TransportEventKind.Disconnected:
            {
                if (_sessions.Remove(evt.Connection.Id, out var session))
                {
                    _zones.Leave(session);
                    session.State = SessionState.Closed;
                    _log.LogInformation("{Session} disconnected: {Reason}", session, evt.Reason);
                }
                break;
            }
        }
    }

    public void DisconnectAll(string reason)
    {
        foreach (var s in _sessions.Values.ToArray())
        {
            _zones.Leave(s);
            s.Send(new DisconnectMsg(reason));
            s.Connection.Close(reason);
        }
        _sessions.Clear();
    }

    private void Dispatch(Session session, Message message)
    {
        switch (message)
        {
            case HelloMsg hello:
                session.ClientVersion = hello.ClientVersion;
                if (hello.Protocol != ProtocolInfo.Version)
                {
                    session.Send(new DisconnectMsg($"protocol {hello.Protocol} not supported; server speaks {ProtocolInfo.Version}"));
                    session.Connection.Close("protocol mismatch");
                    return;
                }
                // Features are opt-in: a hello without them gets exactly the plain protocol.
                session.View.SendDeltas = hello.Features?.Contains(ProtocolInfo.EntityDeltaFeature) == true;
                session.View.SendInterface = hello.Features?.Contains(ProtocolInfo.InterfaceFeature) == true;
                session.View.SendCastForecasts = hello.Features?.Contains(ProtocolInfo.CastForecastFeature) == true;
                session.View.SendCastCues = hello.Features?.Contains(ProtocolInfo.CastCuesFeature) == true || session.View.SendCastForecasts;
                var granted = new List<string>();
                var hitBodies = hello.Features?.Contains(ProtocolInfo.HitBodiesFeature) == true;
                session.View.SendHitBodies = hitBodies;
                if (hitBodies) granted.Add(ProtocolInfo.HitBodiesFeature);
                if (hello.Features?.Contains(ProtocolInfo.ProjectileSlabsFeature) == true) granted.Add(ProtocolInfo.ProjectileSlabsFeature);
                session.EntryFlow = hello.Features?.Contains(ProtocolInfo.EntryFlowFeature) == true;
                if (session.EntryFlow) granted.Add(ProtocolInfo.EntryFlowFeature);
                if (session.View.SendCastCues) granted.Add(ProtocolInfo.CastCuesFeature);
                if (session.View.SendCastForecasts) granted.Add(ProtocolInfo.CastForecastFeature);
                if (session.View.SendDeltas)
                {
                    granted.Add(ProtocolInfo.EntityDeltaFeature);
                }
                if (session.View.SendInterface)
                {
                    granted.Add(ProtocolInfo.InterfaceFeature);
                }
                session.Send(new WelcomeMsg(ServerInfo.Version, ProtocolInfo.Version, _config.Name, _config.Motd) { Features = granted });
                if (hitBodies) session.Send(new HitBodiesMsg(_zones.Content.Each<HitBodyDef>().OrderBy(d => d.Id, StringComparer.Ordinal)
                    .Select(d => new HitBodyInfo(d.Id, d.Aim, d.Volumes.Select(v => new HitVolumeInfo(
                        v.Shape, v.Forward, v.Left, v.Up, v.Radius, v.HalfHeight)).ToList())).ToList()));
                return;
            case PingMsg ping:
                session.Send(new PongMsg(ping.Nonce, _zones.Elapsed));
                return;
            case LoginMsg login:
                Login(session, login);
                return;
        }
        if (session.State == SessionState.Connected)
        {
            session.Error("not_logged_in", "Log in first.");
            return;
        }
        switch (message)
        {
            case CreateCharacterMsg create:
                CreateCharacter(session, create);
                return;
            case SelectCharacterMsg select:
                SelectCharacter(session, select);
                return;
            case LeaveWorldMsg:
                _zones.Leave(session);
                SendCharacterList(session);
                return;
        }
        if (session.State != SessionState.InWorld || session.Zone is null || session.Entity.IsNone)
        {
            session.Error("not_in_world", "Enter the world first.");
            return;
        }
        var sim = session.Zone.Sim;
        var player = sim.World.Get<Player>(session.Entity);
        if (player is null)
        {
            return;
        }
        if (_config.LogIntents)
        {
            _log.LogDebug("{Session} -> {Message}", session, MessageSerializer.ToJson(message));
        }
        switch (message)
        {
            case ChatMsg chat:
            {
                var text = chat.Text.Trim();
                if (text.Length == 0 || text.Length > 400)
                {
                    return;
                }
                sim.Events.Publish(new ChatMessage(session.Entity, chat.Channel, text) { Tick = sim.Clock.Tick });
                return;
            }
            case RequestChunkMsg req:
            {
                // Terrain is served around the player only: a far request would make the world thread generate
                // and keep chunks nobody stands near.
                var here = sim.World.Get<Transform>(session.Entity)?.Position ?? Vector3.Zero;
                var center = sim.Layout.ChunkOf(here.X, here.Y);
                if (Math.Max(Math.Abs(req.X - center.X), Math.Abs(req.Y - center.Y)) > _config.ChunkRequestRadius)
                {
                    session.Error("chunk_out_of_range", $"Chunk {req.X},{req.Y} is too far away.");
                    return;
                }
                var chunk = sim.Terrain.GetChunk(new Core.World.ChunkCoord(req.X, req.Y));
                session.Send(Snapshots.Chunk(chunk));
                return;
            }
        }
        var intent = ToIntent(sim, message);
        if (intent is null)
        {
            session.Error("unknown_message", message.GetType().Name);
            return;
        }
        player.Intents.Enqueue(intent);
        player.LastInputAt = sim.Clock.Time;
    }

    private static IIntent? ToIntent(Simulation sim, Message m) => m switch
    {
        MoveDirMsg d => new MoveDirIntent(new Vector2(d.X, d.Y)),
        MoveToMsg to => new MoveToIntent(new Vector3(to.X, to.Y, to.Z)),
        StopMsg => new StopIntent(),
        JumpMsg => new JumpIntent(),
        UseWeaveMsg u => new UseWeaveIntent(u.Slot, sim.World.Resolve(u.Target), new Vector3(u.X, u.Y, u.Z), new Vector2(u.DirX, u.DirY)),
        CancelCastMsg => new CancelCastIntent(),
        InteractMsg i => new InteractIntent(sim.World.Resolve(i.Target)),
        PickUpMsg p => new PickUpIntent(sim.World.Resolve(p.Drop)),
        EquipMsg e => new EquipIntent(e.ItemUid, e.Slot),
        UnequipMsg u => new UnequipIntent(u.Slot),
        DropItemMsg d => new DropItemIntent(d.ItemUid),
        AllocatePassiveMsg a => new AllocatePassiveIntent(a.Node) { Option = a.Option },
        SetWeaveMsg s => new SetWeaveIntent(s.Slot, s.Spec),
        AcceptQuestMsg q => new AcceptQuestIntent(q.Quest, sim.World.Resolve(q.Giver)),
        TurnInQuestMsg q => new TurnInQuestIntent(q.Quest, sim.World.Resolve(q.Giver)),
        CraftStartMsg c => new CraftStartIntent(c.Recipe, sim.World.Resolve(c.Station)),
        CraftSubmitMsg c => new CraftSubmitIntent(c.Recipe, sim.World.Resolve(c.Station), new ProcedureInput { Times = c.Times, Choices = c.Choices, Values = c.Values }),
        BindFountMsg b => new BindFountIntent(sim.World.Resolve(b.Fount)),
        EnactDecreeMsg d => new EnactDecreeIntent(d.Holding, d.Decree),
        BuildStructureMsg b => new BuildStructureIntent(b.Holding, b.Structure, new Vector3(b.X, b.Y, b.Z)),
        RespawnMsg => new RespawnIntent(),
        ReleaseWeaveMsg r => new ReleaseWeaveIntent(r.Slot),
        PressWeaveMsg p => new PressWeaveIntent(p.Slot),
        AimMsg a => new AimIntent(new Vector3(a.X, a.Y, a.Z), new Vector2(a.DirX, a.DirY)),
        UseMetaMsg u => new UseMetaIntent(u.Slot),
        SwapWeaveSlotsMsg s => new SwapWeaveSlotsIntent(s.A, s.B),
        CommandMinionsMsg c => new CommandMinionsIntent(c.Command, sim.World.Resolve(c.Target), new Vector3(c.X, c.Y, c.Z), c.Group),
        MoveItemMsg mi => new MoveItemIntent(mi.Item, mi.Container, mi.X, mi.Y) { Slot = mi.Slot },
        SocketStoneMsg ss => new SocketStoneIntent(ss.Stone, ss.Slot, ss.Socket) { Host = ss.Host },
        UnsocketStoneMsg us => new UnsocketStoneIntent(us.Slot, us.Socket, us.X, us.Y) { Host = us.Host },
        BuyPageMsg bp => new BuyPageIntent(bp.Container),
        StationServiceMsg sv => new StationServiceIntent(sim.World.Resolve(sv.Station), sv.Service, sv.Item, sv.Slot, sv.Node),
        LearnFormMsg lf => new LearnFormIntent(lf.Item, lf.Slot),
        UnlearnFormMsg uf => new UnlearnFormIntent(uf.Slot, uf.X, uf.Y),
        RefundPassiveMsg rp => new RefundPassiveIntent(rp.Node),
        BindGraftMsg bg => new BindGraftIntent(bg.Source, bg.Slot),
        BuyTreeNodeMsg bt => new BuyTreeNodeIntent(bt.Slot, bt.Node),
        ChooseDialogueMsg cd => new ChooseDialogueIntent(cd.Conversation, cd.Choice, cd.Node),
        CloseDialogueMsg cl => new CloseDialogueIntent(cl.Conversation),
        ClosePaneMsg cp => new ClosePaneIntent(cp.Pane, sim.World.Resolve(cp.Source)),
        BuyMsg b => new BuyIntent(sim.World.Resolve(b.Vendor), b.Stock, b.Count),
        SellMsg s => new SellIntent(sim.World.Resolve(s.Vendor), s.Item, s.Count),
        BuyBackMsg bb => new BuyBackIntent(sim.World.Resolve(bb.Vendor), bb.Item),
        _ => null,
    };

    // ------------------------------------------------------------ accounts and characters

    private void Login(Session session, LoginMsg login)
    {
        var name = login.Account.Trim();
        if (name.Length is < 2 or > 32 || !name.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.'))
        {
            session.Send(new LoginResultMsg(false, null, "Account names are 2 to 32 letters, digits, dots, dashes or underscores."));
            return;
        }
        if (_config.Auth == "token" && !string.Equals(login.Token, _config.Token, StringComparison.Ordinal))
        {
            session.Send(new LoginResultMsg(false, null, "Bad token."));
            return;
        }
        var id = name.ToLowerInvariant();
        foreach (var other in _sessions.Values)
        {
            if (other != session && other.AccountId == id)
            {
                _zones.Leave(other);
                other.Send(new DisconnectMsg("logged in elsewhere"));
                other.Connection.Close("logged in elsewhere");
            }
        }
        var account = _accounts.Load(id) ?? new Account { Id = id, Name = name };
        account.LastLoginAt = DateTimeOffset.UtcNow;
        _accounts.Save(account);
        session.AccountId = id;
        session.State = SessionState.Authenticated;
        session.Send(new LoginResultMsg(true, id, null));
        SendCharacterList(session);
    }

    private EntryFlowDef EntryFlow => _zones.Content.Each<EntryFlowDef>().Single(f => f.Default);

    private bool SelectionAllowed(Session session, Account account)
    {
        var stage = EntryPolicy.Current(EntryFlow, Core.Tags.TagSet.From(account.EntryFacts));
        if (stage is not null && EntryStageKinds.AllowsSelection(stage.Kind)) return true;
        session.Error("entry_required", "Complete the account's current entry stage first.");
        return false;
    }

    private void SendCharacterList(Session session, string? created = null, string? request = null)
    {
        var account = _accounts.Load(session.AccountId!);
        var list = new List<CharacterSummary>();
        if (account is not null)
        {
            foreach (var cid in account.Characters)
            {
                var sheet = _characters.Load(cid);
                if (sheet is not null)
                {
                    list.Add(new CharacterSummary(sheet.Id, sheet.Name, sheet.Calling, sheet.Level, _zones.Generator.Layout.ZoneOf(sheet.X, sheet.Y).Key));
                }
            }
        }
        var facts = Core.Tags.TagSet.From(account?.EntryFacts);
        var choices = _zones.Content.Each<CallingDef>()
            .Select(c => new CallingAvailability(c.Id, EntryPolicy.Available(c, facts))).ToList();
        var stage = EntryPolicy.Current(EntryFlow, facts)!;
        session.Send(new CharacterListMsg(list, choices.Where(c => c.Available).Select(c => c.Id).ToList()) {
            Entry = session.EntryFlow ? new EntryState(EntryFlow.Id, stage.Id, stage.Kind, stage.Presentation,
                (account?.Characters.Count ?? 0) < EntryFlow.CharacterLimit && EntryStageKinds.AllowsSelection(stage.Kind), choices) : null,
            CreatedCharacterId = session.EntryFlow ? created : null,
            RequestId = session.EntryFlow ? request : null,
        });
    }

    private void CreateCharacter(Session session, CreateCharacterMsg create)
    {
        if (session.State != SessionState.Authenticated) { session.Error("in_world", "Leave the world before creating a character."); return; }
        var name = create.Name.Trim();
        if (name.Length is < 2 or > 24 || !name.All(c => char.IsLetter(c) || c == ' ' || c == '\''))
        {
            session.Error("bad_name", "Names are 2 to 24 letters.");
            return;
        }
        if (!_zones.Content.Has<CallingDef>(create.Calling))
        {
            session.Error("bad_calling", "Unknown calling.");
            return;
        }
        var account = _accounts.Load(session.AccountId!)!;
        if (!SelectionAllowed(session, account)) return;
        if (!EntryPolicy.Available(_zones.Content.Get<CallingDef>(create.Calling)!, Core.Tags.TagSet.From(account.EntryFacts)))
        {
            session.Error("calling_unavailable", "This calling is not available to this account.");
            return;
        }
        if (account.Characters.Count >= EntryFlow.CharacterLimit)
        {
            session.Error("too_many", "That account has enough characters.");
            return;
        }
        var start = _zones.GetOrCreate(_zones.Generator.StartZone());
        var sheet = start.Sim.Service<CharacterFactory>().Create(start.Sim, _characters.NewId(), account.Id, name, create.Calling, start.Sim.Service<ItemGenerator>());
        _characters.Save(sheet);
        account.Characters.Add(sheet.Id);
        _accounts.Save(account);
        _log.LogInformation("{Session} created character {Name} ({Calling})", session, name, create.Calling);
        SendCharacterList(session, sheet.Id, create.RequestId);
    }

    private void SelectCharacter(Session session, SelectCharacterMsg select)
    {
        var account = _accounts.Load(session.AccountId!)!;
        if (!SelectionAllowed(session, account)) return;
        if (!account.Characters.Contains(select.CharacterId))
        {
            session.Error("not_yours", "No such character on this account.");
            return;
        }
        var sheet = _characters.Load(select.CharacterId);
        if (sheet is null)
        {
            session.Error("missing", "That character could not be loaded.");
            return;
        }
        if (session.State == SessionState.InWorld)
        {
            _zones.Leave(session);
        }
        _zones.Enter(session, sheet);
    }
}
