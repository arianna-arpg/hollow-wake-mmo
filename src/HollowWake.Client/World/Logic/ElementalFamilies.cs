using HollowWake.Client.UI.Logic;

namespace HollowWake.Client.World.Logic;

/// <summary>Free-form material bodies, never assembled humanoids. Twin of elementalFamilies.ts.</summary>
public static class ElementalFamilies
{
    private static Vec3 V(float x, float y, float z) => new(x, y, z);

    private static void Begin(BodyBuild b)
    {
        var p = b.Plan;
        p.Rig = "wisp";
        p.Height = b.Height;
        p.Bone("torso", "root", V(0, b.Height * 0.45f, 0));
        p.Bone("head", "torso", V(0, b.Height * 0.72f, 0));
        p.Bone("arm_l", "torso", V(0, b.Height * 0.55f, -b.Height * b.S("breadth", 0.6f) * 0.3f));
        p.Bone("arm_r", "torso", V(0, b.Height * 0.55f, b.Height * b.S("breadth", 0.6f) * 0.3f));
    }

    private static void Eyes(BodyBuild b, float x, float y, float spread)
    {
        foreach (var side in new[] { -1f, 1f })
            b.Plan.Add("head", PartShape.Ellipsoid, V(b.Height * 0.045f, b.Height * 0.05f, b.Height * 0.04f),
                V(x, y, side * spread), b.C("core", "#fff0cf"), glow: true, segments: 5);
    }

    // An open horizontal loop made of contiguous flat-faceted segments.
    private static void Loop(BodyBuild b, float y, float width, float depth, float thickness, float phase, bool broken)
    {
        for (var i = 0; i < 12; i++)
        {
            if (broken && i % 4 == 0) continue;
            var a = (i / 12f * 360 + phase) * MathF.PI / 180;
            b.Plan.Add("torso", PartShape.Ellipsoid, V(depth * 0.32f, thickness, width * 0.11f),
                V(MathF.Cos(a) * depth * 0.38f, y, MathF.Sin(a) * width * 0.38f),
                b.C("edge", "#bac8cd"), V(0, 0, -a * 180 / MathF.PI - 90), segments: 5);
        }
    }

    public static void Flame(BodyBuild b)
    {
        Begin(b);
        var p = b.Plan; var H = b.Height; var W = H * b.S("breadth", 0.6f); var D = H * b.S("depth", 0.5f);
        p.Add("torso", PartShape.Ellipsoid, V(D * 0.78f, H * 0.43f, W * 0.70f), V(0, H * 0.28f, 0), b.C("body", "#813d28"), segments: 7);
        p.Add("head", PartShape.Ellipsoid, V(D * 0.45f, H * 0.30f, W * 0.43f), V(D * 0.12f, H * 0.49f, 0), b.C("core", "#fff0cf"), glow: true, segments: 6);
        var n = b.Has("forks") ? 5 : 3;
        for (var i = 0; i < n; i++)
        {
            var z = (i - (n - 1) * 0.5f) * W * 0.17f;
            var h = H * (0.43f + (i % 2) * 0.16f);
            p.Add("torso", PartShape.Cone, V(D * 0.35f, h, W * 0.28f), V(-D * 0.12f, H * 0.65f, z), b.C("edge", "#ec8337"), V(0, -14, 0), glow: true, segments: 5);
        }
        if (b.Has("feet")) foreach (var side in new[] { -1f, 1f })
            for (var i = 0; i < 2; i++) p.Add("root", PartShape.Wedge, V(D * 0.30f, H * 0.18f, W * 0.24f), V((i - 0.5f) * D * 0.55f, H * 0.09f, side * W * 0.36f), b.C("body", "#813d28"));
        if (b.Has("streamers")) foreach (var side in new[] { -1f, 1f })
            p.Add(side < 0 ? "arm_l" : "arm_r", PartShape.Cone, V(D * 0.16f, H * 0.60f, W * 0.15f), V(-D * 0.26f, H * 0.48f, side * W * 0.31f), b.C("edge", "#ec8337"), V(0, 25, 0), glow: true, segments: 4);
        Eyes(b, D * 0.35f, H * 0.42f, W * 0.18f);
    }

