using HollowWake.Client.UI.Logic;
using HollowWake.Core.Definitions;

namespace HollowWake.Client.World.Logic;

/// <summary>Where a piece goes: the plan, the socket, the slot it fills and the size it scales by.</summary>
public sealed class GearFit
{
    public BodyPlan Plan { get; }
    public BodySocket Socket { get; }
    public string Slot { get; }
    /// <summary>The socket's size, which every gear measure is a fraction of (the body height for hands, the head for the head).</summary>
    public float Size { get; }
    public bool Held { get; }
    /// <summary>Drawn lying on the ground rather than worn.</summary>
    public bool Laid { get; init; }

    public GearFit(BodyPlan plan, BodySocket socket, string slot, float size, bool held)
    {
        Plan = plan;
        Socket = socket;
        Slot = slot;
        Size = size;
        Held = held;
    }

    /// <summary>A part in the socket's frame: sizes and offsets are fractions of the socket's size along the body's axes.</summary>
    public void Add(PartShape shape, Vec3 size, Vec3 at, Rgba color, Vec3? turn = null, float taper = 1, bool glow = false, int segments = 8, BodySocket? socket = null)
    {
        var s = Size;
        var local = new Vec3(at.X * s, at.Y * s, at.Z * s);
        var t = turn ?? Vec3.Zero;
        if (Laid)
        {
            // On the ground: the piece's length (its y) lies along the body's forward, just above the grass.
            local = PartGeometry.Turn(local, 0, -MathF.PI / 2, 0);
            t = new Vec3(t.X, t.Y - 90, t.Z);
        }
        var where = socket ?? Socket;
        Plan.Add(where.Bone, shape, new Vec3(size.X * s, size.Y * s, size.Z * s), where.At + local, color, t, taper, glow, segments);
    }
}

/// <summary>
/// The gear families both clients build (docs/design/appearance.md): each adds parts to a body plan at one
/// of its sockets, scaled by the socket's size, so a sword in a hob's hand is hob sized. A held piece hangs
/// from the hand down the body's y, and the animator swings the arm bone it sits on. A dropped item uses the
/// same builder at the drop plan's root, laid flat.
/// </summary>
public static class GearFamilies
{
    public static readonly IReadOnlyDictionary<string, Action<BodyBuild, GearFit>> Builders = new Dictionary<string, Action<BodyBuild, GearFit>>(StringComparer.Ordinal)
    {
        ["field_kit"] = FieldGearFamilies.FieldKit,
        ["blade"] = Blade,
        ["axe"] = Axe,
        ["spear"] = Spear,
        ["maul"] = Maul,
        ["bow"] = Bow,
        ["wand"] = Wand,
        ["staff"] = Staff,
        ["shield"] = Shield,
        ["tome"] = Tome,
        ["helm"] = Helm,
        ["hood"] = Hood,
        ["cuirass"] = Cuirass,
        ["jerkin"] = Jerkin,
        ["robe"] = Robe,
        ["gloves"] = Gloves,
        ["boots"] = Boots,
        ["belt"] = Belt,
        ["sack"] = Sack,
        ["gem"] = Gem,
    };

    /// <summary>Which socket a slot's gear sits at, and whether it is held (and so swings) rather than worn.</summary>
    public static (string Socket, bool Held) SocketOf(string slot) => slot switch
    {
        "main_hand" => ("hand_r", true),
        "off_hand" => ("hand_l", true),
        "head" => ("head", false),
        "chest" => ("chest", false),
        "hands" => ("hand_r", false),
        "feet" => ("feet", false),
        "belt" => ("belt", false),
        _ => ("chest", false),
    };

    /// <summary>Whether a gear family swings when held (else the body gathers and casts with it).</summary>
    public static bool SwingsWith(string family) => family is "blade" or "axe" or "spear" or "maul";

    /// <summary>Adds a worn piece to a body; a body without the socket wears nothing there.</summary>
    public static void Wear(BodyPlan plan, AppearanceDef gear, string slot, long key)
    {
        var (socketName, held) = SocketOf(slot);
        if (!plan.Sockets.TryGetValue(socketName, out var socket) || !Builders.TryGetValue(gear.Body, out var builder))
        {
            return;
        }
        var build = new BodyBuild(gear, key, socket.Size, socket.Size * 0.2f);
        builder(build, new GearFit(plan, socket, slot, socket.Size, held));
        if (held && slot == "main_hand")
        {
            plan.Swings = SwingsWith(gear.Body);
        }
    }

