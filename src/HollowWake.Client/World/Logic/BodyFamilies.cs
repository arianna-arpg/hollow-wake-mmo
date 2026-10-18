using HollowWake.Client.UI.Logic;
using HollowWake.Core.Definitions;

namespace HollowWake.Client.World.Logic;

/// <summary>
/// What a family builder gets: the entry, the entity's size, its seeded variation and accessors for the
/// entry's shape numbers, palette colours and features. Colours come back jittered for the entity.
/// </summary>
public sealed class BodyBuild
{
    public AppearanceDef Def { get; }
    public long Key { get; }
    /// <summary>The body's height in world units after the entry's scale and the seeded jitter.</summary>
    public float Height { get; }
    /// <summary>The entity's collision radius, for props that should fill their footprint.</summary>
    public float Radius { get; }
    public BodyPlan Plan { get; }

    public BodyBuild(AppearanceDef def, long key, float entityHeight, float entityRadius)
    {
        Def = def;
        Key = key;
        var scale = (float)def.Scale * Variation.ScaleOf(def.Variation, key);
        Height = MathF.Max(0.15f, entityHeight) * scale;
        Radius = MathF.Max(0.1f, entityRadius) * scale;
        Plan = new BodyPlan { Family = def.Body, Glow = def.Glow is not null && Rgba.TryParse(def.Glow, out var g) ? Variation.Jitter(g, def.Variation, key) : null };
    }

    public float S(string name, float fallback) => Def.Shape.TryGetValue(name, out var v) ? (float)v : fallback;

    public bool Has(string feature) => Def.Features.Contains(feature);

    public Rgba C(string name, string fallback)
    {
        var text = Def.Palette.TryGetValue(name, out var t) ? t : fallback;
        var color = Rgba.Parse(text, Rgba.Parse(fallback, Rgba.White));
        return Variation.Jitter(color, Def.Variation, Key);
    }

    /// <summary>A second seeded fraction for placing repeated parts (facets, tufts, loops).</summary>
    public float U(int salt) => Variation.Unit(Key, 100 + salt);
}

/// <summary>
/// The body families both clients build (docs/design/appearance.md): each turns an appearance entry and an
/// entity's size into a plan of parts on bones. Proportions are the entry's shape numbers over a family
/// default; nothing about a particular creature is coded here.
/// </summary>
public static class BodyFamilies
{
    public static readonly IReadOnlyDictionary<string, Action<BodyBuild>> Builders = new Dictionary<string, Action<BodyBuild>>(StringComparer.Ordinal)
    {
        ["scaled_biped"] = ScaledBipedFamilies.ScaledBiped,
        ["fauna_quadruped"] = FaunaFamilies.Quadruped,
        ["fauna_bird"] = FaunaFamilies.Bird,
        ["fauna_crab"] = FaunaFamilies.Crab,
        ["orc"] = OrcFamilies.Orc,
        ["goblinkin"] = GoblinkinFamilies.Goblinkin,
        ["gnasher"] = GoblinkinFamilies.Gnasher,
        ["warbeast"] = GoblinkinFamilies.Warbeast,
        ["biped"] = Biped,
        ["skeleton"] = AnatomyFamilies.Skeleton,
        ["stitched"] = AnatomyFamilies.Stitched,
        ["bone_quadruped"] = AnatomyFamilies.BoneQuadruped,
        ["quadruped"] = Quadruped,
        ["crawler"] = Crawler,
        ["wisp"] = Wisp,
        ["totem"] = Totem,
        ["dome"] = Dome,
        ["trap"] = Trap,
        ["orb"] = Orb,
        ["tree"] = Tree,
        ["bush"] = Bush,
        ["fern"] = Fern,
        ["reed"] = Reed,
        ["plant"] = Plant,
        ["knot"] = Knot,
        ["rock"] = Rock,
        ["stone"] = Stone,
        ["log"] = Log,
        ["anvil"] = Anvil,
        ["loom"] = Loom,
        ["chest"] = Chest,
        ["shrine"] = Shrine,
        ["marker"] = Marker,
        ["fount"] = Fount,
        ["drop"] = Drop,
    };

    /// <summary>Builds the plan for an entry; an unknown family gets a plain block so nothing is ever invisible.</summary>
    public static BodyPlan Build(AppearanceDef def, long key, float entityHeight, float entityRadius)
    {
        var build = new BodyBuild(def, key, entityHeight, entityRadius);
        build.Plan.Bone("root", null, Vec3.Zero);
        if (Builders.TryGetValue(def.Body, out var builder))
        {
            builder(build);
        }
        else
        {
            Block(build);
        }
        var plan = build.Plan;
        var (min, max) = plan.Bounds();
        plan.Height = MathF.Max(plan.Height, max.Y);
        plan.Width = MathF.Max(plan.Width, MathF.Max(max.X - min.X, max.Z - min.Z));
        return plan;
    }

    private static Vec3 V(float x, float y, float z) => new(x, y, z);

    private static void Block(BodyBuild b)
    {
        var w = b.Radius * 1.4f;
        b.Plan.Add("root", PartShape.Box, V(w, b.Height, w), V(0, b.Height / 2, 0), b.C("stone", "#8c8a7a"));
        b.Plan.Rig = "static";
    }

    // ------------------------------------------------------------ creatures

