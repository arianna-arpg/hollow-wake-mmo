using Godot;
using HollowWake.Core.Definitions;
using HollowWake.Protocol;

namespace HollowWake.Client.Dev;

public partial class Smoke
{
    // Exercise the real view with controlled replies. No account mutations or world entities.
    private async Task EntryReplySteps()
    {
        var flow = _main.Content.Each<EntryFlowDef>().Single(f => f.Default);
        var future = flow with { Stages = [new() { Id = "tutorial", Kind = "future_tutorial", Presentation = "future_tutorial" }, .. flow.Stages] };
        _main.Content.With(future);
        var panel = new UI.LoginPanel { Content = _main.Content, Appearance = _main.Appearance };
        AddChild(panel);
        try
        {
            var list = new CharacterListMsg([], _main.Login.Callings.ToList());
            panel.ShowCharacters(list);
            Record("legacy Mu fallback", panel.Entry is not null, "an older server selects the terminal selection stage despite a local tutorial prefix");
            var scene = panel.Entry!;
            string? entered = null; panel.EnterRequested += id => entered = id;
            async Task<string> Create(string name)
            {
                scene.Model.Open(0); await Frame();
                ((LineEdit)scene.FindChild("MuName", true, false)).Text = name;
                ((Button)scene.FindChild("MuWake", true, false)).EmitSignal(Button.SignalName.Pressed);
                return scene.Pending!;
            }
            var first = await Create("Wren");
            scene._UnhandledInput(new InputEventAction { Action = "ui_cancel", Pressed = true });
            var second = await Create("Ash");
            var card = scene.FindChild("MuName", true, false);
            scene.ShowCharacters(list with { Characters = [new("first", "Wren", list.Callings[0], 1, "0,0")], RequestId = first, CreatedCharacterId = "first" });
            Record("Mu delayed reply isolation", scene.Pending == second && scene.FindChild("MuName", true, false) == card && entered is null,
                "cancel A, submit B, reply A preserves B's pending card without waking");
            scene.ShowCharacters(list with { Characters = [new("second", "Ash", list.Callings[0], 1, "0,0")], RequestId = second, CreatedCharacterId = "second" });
            Record("Mu matching reply", scene.Pending is null && entered == "second", "only B's successful reply wakes B");
            scene.Error("Selection was refused."); await Frame();
            Record("Mu general refusal", scene.FindChildren("*", "Label", true, false).OfType<Label>().Any(l => l.Text == "Selection was refused."), "non-creation errors remain visible in Mu");
        }
        finally { _main.Content.With(flow); panel.QueueFree(); await Frame(); }

        // A real roster response while Options is open must leave its focus and layer intact.
        var optionsLabel = _main.Content.Get<UiEntrySceneDef>("mu")!.Meta!["labels"]!["options"]!.ToString();
        _main.Login.Entry!.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == optionsLabel).EmitSignal(Button.SignalName.Pressed);
        await Until(() => _main.Hud.OptionsOpen, 3);
        await Frame();
        var focused = GetViewport().GuiGetFocusOwner();
        var lists = _main.CharacterLists;
        _main.Net.Send(new LoginMsg(_account, ""));
        await Until(() => _main.CharacterLists > lists, 10);
        Record("Mu roster behind Options", _main.Hud.OptionsOpen && !_main.Login.Visible && GetViewport().GuiGetFocusOwner() == focused, "refresh does not cover the options pane or steal its focus");
        await CloseOptionsByDone();
    }
}