    /// <summary>Lays a piece on the ground at a drop plan's root, as a dropped item.</summary>
    public static void Lay(BodyPlan plan, AppearanceDef gear, long key)
    {
        if (!Builders.TryGetValue(gear.Body, out var builder))
        {
            return;
        }
        const float size = 1.5f;
        var socket = new BodySocket("root", new Vec3(0, 0.06f, 0), Vec3.Zero, size);
        var build = new BodyBuild(gear, key, size, 0.3f);
        builder(build, new GearFit(plan, socket, "drop", size, false) { Laid = true });
    }

    private static Vec3 V(float x, float y, float z) => new(x, y, z);

    // ------------------------------------------------------------ held in the hand: hilts at the socket, lengths down the body's y

    private static void Blade(BodyBuild b, GearFit f)
    {
        var length = b.S("length", 0.75f) * 0.42f;
        var width = b.S("width", 0.07f) * 0.6f;
        var guard = b.S("guard", 1);
        var metal = b.C("metal", "#b9bcc4");
        var edge = b.C("edge", "#eee9da");
        var grip = b.C("grip", "#4a3226");
        var band = b.C("band", "#b89a67");
        f.Add(PartShape.Cylinder, V(0.028f, 0.1f, 0.028f), V(0, 0.015f, 0), grip, segments: 6);
        f.Add(PartShape.Ellipsoid, V(0.04f, 0.03f, 0.04f), V(0, 0.07f, 0), band, segments: 5);
        if (guard > 0)
        {
            f.Add(PartShape.Box, V(0.025f, 0.018f, 0.1f * guard), V(0, -0.04f, 0), band);
        }
        f.Add(PartShape.Box, V(0.012f, length, width), V(0, -0.05f - length / 2, 0), metal);
        f.Add(PartShape.Box, V(0.006f, length * 0.95f, width * 0.45f), V(0.009f, -0.05f - length / 2, 0), edge);
        f.Add(PartShape.Cone, V(width, width * 0.9f, 0.012f), V(0, -0.05f - length - width * 0.4f, 0), metal, V(180, 0, 0), segments: 4);
    }

    private static void Axe(BodyBuild b, GearFit f)
    {
        var length = b.S("length", 0.7f) * 0.42f;
        var head = b.S("head", 1);
        var metal = b.C("metal", "#a4a6ac");
        var edge = b.C("edge", "#eee9da");
        var wood = b.C("wood", "#5a4634");
        var band = b.C("band", "#b89a67");
        f.Add(PartShape.Cylinder, V(0.028f, length + 0.08f, 0.028f), V(0, 0.04f - length / 2, 0), wood, segments: 6);
        f.Add(PartShape.Cylinder, V(0.034f, 0.03f, 0.034f), V(0, -length + 0.06f, 0), band, segments: 6);
        f.Add(PartShape.Wedge, V(0.11f * head, 0.14f * head, 0.03f), V(0.07f * head, -length + 0.05f, 0), metal, V(0, -90, 0));
        f.Add(PartShape.Box, V(0.012f, 0.13f * head, 0.012f), V(0.125f * head, -length + 0.05f, 0), edge);
        if (b.S("double", 0) > 0)
        {
            f.Add(PartShape.Wedge, V(0.11f * head, 0.14f * head, 0.03f), V(-0.07f * head, -length + 0.05f, 0), metal, V(0, 90, 0));
        }
    }

    private static void Spear(BodyBuild b, GearFit f)
    {
        var length = b.S("length", 1.9f) * 0.42f;
        var head = b.S("head", 1);
        var metal = b.C("metal", "#b9bcc4");
        var wood = b.C("wood", "#6b4a2e");
        var band = b.C("band", "#b89a67");
        // held a third of the way up, so the point stands over the head and the butt reaches the ground
        var centre = length * 0.18f;
        f.Add(PartShape.Cylinder, V(0.024f, length, 0.024f), V(0, centre, 0), wood, segments: 6);
        f.Add(PartShape.Cylinder, V(0.032f, 0.04f, 0.032f), V(0, centre + length / 2 - 0.04f, 0), band, segments: 6);
        f.Add(PartShape.Cone, V(0.06f * head, 0.16f * head, 0.02f), V(0, centre + length / 2 + 0.07f * head, 0), metal, segments: 4);
    }