    public static void Surge(BodyBuild b)
    {
        Begin(b);
        var p = b.Plan; var H = b.Height; var W = H * b.S("breadth", 0.9f); var D = H * b.S("depth", 0.7f);
        p.Add("root", PartShape.Ellipsoid, V(D, H * 0.15f, W), V(0, H * 0.075f, 0), b.C("body", "#3d7e97"), segments: 8);
        // Back wall and curling lip leave a hollow beneath the crest.
        p.Add("torso", PartShape.Ellipsoid, V(D * 0.35f, H * 0.47f, W * 0.66f), V(-D * 0.30f, H * 0.30f, 0), b.C("edge", "#a9d9df"), segments: 7);
        for (var i = 0; i < 5; i++)
        {
            var a = MathF.PI - i * MathF.PI / 4;
            p.Add("torso", PartShape.Ellipsoid, V(D * 0.32f, H * 0.25f, W * (0.64f - i * 0.04f)),
                V(MathF.Cos(a) * D * 0.36f, H * (0.55f + MathF.Sin(a) * 0.28f), 0),
                i == 4 ? b.C("core", "#e6faff") : b.C("edge", "#a9d9df"), segments: 7);
        }
        foreach (var side in new[] { -1f, 1f })
            p.Add(side < 0 ? "arm_l" : "arm_r", PartShape.Wedge, V(D * 0.38f, H * 0.35f, W * 0.18f), V(-D * 0.12f, H * 0.31f, side * W * 0.34f), b.C("body", "#3d7e97"), V(side * 20, 0, 0));
        Eyes(b, D * 0.48f, H * 0.57f, W * 0.12f);
    }

    public static void Shard(BodyBuild b)
    {
        Begin(b);
        var p = b.Plan; var H = b.Height; var W = H * b.S("breadth", 0.6f); var D = H * b.S("depth", 0.5f);
        p.Add("torso", PartShape.Cone, V(D * 0.55f, H * 0.72f, W * 0.55f), V(0, H * 0.46f, 0), b.C("body", "#3d7e97"), segments: 5);
        var n = b.Has("radial") ? 8 : 6;
        for (var i = 0; i < n; i++)
        {
            var a = i * MathF.PI * 2 / n;
            var h = H * (b.Has("radial") ? 0.34f : 0.48f + (i % 2) * 0.18f);
            p.Add("torso", PartShape.Cone, V(D * 0.22f, h, W * 0.22f),
                V(MathF.Cos(a) * D * 0.28f, H * 0.52f, MathF.Sin(a) * W * 0.28f),
                b.C("edge", "#a9d9df"), V(MathF.Sin(a) * 30, -MathF.Cos(a) * 30, 0), segments: 4);
            if (b.Has("crown")) p.Add("torso", PartShape.Cone, V(D * 0.14f, H * 0.25f, W * 0.14f),
                V(MathF.Cos(a) * D * 0.20f, H * 0.22f, MathF.Sin(a) * W * 0.20f),
                b.C("core", "#e6faff"), V(180, 0, 0), segments: 4);
        }
        p.Add("head", PartShape.Cone, V(D * 0.26f, H * 0.45f, W * 0.26f), V(0, H * 0.77f, 0), b.C("core", "#e6faff"), segments: 4);
        Eyes(b, D * 0.24f, H * 0.48f, W * 0.13f);
    }

