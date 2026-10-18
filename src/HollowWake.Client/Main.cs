using HollowWake.Client.UI.Loading;
using Godot;
using HollowWake.Client.Camera;
using HollowWake.Client.Net;
using HollowWake.Client.UI;
using HollowWake.Client.UI.Logic;
using HollowWake.Client.UI.Overlay;
using HollowWake.Client.World;
using HollowWake.Client.World.Appearance;
using HollowWake.Core.Content;
using HollowWake.Protocol;

namespace HollowWake.Client;

/// <summary>
/// The client's root. Owns the connection, the local world, the camera rig and the UI, pumps
/// messages every frame, and turns input into intents. Everything the player sees is derived from
/// server state; everything the player does is a request.
/// </summary>
public partial class Main : Node3D
{
    private readonly NetClient _net = new();
    private readonly Dictionary<string, Action> _appliers = new(StringComparer.Ordinal);
    private ContentRegistry _content = null!;
    private CastVisuals _castVisuals = null!;
    private ClientWorld _world = null!;
    private AppearanceService _appearance = null!;
    private ScreenFilter _filter = null!;
    private CameraRig _camera = null!;
    private Hud _hud = null!;
    private LoginPanel _login = null!;
    private Settings _settings = null!;
    private string _profile = "default";
    private bool _developer;
    private bool _inWorld;
    private Vector2 _lastMoveDir;
    private double _lastPingAt;
    private double _rtt = -1;
    private EntityView? _hovered;
    private long _hoveredKey;
    private string _lastRejected = "";

    // Read by the scripted smoke run (Dev/Smoke.cs) and the mockup (Dev/Mockup.cs); nothing else should need these.
    internal ClientWorld World => _world;
    internal AppearanceService Appearance => _appearance;
    internal CameraRig Rig => _camera;
    internal Hud Hud => _hud;
    internal LoginPanel Login => _login;
    internal NetClient Net => _net;
    internal bool InWorld => _inWorld;
    internal long HoveredKey => _hoveredKey;
    internal int CombatLines { get; private set; }
    internal int CastCuesReceived { get; private set; }
    internal CastVisuals CastVisuals => _castVisuals;
    internal int CharacterLists { get; private set; }
    internal int QuestOffers { get; private set; }
    internal int DialogueStates { get; private set; }
    internal int VendorStates { get; private set; }
    internal int InventoryStates { get; private set; }
    internal string LastRejectedIntent { get; private set; } = "";
    internal ContentRegistry Content => _content;
    internal Settings Settings => _settings;
    // The scripted smoke owns a virtual pointer; a graphical display otherwise queries the OS cursor.
    internal Vector2? SmokePointer { get; set; }

    internal string TakeRejected()
    {
        var r = _lastRejected;
        _lastRejected = "";
        return r;
    }

    private LoadingScreen _loading = null!;
    private LoadingSession.Lease? _entryLease;
    private bool _initialized, _entryFailed;
    private double _entryStarted;
    private double _entryX, _entryY;
    internal LoadingScreen Loading => _loading;

    public override async void _Ready()
    {
        var spec = LoadingSpec.FromDocument(Godot.FileAccess.GetFileAsString("res://Branding/loading.json"));
        _loading = new LoadingScreen(spec) { Name = "Crossing", ReducedMotion = () => _initialized && _settings.Toggle("reduce_motion"), ClearWorldInput = ReleaseLoadingInput };
        AddChild(_loading);
        var boot = _loading.Begin(spec.Text("boot"));
        try
        {
            await _loading.Paint();
            var path = ContentPath();
            var loaded = await Task.Run(() => {
                var registry = ContentRegistry.Load(new[] { path }, out var report);
                return (Registry: registry, Report: report);
            });
            _content = loaded.Registry;
            GD.Print($"content loaded from {path}: {loaded.Report.ErrorCount} errors, {loaded.Report.WarningCount} warnings");
            if (loaded.Report.ErrorCount != 0) throw new InvalidDataException("The content pack could not be validated.");
            Initialize();
            _initialized = true;
            boot.Finish();
            _loading.UseSpec(LoadingSpec.FromMeta(_content.Each<HollowWake.Core.Definitions.UiLoadingScreenDef>().Single(d => d.Default).Meta!));
            var args = OS.GetCmdlineUserArgs();
            if (args.Contains("--boot-check")) { GD.Print("BOOT PASS"); GetTree().Quit(); return; }
            if (args.Contains("--loading-smoke")) { AddChild(new Dev.LoadingSmoke(this, args)); return; }
            if (args.Contains("--quick-play-smoke"))
            {
                AddChild(new Dev.QuickPlaySmoke(this, args));
                AddChild(new Dev.QuickPlay(this, args));
                return;
            }
            if (Arg(args, "--loading-preview") is { } direction && direction is "down" or "left" or "right")
                _loading.Begin(spec.Text("boot"), cancel: () => _loading.Close(), direction: direction);
            else if (args.Contains("--quick-play") && !args.Contains("--smoke") && !args.Contains("--mockup"))
                AddChild(new Dev.QuickPlay(this, args) { Name = "QuickPlay" });
        }
        catch (Exception ex)
        {
            boot.Fail(spec.Text("failed"), ex.Message, () => GetTree().ReloadCurrentScene());
            GD.PushError(ex.ToString());
            if (OS.GetCmdlineUserArgs().Contains("--boot-check")) GetTree().Quit(1);
        }
    }