    private static void Biped(BodyBuild b)
    {
        var p = b.Plan;
        p.Rig = "biped";
        var H = b.Height;
        var width = b.S("width", 1);
        var blocky = b.S("blocky", 0) > 0.5f;
        var legs = 0.47f * H * b.S("legs", 1);
        var torso = 0.3f * H * b.S("torso", 1);
        var head = 0.16f * H * b.S("head", 1);
        var neck = 0.025f * H * b.S("neck", 1);
        var shoulders = 0.28f * H * width * b.S("shoulders", 1);
        var hips = 0.18f * H * width * b.S("stance", 1);
        var depth = 0.13f * H * width;
        var arms = 0.32f * H * b.S("arms", 1);
        var armR = 0.035f * H * width;
        var legR = 0.045f * H * width;
        var hunch = b.S("hunch", 0) * 28f;
        var skin = b.C("skin", "#c8a888");
        var hair = b.C("hair", "#4a3828");
        var cloth = b.C("cloth", "#75685a");
        var cloth2 = b.C("cloth2", "#5c4f44");
        var leather = b.C("leather", "#55463a");
        var eyes = b.C("eyes", "#2a2622");
        var accent = b.C("accent", "#b89a67");

        var chestY = legs + torso;
        p.Bone("hips", "root", V(0, legs, 0));
        p.Bone("torso", "hips", V(0, legs + 0.02f * H, 0), V(0, -hunch, 0));
        p.Bone("head", "torso", V(0, chestY + neck, 0), V(0, hunch * 0.6f, 0));
        p.Bone("arm_l", "torso", V(0, chestY - 0.02f * H, -shoulders / 2));
        p.Bone("arm_r", "torso", V(0, chestY - 0.02f * H, shoulders / 2));
        p.Bone("leg_l", "hips", V(0, legs, -hips / 2));
        p.Bone("leg_r", "hips", V(0, legs, hips / 2));

        // torso and hips
        var torsoShape = blocky ? PartShape.Box : PartShape.Ellipsoid;
        p.Add("torso", PartShape.Box, V(depth, torso * 0.9f, shoulders * 0.78f), V(0, legs + torso * 0.52f, 0), cloth);
        p.Add("torso", torsoShape, V(depth * 1.05f, 0.09f * H, shoulders * 1.02f), V(0, chestY - 0.02f * H, 0), cloth2);
        var belly = b.S("belly", 0);
        if (belly > 0)
        {
            p.Add("torso", PartShape.Ellipsoid, V(depth * (1 + belly * 0.8f), torso * 0.6f, shoulders * 0.7f), V(depth * belly * 0.4f, legs + torso * 0.36f, 0), cloth);
        }
        p.Add("hips", PartShape.Box, V(depth * 0.95f, 0.1f * H, hips * 1.15f), V(0, legs - 0.02f * H, 0), leather);
        // head, hair, eyes
        p.Add("head", blocky ? PartShape.Box : PartShape.Ellipsoid, V(head * 0.9f, head, head * 0.86f), V(0, chestY + neck + head / 2, 0), skin);
        p.Add("head", PartShape.Cylinder, V(head * 0.4f, neck + 0.02f * H, head * 0.4f), V(0, chestY + neck / 2, 0), skin, segments: 6);
        p.Add("head", blocky ? PartShape.Box : PartShape.Ellipsoid, V(head * 0.92f, head * 0.5f, head * 0.9f), V(-head * 0.06f, chestY + neck + head * 0.78f, 0), hair);
        var eye = head * 0.14f;
        p.Add("head", PartShape.Box, V(eye * 0.6f, eye, eye), V(head * 0.42f, chestY + neck + head * 0.55f, -head * 0.2f), eyes);
        p.Add("head", PartShape.Box, V(eye * 0.6f, eye, eye), V(head * 0.42f, chestY + neck + head * 0.55f, head * 0.2f), eyes);
        // arms hang from the shoulders; the hand is the socket for a weapon
        foreach (var side in new[] { -1f, 1f })
        {
            var bone = side < 0 ? "arm_l" : "arm_r";
            var z = side * shoulders / 2;
            var sy = chestY - 0.02f * H;
            p.Add(bone, blocky ? PartShape.Box : PartShape.Cylinder, V(armR * 2, arms * 0.55f, armR * 2), V(0, sy - arms * 0.27f, z), cloth, taper: 0.85f, segments: 6);
            p.Add(bone, blocky ? PartShape.Box : PartShape.Cylinder, V(armR * 1.8f, arms * 0.5f, armR * 1.8f), V(0.01f * H, sy - arms * 0.78f, z), skin, taper: 0.9f, segments: 6);
            p.Add(bone, PartShape.Ellipsoid, V(armR * 2.4f, armR * 2.6f, armR * 2.2f), V(0.012f * H, sy - arms * 1.02f, z), skin, segments: 6);
            p.Sockets[side < 0 ? "hand_l" : "hand_r"] = new BodySocket(bone, V(0.03f * H, sy - arms * 1.02f, z), V(0, 0, 0), H);
            var lz = side * hips / 2;
            p.Add(side < 0 ? "leg_l" : "leg_r", blocky ? PartShape.Box : PartShape.Cylinder, V(legR * 2, legs * 0.95f, legR * 2), V(0, legs * 0.5f, lz), cloth2, taper: 0.8f, segments: 6);
            p.Add(side < 0 ? "leg_l" : "leg_r", PartShape.Box, V(0.13f * H, 0.045f * H, legR * 2.1f), V(0.025f * H, 0.022f * H, lz), leather);
        }
        p.Sockets["head"] = new BodySocket("head", V(0, chestY + neck + head, 0), Vec3.Zero, head);
        p.Sockets["face"] = new BodySocket("head", V(head * 0.45f, chestY + neck + head * 0.5f, 0), Vec3.Zero, head);
        p.Sockets["chest"] = new BodySocket("torso", V(0, legs + torso * 0.5f, 0), Vec3.Zero, H);
        p.Sockets["belt"] = new BodySocket("hips", V(0, legs + 0.01f * H, 0), Vec3.Zero, H);
        p.Sockets["feet"] = new BodySocket("root", V(0, 0, 0), Vec3.Zero, H);
        p.Sockets["back"] = new BodySocket("torso", V(-depth * 0.55f, chestY - 0.04f * H, 0), Vec3.Zero, H);
        p.Sockets["off_hand"] = new BodySocket("arm_l", V(0.03f * H, chestY - 0.02f * H - arms * 0.85f, -shoulders / 2), Vec3.Zero, H);
        // features
        if (b.Has("ears_long"))
        {
            foreach (var side in new[] { -1f, 1f })
            {
                p.Add("head", PartShape.Cone, V(head * 0.16f, head * 0.5f, head * 0.26f), V(-head * 0.05f, chestY + neck + head * 0.72f, side * head * 0.5f), skin, V(side * -55, 0, 0), segments: 5);
            }
        }
        if (b.Has("snout"))
        {
            p.Add("head", PartShape.Ellipsoid, V(head * 0.5f, head * 0.34f, head * 0.5f), V(head * 0.5f, chestY + neck + head * 0.38f, 0), skin, segments: 6);
        }
        if (b.Has("horns"))
        {
            foreach (var side in new[] { -1f, 1f })
            {
                p.Add("head", PartShape.Cone, V(head * 0.16f, head * 0.5f, head * 0.16f), V(head * 0.05f, chestY + neck + head * 1.12f, side * head * 0.28f), accent, V(side * 20, -15, 0), segments: 5);
            }
        }
        if (b.Has("crest"))
        {
            p.Add("head", PartShape.Wedge, V(head * 0.5f, head * 0.4f, head * 0.12f), V(-head * 0.1f, chestY + neck + head * 1.12f, 0), hair);
        }
        if (b.Has("leaf_crown"))
        {
            for (var i = 0; i < 5; i++)
            {
                var a = i * 72f + b.U(i) * 20;
                p.Add("head", PartShape.Cone, V(head * 0.2f, head * 0.55f, head * 0.08f), V(0, chestY + neck + head * 1.15f, 0), accent, V(30, 25, a), segments: 4);
            }
        }
        if (b.Has("tail"))
        {
            p.Add("hips", PartShape.Cone, V(0.05f * H, 0.5f * H, 0.05f * H), V(-0.2f * H, legs - 0.05f * H, 0), skin, V(0, 110, 0), segments: 5);
        }
        if (b.Has("runes"))
        {
            for (var i = 0; i < 3; i++)
            {
                p.Add("torso", PartShape.Box, V(0.01f * H, 0.04f * H, 0.025f * H), V(depth * 0.5f, legs + torso * (0.3f + 0.2f * i), (i - 1) * shoulders * 0.2f), accent, glow: true);
            }
        }
        p.Height = chestY + neck + head * 1.05f;
        p.Width = shoulders * 1.1f;
    }

