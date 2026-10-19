using HollowWake.Client.UI.Logic;

namespace HollowWake.Client.World.Logic;

/// <summary>Forward-set skull, high trapezius and heavy limbs on the shared biped rig.</summary>
public static class OrcFamilies
{
    private static Vec3 V(float x, float y, float z) => new(x, y, z);

    public static void Orc(BodyBuild b)
    {
        var p = b.Plan;
        p.Rig = "biped";
        var H = b.Height;
        var W = H * b.S("breadth", 0.55f);
        var D = H * b.S("depth", 0.38f);
        var hip = H * 0.35f;
        var shoulder = H * 0.77f;
        var reach = H * 0.36f * b.S("arms", 1);
        var hx = D * 0.22f * b.S("hunch", 0.8f);
        var muscle = b.S("muscle", 1);
        var skin = b.C("skin", "#6b7951");
        var dark = b.C("dark", "#292b29");
        var cloth = b.C("cloth", "#593a32");
        var metal = b.C("metal", "#676d6a");
        var bone = b.C("bone", "#d9cbaa");
        var wood = b.C("wood", "#72573b");
        var accent = b.C("accent", "#b96d44");
        p.Bone("hips", "root", V(0, hip, 0));
        p.Bone("torso", "hips", V(0, hip, 0));
        p.Bone("head", "torso", V(hx, H * 0.83f, 0));
        // The arch is authored directly, so posture does not accidentally change the rest height.
        p.Add("torso", PartShape.Ellipsoid, V(D * 0.85f, H * 0.49f, W * 0.73f), V(-D * 0.07f, H * 0.60f, 0), skin, segments: 7);
        p.Add("torso", PartShape.Ellipsoid, V(D * 0.75f, H * 0.30f, W * 0.68f), V(-D * 0.16f, H * 0.77f, 0), skin, segments: 6);
        p.Add("torso", PartShape.Ellipsoid, V(D * 0.65f, H * 0.18f, W * 0.55f), V(hx * 0.2f, H * 0.83f, 0), skin, segments: 6);
        p.Add("hips", PartShape.Wedge, V(D * 0.74f, H * 0.20f, W * 0.60f), V(0, hip, 0), cloth);
        p.Add("hips", PartShape.Box, V(D * 0.78f, H * 0.035f, W * 0.63f), V(0, hip + H * 0.065f, 0), metal);
        p.Add("head", PartShape.Ellipsoid, V(D * 0.48f, H * 0.22f, W * 0.38f), V(hx, H * 0.89f, 0), skin, segments: 6);
        p.Add("head", PartShape.Wedge, V(D * 0.48f, H * 0.10f * b.S("jaw", 1), W * 0.35f), V(hx + D * 0.11f, H * 0.82f, 0), skin);
        p.Add("head", PartShape.Box, V(D * 0.035f, H * 0.018f, W * 0.26f), V(hx + D * 0.36f, H * 0.835f, 0), dark);
        p.Add("head", PartShape.Wedge, V(D * 0.19f, H * 0.07f, W * 0.11f), V(hx + D * 0.24f, H * 0.90f, 0), skin);
        foreach (var side in new[] { -1f, 1f })
        {
            var arm = side < 0 ? "arm_l" : "arm_r";
            var leg = side < 0 ? "leg_l" : "leg_r";
            var z = side * W * 0.38f;
            p.Bone(arm, "torso", V(0, shoulder, z));
            p.Bone(leg, "hips", V(0, hip, side * W * 0.22f));
            p.Add(arm, PartShape.Ellipsoid, V(D * 0.50f, reach * 0.67f, W * 0.24f * muscle), V(0, shoulder - reach * 0.23f, z), skin, segments: 6);
            p.Add(arm, PartShape.Ellipsoid, V(D * 0.42f, reach * 0.56f, W * 0.19f * muscle), V(D * 0.12f, shoulder - reach * 0.70f, z), skin, segments: 6);
            p.Add(arm, PartShape.Box, V(D * 0.38f, H * 0.09f, W * 0.22f), V(D * 0.16f, shoulder - reach, z), skin);
            p.Add(leg, PartShape.Ellipsoid, V(D * 0.42f, hip * 0.82f, W * 0.25f), V(-D * 0.06f, hip * 0.63f, side * W * 0.22f), skin, segments: 6);
            p.Add(leg, PartShape.Box, V(D * 0.36f, hip * 0.42f, W * 0.19f), V(D * 0.02f, hip * 0.28f, side * W * 0.22f), dark);
            p.Add(leg, PartShape.Box, V(D * 0.57f, H * 0.055f, W * 0.25f), V(D * 0.13f, H * 0.0275f, side * W * 0.22f), dark);
            p.Add("head", PartShape.Wedge, V(D * 0.14f, H * 0.09f, W * 0.20f), V(hx - D * 0.03f, H * 0.90f, side * W * 0.24f), skin, V(side * 35, 0, 0));
            p.Add("head", PartShape.Box, V(D * 0.06f, H * 0.035f, W * 0.15f), V(hx + D * 0.21f, H * 0.935f, side * W * 0.105f), dark, V(side * 12, 0, 0));
            p.Add("head", PartShape.Box, V(D * 0.065f, H * 0.018f, W * 0.07f), V(hx + D * 0.24f, H * 0.914f, side * W * 0.105f), b.C("eye", "#d6b66c"));
            if (b.Has("tusks")) p.Add("head", PartShape.Cone, V(W * 0.055f, H * 0.15f, W * 0.055f), V(hx + D * 0.33f, H * 0.86f, side * W * 0.15f), bone, V(0, -13, 0), segments: 5);
            if (b.Has("pauldrons")) p.Add("torso", PartShape.Wedge, V(D * 0.68f, H * 0.12f, W * 0.32f), V(-D * 0.03f, shoulder + H * 0.03f, z), metal, V(side * 10, 0, 0));
            if (b.Has("bracers")) p.Add(arm, PartShape.Cylinder, V(W * 0.23f, reach * 0.25f, W * 0.23f), V(D * 0.1f, shoulder - reach * 0.78f, z), metal, segments: 6);
            if (b.Has("horns")) p.Add("head", PartShape.Cone, V(W * 0.10f, H * 0.25f, W * 0.10f), V(hx - D * 0.1f, H * 1.01f, side * W * 0.20f), bone, V(side * 35, 0, 0), segments: 5);
            p.Sockets[side < 0 ? "hand_l" : "hand_r"] = new BodySocket(arm, V(D * 0.16f, shoulder - reach, z), Vec3.Zero, H * 0.55f);
        }
        if (b.Has("armor")) p.Add("torso", PartShape.Wedge, V(D * 0.15f, H * 0.33f, W * 0.69f), V(D * 0.34f, H * 0.62f, 0), metal);
        if (b.Has("warpaint")) p.Add("torso", PartShape.Box, V(D * 0.025f, H * 0.22f, W * 0.10f), V(D * 0.37f, H * 0.64f, W * 0.14f), accent, V(20, 0, 0));
        if (b.Has("robe")) p.Add("hips", PartShape.Cone, V(D * 0.85f, H * 0.48f, W * 0.80f), V(0, H * 0.28f, 0), cloth, segments: 7);
        if (b.Has("visor")) p.Add("head", PartShape.Wedge, V(D * 0.12f, H * 0.10f, W * 0.42f), V(hx + D * 0.29f, H * 0.87f, 0), metal);
        if (b.Has("mohawk")) for (var i = 0; i < 4; i++) p.Add("head", PartShape.Cone, V(D * 0.13f, H * 0.14f, W * 0.05f), V(hx - D * 0.22f + i * D * 0.12f, H * 1.035f, 0), accent, segments: 4);
        if (b.Has("backspikes")) for (var i = -1; i <= 1; i++) p.Add("torso", PartShape.Cone, V(W * 0.1f, H * 0.30f, W * 0.1f), V(-D * 0.40f, H * 0.90f, i * W * 0.24f), metal, V(0, 30, 0), segments: 4);
        if (b.Has("banner"))
        {
            p.Add("torso", PartShape.Cylinder, V(H * 0.03f, H * 0.8f, H * 0.03f), V(-D * 0.46f, H * 0.95f, W * 0.2f), wood, segments: 5);
            p.Add("torso", PartShape.Wedge, V(H * 0.02f, H * 0.25f, W * 0.55f), V(-D * 0.46f, H * 1.23f, W * 0.43f), accent);
        }
        if (b.Has("quiver")) for (var i = -1; i <= 1; i++)
        {
            p.Add("torso", PartShape.Cylinder, V(H * 0.02f, H * 0.64f, H * 0.02f), V(-D * 0.48f, H * 0.78f, i * W * 0.12f), wood, segments: 5);
            p.Add("torso", PartShape.Cone, V(H * 0.07f, H * 0.16f, H * 0.07f), V(-D * 0.48f, H * 1.12f, i * W * 0.12f), metal, segments: 4);
        }
        if (b.Has("rockpack")) for (var i = -1; i <= 1; i++) p.Add("torso", PartShape.Ellipsoid, V(D * 0.5f, H * 0.19f, W * 0.30f), V(-D * 0.58f, H * 0.66f, i * W * 0.24f), metal, segments: 5);
        if (b.Has("trophies")) for (var i = -1; i <= 1; i++) p.Add("torso", PartShape.Ellipsoid, V(H * 0.07f, H * 0.10f, H * 0.08f), V(D * 0.43f, H * 0.72f, i * W * 0.17f), bone, segments: 5);
        if (b.Has("chains")) for (var i = 0; i < 6; i++) p.Add("hips", PartShape.Box, V(H * 0.04f, H * 0.065f, H * 0.045f), V(D * 0.33f, H * 0.39f - i * H * 0.045f, W * 0.27f), metal, V(i % 2 * 45, 0, 0));
        if (b.Has("drums"))
        {
            p.Add("hips", PartShape.Cylinder, V(W * 0.55f, H * 0.22f, W * 0.55f), V(D * 0.47f, H * 0.44f, 0), wood, segments: 8);
            p.Add("hips", PartShape.Cylinder, V(W * 0.58f, H * 0.02f, W * 0.58f), V(D * 0.47f, H * 0.56f, 0), bone, segments: 8);
        }
        if (b.Has("censer")) p.Add("torso", PartShape.Ellipsoid, V(H * 0.12f, H * 0.17f, H * 0.12f), V(D * 0.42f, H * 0.62f, 0), accent, glow: true, segments: 6);
        p.Swings = true;
    }
}
