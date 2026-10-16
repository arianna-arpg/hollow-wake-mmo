using Godot;
using Skein.Client.Camera;
using Skein.Client.Net;
using Skein.Client.UI;
using Skein.Client.World;
using Skein.Core.Content;
using Skein.Protocol;

namespace Skein.Client;

/// <summary>
/// The client's root. Owns the connection, the local world, the camera rig and the UI, pumps
/// messages every frame, and turns input into intents. Everything the player sees is derived from
/// server state; everything the player does is a request.
/// </summary>
public partial class Main : Node3D
{
    private readonly NetClient _net = new();
    private ContentRegistry _content = null!;
    private ClientWorld _world = null!;
    private CameraRig _camera = null!;
    private Hud _hud = null!;
    private LoginPanel _login = null!;
    private string _zoneName = "";
    private bool _inWorld;
    private Vector2 _lastMoveDir;
    private double _lastPingAt;
    private EntityView? _hovered;
    private long _hoveredKey;

    public override void _Ready()
    {
        InputSetup.Ensure();
        _content = LoadContent();
        _world = new ClientWorld(_content) { Name = "World" };
        AddChild(_world);
        _camera = new CameraRig { Name = "CameraRig" };
        AddChild(_camera);
        _hud = new Hud { Name = "Hud", Visible = false };
        AddChild(_hud);
        _login = new LoginPanel { Name = "Login" };
        AddChild(_login);

        _login.ConnectRequested += OnConnect;
        _login.CreateRequested += (name, calling) => _net.Send(new CreateCharacterMsg(name, calling));
        _login.EnterRequested += id => _net.Send(new SelectCharacterMsg(id));
        _hud.SlotPressed += slot => UseSlot(slot);
        _hud.ChatSubmitted += text => _net.Send(new ChatMsg("zone", text));
        _hud.QuestAccepted += (quest, giver) => _net.Send(new AcceptQuestMsg(quest, giver));
        _hud.QuestTurnedIn += (quest, giver) => _net.Send(new TurnInQuestMsg(quest, giver));
        _net.Closed += reason => CallDeferred(nameof(OnClosed), reason);
    }

    private static ContentRegistry LoadContent()
    {
        // The client needs the same content the server runs to generate terrain and name things.
        // It looks for content/core beside the executable, then up the tree (the repository layout).
        var candidates = new List<string>();
        var exe = OS.GetExecutablePath().GetBaseDir();
        candidates.Add(Path.Combine(exe, "content", "core"));
        var dir = ProjectSettings.GlobalizePath("res://");
        for (var i = 0; i < 5 && !string.IsNullOrEmpty(dir); i++)
        {
            candidates.Add(Path.Combine(dir, "content", "core"));
            dir = Path.GetDirectoryName(dir.TrimEnd('/', '\\')) ?? "";
        }
        foreach (var c in candidates)
        {
            if (Directory.Exists(c))
            {
                var registry = ContentRegistry.Load(new[] { c }, out var report);
                GD.Print($"content loaded from {c}: {report.ErrorCount} errors, {report.WarningCount} warnings");
                return registry;
            }
        }
        GD.PushError("content/core not found; terrain cannot be generated locally");
        return ContentRegistry.Empty();
    }

    private async void OnConnect(string host, int port, string account)
    {
        _login.SetStatus($"Connecting to {host}:{port}...");
        var ok = await _net.ConnectAsync(host, port);
        if (!ok)
        {
            _login.SetStatus("Could not connect: " + _net.LastError);
            return;
        }
        _net.Send(new HelloMsg(ClientInfo.Name + " 0.1", ProtocolInfo.Version));
        _net.Send(new LoginMsg(account, ""));
        _login.SetStatus("Logging in...");
    }

    private void OnClosed(string reason)
    {
        _inWorld = false;
        _hud.Visible = false;
        _login.Visible = true;
        _login.SetStatus("Disconnected: " + reason);
        _world.ClearEntities();
    }

    public override void _Process(double delta)
    {
        var budget = 500;
        while (budget-- > 0 && _net.TryDequeue(out var message))
        {
            Handle(message);
        }
        if (_inWorld)
        {
            var self = _world.Self;
            if (self is not null)
            {
                _camera.Follow(self.GlobalPosition);
                _world.Stream(Coords.ToCore(self.GlobalPosition));
            }
            HandleMovementInput();
            UpdateHover();
            var now = Time.GetTicksMsec() / 1000.0;
            if (now - _lastPingAt > 5)
            {
                _lastPingAt = now;
                _net.Send(new PingMsg((long)(now * 1000)));
            }
        }
    }

    // ------------------------------------------------------------ messages

