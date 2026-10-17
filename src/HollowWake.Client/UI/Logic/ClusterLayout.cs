namespace HollowWake.Client.UI.Logic;

/// <summary>
/// Where the cluster's parts stand on a screen: the life orb on the left, the weave bar in the middle, the
/// mana orb on the right, the menu button beside the mana orb, the cast bar above the weave bar and the
/// status row above that. Ring space is reserved for every pool the layout names, so the cluster does not
/// move when a pool appears. The cluster shrinks to fit a narrow screen. Its drawn rectangle is what every
/// pane fits above.
/// </summary>
public sealed class ClusterLayout
{
    public float Scale { get; private init; }
    public float OrbRadius { get; private init; }
    public float RimWidth { get; private init; }
    public float RingWidth { get; private init; }
    public float RingGap { get; private init; }

    /// <summary>The radius out to the edge of the outermost ring an orb may wear.</summary>
    public float OuterRadius { get; private init; }

    public Box LeftOrb { get; private init; }
    public Box RightOrb { get; private init; }
    public Box Bar { get; private init; }
    public IReadOnlyList<Box> Slots { get; private init; } = Array.Empty<Box>();
    public Box Cast { get; private init; }
    public Box Status { get; private init; }
    public Box Menu { get; private init; }

    /// <summary>The union of everything the cluster draws.</summary>
    public Box Rect { get; private init; }

    public float SlotSize { get; private init; }
    public float StatusIcon { get; private init; }

    /// <summary>The radius of ring <paramref name="index"/> (zero nearest the orb), at its centre line.</summary>
    public float RingRadius(int index) => OrbRadius + RimWidth + RingGap * (index + 1) + RingWidth * index + RingWidth / 2;

    /// <summary>The centre line of the rim band hugging the orb.</summary>
    public float RimRadius => OrbRadius + RimWidth / 2;

    public static ClusterLayout Compute(UiLayout layout, float viewWidth, float viewHeight, float userScale = 1f)
    {
        var rings = layout.Orbs.Count == 0 ? 0 : layout.Orbs.Max(o => o.Rings.Count);
        float M(string name, double fallback) => (float)layout.M(name, fallback);
        var orb = M("orb", 124);
        var rim = M("rim", 7);
        var ring = M("ring", 6);
        var ringGap = M("ring_gap", 3);
        var slot = M("slot", 52);
        var slotGap = M("slot_gap", 6);
        var slots = Math.Max(1, (int)M("slots", 8));
        var pad = M("bar_pad", 10);
        var margin = M("cluster_margin", 10);
        var cast = M("cast_bar", 14);
        var statusIcon = M("status_icon", 28);
        var menu = M("menu_button", 46);
        var edge = M("edge", 14);

        var outer = orb / 2 + rim + rings * (ringGap + ring);
        var barW = slots * slot + (slots - 1) * slotGap + pad * 2;
        var barH = slot + pad * 2;
        var gap = slotGap * 2;
        // The bar stays centred, so the wider half (the mana side, with the menu button) sets the width needed.
        var leftHalf = barW / 2 + gap + outer * 2;
        var rightHalf = barW / 2 + gap * 2 + outer * 2 + menu;
        var width = MathF.Max(leftHalf, rightHalf) * 2;
        var scale = userScale;
        var room = viewWidth - edge * 2;
        if (width * scale > room && width > 0)
        {
            scale = MathF.Max(0.35f, room / width);
        }
        float S(float v) => v * scale;

        var bar = new Box((viewWidth - S(barW)) / 2, viewHeight - S(margin) - S(barH), S(barW), S(barH));
        var slotBoxes = new List<Box>(slots);
        for (var i = 0; i < slots; i++)
        {
            slotBoxes.Add(new Box(bar.X + S(pad) + i * S(slot + slotGap), bar.Y + S(pad), S(slot), S(slot)));
        }
        var outerS = S(outer);
        var bottom = viewHeight - S(margin);
        var left = new Box(bar.X - S(gap) - outerS * 2, bottom - outerS * 2, outerS * 2, outerS * 2);
        var right = new Box(bar.Right + S(gap), bottom - outerS * 2, outerS * 2, outerS * 2);
        var menuBox = new Box(right.Right + S(gap), bottom - S(menu), S(menu), S(menu));
        var castW = bar.W * 0.62f;
        var castBox = new Box(bar.CenterX - castW / 2, bar.Y - S(slotGap) - S(cast), castW, S(cast));
        var statusBox = new Box(bar.X, castBox.Y - S(slotGap) - S(statusIcon), bar.W, S(statusIcon));
        var rect = left.Union(right).Union(bar).Union(menuBox).Union(castBox).Union(statusBox);
        return new ClusterLayout
        {
            Scale = scale,
            OrbRadius = S(orb / 2),
            RimWidth = S(rim),
            RingWidth = S(ring),
            RingGap = S(ringGap),
            OuterRadius = outerS,
            LeftOrb = left,
            RightOrb = right,
            Bar = bar,
            Slots = slotBoxes,
            Cast = castBox,
            Status = statusBox,
            Menu = menuBox,
            Rect = rect,
            SlotSize = S(slot),
            StatusIcon = S(statusIcon),
        };
    }
}
