using HollowWake.Client.UI.Logic;

namespace HollowWake.Client.World.Logic;

/// <summary>Wildlife proportions and features on existing rigs; no species IDs or gameplay decisions.</summary>
public static class FaunaFamilies
{
    private static Vec3 V(float x, float y, float z) => new(x, y, z);

    public static void Quadruped(BodyBuild b)
    {
        var p = b.Plan;
        p.Rig = "quadruped";
        var H = b.Height;
        var W = H * b.S("breadth", 0.6f);
        var D = H * b.S("depth", 1.4f);
        var legs = H * b.S("legs", 0.4f);
        var head = H * b.S("head", 0.22f);
        var torso = (H * 0.82f - legs) * 0.9f;
        var tail = D * b.S("tail", 0.2f) * 0.55f;
        var length = D * (0.72f - b.S("tail", 0.2f) * 0.4f);
        var mid = -D * 0.04f + tail * 0.2f;
        var y = legs + torso * 0.44f;
        var skin = b.C("hide", "#9b7955");
        var belly = b.C("belly", "#c7bba0");
        var dark = b.C("dark", "#36352f");
        var horn = b.C("horn", "#d0c7ab");
        p.Bone("hips", "root", V(mid - length * 0.3f, legs, 0));
        p.Bone("torso", "hips", V(mid, y, 0));
        p.Bone("neck", "torso", V(D * 0.22f, y, 0));
        p.Bone("head", "neck", V(D * 0.31f, H - head, 0));
        p.Bone("tail", "hips", V(mid - length * 0.45f, y, 0));
        p.Add("torso", PartShape.Ellipsoid, V(length, torso, W * 0.90f), V(mid, y, 0), skin, segments: 8);
        p.Add("hips", PartShape.Ellipsoid, V(length * 0.45f, torso * b.S("rump", 1), W * 0.87f), V(mid - length * 0.29f, y, 0), skin, segments: 7);
        var neck = MathF.Max(H * 0.08f, H - head * 0.65f - y);
        p.Add("neck", PartShape.Ellipsoid, V(D * 0.20f, neck, W * 0.52f), V(D * 0.23f, y + neck * 0.45f, 0), skin, segments: 6);
        p.Add("head", PartShape.Ellipsoid, V(D * 0.23f, head, W * 0.61f), V(D * 0.32f, H - head * 0.5f, 0), skin, segments: 7);
        p.Add("head", PartShape.Ellipsoid, V(D * b.S("snout", 0.16f), head * 0.55f, W * 0.39f), V(D * 0.40f, H - head * 0.72f, 0), skin, segments: 6);
        p.Add("head", PartShape.Box, V(D * 0.035f, head * 0.22f, W * 0.27f), V(D * (0.4f + b.S("snout", 0.16f) * 0.48f), H - head * 0.70f, 0), dark);
        foreach (var side in new[] { -1f, 1f })
        {
            var back = side < 0 ? "leg_bl" : "leg_br";
            var front = side < 0 ? "leg_fl" : "leg_fr";
            var z = side * W * 0.31f;
            var bx = mid - length * 0.30f;
            var fx = mid + length * 0.29f;
            p.Bone(back, "hips", V(bx, legs, z));
            p.Bone(front, "torso", V(fx, legs, z));
            p.Add(back, PartShape.Ellipsoid, V(W * 0.34f, legs * 0.70f, W * 0.29f), V(bx - D * 0.025f, legs * 0.73f, z), skin, segments: 6);
            p.Add(back, PartShape.Cylinder, V(W * 0.13f, legs * 0.68f, W * 0.13f), V(bx + D * 0.025f, legs * 0.36f, z), skin, segments: 5);
            p.Add(front, PartShape.Cylinder, V(W * 0.17f, legs * 0.96f, W * 0.17f), V(fx, legs * 0.51f, z), skin, segments: 6);
            p.Add(back, PartShape.Box, V(D * 0.13f, H * 0.04f, W * 0.24f), V(bx + D * 0.045f, H * 0.02f, z), b.Has("hooves") ? dark : skin);
            p.Add(front, PartShape.Box, V(D * 0.12f, H * 0.04f, W * 0.22f), V(fx + D * 0.035f, H * 0.02f, z), b.Has("hooves") ? dark : skin);
            var ears = H * b.S("ears", 0.2f);
            if (ears > 0)
            {
                p.Add("head", b.Has("round_ears") ? PartShape.Ellipsoid : PartShape.Cone, V(head * 0.28f, ears, W * 0.22f), V(D * 0.26f, H + ears * 0.20f, side * W * 0.24f), skin, V(side * 16, 0, 0), segments: 5);
                if (b.Has("long_ears")) p.Add("head", PartShape.Ellipsoid, V(head * 0.11f, ears * 0.70f, W * 0.10f), V(D * 0.29f, H + ears * 0.2f, side * W * 0.24f), belly, segments: 5);
                if (b.Has("tufts")) p.Add("head", PartShape.Cone, V(head * 0.13f, ears * 0.60f, W * 0.10f), V(D * 0.26f, H + ears * 0.77f, side * W * 0.24f), dark, segments: 4);
            }
            p.Add("head", PartShape.Ellipsoid, V(head * 0.16f, head * 0.17f, W * 0.07f), V(D * 0.38f, H - head * 0.30f, side * W * 0.28f), b.C("eye", "#1c2020"), segments: 6);
            if (b.Has("frog"))
            {
                p.Add("head", PartShape.Ellipsoid, V(head * 0.6f, head * 0.6f, W * 0.27f), V(D * 0.31f, H - head * 0.13f, side * W * 0.28f), belly, segments: 6);
                p.Add("head", PartShape.Ellipsoid, V(head * 0.23f, head * 0.28f, W * 0.15f), V(D * 0.36f, H - head * 0.05f, side * W * 0.32f), dark, segments: 6);
            }
            if (b.Has("horns")) p.Add("head", PartShape.Cone, V(W * 0.17f, H * 0.37f, W * 0.17f), V(D * 0.21f, H * 1.08f, side * W * 0.37f), horn, V(side * 42, 38, 0), segments: 6);
            if (b.Has("swept_horns"))
            {
                p.Add("head", PartShape.Cylinder, V(W * 0.16f, H * 0.30f, W * 0.16f), V(D * 0.19f, H * 1.10f, side * W * 0.31f), horn, V(side * 12, 24, 0), segments: 6);
                p.Add("head", PartShape.Cone, V(W * 0.14f, H * 0.34f, W * 0.14f), V(D * 0.07f, H * 1.30f, side * W * 0.34f), horn, V(side * 12, 68, 0), segments: 6);
            }
            if (b.Has("antlers"))
            {
                p.Add("head", PartShape.Cylinder, V(H * 0.025f, H * 0.38f, H * 0.025f), V(D * 0.24f, H * 1.14f, side * W * 0.40f), horn, V(side * 25, 0, 0), segments: 5);
                for (var i = 0; i < 3; i++) p.Add("head", PartShape.Cone, V(H * 0.025f, H * 0.15f, H * 0.025f), V(D * 0.24f + i * H * 0.025f, H * (1.04f + i * 0.11f), side * W * (0.44f + i * 0.09f)), horn, V(side * 55, 20, 0), segments: 4);
            }
            if (b.Has("tusks")) p.Add("head", PartShape.Cone, V(W * 0.06f, H * 0.23f, W * 0.06f), V(D * 0.47f, H - head * 0.70f, side * W * 0.23f), horn, V(side * 10, -25, 0), segments: 5);
            if (b.Has("spots")) for (var i = 0; i < 4; i++) p.Add("torso", PartShape.Ellipsoid, V(D * 0.045f, torso * 0.12f, W * 0.025f), V(mid - length * 0.3f + i * length * 0.18f, y + torso * 0.08f, side * W * 0.445f), belly, segments: 5);
            if (b.Has("stripe")) p.Add("torso", PartShape.Box, V(length * 0.9f, torso * 0.12f, W * 0.025f), V(mid, y + torso * 0.25f, side * W * 0.40f), belly);
        }
        if (tail > 0)
        {
            var thick = b.Has("brush_tail") ? W * 0.37f : W * 0.14f;
            p.Add("tail", PartShape.Ellipsoid, V(tail, thick, thick), V(mid - length * 0.45f - tail * 0.39f, y, 0), b.Has("naked_tail") ? belly : skin, segments: 6);
            if (b.Has("brush_tail")) p.Add("tail", PartShape.Ellipsoid, V(tail * 0.30f, thick * 0.8f, thick * 0.8f), V(mid - length * 0.45f - tail * 0.76f, y, 0), belly, segments: 5);
        }
        if (b.Has("ruff")) p.Add("neck", PartShape.Ellipsoid, V(D * 0.25f, torso * 0.83f, W * 0.83f), V(D * 0.19f, y + torso * 0.20f, 0), skin, segments: 5);
        if (b.Has("hump")) p.Add("torso", PartShape.Ellipsoid, V(length * 0.42f, torso * 0.70f, W * 0.83f), V(mid + length * 0.20f, y + torso * 0.30f, 0), skin, segments: 6);
        if (b.Has("bib")) p.Add("neck", PartShape.Ellipsoid, V(D * 0.06f, neck * 0.64f, W * 0.37f), V(D * 0.32f, y + neck * 0.32f, 0), belly, segments: 5);
        if (b.Has("beard")) p.Add("head", PartShape.Cone, V(W * 0.20f, H * 0.20f, W * 0.20f), V(D * 0.36f, H - head * 1.27f, 0), dark, V(180, 0, 0), segments: 5);
        if (b.Has("ridge") || b.Has("spines")) for (var i = 0; i < 6; i++) p.Add("torso", PartShape.Cone, V(D * 0.10f, H * 0.18f, W * 0.38f), V(mid - length * 0.36f + i * length * 0.14f, y + torso * 0.48f, 0), dark, segments: 4);
        if (b.Has("spines"))
        {
            p.Add("torso", PartShape.Ellipsoid, V(length * 1.10f, torso * 1.20f, W * 0.98f), V(mid - length * 0.10f, y, 0), dark, segments: 8);
            foreach (var side in new[] { -1f, 1f })
                for (var i = 0; i < 5; i++) p.Add("torso", PartShape.Cone, V(D * 0.08f, H * 0.19f, W * 0.18f), V(mid - length * 0.38f + i * length * 0.17f, y + torso * 0.37f, side * W * 0.29f), horn, V(side * 30, -20, 0), segments: 4);
        }
        if (b.Has("shell"))
        {
            p.Add("torso", PartShape.Ellipsoid, V(length * 1.12f, torso * 1.15f, W), V(mid, y + torso * 0.09f, 0), dark, segments: 8);
            for (var i = -1; i <= 1; i++) p.Add("torso", PartShape.Ellipsoid, V(length * 0.28f, torso * 0.20f, W * 0.53f), V(mid + i * length * 0.25f, y + torso * 0.60f, 0), skin, segments: 5);
        }
        p.Swings = false;
    }