    public static void Vortex(BodyBuild b)
    {
        Begin(b);
        var p = b.Plan; var H = b.Height; var W = H * b.S("breadth", 0.6f); var D = H * b.S("depth", 0.6f);
        var broken = b.Has("broken");
        for (var j = 0; j < 4; j++) Loop(b, H * (0.13f + j * 0.22f), W * (0.35f + j * 0.19f), D * (0.35f + j * 0.19f), H * 0.065f, j * 27, broken);
        if (!broken) p.Add("head", PartShape.Ellipsoid, V(D * 0.18f, H * 0.25f, W * 0.18f), V(0, H * 0.58f, 0), b.C("core", "#f2f2e4"), glow: true, segments: 6);
        foreach (var side in new[] { -1f, 1f })
        {
            var bone = side < 0 ? "arm_l" : "arm_r";
            if (b.Has("forks")) for (var i = 0; i < 2; i++)
                p.Add(bone, PartShape.Wedge, V(D * 0.17f, H * 0.20f, W * 0.14f), V(D * 0.13f * i, H * (0.78f + i * 0.09f), side * W * (0.27f + i * 0.08f)), b.C("core", "#f2f2e4"), V(side * 28, 0, 0), glow: true);
            if (b.Has("ribbons")) p.Add(bone, PartShape.Ellipsoid, V(D * 0.75f, H * 0.055f, W * 0.09f), V(-D * 0.15f, H * 0.50f, side * W * 0.32f), b.C("edge", "#a2b9d3"), V(0, 0, side * 18), segments: 5);
            if (broken) p.Add(bone, PartShape.Cone, V(D * 0.20f, H * 0.40f, W * 0.17f), V(0, H * 0.70f, side * W * 0.31f), b.C("body", "#2b2634"), V(side * 15, 0, 0), segments: 3);
        }
    }

    public static void Mound(BodyBuild b)
    {
        Begin(b);
        var p = b.Plan; var H = b.Height; var W = H * b.S("breadth", 1.3f); var D = H * b.S("depth", 1.7f);
        p.Rig = "crawler";
        p.Bone("shell", "root", V(0, H * 0.45f, 0));
        p.Bone("claw_l", "shell", V(D * 0.25f, H * 0.25f, -W * 0.33f));
        p.Bone("claw_r", "shell", V(D * 0.25f, H * 0.25f, W * 0.33f));
        p.Add("shell", PartShape.Ellipsoid, V(D * 0.86f, H * 0.70f, W * 0.84f), V(-D * 0.05f, H * 0.38f, 0), b.C("body", "#635d52"), segments: 6);
        foreach (var side in new[] { -1f, 1f })
        {
            for (var i = 0; i < 3; i++) p.Add("shell", PartShape.Ellipsoid, V(D * 0.32f, H * 0.35f, W * 0.28f), V((i - 1) * D * 0.25f, H * 0.20f, side * W * 0.34f), b.C("edge", "#a19380"), segments: 5);
            p.Add(side < 0 ? "claw_l" : "claw_r", PartShape.Wedge, V(D * 0.30f, H * 0.24f, W * 0.20f), V(D * 0.37f, H * 0.22f, side * W * 0.32f), b.C("body", "#635d52"));
        }
        if (b.Has("geode"))
        {
            p.Add("shell", PartShape.Ellipsoid, V(D * 0.48f, H * 0.30f, W * 0.48f), V(0, H * 0.65f, 0), b.C("core", "#c4dce1"), glow: true, segments: 6);
            for (var i = 0; i < 8; i++)
            {
                var a = i * MathF.PI / 4;
                p.Add("shell", PartShape.Cone, V(D * 0.18f, H * 0.40f, W * 0.18f), V(MathF.Cos(a) * D * 0.28f, H * 0.72f, MathF.Sin(a) * W * 0.28f), b.C("edge", "#a19380"), segments: 4);
            }
        }
        if (b.Has("lobes")) for (var i = 0; i < 4; i++)
            p.Add("shell", PartShape.Ellipsoid, V(D * 0.28f, H * (0.50f + i * 0.07f), W * 0.70f), V((i - 1.5f) * D * 0.18f, H * 0.51f, 0), b.C("body", "#635d52"), segments: 6);
        Eyes(b, D * 0.40f, H * 0.36f, W * 0.15f);
    }