    private void SelectCharacter(string id)
    {
        _entryFailed = false; _entryStarted = LocalNow;
        _entryLease = _loading.Begin(_loading.Spec.Text("entry"), cancel: CancelEntry);
        _net.Send(new SelectCharacterMsg(id));
    }
    private void ReleaseLoadingInput()
    {
        LoadingScreen.ReleaseActions();
        if (_initialized && _inWorld) _net.Send(new StopMsg());
        _lastMoveDir = Vector2.Zero;
    }
    private void CancelEntry()
    {
        _entryLease?.Finish(); _entryLease = null;
        _net.Disconnect("Loading cancelled");
        while (_net.TryDequeue(out _)) { }
        OnClosed("Loading cancelled");
    }
    private void RetryEntry()
    {
        if (!_net.IsConnected) { CancelEntry(); return; }
        _entryFailed = false; _entryStarted = LocalNow;
        _world.RetryPreparation();
        _entryLease?.Update(_loading.Spec.Text("entry"));
        // TCP already owns delivery of the selection. Retry local preparation without
        // selecting twice while an authoritative enter_world response is still arriving.
    }
    private void CheckEntry()
    {
        if (_entryLease?.Current != true || _entryFailed) return;
        if (_inWorld)
        {
            var progress = _world.Readiness(_entryX, _entryY, Math.Min(_world.ViewChunks, _loading.Spec.ReadyRadius));
            var hasSelf = _world.Self is not null;
            _entryLease.Update(_loading.Spec.Text("terrain"), completed: progress.Completed + (hasSelf ? 1 : 0), total: progress.Total + 1);
            if (hasSelf && progress.Completed == progress.Total) { _entryLease.Finish(); _entryLease = null; return; }
        }
        if (LocalNow - _entryStarted > _loading.Spec.TimeoutSeconds)
        {
            _entryFailed = true;
            _entryLease.Fail(_loading.Spec.Text("failed"), _loading.Spec.Text("timeout"), RetryEntry);
        }
    }