    public static void Bird(BodyBuild b)
    {
        var p = b.Plan;
        p.Rig = "biped";
        var H = b.Height;
        var W = H * b.S("breadth", 0.75f);
        var D = H * b.S("depth", 1);
        var legs = H * b.S("legs", 0.25f);
        var skin = b.C("hide", "#a68d61");
        var dark = b.C("dark", "#36352f");
        p.Bone("hips", "root", V(-D * 0.08f, legs, 0));
        p.Bone("torso", "hips", V(0, H * 0.52f, 0));
        p.Bone("head", "torso", V(D * 0.22f, H * 0.76f, 0));
        p.Add("torso", PartShape.Ellipsoid, V(D * 0.72f, H * 0.58f, W * 0.8f), V(-D * 0.02f, H * 0.52f, 0), skin, segments: 7);
        p.Add("head", PartShape.Ellipsoid, V(D * 0.40f, H * 0.30f, W * 0.56f), V(D * 0.22f, H * 0.85f, 0), skin, segments: 6);
        p.Add("head", PartShape.Wedge, V(D * b.S("beak", 0.2f), H * 0.09f, W * 0.18f), V(D * 0.45f, H * 0.83f, 0), dark);
        for (var i = -2; i <= 2; i++) p.Add("torso", PartShape.Wedge, V(D * b.S("tail", 0.3f), H * (b.Has("fan_tail") ? 0.48f : 0.10f), W * 0.15f), V(-D * 0.40f, H * (b.Has("fan_tail") ? 0.72f : 0.51f), i * W * (b.Has("fan_tail") ? 0.18f : 0.10f)), dark, V(i * 15, 25, 0));
        foreach (var side in new[] { -1f, 1f })
        {
            var leg = side < 0 ? "leg_l" : "leg_r";
            var wing = side < 0 ? "arm_l" : "arm_r";
            p.Bone(leg, "hips", V(-D * 0.02f, legs, side * W * 0.23f));
            p.Bone(wing, "torso", V(D * 0.02f, H * 0.64f, side * W * 0.36f));
            p.Add(leg, PartShape.Cylinder, V(W * 0.055f, legs, W * 0.055f), V(-D * 0.02f, legs * 0.52f, side * W * 0.23f), dark, segments: 5);
            p.Add(leg, PartShape.Wedge, V(D * 0.20f, H * 0.035f, W * 0.18f), V(D * 0.04f, H * 0.0175f, side * W * 0.23f), dark);
            p.Add(wing, PartShape.Wedge, V(D * 0.61f, H * 0.32f, W * 0.15f), V(-D * 0.06f, H * 0.53f, side * W * 0.39f), dark);
            p.Add("head", PartShape.Ellipsoid, V(H * 0.06f, H * 0.065f, H * 0.035f), V(D * 0.31f, H * 0.89f, side * W * 0.27f), b.C("eye", "#1c2020"), segments: 5);
            if (b.Has("spots")) for (var i = 0; i < 3; i++) p.Add(wing, PartShape.Box, V(D * 0.06f, H * 0.06f, W * 0.015f), V(-D * 0.23f + i * D * 0.14f, H * 0.57f, side * W * 0.47f), b.C("belly", "#c7bba0"));
        }
        if (b.Has("crest")) p.Add("head", PartShape.Cone, V(W * 0.14f, H * 0.26f, W * 0.13f), V(D * 0.22f, H * 1.06f, 0), b.C("accent", "#b85e43"), V(0, -22, 0), segments: 5);
        if (b.Has("ruff")) p.Add("head", PartShape.Cone, V(W * 0.51f, H * 0.25f, W * 0.52f), V(D * 0.12f, H * 0.72f, 0), dark, V(180, 0, 0), segments: 5);
        p.Swings = false;
    }

