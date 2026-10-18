namespace HollowWake.Client.World.Logic;

/// <summary>Grounded reptilian bipeds on the existing rig. Anatomy is presentation, never collision.</summary>
public static class ScaledBipedFamilies
{
    private static Vec3 V(float x, float y, float z) => new(x, y, z);

    public static void ScaledBiped(BodyBuild b)
    {
        var p = b.Plan;
        p.Rig = "biped";
        var H = b.Height;
        var W = H * b.S("breadth", 0.4f);
        var D = H * b.S("depth", 0.37f);
        var snout = b.S("snout", 1);
        var crest = b.S("crest", 1);
        var tail = H * 0.60f * b.S("tail", 1);
        var hip = H * 0.39f;
        var shoulder = H * 0.70f;
        var hx = D * 0.28f;
        var skin = b.C("skin", "#9d8053");
        var scute = b.C("scute", "#40594e");
        var belly = b.C("belly", "#cfbb88");
        var cloth = b.C("cloth", "#514d47");
        var metal = b.C("metal", "#929897");
        var bone = b.C("bone", "#e6d5ad");
        var wood = b.C("wood", "#765942");
        var accent = b.C("accent", "#c7a36a");
        var dark = b.C("dark", "#252c29");
        p.Bone("hips", "root", V(0, hip, 0));
        p.Bone("torso", "hips", V(0, hip, 0));
        p.Bone("head", "torso", V(hx, H * 0.81f, 0));
        p.Bone("tail", "hips", V(-D * 0.25f, hip, 0));
        p.Add("torso", PartShape.Ellipsoid, V(D * 0.66f, H * 0.40f, W * 0.70f), V(-D * 0.02f, H * 0.59f, 0), skin, segments: 6);
        p.Add("hips", PartShape.Wedge, V(D * 0.71f, H * 0.12f, W * 0.77f), V(0, hip, 0), cloth);
        // Broad ventral plates and a dorsal ridge read as scales at ordinary game distance.
        for (var i = 0; i < 4; i++)
        {
            p.Add("torso", PartShape.Wedge, V(D * 0.12f, H * 0.065f, W * (0.39f - i * 0.035f)), V(D * 0.29f, H * (0.47f + i * 0.07f), 0), belly);
            p.Add("torso", PartShape.Wedge, V(D * 0.22f, H * 0.09f, W * 0.47f), V(-D * 0.32f, H * (0.48f + i * 0.07f), 0), scute);
        }
        p.Add("head", PartShape.Ellipsoid, V(D * 0.69f, H * 0.24f, W * 0.62f), V(hx, H * 0.87f, 0), skin, segments: 6);
        p.Add("head", PartShape.Wedge, V(D * 0.70f * snout, H * 0.12f, W * 0.48f), V(hx + D * 0.38f, H * 0.855f, 0), skin);
        p.Add("head", PartShape.Box, V(D * 0.55f * snout, H * 0.045f, W * 0.42f), V(hx + D * 0.36f, H * 0.785f, 0), belly);
        p.Add("head", PartShape.Box, V(D * 0.56f * snout, H * 0.012f, W * 0.43f), V(hx + D * 0.36f, H * 0.812f, 0), dark);
        for (var i = 0; i < 3; i++)
            p.Add("tail", PartShape.Ellipsoid, V(tail * 0.48f, H * (0.17f - i * 0.045f), W * (0.33f - i * 0.09f)), V(-D * 0.25f - tail * (0.19f + i * 0.29f), hip - H * (0.04f + i * 0.07f), 0), i == 1 ? scute : skin, segments: 6);
        p.Add("tail", PartShape.Cone, V(H * 0.06f, tail * 0.33f, H * 0.06f), V(-D * 0.25f - tail, H * 0.20f, 0), scute, V(0, 80, 0), segments: 5);
        foreach (var side in new[] { -1f, 1f })
        {
            var arm = side < 0 ? "arm_l" : "arm_r";
            var leg = side < 0 ? "leg_l" : "leg_r";
            var z = side * W * 0.37f;
            var lz = side * W * 0.23f;
            p.Bone(arm, "torso", V(0, shoulder, z));
            p.Bone(leg, "hips", V(0, hip, lz));
            p.Add(arm, PartShape.Ellipsoid, V(D * 0.24f, H * 0.25f, W * 0.19f), V(D * 0.06f, H * 0.59f, z), skin, segments: 5);
            p.Add(arm, PartShape.Ellipsoid, V(D * 0.27f, H * 0.11f, W * 0.21f), V(D * 0.23f, H * 0.46f, z), scute, segments: 5);
            p.Add(leg, PartShape.Ellipsoid, V(D * 0.40f, H * 0.26f, W * 0.24f), V(D * 0.08f, H * 0.31f, lz), skin, segments: 6);
            p.Add(leg, PartShape.Cylinder, V(W * 0.13f, H * 0.19f, W * 0.13f), V(-D * 0.11f, H * 0.15f, lz), scute, V(0, -22, 0), segments: 5);
            p.Add(leg, PartShape.Wedge, V(D * 0.55f, H * 0.065f, W * 0.27f), V(D * 0.08f, H * 0.0325f, lz), skin);
            for (var toe = -1; toe <= 1; toe++)
                p.Add(leg, PartShape.Cone, V(H * 0.027f, H * 0.09f, H * 0.027f), V(D * 0.38f, H * 0.035f, lz + toe * W * 0.08f), bone, V(0, -90, 0), segments: 4);
            p.Add("head", PartShape.Wedge, V(D * 0.30f, H * 0.045f, W * 0.14f), V(hx + D * 0.11f, H * 0.94f, side * W * 0.26f), scute);
            p.Add("head", PartShape.Ellipsoid, V(D * 0.16f, H * 0.035f, W * 0.045f), V(hx + D * 0.15f, H * 0.914f, side * W * 0.29f), b.C("eye", "#f0d98a"), segments: 5);
            p.Add("head", PartShape.Box, V(D * 0.018f, H * 0.032f, W * 0.05f), V(hx + D * 0.17f, H * 0.914f, side * W * 0.30f), dark);
            p.Add("head", PartShape.Box, V(D * 0.07f, H * 0.018f, W * 0.04f), V(hx + D * 0.62f * snout, H * 0.87f, side * W * 0.16f), dark);
            if (b.Has("frill")) for (var i = 0; i < 3; i++)
                p.Add("head", PartShape.Wedge, V(D * 0.12f, H * 0.19f * crest, W * 0.30f), V(hx - D * 0.22f, H * (0.81f + i * 0.065f), side * W * (0.27f + i * 0.04f)), accent, V(side * (30 - i * 25), 0, 0));
            if (b.Has("armor")) p.Add("torso", PartShape.Wedge, V(D * 0.44f, H * 0.11f, W * 0.30f), V(-D * 0.03f, shoulder, z), metal);
            p.Sockets[side < 0 ? "hand_l" : "hand_r"] = new BodySocket(arm, V(D * 0.23f, H * 0.43f, z), Vec3.Zero, H * b.S("gear", 0.85f));
        }
        if (b.Has("short_crest") || b.Has("crest") || b.Has("crown")) for (var i = 0; i < 3; i++)
            p.Add("head", PartShape.Wedge, V(D * 0.18f, H * (b.Has("short_crest") ? 0.07f : 0.17f) * crest, W * 0.08f), V(hx - D * 0.26f + i * D * 0.19f, H * 1.00f, 0), b.Has("crown") ? metal : accent);
        if (b.Has("sail")) p.Add("torso", PartShape.Wedge, V(D * 0.39f, H * 0.56f, W * 0.08f), V(-D * 0.41f, H * 0.76f, 0), accent);
        if (b.Has("armor")) p.Add("torso", PartShape.Wedge, V(D * 0.17f, H * 0.28f, W * 0.58f), V(D * 0.31f, H * 0.60f, 0), metal);
        if (b.Has("backplates")) for (var i = 0; i < 3; i++)
            p.Add("torso", PartShape.Wedge, V(D * 0.35f, H * 0.16f, W * 0.77f), V(-D * 0.38f, H * (0.48f + i * 0.13f), 0), metal);
        if (b.Has("robe")) p.Add("hips", PartShape.Cone, V(D * 0.80f, H * 0.32f, W * 0.84f), V(0, H * 0.30f, 0), cloth, segments: 7);
        if (b.Has("satchel")) p.Add("torso", PartShape.Box, V(D * 0.35f, H * 0.27f, W * 0.61f), V(-D * 0.48f, H * 0.56f, 0), cloth);
        if (b.Has("stonepack")) for (var i = -1; i <= 1; i++)
            p.Add("torso", PartShape.Ellipsoid, V(D * 0.43f, H * 0.22f, W * 0.29f), V(-D * 0.48f, H * 0.59f + H * 0.08f * (i == 0 ? 1 : 0), i * W * 0.24f), metal, segments: 5);
        if (b.Has("quiver")) for (var i = -1; i <= 1; i++)
        {
            p.Add("torso", PartShape.Cylinder, V(H * 0.018f, H * 0.53f, H * 0.018f), V(-D * 0.43f, H * 0.69f, i * W * 0.14f), wood, segments: 4);
            p.Add("torso", PartShape.Wedge, V(H * 0.06f, H * 0.09f, H * 0.03f), V(-D * 0.43f, H * 0.96f, i * W * 0.14f), bone);
        }
        if (b.Has("vials")) for (var i = -1; i <= 1; i++)
            p.Add("hips", PartShape.Cylinder, V(H * 0.065f, H * 0.15f, H * 0.065f), V(D * 0.39f, H * 0.39f, i * W * 0.26f), accent, segments: 6);
        if (b.Has("censer")) p.Add("torso", PartShape.Ellipsoid, V(D * 0.37f, H * 0.22f, W * 0.36f), V(-D * 0.47f, H * 0.77f, 0), accent, glow: true, segments: 6);
        if (b.Has("coil_yoke")) for (var i = -1; i <= 1; i += 2)
        {
            p.Add("torso", PartShape.Cylinder, V(H * 0.035f, H * 0.46f, H * 0.035f), V(-D * 0.38f, H * 0.83f, i * W * 0.40f), metal, segments: 5);
            p.Add("torso", PartShape.Ellipsoid, V(H * 0.12f, H * 0.12f, H * 0.12f), V(-D * 0.38f, H * 1.07f, i * W * 0.40f), accent, segments: 6);
        }
        if (b.Has("drum")) p.Add("hips", PartShape.Cylinder, V(W * 0.52f, H * 0.21f, W * 0.52f), V(D * 0.47f, H * 0.43f, 0), wood, segments: 8);
        if (b.Has("twin_blades")) p.Add("arm_l", PartShape.Wedge, V(H * 0.035f, H * 0.28f, H * 0.07f), V(D * 0.23f, H * 0.58f, -W * 0.37f), metal);
        if (b.Has("banner"))
        {
            p.Add("torso", PartShape.Cylinder, V(H * 0.022f, H * 0.65f, H * 0.022f), V(-D * 0.48f, H * 0.91f, W * 0.25f), wood, segments: 4);
            p.Add("torso", PartShape.Wedge, V(H * 0.018f, H * 0.23f, W * 0.62f), V(-D * 0.48f, H * 1.12f, W * 0.49f), accent);
        }
        p.Swings = true;
    }
}
