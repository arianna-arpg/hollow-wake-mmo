using Godot;
using HollowWake.Client.UI.Logic;

namespace HollowWake.Client;

/// <summary>
/// Registers the input map from the input_actions content, never from code: each action with the player's
/// bindings from Options laid over the content's defaults (a primary and a second key, and a controller
/// input). Re-registering after a rebind replaces an action's events.
/// </summary>
public static class InputSetup
{
    private static readonly Dictionary<string, JoyButton> Buttons = new(StringComparer.Ordinal)
    {
        ["A"] = JoyButton.A,
        ["B"] = JoyButton.B,
        ["X"] = JoyButton.X,
        ["Y"] = JoyButton.Y,
        ["LB"] = JoyButton.LeftShoulder,
        ["RB"] = JoyButton.RightShoulder,
        ["Back"] = JoyButton.Back,
        ["Start"] = JoyButton.Start,
        ["LeftStick"] = JoyButton.LeftStick,
        ["RightStick"] = JoyButton.RightStick,
        ["DpadUp"] = JoyButton.DpadUp,
        ["DpadDown"] = JoyButton.DpadDown,
        ["DpadLeft"] = JoyButton.DpadLeft,
        ["DpadRight"] = JoyButton.DpadRight,
    };

    private static readonly Dictionary<string, (JoyAxis Axis, float Sign)> Axes = new(StringComparer.Ordinal)
    {
        ["LT"] = (JoyAxis.TriggerLeft, 1),
        ["RT"] = (JoyAxis.TriggerRight, 1),
        ["LeftStickUp"] = (JoyAxis.LeftY, -1),
        ["LeftStickDown"] = (JoyAxis.LeftY, 1),
        ["LeftStickLeft"] = (JoyAxis.LeftX, -1),
        ["LeftStickRight"] = (JoyAxis.LeftX, 1),
        ["RightStickUp"] = (JoyAxis.RightY, -1),
        ["RightStickDown"] = (JoyAxis.RightY, 1),
        ["RightStickLeft"] = (JoyAxis.RightX, -1),
        ["RightStickRight"] = (JoyAxis.RightX, 1),
    };

    /// <summary>Registers every action with its effective bindings.</summary>
    public static void Apply(Settings settings)
    {
        foreach (var action in settings.Catalog.Actions)
        {
            if (InputMap.HasAction(action.Id))
            {
                InputMap.ActionEraseEvents(action.Id);
            }
            else
            {
                InputMap.AddAction(action.Id, 0.5f);
            }
            var binding = settings.Bindings(action.Id);
            foreach (var key in binding.Keys)
            {
                if (KeyEvent(key) is { } ev)
                {
                    InputMap.ActionAddEvent(action.Id, ev);
                }
            }
            if (PadEvent(binding.Pad) is { } pad)
            {
                InputMap.ActionAddEvent(action.Id, pad);
            }
        }
    }

    /// <summary>A key event for a key name (W, Shift+Tab, F1), or null for a name Godot does not know.</summary>
    public static InputEventKey? KeyEvent(string keyName)
    {
        var name = KeyName.Parse(keyName);
        if (name.IsEmpty)
        {
            return null;
        }
        var key = OS.FindKeycodeFromString(name.Key);
        if (key == Key.None)
        {
            GD.PushWarning($"key '{keyName}' is not a key Godot knows");
            return null;
        }
        return new InputEventKey { Keycode = key, ShiftPressed = name.Shift, CtrlPressed = name.Ctrl, AltPressed = name.Alt };
    }

    public static InputEvent? PadEvent(string name)
    {
        if (Buttons.TryGetValue(name, out var button))
        {
            return new InputEventJoypadButton { ButtonIndex = button, Device = -1 };
        }
        if (Axes.TryGetValue(name, out var axis))
        {
            return new InputEventJoypadMotion { Axis = axis.Axis, AxisValue = axis.Sign, Device = -1 };
        }
        return null;
    }

    /// <summary>The content name of a controller button.</summary>
    public static string PadName(JoyButton button) => Buttons.FirstOrDefault(kv => kv.Value == button).Key ?? "";

    /// <summary>The content name of a stick or trigger direction.</summary>
    public static string PadName(JoyAxis axis, float value) =>
        Axes.FirstOrDefault(kv => kv.Value.Axis == axis && Math.Sign(kv.Value.Sign) == Math.Sign(value)).Key ?? "";

    /// <summary>A key event's content name with its modifiers, for matching against bindings.</summary>
    public static string NameOf(InputEventKey key) =>
        KeyName.Compose(OS.GetKeycodeString(key.Keycode), key.ShiftPressed, key.CtrlPressed, key.AltPressed);
}
