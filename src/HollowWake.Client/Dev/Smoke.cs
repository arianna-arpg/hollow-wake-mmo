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
            login.Create("Smoke", login.Callings.FirstOrDefault() ?? "holdfast");
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
        await Wait(1.0);
        await Shot("01_isometric.png");

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
            var shown = await Until(() => _main.QuestOffers > offers && _main.Hud.QuestsVisible, 10);
            Record("quartermaster offers quests", reached && shown, $"reached={reached} at {Flat(Pos(), new(quartermaster.State.X, quartermaster.State.Y, 0)):0.0} units, F interacted with {_main.LastInteracted}, panel shown={shown}{Rejected()}");
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

    // ------------------------------------------------------------ the interface

    /// <summary>The core pane opens with its key; every ribbon page the hero has opens from its ribbon on the rail.</summary>
    private async Task CorePaneAndPages()
    {
        var hud = _main.Hud;
        await Until(() => hud.Hero.Inventory is not null || !hud.Hero.Interface, 5);
        TapName(hud.KeyOf("inventory"));
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
                TapName(hud.KeyOf(page));
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
            Tap(Key.Escape);
            await Wait(0.15);
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
        var reached = await TravelTo(() => (villager.State.X, villager.State.Y), 1.6f, 45);
        await Wait(0.4);
        var states = _main.DialogueStates;
        await Hover(villager);
        await Click(Screen(villager.GlobalPosition + new Vector3(0, villager.State.Height * 0.5f, 0)));
        var shown = await Until(() => _main.DialogueStates > states && _main.Hud.HasWorldPane("dialogue"), 6);
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

    /// <summary>Opens a vendor (one in view, or the one the conversation opened) and buys the first thing it can.</summary>
    private async Task TradeWithAVendor()
    {
        var hud = _main.Hud;
        if (!hud.HasWorldPane("vendor"))
        {
            var vendor = Nearest(e => e.State.Interactable == "vendor" || e.State.Pattern?.Contains("vendor", StringComparison.Ordinal) == true || e.State.Pattern?.Contains("merchant", StringComparison.Ordinal) == true);
            if (vendor is null)
            {
                Record("trade with a vendor", false, $"no vendor in view and no conversation opened one{Rejected()}");
                return;
            }
            await TravelTo(() => (vendor.State.X, vendor.State.Y), 1.6f, 45);
            await Wait(0.4);
            var states = _main.VendorStates;
            await Click(Screen(vendor.GlobalPosition + new Vector3(0, vendor.State.Height * 0.5f, 0)));
            await Until(() => _main.VendorStates > states && hud.HasWorldPane("vendor"), 6);
        }
        var pane = hud.FrontView<UI.WorldPanes.VendorPane>();
        await Wait(0.4);
        await Shot("09_vendor.png");
        if (pane?.State is not { } state)
        {
            Record("trade with a vendor", false, $"no vendor_state arrived{Rejected()}");
            return;
        }
        var items0 = hud.BagItems;
        var inventories = _main.InventoryStates;
        var buy = pane.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => !b.Disabled && b.Text == "Buy");
        if (buy is null)
        {
            Record("trade with a vendor", false, $"{state.Name}: nothing can be bought ({state.Stock.Count} on the shelf)");
            return;
        }
        await Click(buy.GetGlobalRect().GetCenter());
        var bought = await Until(() => _main.InventoryStates > inventories && hud.BagItems > items0, 6);
        await Shot("10_bought.png");
        Record("trade with a vendor", bought, $"{state.Name}: {state.Stock.Count} on the shelf, bag {items0} -> {hud.BagItems}{Rejected()}");
        Tap(Key.Escape);
        await Wait(0.2);
        Tap(Key.Escape);
        await Wait(0.2);
    }

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

    /// <summary>Moves the cursor to a viewport point and clicks the left button there.</summary>
    private async Task Click(Vector2 screen)
    {
        Input.WarpMouse(screen);
        await Frame();
        await Frame();
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = screen, GlobalPosition = screen });
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = screen, GlobalPosition = screen });
    }

    private async Task Hover(EntityView view)
    {
        Input.WarpMouse(Screen(view.GlobalPosition + new Vector3(0, view.State.Height * 0.5f, 0)));
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