    private static void Maul(BodyBuild b, GearFit f)
    {
        var length = b.S("length", 1.2f) * 0.42f;
        var head = b.S("head", 1);
        var metal = b.C("metal", "#7c7a78");
        var wood = b.C("wood", "#4a3226");
        var band = b.C("band", "#b89a67");
        f.Add(PartShape.Cylinder, V(0.03f, length + 0.06f, 0.03f), V(0, 0.03f - length / 2, 0), wood, segments: 6);
        f.Add(PartShape.Cylinder, V(0.038f, 0.03f, 0.038f), V(0, 0.06f, 0), band, segments: 6);
        if (head >= 0.8f)
        {
            f.Add(PartShape.Box, V(0.12f * head, 0.09f * head, 0.1f * head), V(0, -length + 0.04f, 0), metal);
            f.Add(PartShape.Box, V(0.125f * head, 0.02f, 0.105f * head), V(0, -length + 0.04f, 0), band);
        }
        else
        {
            f.Add(PartShape.Ellipsoid, V(0.09f, 0.16f, 0.09f), V(0, -length + 0.04f, 0), metal, segments: 6);
        }
    }

    private static void Bow(BodyBuild b, GearFit f)
    {
        var length = b.S("length", 1.3f) * 0.42f;
        var curve = b.S("curve", 1);
        var wood = b.C("wood", "#6b4a2e");
        var str = b.C("string", "#d8d2ba");
        var band = b.C("band", "#b89a67");
        f.Add(PartShape.Cylinder, V(0.03f, length * 0.3f, 0.03f), V(0, 0, 0), band, segments: 6);
        f.Add(PartShape.Cylinder, V(0.024f, length * 0.4f, 0.024f), V(-0.03f * curve, length * 0.3f, 0), wood, V(0, -12 * curve, 0), taper: 0.7f, segments: 5);
        f.Add(PartShape.Cylinder, V(0.024f, length * 0.4f, 0.024f), V(-0.03f * curve, -length * 0.3f, 0), wood, V(0, 12 * curve, 0), taper: 0.7f, segments: 5);
        f.Add(PartShape.Box, V(0.006f, length * 0.9f, 0.006f), V(-0.09f * curve, 0, 0), str);
    }

    private static void Wand(BodyBuild b, GearFit f)
    {
        var length = b.S("length", 0.5f) * 0.42f;
        var wood = b.C("wood", "#3a2c28");
        var tip = b.C("tip", "#edac69");
        var band = b.C("band", "#b89a67");
        f.Add(PartShape.Cylinder, V(0.02f, length, 0.02f), V(0, 0.03f - length / 2, 0), wood, taper: 0.7f, segments: 5);
        f.Add(PartShape.Cylinder, V(0.028f, 0.02f, 0.028f), V(0, 0.02f, 0), band, segments: 5);
        f.Add(PartShape.Ellipsoid, V(0.045f * b.S("tip", 1), 0.045f * b.S("tip", 1), 0.045f * b.S("tip", 1)), V(0, 0.03f - length, 0), tip, glow: true, segments: 5);
    }

    private static void Staff(BodyBuild b, GearFit f)
    {
        var length = b.S("length", 1.8f) * 0.42f;
        var wood = b.C("wood", "#5a4634");
        var head = b.C("head", "#9ad6d8");
        var band = b.C("band", "#b89a67");
        var centre = length * 0.15f;
        f.Add(PartShape.Cylinder, V(0.03f, length, 0.03f), V(0, centre, 0), wood, taper: 0.8f, segments: 6);
        f.Add(PartShape.Cylinder, V(0.04f, 0.03f, 0.04f), V(0, centre + length / 2 - 0.08f, 0), band, segments: 6);
        f.Add(PartShape.Ellipsoid, V(0.07f * b.S("head", 1), 0.09f * b.S("head", 1), 0.07f * b.S("head", 1)), V(0, centre + length / 2 + 0.01f, 0), head, glow: true, segments: 6);
    }

    private static void Shield(BodyBuild b, GearFit f)
    {
        var size = b.S("size", 0.6f) * 0.42f;
        var face = b.C("face", "#6b4a2e");
        var rim = b.C("rim", "#b89a67");
        var boss = b.C("boss", "#8c8c90");
        // the plate stands in the body's x-y plane, its face toward the body's left (-z)
        var at = V(0.02f, -0.04f, -0.04f);
        if (b.S("round", 1) > 0)
        {
            f.Add(PartShape.Cylinder, V(size * 1.1f, 0.02f, size * 1.1f), at, rim, V(90, 0, 0), segments: 10);
            f.Add(PartShape.Cylinder, V(size, 0.03f, size), at + V(0, 0, -0.01f), face, V(90, 0, 0), segments: 10);
        }
        else
        {
            f.Add(PartShape.Box, V(size * 0.8f, size * 1.1f, 0.03f), at, face);
            f.Add(PartShape.Box, V(size * 0.85f, size * 1.15f, 0.015f), at + V(0, 0, 0.01f), rim);
        }
        f.Add(PartShape.Ellipsoid, V(size * 0.25f, size * 0.25f, 0.05f), at + V(0, 0, -0.03f), boss, segments: 6);
    }

