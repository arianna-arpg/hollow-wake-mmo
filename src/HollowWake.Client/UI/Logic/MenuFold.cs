namespace HollowWake.Client.UI.Logic;

/// <summary>A page's state in the tray: absent for this hero, sealed here (with the reason), or open.</summary>
public enum TrayState
{
    Hidden,
    Sealed,
    Open,
}

/// <summary>What the server says of one page: whether it exists, why it is sealed, and its pips.</summary>
public sealed record PageStatus(bool Exists, string? Sealed, int Pips);

/// <summary>One row of the menu tray.</summary>
public sealed record TrayRow(UiPage Page, TrayState State, string? Reason, int Pips, string KeyLabel, bool Showing);

/// <summary>
/// Folds the pages into the menu tray: every page content declares, in order, each hidden, sealed or open as
/// the page state says, with its pips and its key. A page the state does not mention is open with the pips the
/// fallback gives (before the page state arrives, the tray reads the content's page list). The button wears
/// the sum of the open pages' pips while the tray is closed.
/// </summary>
public static class MenuFold
{
    public static List<TrayRow> Fold(
        IReadOnlyList<UiPage> pages,
        IReadOnlyDictionary<string, PageStatus>? served,
        Func<string, int> fallbackPips,
        Func<string, bool> showing)
    {
        var rows = new List<TrayRow>(pages.Count);
        foreach (var page in pages)
        {
            var label = KeyName.Label(page.Key);
            if (served is not null && served.TryGetValue(page.Id, out var status))
            {
                if (!status.Exists)
                {
                    rows.Add(new TrayRow(page, TrayState.Hidden, null, 0, label, false));
                    continue;
                }
                var state = status.Sealed is null ? TrayState.Open : TrayState.Sealed;
                rows.Add(new TrayRow(page, state, status.Sealed, Math.Max(0, status.Pips), label, showing(page.Id)));
                continue;
            }
            rows.Add(new TrayRow(page, TrayState.Open, null, Math.Max(0, fallbackPips(page.Id)), label, showing(page.Id)));
        }
        return rows;
    }

    /// <summary>The button's badge: the pips of every page that can be opened.</summary>
    public static int Badge(IEnumerable<TrayRow> rows) => rows.Where(r => r.State == TrayState.Open).Sum(r => r.Pips);

    /// <summary>A badge's text: the count, or the cap and a plus past it.</summary>
    public static string BadgeText(int pips, int cap = 9) => pips <= 0 ? "" : pips > cap ? $"{cap}+" : pips.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>What the rows look like, so a surface rebuilds only when it changes.</summary>
    public static string Signature(IEnumerable<TrayRow> rows) =>
        string.Join("|", rows.Select(r => $"{r.Page.Id}:{r.State}:{r.Reason}:{r.Pips}:{r.KeyLabel}:{r.Showing}"));
}

/// <summary>Key names as content writes them: a key, optionally with modifiers joined by plus signs.</summary>
public readonly record struct KeyName(string Key, bool Shift, bool Ctrl, bool Alt)
{
    public static KeyName Parse(string? text)
    {
        var parts = (text ?? "").Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var shift = false;
        var ctrl = false;
        var alt = false;
        var key = "";
        foreach (var p in parts)
        {
            switch (p.ToLowerInvariant())
            {
                case "shift":
                    shift = true;
                    break;
                case "ctrl":
                case "control":
                    ctrl = true;
                    break;
                case "alt":
                    alt = true;
                    break;
                default:
                    key = p;
                    break;
            }
        }
        return new KeyName(key, shift, ctrl, alt);
    }

    public bool IsEmpty => Key.Length == 0;

    /// <summary>The label a key wears on a ribbon or a tray row.</summary>
    public static string Label(string? text)
    {
        var k = Parse(text);
        if (k.IsEmpty)
        {
            return "";
        }
        var mods = (k.Ctrl ? "Ctrl+" : "") + (k.Alt ? "Alt+" : "") + (k.Shift ? "Shift+" : "");
        return mods + (k.Key.Length == 1 ? k.Key.ToUpperInvariant() : k.Key);
    }

    /// <summary>The browser's KeyboardEvent.code for the key (KeyI, Digit1, Tab, F1).</summary>
    public string WebCode()
    {
        if (Key.Length == 1)
        {
            var c = char.ToUpperInvariant(Key[0]);
            if (c is >= 'A' and <= 'Z')
            {
                return "Key" + c;
            }
            if (c is >= '0' and <= '9')
            {
                return "Digit" + c;
            }
        }
        return Key switch
        {
            "Esc" => "Escape",
            "Return" => "Enter",
            _ => Key,
        };
    }
}
