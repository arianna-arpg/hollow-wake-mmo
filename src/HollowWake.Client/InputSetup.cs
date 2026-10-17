using Godot;
using HollowWake.Client.UI.Logic;

namespace HollowWake.Client;

/// <summary>
/// Registers the input map in code so the project file stays small and bindings are visible here. The
/// interface's own keys (pages, the menu tray, the book walk) come from its content and bind by name.
/// </summary>
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
        for (var i = 1; i <= 8; i++)
        {
            Bind("slot_" + i, Key.Key0 + i);
        }
    }

    private static void Bind(string action, Key key, bool shift = false, bool ctrl = false, bool alt = false)
    {
        if (!InputMap.HasAction(action))
        {
            InputMap.AddAction(action);
        }
        var ev = new InputEventKey { Keycode = key, ShiftPressed = shift, CtrlPressed = ctrl, AltPressed = alt };
        if (!InputMap.ActionHasEvent(action, ev))
        {
            InputMap.ActionAddEvent(action, ev);
        }
    }

    /// <summary>
    /// Binds an action to a key written as content writes keys (I, Tab, Shift+Tab, F1). The action always
    /// exists afterwards, bound or not, so asking for it never fails.
    /// </summary>
    public static void BindName(string action, string keyName)
    {
        if (!InputMap.HasAction(action))
        {
            InputMap.AddAction(action);
        }
        var name = KeyName.Parse(keyName);
        if (name.IsEmpty)
        {
            return;
        }
        var key = OS.FindKeycodeFromString(name.Key);
        if (key == Key.None)
        {
            GD.PushWarning($"key '{keyName}' for {action} is not a key Godot knows");
            return;
        }
        Bind(action, key, name.Shift, name.Ctrl, name.Alt);
    }
}