    private static void Tome(BodyBuild b, GearFit f)
    {
        var size = b.S("size", 0.3f) * 0.42f;
        var cover = b.C("cover", "#5a2a2a");
        var page = b.C("page", "#eee9da");
        var band = b.C("band", "#b89a67");
        f.Add(PartShape.Box, V(size * 0.25f, size * 0.9f, size * 0.7f), V(0.02f, -0.04f, 0), cover);
        f.Add(PartShape.Box, V(size * 0.2f, size * 0.82f, size * 0.62f), V(0.045f, -0.04f, 0), page);
        f.Add(PartShape.Box, V(size * 0.27f, size * 0.12f, size * 0.72f), V(0.02f, -0.04f, 0), band);
    }

    // ------------------------------------------------------------ worn: on the head, the chest, the hands, the feet and the belt

    private static void Helm(BodyBuild b, GearFit f)
    {
        var metal = b.C("metal", "#9a9ca4");
        var band = b.C("band", "#b89a67");
        var brim = b.S("brim", 0.6f);
        var crest = b.S("crest", 0.4f);
        var visor = b.S("visor", 1);
        f.Add(PartShape.Ellipsoid, V(0.98f, 0.7f, 0.95f), V(0, -0.32f, 0), metal, segments: 8);
        if (brim > 0)
        {
            f.Add(PartShape.Cylinder, V(1.05f + 0.15f * brim, 0.06f, 1.0f + 0.15f * brim), V(0, -0.48f, 0), band, segments: 8);
        }
        if (crest > 0)
        {
            f.Add(PartShape.Wedge, V(0.7f, 0.3f * crest, 0.1f), V(-0.05f, 0.08f, 0), band);
        }
        if (visor > 0)
        {
            f.Add(PartShape.Box, V(0.2f, 0.22f, 0.75f), V(0.4f, -0.56f, 0), metal);
        }
    }

    private static void Hood(BodyBuild b, GearFit f)
    {
        var cloth = b.C("cloth", "#4a4458");
        var depth = b.S("depth", 1);
        f.Add(PartShape.Cylinder, V(1.25f, 1.25f, 1.2f), V(-0.2f * depth, -0.42f, 0), cloth, V(0, 10, 0), taper: 0.35f, segments: 7);
        f.Add(PartShape.Ellipsoid, V(1.1f, 0.5f, 1.15f), V(-0.15f * depth, -0.95f, 0), cloth, segments: 7);
    }

    private static void Cuirass(BodyBuild b, GearFit f)
    {
        var metal = b.C("metal", "#9a9ca4");
        var band = b.C("band", "#b89a67");
        var cloth = b.C("cloth", "#4a4a3e");
        var plates = b.S("plates", 1);
        var skirt = b.S("skirt", 0.6f);
        f.Add(PartShape.Box, V(0.148f, 0.29f, 0.245f), V(0, 0.005f, 0), metal);
        f.Add(PartShape.Box, V(0.152f, 0.03f, 0.25f), V(0, 0.08f, 0), band);
        if (plates > 0.3f)
        {
            foreach (var side in new[] { -1f, 1f })
            {
                f.Add(PartShape.Ellipsoid, V(0.11f, 0.075f, 0.11f), V(0, 0.15f, side * 0.15f), metal, segments: 6);
            }
        }
        if (skirt > 0)
        {
            f.Add(PartShape.Cylinder, V(0.26f, 0.12f * skirt, 0.3f), V(0, -0.18f - 0.05f * skirt, 0), cloth, taper: 0.8f, segments: 8);
        }
    }