    private void Initialize()
    {
        var userArgs = OS.GetCmdlineUserArgs();
        IEnumerable<System.Text.Json.Nodes.JsonObject> Raw(string kind) => _content.RawOfKind(kind).Select(r => r.Object);

        // The player's options: a settings file per profile. The smoke and the mockup run on their own fresh profiles.
        var scripted = userArgs.Contains("--smoke") ? "smoke" : userArgs.Contains("--mockup") ? "mockup" : null;
        _profile = Arg(userArgs, "--profile") ?? scripted ?? "default";
        _developer = userArgs.Contains("--developer") || scripted is not null;
        _settings = SettingsFile.Load(OptionsCatalog.Load(Raw), _profile, fresh: scripted is not null);
        if (Arg(userArgs, "--theme") is { } theme)
        {
            _settings.Set("theme", System.Text.Json.Nodes.JsonValue.Create(theme));
        }
        _settings.Changed += OnOptionChanged;
        // The input map is content: every action with the player's rebinds over the defaults.
        InputSetup.Apply(_settings);

        _world = new ClientWorld(_content) { Name = "World" };
        _castVisuals = new CastVisuals(_world, HollowWake.Core.Definitions.CastVisualThemes.Resolve(
            _content.Each<HollowWake.Core.Definitions.CastVisualsDef>().FirstOrDefault()
                ?? new HollowWake.Core.Definitions.CastVisualsDef { Id = "fallback" },
            _content.Each<HollowWake.Core.Definitions.ThreadDef>()));
        AddChild(_castVisuals);
        AddChild(_world);
        // The appearance pipeline: looks, bodies, cards, the day and the motion timings, all from content.
        _appearance = new AppearanceService(_content, _world.Baker);
        _world.UseAppearance(_appearance);
        _camera = new CameraRig { Name = "CameraRig" };
        AddChild(_camera);
        var feel = _appearance.Motion;
        _camera.FollowIso = feel.Camera("follow_iso", 10);
        _camera.FollowThird = feel.Camera("follow_third", 14);
        _camera.ZoomEase = feel.Camera("zoom_ease", 9);
        _camera.ShakeSeconds = feel.Camera("shake_time", 0.28f);
        _camera.ShakeRate = feel.Camera("shake_rate", 28);
        _filter = new ScreenFilter();
        AddChild(_filter);
        _hud = new Hud(UiContent.Load(Raw), ContentNames.Load(Raw), _settings, Fact) { Name = "Hud", Visible = false };
        _appearance.SelfEquipment = () => _hud.Hero.Equipment;
        EntityView.SelfCast = () => _hud.Hero.Cast is { } cast ? (cast.Mode, (float)cast.Progress) : null;
        _hud.Send += message => _net.Send(message);
        _hud.SlotPressed += slot => UseSlot(slot);
        _hud.HeroScreen = HeroScreen;
        _hud.DistanceTo = DistanceTo;
        _hud.Casting = () => _world.Self?.State.Casting is { Length: > 0 } || _hud.Hero.Cast is not null;
        _hud.OptionsClosed += OnOptionsClosed;
        // Marks that end at a server time (a telegraph's until) read the server clock the snapshots teach.
        EntityView.ServerNow = () => _hud.Hero.HasServerClock ? _hud.Hero.ServerNow(LocalNow) : null;
        AddChild(_hud);
        var launch = LaunchOptions.FromCommandLine();
        if (launch.ServerGiven)
        {
            GD.Print($"server {launch.Host}:{launch.Port} from the command line");
        }
        _login = new LoginPanel { Name = "Login", DefaultHost = launch.Host, DefaultPort = launch.Port, Skin = _hud.Skin, Content = _content, Appearance = _appearance, Suspended = () => _loading.Active };
        AddChild(_login);

        _login.ConnectRequested += OnConnect;
        _login.CreateRequested += (name, calling) => _net.Send(new CreateCharacterMsg(name, calling) { RequestId = _login.CreationRequestId });
        _login.EnterRequested += SelectCharacter;
        _login.OptionsRequested += OpenOptionsFromLogin;
        _login.LeaveRequested += () => { _net.Disconnect("Left Mu"); OnClosed("Left Mu"); };
        _net.Closed += (generation, reason) => CallDeferred(nameof(OnConnectionClosed), generation, reason);

        RegisterAppliers();
        foreach (var apply in _appliers.Values.Distinct())
        {
            apply();
        }

        if (userArgs.Contains("--mockup"))
        {
            AddChild(new Dev.Mockup(this, userArgs) { Name = "Mockup" });
        }
        else if (userArgs.Contains("--smoke"))
        {
            AddChild(new Dev.Smoke(this, userArgs) { Name = "Smoke" });
        }
    }