    private void Handle(Message message)
    {
        switch (message)
        {
            case WelcomeMsg w:
                _login.SetStatus($"Connected to {w.ServerName}: {w.Motd}");
                break;
            case LoginResultMsg r:
                _login.SetStatus(r.Ok ? "Logged in. Pick a character." : "Login failed: " + r.Error);
                break;
            case CharacterListMsg list:
                _login.ShowCharacters(list);
                break;
            case ErrorMsg e:
                _hud.Notice(e.Text, "error");
                _login.SetStatus(e.Text);
                break;
            case DisconnectMsg d:
                OnClosed(d.Reason);
                break;
            case EnterWorldMsg enter:
                _world.ApplyWorldInfo(enter.World);
                _world.ClearEntities();
                _world.SelfKey = enter.EntityKey;
                _inWorld = true;
                _login.Visible = false;
                _hud.Visible = true;
                _camera.Follow(Coords.ToGodot(enter.X, enter.Y, enter.Z));
                _hud.Log($"[color=#aaffaa]Entered the world at {enter.X:0}, {enter.Y:0} (zone {enter.Zone}).[/color]");
                break;
            case ZoneChangedMsg z:
                _zoneName = $"{z.ZoneName} (L{z.Level}{(z.Safe ? ", safe" : "")})";
                _hud.Notice($"{z.ZoneName}", "discovery");
                break;
            case EntitySpawnMsg s:
                _world.Spawn(s.State);
                break;
            case EntityUpdateMsg u:
                foreach (var state in u.States)
                {
                    _world.Update(state);
                }
                break;
            case EntityDespawnMsg d:
                _world.Despawn(d.Key);
                break;
            case SelfStateMsg self:
                _hud.ApplySelf(self, _zoneName);
                break;
            case CombatEventMsg c:
                ShowCombat(c);
                break;
            case ChatBroadcastMsg chat:
                _hud.Chat(chat.From, chat.Channel, chat.Text);
                break;
            case NoticeMsg n:
                _hud.Notice(n.Text, n.Level);
                break;
            case QuestOffersMsg q:
                _hud.ShowQuestOffers(q);
                break;
            case IntentRejectedMsg rej:
                _hud.Notice($"{rej.Intent}: {rej.Reason}", "error");
                break;
            case ChunkDataMsg chunk:
                _world.ReceiveChunk(chunk);
                break;
            case CraftChallengeMsg cc:
                _hud.Log($"[color=#ffd27a]Crafting {cc.Recipe}: {cc.Type} challenge ({cc.TimeLimit:0}s)[/color]");
                break;
            case CraftResultMsg cr:
                _hud.Notice(cr.Success ? $"Crafted {cr.Item?.Name} (quality {cr.Quality:0.00})" : $"Craft failed: {cr.Reason}", cr.Success ? "quest" : "error");
                break;
        }
    }

    private void ShowCombat(CombatEventMsg c)
    {
        if (c.Source != _world.SelfKey && c.Target != _world.SelfKey)
        {
            return;
        }
        var source = _world.Entities.GetValueOrDefault(c.Source)?.State.Name ?? "?";
        var target = _world.Entities.GetValueOrDefault(c.Target)?.State.Name ?? "?";
        var text = c.Kind switch
        {
            "damage" => $"{source} hits {target} for {c.Amount:0}{(c.Crit ? " (crit)" : "")}",
            "evade" => $"{target} evades {source}",
            "block" => $"{target} blocks {source}",
            "heal" => $"{source} heals {target} for {c.Amount:0}",
            "status" => $"{target} is {c.Status}",
            "death" => $"{target} dies",
            _ => c.Kind,
        };
        _hud.Log($"[color=#cccccc]{text}[/color]");
    }

    // ------------------------------------------------------------ input