    private static void Quadruped(BodyBuild b)
    {
        var p = b.Plan;
        p.Rig = "quadruped";
        var H = b.Height;
        var width = b.S("width", 1);
        var legs = 0.42f * H * b.S("legs", 1);
        var torsoH = 0.3f * H;
        var torsoL = 0.78f * H * b.S("torso", 1);
        var W = 0.3f * H * width;
        var neck = 0.18f * H * b.S("neck", 1);
        var head = 0.2f * H * b.S("head", 1);
        var snout = b.S("snout", 1);
        var hide = b.C("hide", "#8a6d4b");
        var belly = b.C("belly", "#c2a98a");
        var hoof = b.C("hoof", "#3a2e24");
        var horn = b.C("horn", "#d9cfb5");
        var eyes = b.C("eyes", "#1b1612");
        var legR = 0.045f * H * width;
        var torsoY = legs + torsoH * 0.5f;
        var front = torsoL * 0.36f;
        var neckBase = V(torsoL * 0.45f, legs + torsoH * 0.85f, 0);
        var headAt = neckBase + V(neck * 0.9f, neck * 0.55f, 0);

        p.Bone("torso", "root", V(0, torsoY, 0));
        p.Bone("neck", "torso", neckBase);
        p.Bone("head", "neck", headAt);
        p.Bone("tail", "torso", V(-torsoL * 0.48f, legs + torsoH * 0.75f, 0));
        foreach (var (name, x, z) in new[] { ("leg_fl", front, -W * 0.36f), ("leg_fr", front, W * 0.36f), ("leg_bl", -front, -W * 0.36f), ("leg_br", -front, W * 0.36f) })
        {
            p.Bone(name, "torso", V(x, legs, z));
            p.Add(name, PartShape.Cylinder, V(legR * 2, legs * 0.98f, legR * 2), V(0, legs * 0.5f, z) + V(x, 0, 0), hide, taper: 0.75f, segments: 6);
            p.Add(name, PartShape.Box, V(legR * 2.4f, legR * 1.4f, legR * 2.2f), V(x + 0.01f * H, legR * 0.7f, z), hoof);
        }
        p.Add("torso", PartShape.Ellipsoid, V(torsoL, torsoH, W), V(0, torsoY, 0), hide);
        p.Add("torso", PartShape.Ellipsoid, V(torsoL * 0.5f, torsoH * 1.1f * b.S("rump", 1), W * 1.05f), V(-torsoL * 0.25f, torsoY + torsoH * 0.05f, 0), hide);
        p.Add("torso", PartShape.Ellipsoid, V(torsoL * 0.8f, torsoH * 0.5f, W * 0.8f), V(0.02f * H, torsoY - torsoH * 0.3f, 0), belly);
        var mane = b.S("mane", 0);
        if (mane > 0)
        {
            p.Add("torso", PartShape.Ellipsoid, V(torsoL * 0.45f, torsoH * 0.35f * mane, W * 0.6f), V(torsoL * 0.3f, legs + torsoH * 1.05f, 0), b.C("mane", "#3a2c24"), segments: 6);
        }
        p.Add("neck", PartShape.Cylinder, V(neck * 0.55f, neck * 1.15f, W * 0.5f), neckBase + V(neck * 0.45f, neck * 0.28f, 0), hide, V(0, -52, 0), taper: 0.8f, segments: 6);
        p.Add("head", PartShape.Ellipsoid, V(head * 0.9f, head * 0.8f, head * 0.7f), headAt + V(head * 0.1f, 0, 0), hide, segments: 8);
        p.Add("head", PartShape.Box, V(head * 0.6f * snout, head * 0.4f, head * 0.42f), headAt + V(head * 0.6f, -head * 0.12f, 0), hide);
        p.Add("head", PartShape.Box, V(head * 0.15f, head * 0.12f, head * 0.3f), headAt + V(head * 0.6f + head * 0.3f * snout, -head * 0.05f, 0), hoof);
        var eye = head * 0.12f;
        p.Add("head", PartShape.Box, V(eye * 0.6f, eye, eye), headAt + V(head * 0.35f, head * 0.12f, -head * 0.3f), eyes);
        p.Add("head", PartShape.Box, V(eye * 0.6f, eye, eye), headAt + V(head * 0.35f, head * 0.12f, head * 0.3f), eyes);
        foreach (var side in new[] { -1f, 1f })
        {
            p.Add("head", PartShape.Cone, V(head * 0.18f, head * 0.35f, head * 0.12f), headAt + V(-head * 0.1f, head * 0.45f, side * head * 0.28f), hide, V(side * -25, 10, 0), segments: 4);
        }
        var tail = b.S("tail", 1);
        if (b.Has("tail_bushy"))
        {
            p.Add("tail", PartShape.Ellipsoid, V(0.42f * H * tail, 0.11f * H, 0.11f * H), V(-torsoL * 0.48f - 0.18f * H * tail, legs + torsoH * 0.7f, 0), hide, V(0, 20, 0), segments: 6);
        }
        else if (tail > 0.01f)
        {
            p.Add("tail", PartShape.Cone, V(0.05f * H, 0.3f * H * tail, 0.05f * H), V(-torsoL * 0.48f - 0.1f * H * tail, legs + torsoH * 0.6f, 0), hide, V(0, 115, 0), segments: 5);
        }
        if (b.Has("antlers"))
        {
            foreach (var side in new[] { -1f, 1f })
            {
                var baseAt = headAt + V(-head * 0.05f, head * 0.5f, side * head * 0.25f);
                p.Add("head", PartShape.Cone, V(head * 0.12f, head * 0.9f, head * 0.12f), baseAt + V(0, head * 0.45f, side * head * 0.1f), horn, V(side * 25, -10, 0), segments: 4);
                p.Add("head", PartShape.Cone, V(head * 0.08f, head * 0.5f, head * 0.08f), baseAt + V(head * 0.1f, head * 0.6f, side * head * 0.3f), horn, V(side * 55, 25, 0), segments: 4);
                p.Add("head", PartShape.Cone, V(head * 0.08f, head * 0.45f, head * 0.08f), baseAt + V(-head * 0.15f, head * 0.7f, side * head * 0.25f), horn, V(side * 35, -45, 0), segments: 4);
            }
        }
        if (b.Has("tusks"))
        {
            foreach (var side in new[] { -1f, 1f })
            {
                p.Add("head", PartShape.Cone, V(head * 0.1f, head * 0.35f, head * 0.1f), headAt + V(head * 0.8f, -head * 0.1f, side * head * 0.22f), horn, V(side * 15, -35, 0), segments: 4);
            }
        }
        p.Sockets["head"] = new BodySocket("head", headAt + V(0, head * 0.5f, 0), Vec3.Zero, head);
        p.Sockets["back"] = new BodySocket("torso", V(0, legs + torsoH, 0), Vec3.Zero, H);
        p.Height = MathF.Max(legs + torsoH, headAt.Y + head * 0.6f);
        p.Width = torsoL;
    }