    private static void Jerkin(BodyBuild b, GearFit f)
    {
        var leather = b.C("leather", "#6b4a2e");
        var band = b.C("band", "#b89a67");
        f.Add(PartShape.Box, V(0.144f, 0.275f, 0.24f), V(0, 0, 0), leather);
        if (b.S("straps", 1) > 0)
        {
            f.Add(PartShape.Box, V(0.15f, 0.025f, 0.26f), V(0, 0.09f, 0), band);
            f.Add(PartShape.Box, V(0.15f, 0.2f, 0.03f), V(0, 0.02f, 0.03f), band);
        }
    }

    private static void Robe(BodyBuild b, GearFit f)
    {
        var cloth = b.C("cloth", "#5a4a6a");
        var sash = b.C("sash", "#b89a67");
        var length = b.S("length", 1);
        f.Add(PartShape.Box, V(0.142f, 0.28f, 0.238f), V(0, 0, 0), cloth);
        f.Add(PartShape.Cylinder, V(0.27f, 0.38f * length, 0.27f), V(0, -0.33f * length, 0), cloth, taper: 0.75f, segments: 8);
        if (b.S("sash", 1) > 0)
        {
            f.Add(PartShape.Box, V(0.15f, 0.035f, 0.25f), V(0, -0.1f, 0), sash);
        }
    }

    private static void Gloves(BodyBuild b, GearFit f)
    {
        var leather = b.C("leather", "#5a4634");
        var cuff = b.S("cuff", 1);
        foreach (var name in new[] { "hand_r", "hand_l" })
        {
            if (f.Plan.Sockets.TryGetValue(name, out var hand))
            {
                f.Add(PartShape.Ellipsoid, V(0.085f, 0.09f, 0.08f), V(0, 0.01f, 0), leather, segments: 6, socket: hand);
                f.Add(PartShape.Cylinder, V(0.08f, 0.05f * cuff, 0.08f), V(-0.01f, 0.07f, 0), leather, taper: 1.15f, segments: 6, socket: hand);
            }
        }
    }

    private static void Boots(BodyBuild b, GearFit f)
    {
        var leather = b.C("leather", "#4a3226");
        var height = b.S("height", 1);
        foreach (var name in new[] { "leg_l", "leg_r" })
        {
            var leg = f.Plan.Bones.FirstOrDefault(x => x.Name == name);
            if (leg is null)
            {
                continue;
            }
            var socket = new BodySocket(name, new Vec3(0, 0, leg.Pivot.Z), Vec3.Zero, f.Size);
            f.Add(PartShape.Box, V(0.14f, 0.05f, 0.1f), V(0.025f, 0.025f, 0), leather, socket: socket);
            f.Add(PartShape.Cylinder, V(0.1f, 0.09f * height, 0.1f), V(0, 0.07f * height, 0), leather, taper: 1.1f, segments: 6, socket: socket);
        }
    }

    private static void Belt(BodyBuild b, GearFit f)
    {
        var leather = b.C("leather", "#4a3226");
        var band = b.C("band", "#b89a67");
        f.Add(PartShape.Box, V(0.138f, 0.032f, 0.225f), V(0, 0, 0), leather);
        f.Add(PartShape.Box, V(0.03f, 0.04f, 0.035f), V(0.072f, 0, 0), band);
        var pouches = (int)b.S("pouches", 2);
        for (var i = 0; i < pouches; i++)
        {
            var side = i % 2 == 0 ? 1f : -1f;
            f.Add(PartShape.Box, V(0.05f, 0.06f, 0.05f), V(0.02f, -0.035f, side * 0.1f), leather);
        }
    }

    private static void Sack(BodyBuild b, GearFit f)
    {
        var cloth = b.C("cloth", "#b8a888");
        var band = b.C("band", "#6b4a2e");
        var size = b.S("size", 1) * 0.3f;
        f.Add(PartShape.Ellipsoid, V(size, size * 1.1f, size), V(0, -size * 0.4f, 0), cloth, segments: 7);
        f.Add(PartShape.Cylinder, V(size * 0.4f, size * 0.1f, size * 0.4f), V(0, size * 0.12f, 0), band, segments: 6);
    }

    private static void Gem(BodyBuild b, GearFit f)
    {
        var metal = b.C("metal", "#c9a458");
        var gem = b.C("gem", "#81c3c9");
        var size = b.S("size", 1) * 0.1f;
        f.Add(PartShape.Cylinder, V(size * 1.3f, size * 0.3f, size * 1.3f), V(0, -size * 0.5f, 0), metal, segments: 8);
        f.Add(PartShape.Ellipsoid, V(size * 0.7f, size * 0.7f, size * 0.7f), V(0, 0, 0), gem, glow: true, segments: 6);
    }
}
