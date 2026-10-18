using HollowWake.Client.UI.Logic;

namespace HollowWake.Client.World.Logic;

/// <summary>Warren anatomy on ordinary rigs. Breadth and depth are envelope ratios to height.</summary>
public static class GoblinkinFamilies
{
    private static Vec3 V(float x, float y, float z) => new(x, y, z);

    public static void Goblinkin(BodyBuild b)
    {
        var p = b.Plan;
        p.Rig = "biped";
        var H = b.Height;
        var W = H * b.S("breadth", 0.5f);
        var D = H * b.S("depth", 0.36f);
        var legs = H * 0.36f * b.S("legs", 1);
        var chest = H * 0.7f;
        var arms = H * 0.34f * b.S("arms", 1);
        var head = H * 0.22f * b.S("head", 1);
        var skin = b.C("skin", "#64834a");
        var cloth = b.C("cloth", "#654239");
        var dark = b.C("dark", "#302d2c");
        var metal = b.C("metal", "#787c78");
        var bone = b.C("bone", "#dfd5b3");
        var wood = b.C("wood", "#725b3d");
        var accent = b.C("accent", "#ba754b");
        p.Bone("hips", "root", V(0, legs, 0));
        p.Bone("torso", "hips", V(0, legs, 0));
        p.Bone("head", "torso", V(D * 0.12f, H * 0.78f, 0));
        p.Add("torso", PartShape.Ellipsoid, V(D * 0.72f, chest - legs, W * 0.65f), V(-D * 0.08f, (chest + legs) / 2, 0), skin, segments: 7);
        p.Add("hips", PartShape.Wedge, V(D * 0.65f, H * 0.16f, W * 0.58f), V(0, legs, 0), cloth);
        p.Add("hips", PartShape.Box, V(D * 0.72f, H * 0.035f, W * 0.61f), V(0, legs + H * 0.055f, 0), dark);
        p.Add("head", PartShape.Ellipsoid, V(head * 0.9f, head, head * 1.2f), V(D * 0.1f, H - head * 0.52f, 0), skin, segments: 6);
        p.Add("head", PartShape.Box, V(head * 0.9f, head * 0.3f * b.S("jaw", 1), head * 0.95f), V(D * 0.12f + head * 0.18f, H - head * 0.9f, 0), skin);
        p.Add("head", PartShape.Wedge, V(head * 0.62f, head * 0.38f, head * 0.32f), V(D * 0.12f + head * 0.5f, H - head * 0.55f, 0), skin, V(0, -12, 0));
        p.Add("head", PartShape.Box, V(head * 0.035f, head * 0.045f, head * 0.65f), V(D * 0.12f + head * 0.64f, H - head * 0.83f, 0), dark);
        foreach (var side in new[] { -1f, 1f })
        {
            var arm = side < 0 ? "arm_l" : "arm_r";
            var leg = side < 0 ? "leg_l" : "leg_r";
            var z = side * W * 0.39f;
            p.Bone(arm, "torso", V(0, chest, z));
            p.Bone(leg, "hips", V(0, legs, side * W * 0.2f));
            p.Add(arm, PartShape.Ellipsoid, V(D * 0.38f, arms * 0.92f, W * 0.21f), V(0, chest - arms * 0.43f, z), skin, segments: 6);
            p.Add(arm, PartShape.Box, V(D * 0.4f, H * 0.075f, W * 0.23f), V(D * 0.08f, chest - arms, z), skin);
            p.Add(leg, PartShape.Cylinder, V(W * 0.18f, legs * 0.86f, W * 0.18f), V(0, legs * 0.49f, side * W * 0.2f), skin, segments: 6);
            p.Add(leg, PartShape.Box, V(D * 0.62f, H * 0.05f, W * 0.24f), V(D * 0.14f, H * 0.025f, side * W * 0.2f), dark);
            p.Add("head", PartShape.Wedge, V(head * 0.3f, head * 0.75f, head * 0.75f), V(D * 0.02f, H - head * 0.39f, side * head * 0.78f), skin, V(side * 62, 0, 0));
            p.Add("head", PartShape.Box, V(head * 0.08f, head * 0.16f, head * 0.27f), V(D * 0.1f + head * 0.43f, H - head * 0.42f, side * head * 0.29f), dark, V(side * 15, 0, 0));
            p.Add("head", PartShape.Box, V(head * 0.09f, head * 0.06f, head * 0.12f), V(D * 0.1f + head * 0.48f, H - head * 0.43f, side * head * 0.28f), b.C("eye", "#e6c16b"));
            p.Sockets[side < 0 ? "hand_l" : "hand_r"] = new BodySocket(arm, V(D * 0.1f, chest - arms, z), Vec3.Zero, H * MathF.Min(0.55f, (chest - arms) / H * 1.5f));
            if (b.Has("tusks")) p.Add("head", PartShape.Cone, V(head * 0.16f, head * 0.55f, head * 0.16f), V(D * 0.12f + head * 0.58f, H - head * 0.7f, side * head * 0.36f), bone, V(0, -14, 0), segments: 5);
            if (b.Has("pauldrons")) p.Add("torso", PartShape.Wedge, V(D * 0.7f, H * 0.13f, W * 0.32f), V(0, chest, z), metal, V(side * 12, 0, 0));
            if (b.Has("stone_harness")) p.Add("torso", PartShape.Box, V(D * 0.7f, H * 0.2f, W * 0.28f), V(0, chest, z), b.C("stone", "#8d9185"), V(side * 18, 0, 0));
            if (b.Has("moss")) p.Add("torso", PartShape.Cone, V(D * 0.65f, H * 0.15f, W * 0.3f), V(-D * 0.16f, chest + H * 0.03f, z), b.C("moss", "#536f3e"), segments: 5);
            if (b.Has("goggles")) p.Add("head", PartShape.Cylinder, V(head * 0.32f, head * 0.13f, head * 0.32f), V(D * 0.1f + head * 0.49f, H - head * 0.4f, side * head * 0.29f), metal, V(0, 90, 0), segments: 8);
        }
        if (b.Has("armor")) p.Add("torso", PartShape.Wedge, V(D * 0.2f, (chest - legs) * 0.85f, W * 0.62f), V(D * 0.3f, (chest + legs) / 2, 0), metal);
        if (b.Has("warpaint")) p.Add("torso", PartShape.Box, V(D * 0.03f, H * 0.19f, W * 0.09f), V(D * 0.32f, chest * 0.85f, W * 0.12f), accent, V(20, 0, 0));
        if (b.Has("robe")) p.Add("hips", PartShape.Cone, V(W * 0.72f, H * 0.5f, W * 0.75f), V(0, H * 0.3f, 0), cloth, segments: 7);
        if (b.Has("hat"))
        {
            p.Add("head", PartShape.Cylinder, V(head * 1.8f, H * 0.025f, head * 1.8f), V(0, H * 0.99f, 0), cloth, segments: 7);
            p.Add("head", PartShape.Cone, V(head * 1.2f, H * 0.32f, head * 1.2f), V(-head * 0.16f, H * 1.13f, 0), cloth, V(0, 14, 0), segments: 6);
        }
        if (b.Has("crown"))
        {
            p.Add("head", PartShape.Cylinder, V(head * 1.2f, head * 0.15f, head * 1.35f), V(D * 0.1f, H * 0.99f, 0), accent, segments: 6);
            for (var i = -1; i <= 1; i++) p.Add("head", PartShape.Cone, V(head * 0.18f, head * (i == 0 ? 0.65f : 0.45f), head * 0.18f), V(head * 0.15f, H + head * 0.22f, i * head * 0.5f), bone, segments: 4);
        }
        if (b.Has("banner"))
        {
            p.Add("torso", PartShape.Cylinder, V(H * 0.025f, H * 0.8f, H * 0.025f), V(-D * 0.5f, H * 0.9f, W * 0.25f), wood, segments: 5);
            p.Add("torso", PartShape.Wedge, V(H * 0.025f, H * 0.28f, W * 0.52f), V(-D * 0.5f, H * 1.15f, W * 0.4f), accent);
        }
        if (b.Has("sack")) p.Add("torso", PartShape.Ellipsoid, V(D * 0.65f, H * 0.3f, W * 0.68f), V(-D * 0.5f, H * 0.55f, 0), wood, segments: 6);
        if (b.Has("keg"))
        {
            p.Add("torso", PartShape.Cylinder, V(W * 0.68f, H * 0.48f, W * 0.68f), V(-D * 0.4f, H * 0.56f, 0), wood, segments: 8);
            foreach (var y in new[] { 0.4f, 0.7f }) p.Add("torso", PartShape.Cylinder, V(W * 0.72f, H * 0.04f, W * 0.72f), V(-D * 0.4f, H * y, 0), metal, segments: 8);
        }
        if (b.Has("torch"))
        {
            p.Add("arm_r", PartShape.Cylinder, V(H * 0.035f, H * 0.32f, H * 0.035f), V(D * 0.15f, chest - arms + H * 0.12f, W * 0.39f), wood, segments: 5);
            p.Add("arm_r", PartShape.Cone, V(H * 0.1f, H * 0.2f, H * 0.1f), V(D * 0.15f, chest - arms + H * 0.36f, W * 0.39f), accent, glow: true, segments: 5);
        }
        if (b.Has("sling"))
        {
            p.Add("arm_r", PartShape.Box, V(H * 0.018f, H * 0.35f, H * 0.02f), V(D * 0.12f, chest - arms - H * 0.1f, W * 0.39f), dark);
            p.Add("arm_r", PartShape.Ellipsoid, V(H * 0.12f, H * 0.08f, H * 0.1f), V(D * 0.12f, chest - arms - H * 0.28f, W * 0.39f), wood, segments: 5);
        }
        p.Swings = true;
    }