    private static void Crawler(BodyBuild b)
    {
        var p = b.Plan;
        p.Rig = "crawler";
        var H = b.Height;
        var W = b.Radius * 2f * b.S("width", 1);
        var shell = b.C("shell", "#314f52");
        var flesh = b.C("flesh", "#5e8a8c");
        var claw = b.C("claw", "#d8d2ba");
        var eyes = b.C("eyes", "#81c3c9");
        var shellH = H * 0.6f * b.S("shell", 1);
        p.Bone("shell", "root", V(0, H * 0.3f, 0));
        p.Bone("claw_l", "shell", V(W * 0.4f, H * 0.3f, -W * 0.3f));
        p.Bone("claw_r", "shell", V(W * 0.4f, H * 0.3f, W * 0.3f));
        p.Add("shell", PartShape.Ellipsoid, V(W * 0.9f, shellH, W), V(0, H * 0.5f, 0), shell, segments: 8);
        p.Add("shell", PartShape.Ellipsoid, V(W * 0.7f, H * 0.35f, W * 0.8f), V(0, H * 0.28f, 0), flesh, segments: 8);
        for (var i = 0; i < 3; i++)
        {
            var x = (i - 1) * W * 0.3f;
            foreach (var side in new[] { -1f, 1f })
            {
                p.Add("shell", PartShape.Cone, V(H * 0.12f, W * 0.55f * b.S("legs", 1), H * 0.12f), V(x, H * 0.25f, side * W * 0.55f), flesh, V(side * 115, 0, 0), segments: 4);
            }
        }
        foreach (var side in new[] { -1f, 1f })
        {
            var bone = side < 0 ? "claw_l" : "claw_r";
            p.Add(bone, PartShape.Wedge, V(W * 0.4f * b.S("claws", 1), H * 0.25f, W * 0.18f), V(W * 0.6f, H * 0.3f, side * W * 0.3f), claw);
            var eye = H * 0.1f * b.S("eyes", 1);
            p.Add("shell", PartShape.Ellipsoid, V(eye, eye, eye), V(W * 0.4f, H * 0.62f, side * W * 0.15f), eyes, glow: true, segments: 6);
        }
        p.Height = H * 0.8f;
        p.Width = W;
    }

    private static void Wisp(BodyBuild b)
    {
        var p = b.Plan;
        p.Rig = "wisp";
        var H = b.Height;
        var hover = H * 0.2f * b.S("hover", 1);
        p.Hover = hover;
        var width = 0.3f * H * b.S("width", 1);
        var torsoH = 0.5f * H * b.S("torso", 1);
        var head = 0.16f * H * b.S("head", 1);
        var cloth = b.C("cloth", "#4a4858");
        var cloth2 = b.C("cloth2", "#2c2a36");
        var face = b.C("face", "#d9d4c0");
        var glow = b.C("glow", "#c1a1c8");
        var torsoBase = hover + H * 0.08f;
        var chestY = torsoBase + torsoH;
        p.Bone("torso", "root", V(0, torsoBase, 0));
        p.Bone("head", "torso", V(0, chestY, 0));
        p.Add("torso", PartShape.Cylinder, V(width, torsoH, width * 0.9f), V(0, torsoBase + torsoH / 2, 0), cloth, taper: 0.3f, segments: 7);
        p.Add("torso", PartShape.Ellipsoid, V(width * 0.5f, H * 0.08f, width * 1.1f), V(0, chestY - H * 0.02f, 0), cloth2, segments: 7);
        var trail = b.S("trail", 1);
        for (var i = 0; i < 3; i++)
        {
            var a = (i - 1) * 60f;
            p.Add("torso", PartShape.Wedge, V(width * 0.08f, H * 0.25f * trail, width * 0.3f), V(0, torsoBase - H * 0.06f * trail, 0), cloth2, V(0, 180, a + 90));
        }
        if (b.Has("hood"))
        {
            p.Add("head", PartShape.Ellipsoid, V(head * 1.1f, head * 1.15f, head * 1.05f), V(-head * 0.05f, chestY + head * 0.55f, 0), cloth, segments: 7);
            p.Add("head", PartShape.Ellipsoid, V(head * 0.55f, head * 0.6f, head * 0.6f), V(head * 0.28f, chestY + head * 0.45f, 0), face, glow: true, segments: 6);
        }
        else
        {
            p.Add("head", PartShape.Ellipsoid, V(head * 0.9f, head, head * 0.85f), V(0, chestY + head / 2, 0), face, glow: true);
        }
        var eye = head * 0.14f;
        p.Add("head", PartShape.Box, V(eye * 0.5f, eye * 0.7f, eye), V(head * 0.5f, chestY + head * 0.55f, -head * 0.18f), glow, glow: true);
        p.Add("head", PartShape.Box, V(eye * 0.5f, eye * 0.7f, eye), V(head * 0.5f, chestY + head * 0.55f, head * 0.18f), glow, glow: true);
        if (b.Has("arms"))
        {
            var arms = 0.3f * H * b.S("arms", 1);
            var armR = 0.03f * H;
            foreach (var side in new[] { -1f, 1f })
            {
                var bone = side < 0 ? "arm_l" : "arm_r";
                var z = side * width * 0.55f;
                p.Bone(bone, "torso", V(0, chestY - H * 0.04f, z));
                p.Add(bone, PartShape.Cylinder, V(armR * 2, arms, armR * 2), V(0, chestY - H * 0.04f - arms / 2, z), cloth, taper: 0.7f, segments: 5);
                p.Add(bone, PartShape.Ellipsoid, V(armR * 2.4f, armR * 2.4f, armR * 2.2f), V(0.01f * H, chestY - H * 0.04f - arms, z), face, segments: 5);
                p.Sockets[side < 0 ? "hand_l" : "hand_r"] = new BodySocket(bone, V(0.02f * H, chestY - H * 0.04f - arms, z), Vec3.Zero, H);
            }
        }
        p.Sockets["head"] = new BodySocket("head", V(0, chestY + head, 0), Vec3.Zero, head);
        p.Height = chestY + head * 1.15f;
        p.Width = width * 1.2f;
    }

    private static void Totem(BodyBuild b)
    {
        var p = b.Plan;
        var H = b.Height;
        var R = b.Radius * 0.7f * b.S("width", 1);
        var wood = b.C("wood", "#4a3226");
        var band = b.C("band", "#b89a67");
        var face = b.C("face", "#edac69");
        p.Add("root", PartShape.Cylinder, V(R * 2, H * 0.92f, R * 2), V(0, H * 0.46f, 0), wood, taper: 0.85f, segments: 7);
        var faces = (int)b.S("faces", 3);
        for (var i = 0; i < faces; i++)
        {
            var y = H * (0.2f + 0.6f * i / MathF.Max(1, faces - 1));
            p.Add("root", PartShape.Cylinder, V(R * 2.3f, H * 0.04f, R * 2.3f), V(0, y - H * 0.1f, 0), band, segments: 7);
            p.Add("root", PartShape.Box, V(R * 0.5f, H * 0.06f, R * 0.5f), V(R * 0.95f, y, 0), face, glow: true);
        }
        if (b.S("crown", 1) > 0)
        {
            p.Add("root", PartShape.Cone, V(R * 2.2f, H * 0.16f, R * 2.2f), V(0, H * 0.98f, 0), band, segments: 7);
        }
        p.Height = H * 1.06f;
        p.Width = R * 2.3f;
    }