    public static void Veil(BodyBuild b)
    {
        Begin(b);
        var p = b.Plan; var H = b.Height; var W = H * b.S("breadth", 0.6f); var D = H * b.S("depth", 0.5f);
        var petals = b.Has("petals");
        p.Add("head", PartShape.Ellipsoid, V(D * 0.75f, H * 0.28f, W * 0.90f), V(0, H * 0.78f, 0), b.C("body", "#373249"), segments: 7);
        p.Add("head", PartShape.Ellipsoid, V(D * 0.35f, H * 0.16f, W * 0.35f), V(D * 0.20f, H * 0.70f, 0), b.C("core", "#bdb4d0"), glow: true, segments: 6);
        for (var i = 0; i < 7; i++)
        {
            var a = i * MathF.PI * 2 / 7;
            var h = H * (0.37f + (i % 3) * 0.07f);
            p.Add("torso", petals ? PartShape.Ellipsoid : PartShape.Wedge, V(D * 0.16f, h, W * 0.19f),
                V(MathF.Cos(a) * D * 0.30f, H * 0.42f, MathF.Sin(a) * W * 0.35f),
                b.C("edge", "#766785"), V(MathF.Sin(a) * 12, 0, -MathF.Cos(a) * 12), segments: 5);
            if (petals) p.Add("torso", PartShape.Cylinder, V(D * 0.035f, H * 0.25f, W * 0.035f),
                V(MathF.Cos(a) * D * 0.23f, H * 0.18f, MathF.Sin(a) * W * 0.25f), b.C("body", "#576b45"), segments: 4);
        }
        Eyes(b, D * 0.37f, H * 0.74f, W * 0.16f);
    }

    public static void Prism(BodyBuild b)
    {
        Begin(b);
        var p = b.Plan; var H = b.Height; var W = H * b.S("breadth", 0.6f); var D = H * b.S("depth", 0.5f);
        var n = b.Has("triune") ? 3 : 1;
        for (var i = 0; i < n; i++)
        {
            var z = (i - (n - 1) * 0.5f) * W * 0.28f;
            var y = H * (n == 1 ? 0.53f : 0.48f + (i % 2) * 0.18f);
            var color = b.C(n == 1 ? "core" : i == 0 ? "ember" : i == 1 ? "tide" : "storm", "#eee0f3");
            p.Add(i == 0 ? "arm_l" : i == 2 ? "arm_r" : "head", PartShape.Cone, V(D * 0.26f, H * 0.28f, W * 0.24f), V(0, y + H * 0.14f, z), color, glow: true, segments: 4);
            p.Add(i == 0 ? "arm_l" : i == 2 ? "arm_r" : "head", PartShape.Cone, V(D * 0.26f, H * 0.28f, W * 0.24f), V(0, y - H * 0.14f, z), color, V(180, 0, 0), glow: true, segments: 4);
        }
        if (b.Has("halo"))
        {
            Loop(b, H * 0.52f, W, D, H * 0.045f, 0, false);
            foreach (var side in new[] { -1f, 1f })
                p.Add("torso", PartShape.Wedge, V(D * 0.15f, H * 0.09f, W * 0.48f), V(0, H * 0.53f, side * W * 0.30f), b.C("edge", "#e6d39a"), glow: true);
        }
        if (b.Has("lattice") || b.Has("triune"))
            foreach (var side in new[] { -1f, 1f })
                for (var i = 0; i < 2; i++) p.Add("torso", PartShape.Wedge, V(D * 0.14f, H * 0.43f, W * 0.15f),
                    V(0, H * (0.32f + i * 0.36f), side * W * 0.30f), b.C("edge", "#b18ebc"), V(side * (i == 0 ? -30 : 30), 0, 0));
    }
}