    public static void Crab(BodyBuild b)
    {
        var p = b.Plan;
        p.Rig = "crawler";
        var H = b.Height;
        var W = H * b.S("breadth", 1.7f);
        var D = H * b.S("depth", 1.3f);
        var skin = b.C("hide", "#bc8c65");
        var dark = b.C("dark", "#36352f");
        p.Bone("shell", "root", V(0, H * 0.52f, 0));
        p.Add("shell", PartShape.Ellipsoid, V(D * 0.68f, H * 0.50f, W * 0.63f), V(-D * 0.06f, H * 0.53f, 0), skin, segments: 8);
        foreach (var side in new[] { -1f, 1f })
        {
            var claw = side < 0 ? "claw_l" : "claw_r";
            var scale = b.S("claw", 1) * (b.Has("fiddler") && side > 0 ? 1.45f : 1);
            p.Bone(claw, "shell", V(D * 0.20f, H * 0.40f, side * W * 0.24f));
            p.Add(claw, PartShape.Cylinder, V(W * 0.10f, D * 0.30f, W * 0.10f), V(D * 0.29f, H * 0.40f, side * W * 0.30f), skin, V(0, 75, 0), segments: 5);
            p.Add(claw, PartShape.Ellipsoid, V(D * 0.28f * scale, H * 0.23f * scale, W * 0.14f * scale), V(D * 0.43f, H * 0.43f, side * W * 0.34f), skin, segments: 6);
            for (var jaw = -1; jaw <= 1; jaw += 2) p.Add(claw, PartShape.Cone, V(W * 0.07f, D * 0.22f * scale, W * 0.07f), V(D * 0.55f, H * 0.43f, side * W * 0.34f + jaw * W * 0.045f), dark, V(0, 90, 0), segments: 4);
            p.Add("shell", PartShape.Cylinder, V(W * 0.035f, H * 0.30f, W * 0.035f), V(D * 0.18f, H * 0.80f, side * W * 0.16f), skin, segments: 5);
            p.Add("shell", PartShape.Ellipsoid, V(W * 0.08f, H * 0.12f, W * 0.08f), V(D * 0.18f, H * 0.96f, side * W * 0.16f), b.C("eye", "#1c2020"), segments: 6);
            for (var i = 0; i < 3; i++)
            {
                p.Add("shell", PartShape.Cylinder, V(W * 0.045f, W * 0.32f, W * 0.045f), V(-D * 0.26f + i * D * 0.18f, H * 0.34f, side * W * 0.34f), skin, V(side * 60, 0, 0), segments: 4);
                p.Add("shell", PartShape.Cone, V(W * 0.05f, H * 0.28f, W * 0.05f), V(-D * 0.26f + i * D * 0.18f, H * 0.14f, side * W * 0.46f), dark, V(180, 0, 0), segments: 4);
            }
        }
        p.Swings = false;
    }
}