    private static void Dome(BodyBuild b)
    {
        var p = b.Plan;
        var H = b.Height;
        var R = b.Radius * 1.6f * b.S("width", 1);
        var shell = b.C("shell", "#eee0a3");
        var rib = b.C("rib", "#b89a67");
        p.Add("root", PartShape.Ellipsoid, V(R * 2, H * 2, R * 2), V(0, 0, 0), shell, glow: true, segments: 10);
        var ribs = (int)b.S("ribs", 6);
        for (var i = 0; i < ribs; i++)
        {
            var a = 360f * i / ribs;
            p.Add("root", PartShape.Box, V(R * 1.6f, H * 0.06f, 0.04f), V(0, H * 0.55f, 0), rib, V(0, 0, a));
        }
        p.Height = H;
        p.Width = R * 2;
    }

    private static void Trap(BodyBuild b)
    {
        var p = b.Plan;
        var H = b.Height;
        var R = b.Radius * 1.5f * b.S("width", 1);
        var metal = b.C("metal", "#7c7a78");
        var teeth = b.C("teeth", "#c5ae82");
        p.Add("root", PartShape.Cylinder, V(R * 2, H * 0.35f, R * 2), V(0, H * 0.18f, 0), metal, segments: 10);
        var n = (int)b.S("teeth", 8);
        for (var i = 0; i < n; i++)
        {
            var a = 2 * MathF.PI * i / n;
            p.Add("root", PartShape.Cone, V(R * 0.22f, H * 0.9f, R * 0.12f), V(MathF.Cos(a) * R * 0.85f, H * 0.6f, MathF.Sin(a) * R * 0.85f), teeth, V(0, 0, -a * 180 / MathF.PI), segments: 4);
        }
        p.Height = H;
        p.Width = R * 2;
    }

    private static void Orb(BodyBuild b)
    {
        var p = b.Plan;
        var r = MathF.Max(0.12f, b.Radius * 0.6f) * b.S("core", 1);
        var core = b.C("core", "#eee9da");
        var halo = b.C("halo", "#edac69");
        p.Add("root", PartShape.Ellipsoid, V(r * 2, r * 2, r * 2), V(0, b.Height * 0.5f, 0), core, glow: true, segments: 8);
        p.Add("root", PartShape.Ellipsoid, V(r * 3.2f * b.S("halo", 1), r * 1.6f, r * 1.6f), V(-r * 0.6f, b.Height * 0.5f, 0), halo, glow: true, segments: 6);
        p.Height = b.Height;
        p.Width = r * 3;
    }

    // ------------------------------------------------------------ flora and stone

    private static void Tree(BodyBuild b)
    {
        var p = b.Plan;
        p.Rig = "tree";
        var H = b.Height;
        var trunkR = MathF.Max(0.12f, b.Radius * 0.55f) * b.S("trunk", 1);
        var bark = b.C("bark", "#5a4634");
        var canopy = b.C("canopy", "#4f7a3c");
        var canopy2 = b.C("canopy2", "#6f9a4e");
        var lean = b.S("lean", 0) * 8f * (b.U(1) - 0.5f) * 2;
        var layers = Math.Max(1, (int)b.S("layers", 3));
        var spread = b.S("spread", 1);
        var taper = b.S("taper", 0.7f);
        var bare = b.S("bare", 0) > 0.5f;
        var trunkH = bare ? H * 0.7f : H * 0.45f;
        p.Bone("canopy", "root", V(0, trunkH * 0.8f, 0));
        p.Add("root", PartShape.Cylinder, V(trunkR * 2, trunkH, trunkR * 2), V(0, trunkH / 2, 0), bark, V(0, 0, lean), taper: 0.7f, segments: 7);
        p.Add("root", PartShape.Cylinder, V(trunkR * 3, trunkH * 0.12f, trunkR * 3), V(0, trunkH * 0.06f, 0), bark, taper: 0.7f, segments: 7);
        if (bare)
        {
            var n = 5;
            for (var i = 0; i < n; i++)
            {
                var a = 360f * i / n + b.U(i) * 40;
                var len = H * (0.3f + 0.15f * b.U(10 + i)) * spread;
                p.Add("canopy", PartShape.Cone, V(trunkR * 1.2f, len, trunkR * 1.2f), V(0, trunkH * 0.8f + len * 0.35f, 0), bark, V(0, 35 + 20 * b.U(20 + i), a), segments: 5);
                if (p.Glow is { } g)
                {
                    p.Add("canopy", PartShape.Ellipsoid, V(trunkR * 1.4f, trunkR * 1.4f, trunkR * 1.4f), PartGeometry.Turn(V(0, len * 0.78f, 0), 0, (35 + 20 * b.U(20 + i)) * MathF.PI / 180, a * MathF.PI / 180) + V(0, trunkH * 0.8f, 0), canopy2, glow: true, segments: 5);
                }
            }
            p.Height = H;
            p.Width = H * 0.6f * spread;
            return;
        }
        var conifer = taper <= 0.5f;
        var canopyR = H * 0.3f * spread * b.S("canopy", 1);
        var canopyBase = trunkH * 0.85f;
        var canopyH = H - canopyBase;
        for (var i = 0; i < layers; i++)
        {
            var t = layers == 1 ? 0.5f : (float)i / (layers - 1);
            var color = i % 2 == 0 ? canopy : canopy2;
            if (conifer)
            {
                var r = canopyR * (1.25f - 0.75f * t);
                var h = canopyH * (0.55f);
                var y = canopyBase + canopyH * (0.15f + 0.65f * t);
                p.Add("canopy", PartShape.Cone, V(r * 2, h, r * 2), V(0, y, 0), color, segments: 7);
            }
            else
            {
                var r = canopyR * (0.75f + 0.45f * MathF.Sin(MathF.PI * (0.2f + 0.6f * t)));
                var y = canopyBase + canopyH * (0.2f + 0.6f * t);
                var dx = (b.U(30 + i) - 0.5f) * canopyR * 0.6f;
                var dz = (b.U(40 + i) - 0.5f) * canopyR * 0.6f;
                p.Add("canopy", PartShape.Ellipsoid, V(r * 2, canopyH * 0.55f * taper + r * 0.6f, r * 2), V(dx, y, dz), color, segments: 8);
            }
        }
        p.Height = H;
        p.Width = canopyR * 2.4f;
    }

