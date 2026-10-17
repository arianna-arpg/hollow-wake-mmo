using Godot;

namespace HollowWake.Client;

/// <summary>Registers the input map in code so the project file stays small and bindings are visible here.</summary>
public static class InputSetup
{
    public static void Ensure()
    {
        Bind("move_forward", Key.W);
        Bind("move_back", Key.S);
        Bind("move_left", Key.A);
        Bind("move_right", Key.D);
        Bind("jump", Key.Space);
        Bind("interact", Key.F);
        Bind("camera_toggle", Key.V);
        Bind("camera_rotate_left", Key.Q);
        Bind("camera_rotate_right", Key.E);
        Bind("chat", Key.Enter);
        Bind("cancel", Key.Escape);
        Bind("pick_up", Key.G);
        Bind("respawn", Key.R);
        Bind("toggle_sheet", Key.C);
        for (var i = 1; i <= 8; i++)
        {
            Bind("slot_" + i, Key.Key0 + i);
        }
    }

    private static void Bind(string action, Key key)
    {
        if (!InputMap.HasAction(action))
        {
            InputMap.AddAction(action);
        }
        var ev = new InputEventKey { Keycode = key };
        if (!InputMap.ActionHasEvent(action, ev))
        {
            InputMap.ActionAddEvent(action, ev);
        }
    }
}
