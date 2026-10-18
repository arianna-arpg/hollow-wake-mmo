using Godot;
using HollowWake.Client.World;
using HollowWake.Protocol;

namespace HollowWake.Client.Dev;

/// <summary>
/// A scripted play-through for checking the client against a running server with no hand on the
/// keyboard: connect, enter the world, then exercise held movement, jumping, click-to-move, the
/// quest giver, both cameras, the sheet and weaves on a hob. Input goes through the same input map
/// and handlers a player uses, and screenshots are saved along the way. Start it with user arguments:
/// <c>godot --path src/HollowWake.Client -- --smoke [--shots DIR] [--account NAME] [--host H] [--port P]</c>.
/// Prints one SMOKE PASS or FAIL line per check and exits non-zero when any check fails.
/// </summary>
public partial class Smoke : Node
{
    private readonly Main _main;
    private readonly string[] _args;
    private readonly List<(string Name, bool Pass, string Note)> _results = new();
    private string _shots = "";
    private string _account = "smoke";
    private string _host = "127.0.0.1";
    private int _port = 7777;
    private double _started;
    private System.Numerics.Vector3 _lastPos;

    public Smoke(Main main, string[] args)
    {
        _main = main;
        _args = args;
    }

    public override void _Ready()
    {
        for (var i = 0; i + 1 < _args.Length; i++)
        {
            switch (_args[i])
            {
                case "--shots":
                    _shots = _args[i + 1];
                    break;
                case "--account":
                    _account = _args[i + 1];
                    break;
                case "--host":
                    _host = _args[i + 1];
                    break;
                case "--port":
                    _port = int.TryParse(_args[i + 1], out var p) ? p : _port;
                    break;
            }
        }
        _started = Now;
        // A headless window never receives the operating system's mouse-enter notification.
        // Without it GUI clicks fall through to world input even when their coordinates hit a control.
        if (DisplayServer.GetName() == "headless")
        {
            GetWindow().Size = new Vector2I(
                (int)ProjectSettings.GetSetting("display/window/size/viewport_width"),
                (int)ProjectSettings.GetSetting("display/window/size/viewport_height"));
            GetViewport().NotifyMouseEntered();
        }
        GD.Print($"SMOKE viewport {GetViewport().GetVisibleRect()}, window {GetWindow().Size}");
        _ = Run();
    }

    private static double Now => Time.GetTicksMsec() / 1000.0;

    private EntityView? Self => _main.World.Self;