    private static void Bush(BodyBuild b)
    {
        var p = b.Plan;
        var H = b.Height;
        var R = b.Radius * 1.3f * b.S("spread", 1);
        var leaf = b.C("leaf", "#5f7a46");
        var leaf2 = b.C("leaf2", "#7f9a5c");
        var lobes = Math.Max(1, (int)b.S("lobes", 4));
        for (var i = 0; i < lobes; i++)
        {
            var a = 2 * MathF.PI * i / lobes + b.U(i);
            var r = R * (0.55f + 0.25f * b.U(10 + i));
            p.Add("root", PartShape.Ellipsoid, V(r * 2, H * (0.7f + 0.3f * b.U(20 + i)), r * 2), V(MathF.Cos(a) * R * 0.4f, H * 0.4f, MathF.Sin(a) * R * 0.4f), i % 2 == 0 ? leaf : leaf2, segments: 6);
        }
        p.Add("root", PartShape.Ellipsoid, V(R * 1.4f, H * 1.1f, R * 1.4f), V(0, H * 0.5f, 0), leaf, segments: 6);
        p.Height = H;
        p.Width = R * 2;
    }

    private static void Fern(BodyBuild b)
    {
        var p = b.Plan;
        var H = b.Height;
        var R = b.Radius * 1.5f * b.S("spread", 1);
        var leaf = b.C("leaf", "#4a7a3c");
        var leaf2 = b.C("leaf2", "#6f9a4e");
        var fronds = Math.Max(3, (int)b.S("fronds", 6));
        for (var i = 0; i < fronds; i++)
        {
            var a = 360f * i / fronds + b.U(i) * 30;
            p.Add("root", PartShape.Wedge, V(R * 1.1f, H * 0.08f, R * 0.35f), V(0, H * 0.35f, 0), i % 2 == 0 ? leaf : leaf2, V(0, 35 + 15 * b.U(10 + i), a));
        }
        p.Height = H;
        p.Width = R * 2;
    }

    private static void Reed(BodyBuild b)
    {
        var p = b.Plan;
        var H = b.Height;
        var R = b.Radius * 1.2f * b.S("spread", 1);
        var leaf = b.C("leaf", "#7f8a4a");
        var leaf2 = b.C("leaf2", "#b8a35a");
        var stalks = Math.Max(2, (int)b.S("stalks", 7));
        for (var i = 0; i < stalks; i++)
        {
            var a = 2 * MathF.PI * i / stalks + b.U(i);
            var h = H * (0.7f + 0.3f * b.U(10 + i));
            var x = MathF.Cos(a) * R * 0.5f;
            var z = MathF.Sin(a) * R * 0.5f;
            p.Add("root", PartShape.Cylinder, V(0.05f, h, 0.05f), V(x, h / 2, z), i % 3 == 0 ? leaf2 : leaf, V((b.U(20 + i) - 0.5f) * 16, 0, (b.U(30 + i) - 0.5f) * 16), taper: 0.5f, segments: 4);
            p.Add("root", PartShape.Ellipsoid, V(0.08f, h * 0.18f, 0.08f), V(x, h * 0.95f, z), leaf2, segments: 4);
        }
        p.Height = H;
        p.Width = R * 1.4f;
    }

    private static void Plant(BodyBuild b)
    {
        var p = b.Plan;
        var H = b.Height;
        var stem = b.C("stem", "#5f7a46");
        var petal = b.C("petal", "#edac69");
        var bud = b.C("bud", "#ffd27a");
        var stemH = H * 0.7f * b.S("stem", 1);
        p.Add("root", PartShape.Cylinder, V(0.06f, stemH, 0.06f), V(0, stemH / 2, 0), stem, taper: 0.7f, segments: 5);
        var petals = Math.Max(3, (int)b.S("petals", 5));
        for (var i = 0; i < petals; i++)
        {
            var a = 360f * i / petals;
            p.Add("root", PartShape.Wedge, V(H * 0.3f, 0.03f, H * 0.14f), V(0, stemH, 0), petal, V(0, 25, a));
        }
        p.Add("root", PartShape.Ellipsoid, V(H * 0.22f, H * 0.22f, H * 0.22f), V(0, stemH + H * 0.08f, 0), bud, glow: true, segments: 6);
        for (var i = 0; i < 3; i++)
        {
            var a = 120f * i + 30;
            p.Add("root", PartShape.Wedge, V(H * 0.35f, 0.03f, H * 0.12f), V(0, H * 0.05f, 0), stem, V(0, 12, a));
        }
        p.Height = H;
        p.Width = H * 0.6f;
    }

    private static void Knot(BodyBuild b)
    {
        var p = b.Plan;
        p.Rig = "knot";
        var H = b.Height;
        var R = MathF.Max(b.Radius, H * 0.3f) * b.S("spread", 1);
        var strand = b.C("strand", "#d8d2ba");
        var strand2 = b.C("strand2", "#b89a67");
        var core = b.C("core", "#eee9da");
        var loops = Math.Max(2, (int)b.S("loops", 5));
        var thick = MathF.Max(0.05f, R * 0.14f);
        for (var i = 0; i < loops; i++)
        {
            var yaw = 360f * i / loops + b.U(i) * 50;
            var pitch = 20 + 50 * b.U(10 + i);
            var len = R * (1.4f + 0.6f * b.U(20 + i));
            p.Add("root", PartShape.Cylinder, V(thick, len, thick), V((b.U(30 + i) - 0.5f) * R * 0.5f, H * (0.3f + 0.4f * b.U(40 + i)), (b.U(50 + i) - 0.5f) * R * 0.5f), i % 2 == 0 ? strand : strand2, V(0, pitch, yaw), segments: 5);
        }
        p.Add("root", PartShape.Ellipsoid, V(R * 0.9f, H * 0.55f, R * 0.9f), V(0, H * 0.45f, 0), strand2, segments: 6);
        p.Add("root", PartShape.Ellipsoid, V(R * 0.35f, R * 0.35f, R * 0.35f), V(0, H * 0.5f, 0), core, glow: p.Glow is not null, segments: 6);
        p.Height = H;
        p.Width = R * 2.2f;
    }

    private static void Rock(BodyBuild b)
    {
        var p = b.Plan;
        var H = b.Height;
        var R = b.Radius * 1.1f;
        var stone = b.C("stone", "#8c8a7a");
        var stone2 = b.C("stone2", "#6e6c60");
        var fleck = b.C("fleck", "#b89a67");
        var flat = b.S("flat", 0.3f);
        p.Add("root", PartShape.Ellipsoid, V(R * 2, H * (1.1f - 0.3f * flat), R * 1.8f), V(0, H * 0.4f, 0), stone, V(0, (b.U(1) - 0.5f) * 20, b.U(2) * 360), segments: 6);
        var facets = Math.Max(1, (int)(2 + 2 * b.S("facets", 1)));
        for (var i = 0; i < facets; i++)
        {
            var a = 2 * MathF.PI * i / facets + b.U(10 + i);
            var r = R * (0.5f + 0.3f * b.U(20 + i));
            p.Add("root", PartShape.Box, V(r * 1.4f, H * (0.5f + 0.3f * b.U(30 + i)), r * 1.2f), V(MathF.Cos(a) * R * 0.5f, H * 0.3f, MathF.Sin(a) * R * 0.5f), i % 2 == 0 ? stone2 : stone, V(b.U(40 + i) * 30, b.U(50 + i) * 30, a * 180 / MathF.PI));
        }
        if (b.S("flecks", 0) > 0)
        {
            for (var i = 0; i < 6; i++)
            {
                var a = 2 * MathF.PI * i / 6 + b.U(60 + i);
                p.Add("root", PartShape.Box, V(R * 0.18f, H * 0.12f, R * 0.18f), V(MathF.Cos(a) * R * 0.85f, H * (0.3f + 0.4f * b.U(70 + i)), MathF.Sin(a) * R * 0.8f), fleck, V(b.U(80 + i) * 40, 0, a * 180 / MathF.PI));
            }
        }
        p.Height = H;
        p.Width = R * 2.2f;
    }