    public static void Gnasher(BodyBuild b) => Beast(b, false);
    public static void Warbeast(BodyBuild b) => Beast(b, true);

    private static void Beast(BodyBuild b, bool longBody)
    {
        var p = b.Plan;
        p.Rig = longBody ? "quadruped" : "biped";
        var H = b.Height;
        var W = H * b.S("breadth", 0.9f);
        var D = H * b.S("depth", 1.1f);
        var skin = b.C("skin", "#a58155");
        var dark = b.C("dark", "#302d2c");
        var bone = b.C("bone", "#dfd5b3");
        p.Bone("hips", "root", V(-D * 0.2f, H * 0.4f, 0));
        p.Bone("torso", "hips", V(0, H * 0.55f, 0));
        p.Bone("head", "torso", V(D * 0.25f, H * 0.65f, 0));
        p.Add("torso", PartShape.Ellipsoid, V(D * 0.8f, H * 0.7f, W * 0.95f), V(-D * 0.06f, H * 0.6f, 0), skin, segments: 7);
        p.Add("head", PartShape.Ellipsoid, V(D * (longBody ? 0.5f : 0.4f), H * (longBody ? 0.35f : 0.5f), W * (longBody ? 0.58f : 0.8f)), V(D * 0.27f, H * 0.67f, 0), skin, segments: 6);
        p.Add("head", PartShape.Box, V(D * 0.07f, H * 0.26f, W * 0.72f), V(D * 0.48f, H * 0.51f, 0), dark);
        for (var i = -2; i <= 2; i += longBody ? 4 : 1)
        {
            p.Add("head", PartShape.Cone, V(W * 0.1f, H * 0.18f, W * 0.1f), V(D * 0.52f, H * 0.48f, i * W * 0.13f), bone, segments: 4);
            p.Add("head", PartShape.Cone, V(W * 0.09f, H * 0.14f, W * 0.09f), V(D * 0.52f, H * 0.59f, i * W * 0.13f), bone, V(180, 0, 0), segments: 4);
        }
        foreach (var side in new[] { -1f, 1f })
        {
            p.Add("head", PartShape.Ellipsoid, V(D * 0.08f, H * 0.14f, W * 0.17f), V(D * 0.44f, H * 0.82f, side * W * 0.25f), b.C("eye", "#e6c16b"), segments: 6);
            if (longBody) p.Add("head", PartShape.Cone, V(W * 0.22f, H * 0.25f, W * 0.18f), V(D * 0.22f, H * 0.99f, side * W * 0.3f), dark, segments: 4);
            var back = longBody ? (side < 0 ? "leg_bl" : "leg_br") : (side < 0 ? "leg_l" : "leg_r");
            p.Bone(back, "hips", V(-D * 0.22f, H * 0.4f, side * W * 0.32f));
            p.Add(back, PartShape.Cylinder, V(W * 0.2f, H * 0.4f, W * 0.2f), V(-D * 0.22f, H * 0.23f, side * W * 0.32f), skin, segments: 5);
            p.Add(back, PartShape.Box, V(D * 0.27f, H * 0.07f, W * 0.27f), V(-D * 0.12f, H * 0.04f, side * W * 0.32f), dark);
            if (longBody)
            {
                var front = side < 0 ? "leg_fl" : "leg_fr";
                p.Bone(front, "torso", V(D * 0.2f, H * 0.5f, side * W * 0.3f));
                p.Add(front, PartShape.Cylinder, V(W * 0.18f, H * 0.5f, W * 0.18f), V(D * 0.2f, H * 0.26f, side * W * 0.3f), skin, segments: 5);
                p.Add(front, PartShape.Box, V(D * 0.2f, H * 0.07f, W * 0.24f), V(D * 0.26f, H * 0.04f, side * W * 0.3f), dark);
            }
        }
        if (b.Has("spines") || b.Has("mane"))
        {
            for (var i = 0; i < 4; i++) p.Add("torso", PartShape.Cone, V(D * 0.15f, H * 0.22f, W * 0.24f), V(-D * 0.35f + i * D * 0.17f, H * 0.94f, 0), dark, V(0, 25, 0), segments: 4);
        }
        if (longBody) p.Add("hips", PartShape.Cone, V(W * 0.22f, D * 0.34f, W * 0.22f), V(-D * 0.51f, H * 0.55f, 0), dark, V(0, 65, 0), segments: 5);
        p.Swings = false;
    }
}