    private async Task Run()
    {
        try
        {
            await Script();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"SMOKE CRASH {ex}");
            Record("script", false, ex.Message);
        }
        var passed = _results.Count(r => r.Pass);
        GD.Print("SMOKE SUMMARY");
        foreach (var (name, pass, note) in _results)
        {
            GD.Print($"SMOKE {(pass ? "PASS" : "FAIL")} {name}: {note}");
        }
        GD.Print($"SMOKE DONE {passed}/{_results.Count} passed in {Now - _started:0.0}s; {Engine.GetFramesPerSecond()} fps at the end with {_main.World.BuiltChunks} chunks and {_main.World.Entities.Count} entities");
        await Frame();
        GetTree().Quit(passed == _results.Count ? 0 : 1);
    }

    private void Record(string name, bool pass, string note)
    {
        _results.Add((name, pass, note));
        GD.Print($"SMOKE {(pass ? "ok  " : "FAIL")} {name}: {note}");
    }

    private async Task Script()
    {
        var login = _main.Login;
        var lists = _main.CharacterLists;
        login.ConnectTo(_host, _port, _account);
        var connected = await Until(() => _main.CharacterLists > lists, 20);
        Record("connect and log in", connected, login.Status);
        if (!connected)
        {
            return;
        }

        var character = login.Characters.FirstOrDefault(c => c.Name == "Smoke");
        if (character is null)
        {
            lists = _main.CharacterLists;
            login.Create("Smoke", Arg("--calling") ?? login.Callings.FirstOrDefault() ?? "holdfast");
            await Until(() => _main.CharacterLists > lists, 20);
            character = login.Characters.FirstOrDefault(c => c.Name == "Smoke");
            Record("create character", character is not null, character is null ? login.Status : $"{character.Name} the {character.Calling}");
            if (character is null)
            {
                return;
            }
        }
        login.Enter(character.Id);
        var entered = await Until(() => _main.InWorld && Self is not null, 20);
        Record("enter world", entered, entered ? $"{Self!.State.Name} at {Fmt(Pos())}" : login.Status);
        if (!entered)
        {
            return;
        }

        var t0 = Now;
        var built = await Until(() => _main.World.BuiltChunks > 0 && _main.World.PendingChunks == 0, 90);
        Record("terrain built", built, $"{_main.World.BuiltChunks} chunks in {Now - t0:0.0}s, {_main.World.Entities.Count} entities in view, {Engine.GetFramesPerSecond()} fps");
        var ready = await Until(() => !_main.Loading.BlocksWorldInput, 20);
        Record("world input ready", ready, $"loading={_main.Loading.Active}, blocked={_main.Loading.BlocksWorldInput}, chat={_main.Hud.ChatFocused}");
        if (!ready) return;
        await Wait(1.0);
        await Shot("01_isometric.png");
        await LookStages();

        // Held movement: W for a moment, then see where the server put us.
        var p0 = Pos();
        Input.ActionPress("move_forward");
        await Wait(1.5);
        Input.ActionRelease("move_forward");
        await Wait(0.6);
        var walked = Flat(Pos(), p0);
        Record("wasd move", walked > 1f, $"moved {walked:0.0} units{Rejected()}");

        // Jump: watch our height for a lift.
        var z0 = Pos().Z;
        var zMax = z0;
        Tap(Key.Space);
        var until = Now + 1.5;
        while (Now < until)
        {
            zMax = Mathf.Max(zMax, Pos().Z);
            await Frame();
        }
        Record("jump", zMax - z0 > 0.15f, $"rose {zMax - z0:0.00} units{Rejected()}");

        // Click-to-move: click the ground a few units away, in whichever direction is open.
        var moved = false;
        var tried = "";
        foreach (var (dx, dy) in new[] { (5f, 0f), (0f, 5f), (-5f, 0f), (0f, -5f) })
        {
            var from = Pos();
            var target = Coords.ToGodot(from.X + dx, from.Y + dy, _main.World.HeightAt(from.X + dx, from.Y + dy));
            await Click(Screen(target));
            await Wait(2.5);
            tried += $"({dx:0},{dy:0}) {Flat(Pos(), from):0.0}  ";
            if (Flat(Pos(), from) > 2f)
            {
                moved = true;
                break;
            }
        }
        Record("click to move", moved, $"tried {tried.Trim()}{Rejected()}");

        // Quest giver: walk up to the quartermaster and press F.
        var quartermaster = Nearest(e => e.State.Pattern == "quartermaster" || e.State.Name == "Quartermaster");
        if (quartermaster is null)
        {
            Record("quartermaster offers quests", false, "no quartermaster in view");
        }
        else
        {
            var reached = await TravelTo(() => (quartermaster.State.X, quartermaster.State.Y), 1.4f, 45);
            await Wait(0.4);
            var offers = _main.QuestOffers;
            Tap(Key.F);
            var shown = await Until(() => _main.QuestOffers > offers && _main.Hud.QuestsVisible, 6);
            var how = $"F interacted with {_main.LastInteracted}";
            if (!shown)
            {
                // A villager wandered nearer and F took it: close what it opened and click the quartermaster.
                await CloseEverything();
                await Hover(quartermaster);
                await Click(Screen(quartermaster.GlobalPosition + new Vector3(0, quartermaster.State.Height * 0.5f, 0)));
                shown = await Until(() => _main.QuestOffers > offers && _main.Hud.QuestsVisible, 8);
                how += ", then clicked the quartermaster";
            }
            Record("quartermaster offers quests", reached && shown, $"reached={reached} at {Flat(Pos(), new(quartermaster.State.X, quartermaster.State.Y, 0)):0.0} units, {how}, panel shown={shown}{Rejected()}");
            await Shot("02_quest_offer.png");
            Tap(Key.Escape);
            await Wait(0.3);
        }

        // Cameras: V toggles, Q and E rotate.
        var mode0 = _main.Rig.Mode;
        Tap(Key.V);
        await Wait(0.6);
        var toggled = _main.Rig.Mode != mode0;
        await Shot("03_third_person.png");
        var yaw0 = _main.Rig.Yaw;
        Tap(Key.Q);
        await Wait(0.2);
        var rotated = Mathf.Abs(_main.Rig.Yaw - yaw0) > 0.01f;
        Tap(Key.E);
        await Wait(0.2);
        var rotatedBack = Mathf.Abs(_main.Rig.Yaw - yaw0) < 0.01f;
        Tap(Key.V);
        await Wait(0.4);
        Record("camera toggle", toggled && _main.Rig.Mode == mode0, $"{mode0} -> {(toggled ? "switched" : "unchanged")} -> {_main.Rig.Mode}");
        Record("camera rotate", rotated && rotatedBack, $"Q turned {(rotated ? "yes" : "no")}, E turned back {(rotatedBack ? "yes" : "no")}");

        // Sheet: C opens and closes.
        Tap(Key.C);
        await Wait(0.3);
        var sheetOpen = _main.Hud.SheetVisible;
        await Shot("04_sheet.png");
        Tap(Key.C);
        await Wait(0.3);
        Record("character sheet", sheetOpen && !_main.Hud.SheetVisible, $"opened={sheetOpen} closed={!_main.Hud.SheetVisible}");

        await CorePaneAndPages();
        await TalkToAVillager();
        await TradeWithAVendor();
        await OptionsSteps();

        if (_args.Contains("--cast-cues")) await CastCueSequence();

        // Weaves: walk to the nearest hob, hover it and press 1 to 4 until a combat line shows.
        var hob = Nearest(e => e.State.Pattern is not null && e.State.Pattern.StartsWith("hob", StringComparison.Ordinal) && !e.State.Dead);
        if (hob is null)
        {
            Record("weaves on a hob", false, $"no hob among {_main.World.Entities.Count} entities in view");
        }
        else
        {
            var reached = await TravelTo(() => (hob.State.X, hob.State.Y), 2.2f, 60);
            var lines0 = _main.CombatLines;
            var hovered = false;
            var used = "";
            for (var round = 0; round < 3 && _main.CombatLines == lines0; round++)
            {
                for (var slot = 1; slot <= 4 && _main.CombatLines == lines0; slot++)
                {
                    await Hover(hob);
                    hovered |= _main.HoveredKey != 0;
                    Tap(Key.Key0 + slot);
                    used += slot;
                    await Wait(1.0);
                }
            }
            var fought = await Until(() => _main.CombatLines > lines0, 6);
            Record("weaves on a hob", fought, $"reached={reached} hovered={hovered} slots {used} combat lines={_main.CombatLines - lines0}{Rejected()}");
            await Shot("05_combat.png");
            if (Self?.State.Dead == true)
            {
                Tap(Key.R);
                await Wait(1.0);
            }
        }
        await Wait(0.5);
    }

    // ------------------------------------------------------------ the look

    /// <summary>
    /// The look's stages on the busiest scene, the hamlet on entry: the measurement line for painted geometry and
    /// for directional cards (frame times, draw calls, primitives, texture memory, atlas bytes), then the
    /// readability checks of the visual language (grayscale, reduced motion) and the day at night and evening.
    /// </summary>
    private async Task LookStages()
    {
        var world = _main.World;
        var settings = _main.Settings;
        if (DisplayServer.GetName() == "headless")
        {
            GD.Print("SMOKE SKIP appearance rendering: headless display; geometry/card performance and readability require graphical evidence");
        }
        else
        {
            var geometry = await Measure("geometry");
            Record("bodies as geometry", geometry.Bodies > 0 && geometry.P95 < 50, geometry.Line);
            settings.Set("shadows", System.Text.Json.Nodes.JsonValue.Create(false));
            await Wait(0.3);
            var unshadowed = await Measure("geometry, no shadows");
            settings.Set("shadows", System.Text.Json.Nodes.JsonValue.Create(true));
            Record("bodies without shadows", unshadowed.Bodies > 0, unshadowed.Line);
            settings.Set("bodies", System.Text.Json.Nodes.JsonValue.Create("cards"));
            var baked = await Until(() => world.Appearance?.Baker.Pending == 0 && world.BodyCounts.Cards > 0, 20);
            await Wait(0.5);
            var cards = await Measure("cards");
            await Shot("16_cards.png");
            Record("bodies as cards", baked && cards.Cards > 0 && cards.P95 < 50, cards.Line);
            settings.Set("bodies", System.Text.Json.Nodes.JsonValue.Create("auto"));
            await Wait(0.3);
            settings.Set("grayscale", System.Text.Json.Nodes.JsonValue.Create(true));
            await Wait(0.3);
            await Shot("17_grayscale.png");
            settings.Set("grayscale", System.Text.Json.Nodes.JsonValue.Create(false));
            settings.Set("reduce_motion", System.Text.Json.Nodes.JsonValue.Create(true));
            await Wait(0.3);
            await Shot("18_reduced_motion.png");
            settings.Set("reduce_motion", System.Text.Json.Nodes.JsonValue.Create(false));
        }
        settings.Set("day_lock", System.Text.Json.Nodes.JsonValue.Create(true));
        settings.Set("day_time", System.Text.Json.Nodes.JsonValue.Create(1.0));
        await Wait(0.4);
        var night = world.TimeOfDay;
        await Shot("19_night.png");
        settings.Set("day_time", System.Text.Json.Nodes.JsonValue.Create(18.5));
        await Wait(0.4);
        var evening = world.TimeOfDay;
        await Shot("20_evening.png");
        settings.Set("day_lock", System.Text.Json.Nodes.JsonValue.Create(false));
        await Wait(0.3);
        Record("day cycle", Math.Abs(night - 1.0 / 24) < 0.01 && Math.Abs(evening - 18.5 / 24) < 0.01, $"night at {night:0.00} of the day, evening at {evening:0.00}, the server's day at {world.TimeOfDay:0.00}");
    }

    private sealed record Measurement(string Line, double P95, int Bodies, int Cards);

    /// <summary>Samples two seconds of frames: frame time percentiles, draw calls, primitives, objects, texture memory and the bodies drawn.</summary>
    private async Task<Measurement> Measure(string mode)
    {
        var frames = new List<double>();
        var t0 = Now;
        var last = t0;
        while (Now - t0 < 2.0)
        {
            await Frame();
            var now = Now;
            frames.Add((now - last) * 1000);
            last = now;
        }
        frames.Sort();
        double At(double q) => frames.Count == 0 ? 0 : frames[Math.Min(frames.Count - 1, (int)(q * frames.Count))];
        var draws = Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
        var primitives = Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame);
        var objects = Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame);
        var textureBytes = Performance.GetMonitor(Performance.Monitor.RenderTextureMemUsed);
        var videoBytes = Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed);
        var world = _main.World;
        var (triangles, cards, bodies) = world.BodyCounts;
        var atlasBytes = world.Appearance?.AtlasBytes ?? 0;
        var atlases = world.Appearance?.AtlasCount ?? 0;
        var line = $"{mode}: {frames.Count / 2.0:0} fps, frame ms p50 {At(0.5):0.00} p95 {At(0.95):0.00} max {frames[^1]:0.00}; {draws:0} draw calls, {primitives:0} primitives, {objects:0} objects; "
            + $"{bodies} bodies ({cards} cards, {triangles} body triangles), {atlases} atlases {atlasBytes / 1024.0 / 1024.0:0.00} MiB; texture memory {textureBytes / 1024.0 / 1024.0:0.0} MiB, video memory {videoBytes / 1024.0 / 1024.0:0.0} MiB; {world.Entities.Count} entities, {world.BuiltChunks} chunks";
        GD.Print($"SMOKE measure {line}");
        return new Measurement(line, At(0.95), bodies, cards);
    }

    // ------------------------------------------------------------ the interface

    private string? Arg(string key)
    {
        var i = Array.IndexOf(_args, key);
        return i >= 0 && i + 1 < _args.Length ? _args[i + 1] : null;
    }

    private async Task CastCueSequence()
    {
        var phases = new HashSet<string>();
        // Real moving targets can evade a projectile. Retry ordinary input; never synthesize a contact.
        for (var attempt = 0; attempt < 3 && !phases.Contains("impact"); attempt++)
        {
            var hob = Nearest(e => e.State.Pattern?.StartsWith("hob", StringComparison.Ordinal) == true && !e.State.Dead);
            if (hob is null) break;
            await TravelTo(() => (hob.State.X, hob.State.Y), 3f, 45);
            await Hover(hob);
            Tap(Key.Key1);
            var until = Now + 3;
            while (Now < until)
            {
                foreach (var cue in _main.CastVisuals.Timeline.Active.ToArray())
                {
                    if (cue.Event.Source == Self?.Key && phases.Add(cue.Phase)) await Shot($"cast_cue_{cue.Phase}.png");
                }
                await Frame();
            }
        }
        Record("Cast cue presentation", phases.Contains("start") && phases.Contains("release") && phases.Contains("impact"),
            $"phases={string.Join(',', phases)}; received={_main.CastCuesReceived}");
    }

    /// <summary>The core pane opens with its key; every ribbon page the hero has opens from its ribbon on the rail.</summary>
    private async Task CorePaneAndPages()
    {
        var hud = _main.Hud;
        await Until(() => hud.Hero.Inventory is not null || !hud.Hero.Interface, 5);
        TapAction(hud.ActionOf("inventory"));
        await Wait(0.5);
        var open = hud.CoreOpen;
        var bag = hud.Hero.Bag(10);
        await Shot("06_core_pane.png");
        Record("core pane", open && bag is not null && hud.Rects is not null,
            $"open={open}, bag {bag?.Items.Count ?? 0} items on {bag?.Width ?? 0}x{bag?.Height ?? 0}, {hud.Hero.Inventory?.Slots.Count ?? 0} doll slots, wallet {hud.Hero.Inventory?.Wallet.Count ?? 0}, interface={hud.Hero.Interface}");
        var pages = hud.RibbonPages.ToList();
        var visited = new List<string>();
        foreach (var page in pages)
        {
            var at = hud.RibbonCenter(page);
            if (at is { } p)
            {
                await Click(p);
            }
            else
            {
                TapAction(hud.ActionOf(page));
            }
            var shown = await Until(() => hud.IsShown(page), 3);
            await Wait(0.4);
            await Shot($"07_page_{page}.png");
            visited.Add($"{page}={(shown ? "shown" : "missing")}");
        }
        Record("ribbon pages", pages.Count > 0 && visited.All(v => v.EndsWith("=shown", StringComparison.Ordinal)), string.Join(", ", visited) + Rejected());
        // Escape closes the pages, then the core pane.
        for (var i = 0; i < pages.Count + 2 && hud.CoreOpen; i++)
        {
            await EscapeAndWait();
        }
        Record("escape closes the core pane last", !hud.CoreOpen, $"core open={hud.CoreOpen}");
    }

    /// <summary>Walks to the nearest villager, talks, and takes the first open choice.</summary>
    private async Task TalkToAVillager()
    {
        var villager = Nearest(e => e.State.Pattern == "villager" || e.State.Interactable == "talk");
        if (villager is null)
        {
            Record("talk to a villager", false, "no villager in view");
            return;
        }
        var reached = await TravelTo(() => (villager.State.X, villager.State.Y), 1.0f, 45);
        await Wait(0.3);
        await CloseEverything();
        var states = _main.DialogueStates;
        bool Talking() => _main.DialogueStates > states && _main.Hud.FrontView<UI.WorldPanes.DialoguePane>()?.State?.Source == villager.Key;
        // Beside the villager, F talks to it; when someone stands nearer, click the villager instead.
        Tap(Key.F);
        var shown = await Until(Talking, 4);
        if (!shown)
        {
            await CloseEverything();
            await Hover(villager);
            await Click(Screen(villager.GlobalPosition + new Vector3(0, villager.State.Height * 0.5f, 0)));
            shown = await Until(Talking, 6);
        }
        await Shot("08_dialogue.png");
        var pane = _main.Hud.FrontView<UI.WorldPanes.DialoguePane>();
        var line = pane?.State is { } s ? $"{s.Speaker}: \"{Trim(s.Line)}\" with {s.Choices.Count} choices" : "no dialogue_state arrived";
        if (shown && pane?.State is { } state && state.Choices.FirstOrDefault(c => c.Enabled) is { } choice)
        {
            var before = _main.DialogueStates;
            var button = pane.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => !b.Disabled);
            if (button is not null)
            {
                await Click(button.GetGlobalRect().GetCenter());
                var answered = await Until(() => _main.DialogueStates > before, 5);
                line += $", chose \"{Trim(choice.Text)}\" (answered={answered})";
            }
        }
        Record("talk to a villager", reached && shown, $"reached={reached}, {line}{Rejected()}");
        if (_main.Hud.HasWorldPane("dialogue"))
        {
            Tap(Key.Escape);
            await Wait(0.3);
        }
    }

    /// <summary>
    /// Opens a vendor (one in view, else the quartermaster's, asked through its conversation), sees its shelf,
    /// and buys the first thing the hero can afford when there is one.
    /// </summary>
    private async Task TradeWithAVendor()
    {
        var hud = _main.Hud;
        if (!hud.HasWorldPane("vendor"))
        {
            var vendor = Nearest(e => e.State.Interactable == "vendor" || e.State.Pattern?.Contains("vendor", StringComparison.Ordinal) == true || e.State.Pattern?.Contains("merchant", StringComparison.Ordinal) == true)
                ?? Nearest(e => e.State.Pattern == "quartermaster" || e.State.Name == "Quartermaster");
            if (vendor is null)
            {
                Record("trade with a vendor", false, $"no vendor or quartermaster in view{Rejected()}");
                return;
            }
            await TravelTo(() => (vendor.State.X, vendor.State.Y), 1.4f, 45);
            await Wait(0.4);
            var states = _main.VendorStates;
            var talks = _main.DialogueStates;
            await Click(Screen(vendor.GlobalPosition + new Vector3(0, vendor.State.Height * 0.5f, 0)));
            await Until(() => (_main.VendorStates > states && hud.HasWorldPane("vendor")) || (_main.DialogueStates > talks && hud.HasWorldPane("dialogue")), 6);
            // A trader that talks first: bring its conversation to the front and ask to trade, as a player would.
            if (!hud.HasWorldPane("vendor") && hud.FrontView<UI.WorldPanes.DialoguePane>() is { State: { } talk })
            {
                hud.Workspace.SelectTab($"dialogue:{talk.Source}");
                await Wait(0.3);
                // Layout/restyling or a server close may replace the view during the await.
                var dialogue = hud.FrontView<UI.WorldPanes.DialoguePane>();
                var current = dialogue?.State;
                var index = current?.Source == vendor.Key ? current.Choices.FindIndex(c => c.Id == "trade" && c.Enabled) : -1;
                var button = index < 0 || !GodotObject.IsInstanceValid(dialogue) ? null : dialogue!.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.Text.StartsWith($"{index + 1}. ", StringComparison.Ordinal));
                if (button is not null)
                {
                    await Click(ScreenCenter(button));
                }
                await Until(() => _main.VendorStates > states && hud.HasWorldPane("vendor"), 6);
            }
        }
        var pane = hud.FrontView<UI.WorldPanes.VendorPane>();
        if (pane?.State is { } shown)
        {
            // The shelf arrives as a fresh tab behind the conversation that opened it: look at it.
            hud.Workspace.SelectTab($"vendor:{shown.Source}");
        }
        await Wait(0.5);
        await Shot("09_vendor.png");
        if (pane?.State is not { } state)
        {
            Record("trade with a vendor", false, $"no vendor_state arrived{Rejected()}");
            return;
        }
        var items0 = hud.BagItems;
        var inventories = _main.InventoryStates;
        var buy = pane.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => !b.Disabled && b.Text == "Buy");
        var bought = "nothing the hero can afford";
        if (buy is not null)
        {
            await Click(ScreenCenter(buy));
            bought = await Until(() => _main.InventoryStates > inventories && hud.BagItems > items0, 6) ? $"bought one, bag {items0} -> {hud.BagItems}" : $"asked to buy{Rejected()}";
            await Shot("10_bought.png");
        }
        Record("trade with a vendor", state.Stock.Count > 0, $"{state.Name}: {state.Stock.Count} on the shelf, {bought}");
        for (var i = 0; i < 6 && (hud.CoreOpen || hud.HasWorldPane("vendor") || hud.HasWorldPane("dialogue")); i++)
        {
            Tap(Key.Escape);
            await Wait(0.2);
        }
    }

    /// <summary>
    /// Options: open it, switch the theme and see the cluster recolour, rebind a key and use it, change the
    /// interface scale and see the panes refit, and reset a group, all through the pane's own controls.
    /// </summary>
    private async Task OptionsSteps()
    {
        var hud = _main.Hud;
        TapAction("page_options");
        var open = await Until(() => hud.OptionsOpen && hud.FrontView<UI.Pages.OptionsPane>() is not null, 3);
        await Wait(0.4);
        await Shot("11_options.png");
        Record("options open", open, $"open={open}, page {hud.FrontView<UI.Pages.OptionsPane>()?.SelectedPage}, {hud.FrontView<UI.Pages.OptionsPane>()?.RowCount} rows");
        if (!open)
        {
            return;
        }

        // The theme: pick the other direction in the theme row's list; the cluster redraws in it.
        var rendered = DisplayServer.GetName() != "headless";
        var before = rendered ? SamplePixel(hud.ClusterRect) : "not rendered";
        var themeRow = Pane()?.RowOf("theme");
        var picker = themeRow?.FindChildren("*", "OptionButton", true, false).OfType<OptionButton>().FirstOrDefault();
        var other = 0;
        if (picker is not null)
        {
            for (var i = 0; i < picker.ItemCount; i++)
            {
                if (picker.GetItemText(i) != picker.GetItemText(picker.Selected))
                {
                    other = i;
                }
            }
            picker.Select(other);
            picker.EmitSignal(OptionButton.SignalName.ItemSelected, other);
        }
        await Wait(0.6);
        var themed = _main.Hud.Skin.T.Id;
        var after = rendered ? SamplePixel(_main.Hud.ClusterRect) : "not rendered";
        await Shot("12_theme.png");
        Record("options switch the theme", picker is not null && themed != "etched_stone" && (!rendered || before != after),
            $"theme {themed}, cluster pixel {before} -> {after}");
        if (!rendered)
        {
            GD.Print("SMOKE SKIP theme rendered pixels: headless display; theme state checked above");
        }

        // A key: find the camera switch by searching, rebind it to B through its row, use it, and see the camera turn.
        await ShowOptionsPage("controls");
        await SearchOptions("Switch camera");
        var camRow = Pane()?.RowOf("key.camera_toggle");
        var primary = camRow?.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.Text == "V");
        var rebound = false;
        if (primary is not null)
        {
            await Click(ScreenCenter(primary));
            await Wait(0.2);
            Tap(Key.B);
            await Wait(0.3);
            rebound = _main.Settings.Bindings("camera_toggle").Primary == "B";
        }
        await Shot("13_rebound.png");
        // Done keeps the changes and closes the pane; the new key works in the world.
        await CloseOptionsByDone();
        var mode0 = _main.Rig.Mode;
        Tap(Key.B);
        await Wait(0.5);
        var turned = _main.Rig.Mode != mode0;
        Tap(Key.B);
        await Wait(0.4);
        Record("options rebind a key", rebound && turned, $"camera switch on B={rebound}, B turned the camera={turned}");

        // The interface scale: larger, with the core pane open, and the panes refit above the cluster.
        TapAction("page_options");
        await Until(() => hud.OptionsOpen, 3);
        TapAction("page_inventory");
        await Wait(0.3);
        await ShowOptionsPage("interface");
        var scaleRow = Pane()?.RowOf("ui_scale");
        var slider = scaleRow?.FindChildren("*", "HSlider", true, false).OfType<HSlider>().FirstOrDefault();
        var clusterBefore = hud.ClusterRect.W * hud.UiScale;
        slider?.SetValue(1.25);
        await Wait(0.6);
        var clusterAfter = hud.ClusterRect.W * hud.UiScale;
        var fits = hud.Rects is { } r && r.Core.Bottom <= hud.ClusterRect.Y + 0.5f && r.Core.Right <= hud.ClusterRect.X + 1e5f;
        await Shot("14_scale.png");
        Record("options scale the interface", Math.Abs(hud.UiScale - 1.25f) < 0.01f && clusterAfter > clusterBefore + 10 && fits,
            $"scale {hud.UiScale:0.00}, cluster {clusterBefore:0} -> {clusterAfter:0} px wide, panes above the cluster={fits}");

        // Reset the group: the theme and the scale return to their defaults.
        var lookReset = Pane()?.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.Text == "Reset group");
        if (lookReset is not null)
        {
            await Click(ScreenCenter(lookReset));
        }
        await Wait(0.6);
        var reset = _main.Settings.Text("theme") == "etched_stone" && Math.Abs(_main.Settings.Number("ui_scale") - 1) < 1e-6;
        await Shot("15_reset.png");
        Record("options reset a group", lookReset is not null && reset, $"theme {_main.Settings.Text("theme")}, scale {_main.Settings.Number("ui_scale"):0.00}");
        // Leave the key as it was for the steps that follow, and close everything.
        _main.Settings.Reset(UI.Logic.OptionsCatalog.KeyEntryId("camera_toggle"));
        await CloseOptionsByDone();
        for (var i = 0; i < 6 && hud.CoreOpen; i++)
        {
            Tap(Key.Escape);
            await Wait(0.15);
        }
    }

    private UI.Pages.OptionsPane? Pane() => _main.Hud.FrontView<UI.Pages.OptionsPane>();

    /// <summary>Presses Escape until the core pane and every pane are closed (with nothing open it would open Options).</summary>
    private async Task CloseEverything()
    {
        var hud = _main.Hud;
        for (var i = 0; i < 8 && (hud.CoreOpen || hud.Workspace.Book.Leaves.Count > 0); i++)
        {
            await EscapeAndWait();
        }
    }

    private string WorkspaceState()
    {
        var hud = _main.Hud;
        return $"core={hud.CoreOpen}, tray={hud.Workspace.TrayOpen}, front={hud.Workspace.Book.Active()?.Id}, leaves={string.Join(',', hud.Workspace.Book.Leaves.Select(l => l.Id))}";
    }

    private async Task EscapeAndWait()
    {
        var before = WorkspaceState();
        Tap(Key.Escape);
        if (!await Until(() => WorkspaceState() != before, 2))
            GD.Print($"SMOKE escape unchanged: {before}; loading={_main.Loading.BlocksWorldInput}, focus={GetViewport().GuiGetFocusOwner()?.GetType().Name}, chat={_main.Hud.ChatFocused}");
        await Wait(0.15);
    }

    /// <summary>Types into the Options search field; the rows narrow to the matching entries of every page.</summary>
    private async Task SearchOptions(string text)
    {
        var field = Pane()?.FindChildren("*", "LineEdit", true, false).OfType<LineEdit>().FirstOrDefault(l => l.PlaceholderText.StartsWith("Search", StringComparison.Ordinal));
        if (field is null)
        {
            return;
        }
        await Click(ScreenCenter(field));
        field.Text = text;
        field.EmitSignal(LineEdit.SignalName.TextChanged, text);
        await Wait(0.3);
    }

    /// <summary>Picks an Options page through its button in the pane's page list, as a player would.</summary>
    private async Task ShowOptionsPage(string id)
    {
        if (Pane() is not { } pane || pane.SelectedPage == id)
        {
            return;
        }
        var name = _main.Settings.Catalog.Pages.FirstOrDefault(p => p.Id == id)?.Name;
        var button = pane.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.Text == name);
        if (button is not null)
        {
            await Click(ScreenCenter(button));
        }
        await Until(() => Pane()?.SelectedPage == id, 2);
        await Wait(0.3);
    }

    private async Task CloseOptionsByDone()
    {
        var done = Pane()?.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.Text == "Done");
        if (done is not null)
        {
            await Click(ScreenCenter(done));
        }
        await Until(() => !_main.Hud.OptionsOpen, 3);
    }

    /// <summary>A control's middle in viewport pixels, through the interface's scale.</summary>
    private static Vector2 ScreenCenter(Control control) => control.GetGlobalTransformWithCanvas() * (control.Size / 2);

    /// <summary>The colour of the weave bar's frame in the drawn frame, to see the cluster recolour.</summary>
    private string SamplePixel(UI.Logic.Box cluster)
    {
        var image = GetViewport().GetTexture().GetImage();
        var scale = _main.Hud.UiScale;
        var x = (int)((cluster.X + cluster.W / 2) * scale);
        var y = (int)((cluster.Y + cluster.H - 4) * scale);
        x = Math.Clamp(x, 0, image.GetWidth() - 1);
        y = Math.Clamp(y, 0, image.GetHeight() - 1);
        return image.GetPixel(x, y).ToHtml(false);
    }

    private void TapAction(string action) => TapName(_main.Settings.Bindings(action).Primary);

    private static string Trim(string text) => text.Length > 48 ? text[..48] + "..." : text;

    /// <summary>Presses a key written as content writes keys (I, K, Shift+Tab).</summary>
    private static void TapName(string keyName)
    {
        var name = UI.Logic.KeyName.Parse(keyName);
        if (name.IsEmpty)
        {
            return;
        }
        var key = OS.FindKeycodeFromString(name.Key);
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true, ShiftPressed = name.Shift });
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false, ShiftPressed = name.Shift });
    }

    // ------------------------------------------------------------ helpers

    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private async Task<bool> Until(Func<bool> condition, double timeout)
    {
        var deadline = Now + timeout;
        while (Now < deadline)
        {
            if (condition())
            {
                return true;
            }
            await Frame();
        }
        return condition();
    }

    /// <summary>Presses and releases a key through the input map, as the keyboard would.</summary>
    private static void Tap(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
    }

    /// <summary>Moves the client's virtual pointer without touching the operating system cursor.</summary>
    private void Point(Vector2 screen)
    {
        var previous = _main.SmokePointer ?? GetViewport().GetMousePosition();
        _main.SmokePointer = screen;
        Input.ParseInputEvent(new InputEventMouseMotion
        {
            Position = screen,
            GlobalPosition = screen,
            Relative = screen - previous,
        });
    }

    /// <summary>Points and clicks inside the client, including with the headless display driver.</summary>
    private async Task Click(Vector2 screen)
    {
        Point(screen);
        await Frame();
        await Frame();
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = screen, GlobalPosition = screen });
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = screen, GlobalPosition = screen });
    }

    private async Task Hover(EntityView view)
    {
        Point(Screen(view.GlobalPosition + new Vector3(0, view.State.Height * 0.5f, 0)));
        await Frame();
        await Frame();
    }

    private Vector2 Screen(Vector3 godotPoint) => _main.Rig.Camera.UnprojectPosition(godotPoint);

    private async Task Shot(string name)
    {
        if (_shots.Length == 0 || DisplayServer.GetName() == "headless")
        {
            return;
        }
        await Frame();
        await Frame();
        Directory.CreateDirectory(_shots);
        var path = Path.Combine(_shots, name);
        var err = GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"SMOKE shot {path}: {err}");
    }

    private System.Numerics.Vector3 Pos()
    {
        var s = Self;
        if (s is not null)
        {
            _lastPos = new System.Numerics.Vector3(s.State.X, s.State.Y, s.State.Z);
        }
        return _lastPos;
    }

    private static float Flat(System.Numerics.Vector3 a, System.Numerics.Vector3 b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static string Fmt(System.Numerics.Vector3 p) => $"{p.X:0.0}, {p.Y:0.0}, {p.Z:0.0}";

    private string Rejected()
    {
        var r = _main.TakeRejected();
        return r.Length == 0 ? "" : $" (rejected: {r})";
    }

    private EntityView? Nearest(Func<EntityView, bool> where)
    {
        var self = Self;
        if (self is null)
        {
            return null;
        }
        return _main.World.Entities.Values
            .Where(e => e.Key != self.Key && where(e))
            .OrderBy(e => Flat(new(e.State.X, e.State.Y, 0), Pos()))
            .FirstOrDefault();
    }

    /// <summary>Sends move-to toward a (moving) target until we are within reach or time runs out.</summary>
    private async Task<bool> TravelTo(Func<(float X, float Y)> target, float stopWithin, double timeout)
    {
        var deadline = Now + timeout;
        var lastSent = -10.0;
        while (Now < deadline)
        {
            if (Self is null)
            {
                return false;
            }
            var (x, y) = target();
            if (Flat(Pos(), new(x, y, 0)) <= stopWithin)
            {
                _main.Net.Send(new StopMsg());
                return true;
            }
            if (Now - lastSent > 1.5)
            {
                lastSent = Now;
                _main.Net.Send(new MoveToMsg(x, y, _main.World.HeightAt(x, y)));
            }
            await Frame();
        }
        return false;
    }
}