    private static void Stone(BodyBuild b)
    {
        var p = b.Plan;
        var H = b.Height;
        var R = b.Radius;
        var stone = b.C("stone", "#9a9484");
        var stone2 = b.C("stone2", "#6e6c60");
        var rune = b.C("rune", "#b89a67");
        var lean = b.S("lean", 0.1f) * 12f * (b.U(1) - 0.5f) * 2;
        p.Add("root", PartShape.Cylinder, V(R * 1.6f, H * 0.95f, R * 1.1f), V(0, H * 0.475f, 0), stone, V(0, 0, lean), taper: b.S("taper", 0.7f), segments: 5);
        p.Add("root", PartShape.Cylinder, V(R * 2.2f, H * 0.08f, R * 1.8f), V(0, H * 0.04f, 0), stone2, segments: 6);
        if (b.S("cap", 1) > 0)
        {
            p.Add("root", PartShape.Box, V(R * 1.5f, H * 0.08f, R * 1.1f), V(0, H * 0.97f, 0), stone2, V(0, 0, lean));
        }
        for (var i = 0; i < 3; i++)
        {
            p.Add("root", PartShape.Box, V(0.02f, H * 0.07f, R * 0.25f), V(R * 0.78f, H * (0.3f + 0.2f * i), 0), rune, glow: false);
        }
        p.Height = H;
        p.Width = R * 2.2f;
    }

    private static void Log(BodyBuild b)
    {
        var p = b.Plan;
        var H = b.Height;
        var L = b.Radius * 2.2f * b.S("length", 1);
        var bark = b.C("bark", "#5a4634");
        var wood = b.C("wood", "#c2a98a");
        var moss = b.C("moss", "#71816c");
        var r = H * 0.45f;
        var yaw = b.U(1) * 360;
        p.Add("root", PartShape.Cylinder, V(r * 2, L, r * 2), V(0, r, 0), bark, V(0, 90, yaw), taper: 0.85f, segments: 7);
        p.Add("root", PartShape.Cylinder, V(r * 1.7f, 0.04f, r * 1.7f), PartGeometry.Turn(V(0, L * 0.5f + 0.01f, 0), 0, MathF.PI / 2, yaw * MathF.PI / 180) + V(0, r, 0), wood, V(0, 90, yaw), segments: 7);
        var stubs = (int)b.S("stubs", 3);
        for (var i = 0; i < stubs; i++)
        {
            var along = (b.U(10 + i) - 0.5f) * L * 0.7f;
            var at = PartGeometry.Turn(V(0, along, 0), 0, MathF.PI / 2, yaw * MathF.PI / 180) + V(0, r * 1.2f, 0);
            p.Add("root", PartShape.Cone, V(r * 0.5f, r * 1.4f, r * 0.5f), at + V(0, r * 0.5f, 0), bark, V((b.U(20 + i) - 0.5f) * 60, 0, b.U(30 + i) * 360), segments: 4);
        }
        p.Add("root", PartShape.Ellipsoid, V(L * 0.4f, r * 0.5f, r * 1.4f), V(0, r * 1.7f, 0) + PartGeometry.Turn(V(0, L * 0.1f, 0), 0, MathF.PI / 2, yaw * MathF.PI / 180), moss, V(0, 0, yaw), segments: 6);
        p.Height = r * 2;
        p.Width = L;
    }

    // ------------------------------------------------------------ stations, props and places

    private static void Anvil(BodyBuild b)
    {
        var p = b.Plan;
        var H = b.Height;
        var R = b.Radius;
        var metal = b.C("metal", "#5c5c60");
        var wood = b.C("wood", "#4a3226");
        var band = b.C("band", "#b89a67");
        p.Add("root", PartShape.Cylinder, V(R * 1.6f, H * 0.5f, R * 1.6f), V(0, H * 0.25f, 0), wood, taper: 0.9f, segments: 8);
        p.Add("root", PartShape.Cylinder, V(R * 1.7f, H * 0.05f, R * 1.7f), V(0, H * 0.45f, 0), band, segments: 8);
        p.Add("root", PartShape.Box, V(R * 1.9f * b.S("block", 1), H * 0.3f, R * 0.9f), V(0, H * 0.68f, 0), metal);
        p.Add("root", PartShape.Box, V(R * 1.5f, H * 0.12f, R * 1.1f), V(0, H * 0.9f, 0), metal);
        p.Add("root", PartShape.Cone, V(R * 0.6f, R * 1.1f * b.S("horn", 1), R * 0.6f), V(R * 1.35f, H * 0.86f, 0), metal, V(0, -90, 0), segments: 6);
        p.Height = H;
        p.Width = R * 3;
    }

    private static void Loom(BodyBuild b)
    {
        var p = b.Plan;
        var H = b.Height;
        var W = b.Radius * 1.8f * b.S("frame", 1);
        var wood = b.C("wood", "#6b4a2e");
        var thread = b.C("thread", "#d8d2ba");
        var thread2 = b.C("thread2", "#81c3c9");
        var band = b.C("band", "#b89a67");
        var post = W * 0.08f;
        foreach (var side in new[] { -1f, 1f })
        {
            p.Add("root", PartShape.Box, V(post, H, post), V(0, H / 2, side * W / 2), wood);
            p.Add("root", PartShape.Box, V(W * 0.5f, post, post), V(0, post / 2, side * W / 2), wood);
        }
        p.Add("root", PartShape.Box, V(post, post, W), V(0, H * 0.95f, 0), wood);
        p.Add("root", PartShape.Box, V(post, post, W), V(0, H * 0.25f, 0), wood);
        p.Add("root", PartShape.Box, V(post * 1.2f, post * 0.4f, W), V(0, H * 0.6f, 0), band);
        var threads = Math.Max(2, (int)b.S("threads", 12));
        for (var i = 0; i < threads; i++)
        {
            var z = -W * 0.42f + W * 0.84f * i / (threads - 1);
            p.Add("root", PartShape.Box, V(0.015f, H * 0.68f, 0.015f), V(0, H * 0.6f, z), i % 4 == 0 ? thread2 : thread);
        }
        p.Height = H;
        p.Width = W;
    }

