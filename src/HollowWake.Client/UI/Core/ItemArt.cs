using Godot;
using HollowWake.Client.UI.Widgets;
using HollowWake.Protocol;

namespace HollowWake.Client.UI.Core;

/// <summary>How an item is drawn on a board, a doll slot or a carry: a tile in its rarity's colour with its glyph.</summary>
public static class ItemArt
{
    /// <summary>The glyph for an item: its own icon if the set has it, its slot's, its class's family, else none.</summary>
    public static string? Glyph(Skin skin, ItemInfo item)
    {
        if (item.Icon is { } icon && skin.IconSet.Icons.ContainsKey(icon))
        {
            return icon;
        }
        if (item.Slot is { } slot && skin.IconSet.Slots.TryGetValue(slot, out var bySlot))
        {
            return bySlot;
        }
        var cls = item.Class ?? "";
        if (cls.StartsWith("glyph", StringComparison.Ordinal))
        {
            return "gem";
        }
        if (cls.StartsWith("form", StringComparison.Ordinal))
        {
            return "weave";
        }
        if (cls.StartsWith("weapon", StringComparison.Ordinal))
        {
            return "blade";
        }
        if (cls.StartsWith("shield", StringComparison.Ordinal))
        {
            return "shield";
        }
        // armor.head, jewelry.ring: the part after the family names the slot whose glyph the item wears.
        var dot = cls.IndexOf('.');
        if (dot > 0 && skin.IconSet.Slots.TryGetValue(cls[(dot + 1)..], out var byClass))
        {
            return byClass;
        }
        return null;
    }

    public static void Tile(CanvasItem ci, Skin skin, Rect2 r, ItemInfo item, float alpha = 1f, bool hover = false)
    {
        var rarity = skin.Rarity(item.Rarity);
        // The tile sits on the pane's own inset, tinted toward its rarity, so the rarity's ink reads in either texture.
        var floor = skin.S("inset").Lerp(rarity, 0.16f) with { A = 0.95f * alpha };
        ci.DrawRect(r.Grow(-1), floor);
        // a lit gradient from the top
        ci.DrawRect(new Rect2(r.Position + new Vector2(1, 1), new Vector2(r.Size.X - 2, r.Size.Y * 0.35f)), Colors.White with { A = 0.05f * alpha });
        ci.DrawRect(r.Grow(-1), rarity with { A = (hover ? 1f : 0.85f) * alpha }, false, hover ? 2 : 1.5f);
        var glyph = Glyph(skin, item);
        var side = Math.Min(r.Size.X, r.Size.Y) * 0.68f;
        var ink = (floor.Luminance > 0.5f ? rarity.Darkened(0.1f) : rarity.Lightened(0.15f)) with { A = alpha };
        if (glyph is not null)
        {
            var g = new Rect2(r.GetCenter() - new Vector2(side, side) / 2, new Vector2(side, side));
            ci.DrawTextureRect(skin.Icon(glyph, (int)side), g, false, ink);
        }
        else
        {
            var letter = item.Name.Length > 0 ? item.Name[..1].ToUpperInvariant() : "?";
            var size = (int)(side * 0.9f);
            Paint.Text(ci, skin.Title, new Vector2(r.Position.X, r.GetCenter().Y + size * 0.36f), letter, size, ink, HorizontalAlignment.Center, r.Size.X, 3);
        }
        if (item.Count > 1)
        {
            Paint.Text(ci, skin.Numerals, new Vector2(r.Position.X, r.End.Y - 3), item.Count.ToString(), Math.Max(9, (int)(r.Size.Y * 0.3f)), Colors.White with { A = alpha }, HorizontalAlignment.Right, r.Size.X - 4, 3);
        }
        if (item.Locked == true)
        {
            ci.DrawTextureRect(skin.Icon("lock", 12), new Rect2(r.Position + new Vector2(3, 3), new Vector2(11, 11)), false, skin.S("ink_muted") with { A = alpha });
        }
        if (item.Requirements?.Any(q => !q.Met) == true)
        {
            ci.DrawRect(r.Grow(-1), skin.S("bad") with { A = 0.22f * alpha });
        }
    }
}