    private void HandleMovementInput()
    {
        if (_hud.ChatFocused)
        {
            return;
        }
        var input = Vector2.Zero;
        if (Input.IsActionPressed("move_forward"))
        {
            input.Y += 1;
        }
        if (Input.IsActionPressed("move_back"))
        {
            input.Y -= 1;
        }
        if (Input.IsActionPressed("move_right"))
        {
            input.X += 1;
        }
        if (Input.IsActionPressed("move_left"))
        {
            input.X -= 1;
        }
        Vector2 dir = Vector2.Zero;
        if (input.LengthSquared() > 0)
        {
            var forward = _camera.GroundForward();
            var right = _camera.GroundRight();
            var godotGround = (forward * input.Y + right * input.X).Normalized();
            dir = Coords.GroundToCore(godotGround) is var core ? new Vector2(core.X, core.Y) : Vector2.Zero;
        }
        if (dir != _lastMoveDir)
        {
            _lastMoveDir = dir;
            if (dir == Vector2.Zero)
            {
                _net.Send(new StopMsg());
            }
            else
            {
                _net.Send(new MoveDirMsg(dir.X, dir.Y));
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_inWorld)
        {
            return;
        }
        if (@event.IsActionPressed("chat"))
        {
            _hud.FocusChat();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_hud.ChatFocused)
        {
            return;
        }
        if (@event.IsActionPressed("jump"))
        {
            _net.Send(new JumpMsg());
        }
        else if (@event.IsActionPressed("camera_toggle"))
        {
            _camera.Toggle();
        }
        else if (@event.IsActionPressed("camera_rotate_left"))
        {
            _camera.RotateStep(1);
        }
        else if (@event.IsActionPressed("camera_rotate_right"))
        {
            _camera.RotateStep(-1);
        }
        else if (@event.IsActionPressed("interact"))
        {
            InteractNearest("interact");
        }
        else if (@event.IsActionPressed("pick_up"))
        {
            InteractNearest("pick_up");
        }
        else if (@event.IsActionPressed("respawn"))
        {
            _net.Send(new RespawnMsg());
        }
        else if (@event.IsActionPressed("toggle_sheet"))
        {
            _hud.ToggleSheet();
        }
        else if (@event.IsActionPressed("cancel"))
        {
            _net.Send(new CancelCastMsg());
            _hud.HideQuests();
        }
        else
        {
            for (var i = 1; i <= 8; i++)
            {
                if (@event.IsActionPressed("slot_" + i))
                {
                    UseSlot(i - 1);
                    return;
                }
            }
        }
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            var (origin, direction) = MouseRay();
            var picked = _world.PickEntity(origin, direction);
            if (picked is not null && picked.State.Interactable is not null && picked.State.Interactable != "opened")
            {
                _net.Send(picked.State.Look?.StartsWith("drop:") == true ? new PickUpMsg(picked.Key) : new InteractMsg(picked.Key));
                return;
            }
            if (picked is not null && picked.State.Look?.StartsWith("drop:") == true)
            {
                _net.Send(new PickUpMsg(picked.Key));
                return;
            }
            var ground = _world.RayToGround(origin, direction);
            if (ground is { } g)
            {
                _net.Send(new MoveToMsg(g.X, g.Y, g.Z));
                _lastMoveDir = Vector2.Zero;
            }
        }
    }

    private (Vector3 Origin, Vector3 Direction) MouseRay()
    {
        var mouse = GetViewport().GetMousePosition();
        return (_camera.Camera.ProjectRayOrigin(mouse), _camera.Camera.ProjectRayNormal(mouse));
    }

    private void UpdateHover()
    {
        var (origin, direction) = MouseRay();
        _hovered = _world.PickEntity(origin, direction);
        _hoveredKey = _hovered?.Key ?? 0;
        if (_hovered is null)
        {
            _hud.SetTarget("");
            return;
        }
        var s = _hovered.State;
        var life = s.Resources.TryGetValue("life", out var l) && s.ResourceMax.TryGetValue("life", out var m) ? $"  {l:0}/{m:0}" : "";
        _hud.SetTarget($"{s.Name}{(s.Level > 0 ? " [" + s.Level + "]" : "")}{life}{(s.Interactable is not null ? "  (" + s.Interactable + ")" : "")}");
    }

    private void UseSlot(int slot)
    {
        var (origin, direction) = MouseRay();
        var ground = _world.RayToGround(origin, direction);
        var self = _world.Self;
        var target = _hoveredKey;
        var point = ground ?? (self is null ? default : Coords.ToCore(self.GlobalPosition));
        var dir = Vector2.Zero;
        if (self is not null)
        {
            var sc = Coords.ToCore(self.GlobalPosition);
            dir = new Vector2(point.X - sc.X, point.Y - sc.Y);
        }
        _net.Send(new UseWeaveMsg(slot, target, point.X, point.Y, point.Z, dir.X, dir.Y));
    }

    private void InteractNearest(string kind)
    {
        var self = _world.Self;
        if (self is null)
        {
            return;
        }
        EntityView? best = null;
        var bestDist = float.MaxValue;
        foreach (var v in _world.Entities.Values)
        {
            if (v.Key == _world.SelfKey)
            {
                continue;
            }
            var isDrop = v.State.Look?.StartsWith("drop:") == true;
            if (kind == "pick_up" ? !isDrop : v.State.Interactable is null || isDrop)
            {
                continue;
            }
            var d = (v.GlobalPosition - self.GlobalPosition).Length();
            if (d < bestDist && d < 6)
            {
                bestDist = d;
                best = v;
            }
        }
        if (best is not null)
        {
            _net.Send(kind == "pick_up" ? new PickUpMsg(best.Key) : new InteractMsg(best.Key));
        }
    }

    public override void _ExitTree() => _net.Dispose();
}