    private static void Chest(BodyBuild b)
    {
        var p = b.Plan;
        var H = b.Height;
        var W = b.Radius * 2f;
        var D = b.Radius * 1.3f;
        var wood = b.C("wood", "#4a3226");
        var band = b.C("band", "#b89a67");
        var lockC = b.C("lock", "#7c7a78");
        p.Add("root", PartShape.Box, V(D, H * 0.62f, W), V(0, H * 0.31f, 0), wood);
        p.Add("root", PartShape.Cylinder, V(D, W, D * 0.95f), V(0, H * 0.62f, 0), wood, V(90, 0, 0), segments: 8);
        var bands = Math.Max(1, (int)b.S("bands", 2));
        for (var i = 0; i < bands; i++)
        {
            var z = bands == 1 ? 0 : -W * 0.3f + W * 0.6f * i / (bands - 1);
            p.Add("root", PartShape.Box, V(D * 1.04f, H * 0.64f, W * 0.08f), V(0, H * 0.31f, z), band);
            p.Add("root", PartShape.Cylinder, V(D * 1.04f, W * 0.08f, D * 0.99f), V(0, H * 0.62f, z), band, V(90, 0, 0), segments: 8);
        }
        p.Add("root", PartShape.Box, V(0.04f, H * 0.18f, W * 0.14f), V(D * 0.52f, H * 0.6f, 0), lockC);
        p.Height = H * 1.1f;
        p.Width = W;
    }

    private static void Shrine(BodyBuild b)
    {
        var p = b.Plan;
        var H = b.Height;
        var R = b.Radius;
        var stone = b.C("stone", "#d8d2ba");
        var stone2 = b.C("stone2", "#b8b2a0");
        var light = b.C("light", "#eee0a3");
        var steps = Math.Max(0, (int)b.S("steps", 2));
        var y = 0f;
        for (var i = 0; i < steps; i++)
        {
            var w = R * (2.6f - 0.5f * i);
            p.Add("root", PartShape.Box, V(w, H * 0.08f, w), V(0, y + H * 0.04f, 0), i % 2 == 0 ? stone2 : stone);
            y += H * 0.08f;
        }
        foreach (var side in new[] { -1f, 1f })
        {
            p.Add("root", PartShape.Box, V(R * 0.5f, H - y - H * 0.1f, R * 0.4f), V(0, y + (H - y - H * 0.1f) / 2, side * R * 0.65f), stone);
        }
        p.Add("root", PartShape.Box, V(R * 0.7f, H * 0.12f, R * 1.9f), V(0, H - H * 0.06f, 0), stone2);
        p.Add("root", PartShape.Wedge, V(R * 0.8f, H * 0.14f, R * 2.1f), V(0, H + H * 0.05f, 0), stone);
        p.Add("root", PartShape.Box, V(R * 0.35f, H - y - H * 0.1f, R * 0.9f), V(-R * 0.2f, y + (H - y - H * 0.1f) / 2, 0), stone2);
        if (b.S("niche", 1) > 0)
        {
            p.Add("root", PartShape.Ellipsoid, V(R * 0.3f, R * 0.3f, R * 0.3f), V(R * 0.05f, y + (H - y) * 0.5f, 0), light, glow: true, segments: 6);
        }
        p.Height = H * 1.12f;
        p.Width = R * 2.6f;
    }

    private static void Marker(BodyBuild b)
    {
        var p = b.Plan;
        var H = MathF.Max(1.5f, 3f * b.S("height", 1));
        var R = 0.45f;
        var stone = b.C("stone", "#d8d2ba");
        var stone2 = b.C("stone2", "#b8b2a0");
        var rune = b.C("rune", "#b89a67");
        if (b.S("knotted", 0) > 0)
        {
            for (var i = 0; i < 4; i++)
            {
                var yaw = 90f * i + b.U(i) * 30;
                p.Add("root", PartShape.Cylinder, V(R * 0.5f, H * 0.9f, R * 0.5f), V(0, H * 0.45f, 0), i % 2 == 0 ? stone : stone2, V(0, 12 + 10 * b.U(10 + i), yaw), taper: 0.6f, segments: 5);
            }
            p.Add("root", PartShape.Ellipsoid, V(R * 1.2f, R * 1.2f, R * 1.2f), V(0, H * 0.8f, 0), rune, glow: true, segments: 6);
        }
        else
        {
            p.Add("root", PartShape.Cylinder, V(R * 1.6f, H * 0.85f, R * 1.2f), V(0, H * 0.425f, 0), stone, taper: 0.8f, segments: 4);
            p.Add("root", PartShape.Box, V(R * 2.2f, H * 0.1f, R * 1.8f), V(0, H * 0.05f, 0), stone2);
            if (b.S("cap", 1) > 0)
            {
                p.Add("root", PartShape.Cone, V(R * 1.5f, H * 0.2f, R * 1.2f), V(0, H * 0.95f, 0), stone2, segments: 4);
            }
            for (var i = 0; i < 3; i++)
            {
                p.Add("root", PartShape.Box, V(0.02f, H * 0.08f, R * 0.3f), V(R * 0.72f, H * (0.4f + 0.15f * i), 0), rune, glow: true);
            }
        }
        p.Height = H;
        p.Width = R * 2.4f;
    }

    private static void Fount(BodyBuild b)
    {
        var p = b.Plan;
        var H = b.Height;
        var R = MathF.Max(0.6f, b.Radius) * 1.4f * b.S("basin", 1);
        var stone = b.C("stone", "#d8d2ba");
        var stone2 = b.C("stone2", "#b8b2a0");
        var water = b.C("water", "#314f52");
        var light = b.C("light", "#9ad6d8");
        p.Add("root", PartShape.Cylinder, V(R * 2.2f, H * 0.12f, R * 2.2f), V(0, H * 0.06f, 0), stone2, segments: 9);
        p.Add("root", PartShape.Cylinder, V(R * 2, H * 0.45f, R * 2), V(0, H * 0.3f, 0), stone, taper: 1.08f, segments: 9);
        p.Add("root", PartShape.Cylinder, V(R * 2.2f, H * 0.08f * b.S("rim", 1), R * 2.2f), V(0, H * 0.55f, 0), stone2, segments: 9);
        p.Add("root", PartShape.Cylinder, V(R * 1.7f, 0.03f, R * 1.7f), V(0, H * 0.52f, 0), water, segments: 9);
        p.Add("root", PartShape.Ellipsoid, V(R * 0.6f, 0.06f, R * 0.6f), V(0, H * 0.54f, 0), light, glow: true, segments: 8);
        if (b.S("spill", 1) > 0)
        {
            p.Add("root", PartShape.Cylinder, V(R * 0.3f, H * 0.5f, R * 0.3f), V(0, H * 0.75f, 0), stone, taper: 0.7f, segments: 6);
            p.Add("root", PartShape.Ellipsoid, V(R * 0.5f, R * 0.3f, R * 0.5f), V(0, H * 1.02f, 0), light, glow: true, segments: 6);
        }
        p.Height = H;
        p.Width = R * 2.2f;
    }

    private static void Drop(BodyBuild b)
    {
        var p = b.Plan;
        var ring = b.C("ring", "#eee9da");
        p.Add("root", PartShape.Cylinder, V(0.7f, 0.03f, 0.7f), V(0, 0.015f, 0), ring, taper: 1, segments: 10);
        p.Height = 0.3f;
        p.Width = 0.7f;
    }
}