    private static string? Arg(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    // ------------------------------------------------------------ options

    /// <summary>The client's facts the options' gates and requirements read.</summary>
    private double? Fact(string name) => name switch
    {
        "client.desktop" => 1,
        "client.web" => 0,
        "client.developer" => _developer ? 1 : 0,
        "client.controller" => Input.GetConnectedJoypads().Count > 0 ? 1 : 0,
        _ => null,
    };

    /// <summary>What each option does when it changes, by entry id; entries without one are read where they are used.</summary>
    private void RegisterAppliers()
    {
        void Camera() => _camera.Configure(
            (float)_settings.Number("iso_rotate_step", 45),
            (float)_settings.Number("iso_zoom_max", 90),
            (float)_settings.Number("third_sensitivity", 1),
            _settings.Toggle("third_invert"),
            (float)_settings.Number("third_fov", 65));
        foreach (var id in new[] { "iso_rotate_step", "iso_zoom_max", "third_sensitivity", "third_invert", "third_fov" })
        {
            _appliers[id] = Camera;
        }
        _appliers["camera_mode"] = () => _camera.SetMode(_settings.Text("camera_mode") == "third_person" ? CameraMode.ThirdPerson : CameraMode.Isometric);
        _appliers["view_distance"] = () => _world.ViewChunks = (int)_settings.Number("view_distance", 3);
        _appliers["shadows"] = () => _world.SetShadows(_settings.Toggle("shadows", true));
        _appliers["fog"] = () => _world.SetFog(_settings.Toggle("fog", true));
        _appliers["prop_labels"] = () => _world.SetPropLabels(_settings.Toggle("prop_labels", true));
        _appliers["bodies"] = () =>
        {
            _appearance.Mode = _settings.Text("bodies") switch { "geometry" => BodiesMode.Geometry, "cards" => BodiesMode.Cards, _ => BodiesMode.Auto };
        };
        _appliers["water_detail"] = () => TerrainMesh.SetWaterDetail(_settings.Toggle("water_detail", true));
        _appliers["foliage_density"] = () => _world.FoliageDensity = (float)_settings.Number("foliage_density", 1);
        _appliers["screen_shake"] = () => _camera.ShakeEnabled = _settings.Toggle("screen_shake", true);
        _appliers["reduce_motion"] = () =>
        {
            _camera.ReducedMotion = _settings.Toggle("reduce_motion");
            _appearance.ReducedMotion = _settings.Toggle("reduce_motion");
        };
        _appliers["grayscale"] = () => _filter.Grayscale = _settings.Toggle("grayscale");
        void Day() => _world.DayOverride = _settings.Toggle("day_lock") ? (float)(_settings.Number("day_time", 12) / 24.0) : null;
        _appliers["day_lock"] = Day;
        _appliers["day_time"] = Day;
        _appliers["master_volume"] = () => AudioServer.SetBusVolumeDb(0, Mathf.LinearToDb((float)_settings.Number("master_volume", 80) / 100f));
        if (DisplayServer.GetName() != "headless")
        {
            _appliers["window_mode"] = () => DisplayServer.WindowSetMode(_settings.Text("window_mode") switch
            {
                "fullscreen" => DisplayServer.WindowMode.ExclusiveFullscreen,
                "borderless" => DisplayServer.WindowMode.Fullscreen,
                _ => DisplayServer.WindowMode.Windowed,
            });
            _appliers["vsync"] = () => DisplayServer.WindowSetVsyncMode(_settings.Toggle("vsync", true) ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
        }
        _appliers["frame_cap"] = () => Engine.MaxFps = (int)_settings.Number("frame_cap", 0);
        _appliers["antialiasing"] = () => GetViewport().Msaa3D = _settings.Text("antialiasing") switch
        {
            "off" => Viewport.Msaa.Disabled,
            "msaa_4x" => Viewport.Msaa.Msaa4X,
            "msaa_8x" => Viewport.Msaa.Msaa8X,
            _ => Viewport.Msaa.Msaa2X,
        };
        _appliers["input"] = () => InputSetup.Apply(_settings);
    }

    private void OnOptionChanged(OptionEntry entry)
    {
        SettingsFile.Save(_settings, _profile);
        if (entry.Applies != "live")
        {
            return;
        }
        Apply(entry);
    }

    private void Apply(OptionEntry entry)
    {
        if (entry.Kind == "key")
        {
            _appliers["input"]();
        }
        else if (_appliers.TryGetValue(entry.Id, out var apply))
        {
            apply();
        }
    }

    /// <summary>Entries that apply when the pane closes take effect now; the login screen comes back when not in the world.</summary>
    private void OnOptionsClosed()
    {
        foreach (var entry in _settings.Catalog.Entries.Values.Where(e => e.Applies == "on_close"))
        {
            Apply(entry);
        }
        if (!_inWorld)
        {
            _hud.Visible = false;
            _login.Visible = true;
        }
    }

    private void OpenOptionsFromLogin()
    {
        _login.Visible = false;
        _hud.Visible = true;
        _hud.OpenOptions();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut && _settings?.Toggle("mute_unfocused") == true)
        {
            AudioServer.SetBusMute(0, true);
        }
        else if (what == NotificationApplicationFocusIn)
        {
            AudioServer.SetBusMute(0, false);
        }
    }

    /// <summary>Draws the interface with another theme (scripted shots); the interface restyles on its next frame.</summary>
    internal void UseTheme(string theme) => _settings.Set("theme", System.Text.Json.Nodes.JsonValue.Create(theme));

    private Vector2? HeroScreen()
    {
        var self = _world.Self;
        if (self is null || _camera.Camera.IsPositionBehind(self.GlobalPosition))
        {
            return null;
        }
        return _camera.Camera.UnprojectPosition(self.GlobalPosition + new Vector3(0, self.State.Height + 0.4f, 0));
    }

    private float? DistanceTo(long key)
    {
        var self = _world.Self;
        if (self is null || !_world.Entities.TryGetValue(key, out var other))
        {
            return null;
        }
        var a = self.State;
        var b = other.State;
        return MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    }

    private static string ContentPath()
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
                return c;
            }
        }
        throw new DirectoryNotFoundException("content/core not found; terrain cannot be prepared.");
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
        // The client applies entity deltas; --full-states asks for full entity states only (for comparison and debugging).
        // It always draws the interface, so it asks for the interface's states.
        var features = new List<string> { ProtocolInfo.InterfaceFeature, ProtocolInfo.CastCuesFeature, ProtocolInfo.EntryFlowFeature };
        if (!OS.GetCmdlineUserArgs().Contains("--full-states") && _settings.Text("snapshot_stream") != "full")
        {
            features.Insert(0, ProtocolInfo.EntityDeltaFeature);
        }
        _net.Send(new HelloMsg(ClientInfo.Name + " 0.1", ProtocolInfo.Version) { Features = features });
        _net.Send(new LoginMsg(account, ""));
        _login.SetStatus("Logging in...");
    }

    private void OnConnectionClosed(long generation, string reason)
    {
        if (generation == _net.Generation) OnClosed(reason);
    }

    private void OnClosed(string reason)
    {
        if (_entryLease?.Current == true)
        {
            _entryFailed = true;
            _entryLease.Fail(_loading.Spec.Text("failed"), reason);
        }
        _inWorld = false;
        _hud.InWorld = false;
        _hud.Visible = false;
        _hud.Workspace.CloseCore();
        _hud.Hero.Clear();
        _login.Visible = true;
        _login.Reset();
        _login.SetStatus("Disconnected: " + reason);
        _world.ClearEntities();
        _castVisuals.Clear();
    }

    public override void _Process(double delta)
    {
        if (!_initialized) return;
        var budget = 500;
        while (budget-- > 0 && _net.TryDequeue(out var message))
        {
            Handle(message);
        }
        _castVisuals.Advance(_hud.Hero.ServerNow(LocalNow), _settings.Toggle("reduce_motion"));
        CheckEntry();
        if (_inWorld)
        {
            var self = _world.Self;
            if (self is not null)
            {
                _camera.Follow(self.GlobalPosition);
                _world.Stream(Coords.ToCore(self.GlobalPosition));
            }
            _world.ObserveCamera(_camera.Mode == CameraMode.Isometric, _camera.OrthoSize, _camera.YawNow);
            if (_hud.Hero.HasServerClock)
            {
                _world.ObserveServerTime(_hud.Hero.ServerNow(LocalNow));
            }
            _hud.SetSelfEntity(self?.State);
            if (!_loading.BlocksWorldInput) { HandleMovementInput(); UpdateHover(); }
            var now = Time.GetTicksMsec() / 1000.0;
            if (now - _lastPingAt > 5)
            {
                _lastPingAt = now;
                _net.Send(new PingMsg((long)(now * 1000)));
            }
            _hud.NetText = $"{(_rtt < 0 ? "" : $"{_rtt:0} ms  ")}{_world.BuiltChunks} chunks  {_world.Entities.Count} entities";
        }
    }

    // ------------------------------------------------------------ messages

    private static double LocalNow => Time.GetTicksMsec() / 1000.0;

    private void Handle(Message message)
    {
        switch (message)
        {
            case WelcomeMsg w:
                _login.SetStatus($"Connected to {w.ServerName}: {w.Motd}");
                _hud.Hero.Interface = w.Features?.Contains(ProtocolInfo.InterfaceFeature) == true;
                GD.Print($"server granted features: {(w.Features is null ? "none" : string.Join(", ", w.Features))}");
                break;
            case LoginResultMsg r:
                _login.SetStatus(r.Ok ? "Logged in. Pick a character." : "Login failed: " + r.Error);
                break;
            case CharacterListMsg list:
                if (_inWorld)
                {
                    _inWorld = false; _hud.InWorld = false; _hud.Visible = false;
                    _hud.Workspace.CloseCore(); _hud.Hero.Clear();
                    _world.ClearEntities(); _castVisuals.Clear(); _entryLease?.Finish();
                    ReleaseLoadingInput();
                }
                _login.Visible = true;
                CharacterLists++;
                _login.ShowCharacters(list);
                break;
            case ErrorMsg e:
                if (_entryLease?.Current == true)
                {
                    _entryFailed = true;
                    _entryLease.Fail(_loading.Spec.Text("failed"), e.Text, RetryEntry);
                }
                _hud.Notice(e.Text, "error");
                _login.SetStatus(e.Text);
                break;
            case DisconnectMsg d:
                OnClosed(d.Reason);
                break;
            case EnterWorldMsg enter:
                if (_entryLease?.Current != true && !_inWorld) break;
                if (_entryLease?.Current != true)
                {
                    _entryStarted = LocalNow;
                    _entryLease = _loading.Begin(_loading.Spec.Text("entry"), kind: "travel", cancel: CancelEntry);
                }
                _entryX = enter.X; _entryY = enter.Y; _entryFailed = false;
                _world.ApplyWorldInfo(enter.World);
                _world.ClearEntities();
                _castVisuals.Clear();
                _world.SelfKey = enter.EntityKey;
                _inWorld = true;
                _login.Visible = false;
                _hud.InWorld = true;
                _hud.Visible = true;
                _hud.Hero.ObserveServerTime(enter.ServerTime, LocalNow);
                _world.ObserveDay(enter.DayLength, enter.TimeOfDay, enter.ServerTime, entering: true);
                _camera.Follow(Coords.ToGodot(enter.X, enter.Y, enter.Z));
                _hud.Log($"[color=#{_hud.Skin.Notice("quest").ToHtml(false)}]Entered the world at {enter.X:0}, {enter.Y:0} (zone {Hud.Escape(enter.Zone)}).[/color]");
                break;
            case ZoneChangedMsg z:
                _hud.Notice($"{z.ZoneName}{(z.Safe ? ", safe" : "")}", "discovery");
                break;
            case EntitySpawnMsg s:
                _world.Spawn(s.State);
                break;
            case EntityUpdateMsg u:
                _hud.Hero.ObserveServerTime(u.ServerTime, LocalNow);
                foreach (var state in u.States)
                {
                    _world.Update(state);
                }
                break;
            case EntityDeltaMsg d:
                _hud.Hero.ObserveServerTime(d.ServerTime, LocalNow);
                foreach (var delta in d.Deltas)
                {
                    _world.ApplyDelta(delta);
                }
                break;
            case EntityDespawnMsg d:
                _castVisuals.Timeline.RemoveEntity(d.Key);
                _world.Despawn(d.Key);
                break;
            case SelfStateMsg self:
                _hud.ApplySelf(self);
                if (_hud.Hero.HasServerClock)
                {
                    _world.ObserveDay(null, self.TimeOfDay, _hud.Hero.ServerNow(LocalNow));
                }
                break;
            case WeaveSlotsMsg slots:
                _hud.ApplySlots(slots);
                break;
            case InventoryStateMsg inventory:
                InventoryStates++;
                _hud.ApplyInventory(inventory);
                _world.RefreshSelfEquipment();
                break;
            case PassiveStateMsg passives:
                _hud.ApplyPassives(passives);
                break;
            case PageStateMsg pages:
                _hud.ApplyPages(pages);
                break;
            case DialogueStateMsg dialogue:
                DialogueStates++;
                _hud.ShowDialogue(dialogue);
                break;
            case VendorStateMsg vendor:
                VendorStates++;
                _hud.ShowVendor(vendor);
                break;
            case StationStateMsg station:
                _hud.ShowStation(station);
                break;
            case CombatEventMsg c:
                ShowCombat(c);
                break;
            case CastCueMsg cue:
                CastCuesReceived++;
                _hud.Hero.ObserveServerTime(cue.ServerTime, LocalNow);
                _castVisuals.Receive(cue, _hud.Hero.ServerNow(LocalNow));
                break;
            case ChatBroadcastMsg chat:
                _hud.Chat(chat.From, chat.Channel, chat.Text);
                break;
            case NoticeMsg n:
                _hud.Notice(n.Text, n.Level);
                break;
            case QuestOffersMsg q:
                QuestOffers++;
                _hud.ShowQuestOffers(q);
                break;
            case PongMsg pong:
                _rtt = (long)Time.GetTicksMsec() - pong.Nonce;
                break;
            case IntentRejectedMsg rej:
                _lastRejected = $"{rej.Intent}: {rej.Reason}";
                LastRejectedIntent = _lastRejected;
                _hud.Notice(_lastRejected, "error");
                break;
            case ChunkDataMsg chunk:
                _world.ReceiveChunk(chunk);
                break;
            case CraftChallengeMsg cc:
                _hud.Log($"[color=#{_hud.Skin.Notice("event").ToHtml(false)}]Crafting {Hud.Escape(cc.Recipe)}: {Hud.Escape(cc.Type)} challenge ({cc.TimeLimit:0}s)[/color]");
                break;
            case CraftResultMsg cr:
                _hud.Notice(cr.Success ? $"Crafted {cr.Item?.Name} (quality {cr.Quality:0.00})" : $"Craft failed: {cr.Reason}", cr.Success ? "quest" : "error");
                break;
        }
    }

    private void ShowCombat(CombatEventMsg c)
    {
        // every body in view reacts: the struck flinch and flash, the strikers' swings come through
        _world.CombatHit(c);
        if (c.Source != _world.SelfKey && c.Target != _world.SelfKey)
        {
            return;
        }
        var targetView = _world.Entities.GetValueOrDefault(c.Target);
        var source = _world.Entities.GetValueOrDefault(c.Source)?.State.Name ?? "?";
        var target = targetView?.State.Name ?? "?";
        var feel = _appearance.Motion;
        if (c.Target == _world.SelfKey && c.Kind == "damage")
        {
            _camera.Shake(c.Crit ? feel.Camera("shake_crit", 0.25f) : feel.Camera("shake_hit", 0.12f));
        }
        else if (c.Source == _world.SelfKey && c.Kind == "death")
        {
            _camera.Shake(feel.Camera("shake_death", 0.35f));
        }
        var maxLife = targetView?.State.ResourceMax.GetValueOrDefault("life") ?? 0;
        var weight = maxLife > 0 && c.Kind is "damage" or "heal" ? Mathf.Clamp((float)(c.Amount / maxLife) / Mathf.Max(0.01f, feel.CombatText("heavy_fraction", 0.12f)), 0f, 1f) : 0f;
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
        _hud.Log($"[color=#{_hud.Skin.S("ink_muted").ToHtml(false)}]{Hud.Escape(text)}[/color]");
        _hud.CombatText(c.Kind, c.Amount, c.Crit, c.Source == _world.SelfKey, c.Target == _world.SelfKey, weight);
        CombatLines++;
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
        if (!_initialized || _loading.BlocksWorldInput) return;
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
        // The interface's keys come first: Escape closes its panes before it cancels a cast.
        if (_hud.HandleInput(@event))
        {
            GetViewport().SetInputAsHandled();
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
        else if (@event.IsActionPressed("cancel"))
        {
            _net.Send(new CancelCastMsg());
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
            var ground = _settings.Toggle("click_to_move", true) ? _world.RayToGround(origin, direction) : null;
            if (ground is { } g)
            {
                _net.Send(new MoveToMsg(g.X, g.Y, g.Z));
                _lastMoveDir = Vector2.Zero;
            }
        }
    }

    private (Vector3 Origin, Vector3 Direction) MouseRay()
    {
        var mouse = SmokePointer ?? GetViewport().GetMousePosition();
        return (_camera.Camera.ProjectRayOrigin(mouse), _camera.Camera.ProjectRayNormal(mouse));
    }

    private void UpdateHover()
    {
        var (origin, direction) = MouseRay();
        _hovered = _world.PickEntity(origin, direction);
        _hoveredKey = _hovered?.Key ?? 0;
        EntityView.HoveredKey = _hoveredKey;
        _hud.SetTarget(_hovered?.State);
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
            LastInteracted = $"{best.State.Name} ({best.State.Interactable}) at {bestDist:0.0}";
            _net.Send(kind == "pick_up" ? new PickUpMsg(best.Key) : new InteractMsg(best.Key));
        }
        else
        {
            LastInteracted = "nothing within reach";
        }
    }

    internal string LastInteracted { get; private set; } = "";

    public override void _ExitTree() => _net.Dispose();
}
