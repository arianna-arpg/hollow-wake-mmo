using Godot;
using HollowWake.Client.UI.Widgets;
using HollowWake.Protocol;

namespace HollowWake.Client.UI.Core;

/// <summary>
/// The card shown while an item is hovered: its name in its rarity's colour, its base and class, its level,
/// its requirements (unmet ones in the theme's warning colour), its lines, sockets, what a stone or form holds
/// and how a loose stone fits each seated weave, its flavor, and any lines the surface adds (a price). Beside
/// it, the item worn in the same slot, for comparison. Every value is as the server sent it.
/// </summary>
public partial class ItemCard : Control
{
    private readonly Skin _skin;
    private readonly List<Card> _cards = new();

    private sealed record Line(string Text, Color Color, Font Font, int Size, float Gap = 0);

    private sealed class Card
    {
        public List<Line> Lines = new();
        public Vector2 Size;
        public string? Caption;
        public Color Edge;
    }

    public ItemCard(Skin skin)
    {
        _skin = skin;
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        ZIndex = 50;
    }

    public long ShownUid { get; private set; }

    public void Hide2()
    {
        Visible = false;
        ShownUid = 0;
    }

    /// <summary>Shows the card for an item near a screen point, with the worn item beside it when given.</summary>
    public void ShowFor(ItemInfo item, Vector2 at, Vector2 viewport, ItemInfo? worn = null, IEnumerable<string>? extra = null, Func<int, string>? seatName = null)
    {
        _cards.Clear();
        _cards.Add(Build(item, null, extra, seatName));
        if (worn is not null && worn.Uid != item.Uid)
        {
            _cards.Add(Build(worn, "Worn", null, seatName));
        }
        var width = _cards.Sum(c => c.Size.X) + (_cards.Count - 1) * 8;
        var height = _cards.Max(c => c.Size.Y);
        var x = at.X + 18;
        if (x + width > viewport.X - 6)
        {
            x = at.X - width - 18;
        }
        var y = Math.Clamp(at.Y - 20, 6, Math.Max(6, viewport.Y - height - 6));
        Position = new Vector2(Math.Max(6, x), y);
        Size = new Vector2(width, height);
        Visible = true;
        ShownUid = item.Uid;
        QueueRedraw();
    }

    private Card Build(ItemInfo item, string? caption, IEnumerable<string>? extra, Func<int, string>? seatName)
    {
        var s = _skin;
        var ink = s.S("ink");
        var muted = s.S("ink_muted");
        var good = s.S("good", ink);
        var bad = s.S("bad", ink);
        var accent = s.S("accent");
        var card = new Card { Caption = caption, Edge = s.Rarity(item.Rarity) };
        void Add(string text, Color color, Font? font = null, int? size = null, float gap = 0) =>
            card.Lines.Add(new Line(text, color, font ?? s.Body, size ?? s.BodySize, gap));

        Add(item.Name, s.Rarity(item.Rarity), s.Title, s.TitleSize);
        var kind = string.Join("  ", new[] { item.Base != item.Name ? Humanize(item.Base) : null, item.Class is { } c ? Humanize(c) : null }.Where(t => !string.IsNullOrEmpty(t)));
        if (kind.Length > 0)
        {
            Add(kind, muted);
        }
        var meta = new List<string>();
        if (item.Level > 0)
        {
            meta.Add($"Item level {item.Level}");
        }
        if (item.Count > 1)
        {
            meta.Add($"{item.Count} in the stack");
        }
        if (item.Quality is { } q)
        {
            meta.Add($"Quality {q:0.##}");
        }
        if (meta.Count > 0)
        {
            Add(string.Join("   ", meta), muted);
        }
        if (item.Requirements is { Count: > 0 } reqs)
        {
            Add("Requires " + string.Join(", ", reqs.Select(r => r.Text)), reqs.All(r => r.Met) ? muted : bad, gap: 4);
        }
        if (item.Lines.Count > 0)
        {
            var first = true;
            foreach (var line in item.Lines)
            {
                Add(line, ink, gap: first ? 6 : 0);
                first = false;
            }
        }
        if (item.Form is { } form)
        {
            Add($"Teaches {form.Name} {form.Level}, {form.Sockets} socket{(form.Sockets == 1 ? "" : "s")}", accent, gap: 6);
        }
        if (item.Glyph is { } glyph)
        {
            Add($"Glyph {glyph.Name} {glyph.Level}", accent, gap: 6);
            foreach (var row in glyph.Rows ?? new List<string>())
            {
                Add(row, ink);
            }
        }
        if (item.Fits is { Count: > 0 } fits)
        {
            Add("Fits the seated weaves:", muted, gap: 6);
            foreach (var fit in fits)
            {
                var name = seatName?.Invoke(fit.Slot) ?? $"Seat {fit.Slot + 1}";
                var (text, color) = fit.Fit switch
                {
                    "fits" => ("fits", good),
                    "misfit" => ("dormant: " + (fit.Reason ?? "misfit"), bad),
                    "full" => ("sockets full", muted),
                    "no_sockets" => ("no sockets", muted),
                    _ => (fit.Fit, muted),
                };
                Add($"  {name}: {text}", color);
            }
        }
        if (item.Sockets is { Count: > 0 } sockets)
        {
            Add($"Sockets: {string.Join("  ", sockets.Select(k => k.Stone?.Name ?? "empty"))}", muted, gap: 6);
        }
        if (item.Flavor is { Length: > 0 } flavor)
        {
            Add(flavor, accent with { A = 0.85f }, gap: 6);
        }
        if (extra is not null)
        {
            var first = true;
            foreach (var e in extra)
            {
                Add(e, s.S("accent"), gap: first ? 6 : 0);
                first = false;
            }
        }
        var width = 0f;
        var height = 14f + (caption is null ? 0 : s.BodySize + 4);
        foreach (var l in card.Lines)
        {
            width = Math.Max(width, l.Font.GetStringSize(l.Text, HorizontalAlignment.Left, -1, l.Size).X);
            height += l.Size + 5 + l.Gap;
        }
        card.Size = new Vector2(Math.Clamp(width + 28, 180, 420), height + 8);
        return card;
    }

    private static string Humanize(string id) => Logic.ContentNames.Humanize(id.Replace('.', '_'));

    public override void _Draw()
    {
        var x = 0f;
        foreach (var card in _cards)
        {
            var r = new Rect2(x, 0, card.Size.X, card.Size.Y);
            Paint.Shadow(this, r, 0.7f);
            DrawRect(r, _skin.S("pane") with { A = 0.97f });
            Paint.Frame(this, _skin, r);
            DrawRect(new Rect2(r.Position + new Vector2(6, 6), new Vector2(r.Size.X - 12, 2)), card.Edge with { A = 0.8f });
            var y = 14f;
            if (card.Caption is { } caption)
            {
                DrawString(_skin.Body, new Vector2(x + 14, y + _skin.BodySize), caption, HorizontalAlignment.Left, -1, _skin.BodySize - 1, _skin.S("ink_muted"));
                y += _skin.BodySize + 4;
            }
            foreach (var line in card.Lines)
            {
                y += line.Gap + line.Size + 2;
                DrawString(line.Font, new Vector2(x + 14, y), line.Text, HorizontalAlignment.Left, card.Size.X - 24, line.Size, line.Color);
                y += 3;
            }
            x += card.Size.X + 8;
        }
    }
}
