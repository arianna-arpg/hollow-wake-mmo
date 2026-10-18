using HollowWake.Client.UI.Logic;

namespace HollowWake.Client.World.Logic;

/// <summary>Exposed bone and stitched flesh anatomy, composed on the ordinary animated rigs.</summary>
public static class AnatomyFamilies
{
    private static Vec3 V(float x, float y, float z) => new(x, y, z);

    public static void Skeleton(BodyBuild b) => Upright(b, false);
    public static void Stitched(BodyBuild b) => Upright(b, true);

    private static void Skull(BodyBuild b, string bone, float x, float y, float z, float size)
    {
        var p = b.Plan;
        var ivory = b.C("bone", "#d8d0b8");
        var hollow = b.C("hollow", "#28252e");
        p.Add(bone, PartShape.Ellipsoid, V(size * 0.85f, size * 0.72f, size * 0.8f), V(x, y + size * 0.62f, z), ivory, segments: 6);
        p.Add(bone, PartShape.Box, V(size * 0.62f, size * 0.25f, size * 0.62f), V(x + size * 0.08f, y + size * 0.16f, z), ivory);
        foreach (var side in new[] { -1f, 1f })
        {
            p.Add(bone, PartShape.Box, V(size * 0.12f, size * 0.25f, size * 0.25f), V(x + size * 0.37f, y + size * 0.57f, z + side * size * 0.21f), hollow);
            p.Add(bone, PartShape.Box, V(size * 0.035f, size * 0.07f, size * 0.07f), V(x + size * 0.44f, y + size * 0.56f, z + side * size * 0.21f), b.C("glow", "#c1a1c8"), glow: true);
        }
        p.Add(bone, PartShape.Wedge, V(size * 0.1f, size * 0.19f, size * 0.13f), V(x + size * 0.43f, y + size * 0.33f, z), hollow);
        for (var i = -2; i <= 2; i++)
        {
            p.Add(bone, PartShape.Box, V(size * 0.09f, size * 0.09f, size * 0.035f), V(x + size * 0.41f, y + size * 0.2f, z + i * size * 0.095f), hollow);
        }
    }

    private static void Upright(BodyBuild b, bool flesh)
    {
        var p = b.Plan;
        p.Rig = "biped";
        var H = b.Height;
        var width = b.S("width", 1);
        var legs = H * 0.46f * b.S("legs", 1);
        var torso = H * 0.3f * b.S("torso", 1);
        var shoulders = H * 0.27f * width * b.S("shoulders", 1);
        var hips = H * 0.17f * width;
        var depth = H * 0.14f * width;
        var arms = H * 0.34f * b.S("arms", 1);
        var head = H * 0.17f * b.S("head", 1);
        var chest = legs + torso;
        var hunch = b.S("hunch", 0) * 28;
        var boneColor = b.C("bone", "#d8d0b8");
        var skin = b.C("skin", "#aa8475");
        var binding = b.C("binding", "#51453f");
        var cloth = b.C("cloth", "#4a4458");
        var accent = b.C("accent", "#b89a67");
        var thick = b.S("thickness", 1);
        p.Bone("hips", "root", V(0, legs, 0));
        p.Bone("torso", "hips", V(0, legs, 0), V(0, -hunch, 0));
        p.Bone("head", "torso", V(0, chest + H * 0.025f, 0), V(0, hunch * 0.5f, 0));
        p.Bone("arm_l", "torso", V(0, chest, -shoulders / 2));
        p.Bone("arm_r", "torso", V(0, chest, shoulders / 2));
        p.Bone("leg_l", "hips", V(0, legs, -hips / 2));
        p.Bone("leg_r", "hips", V(0, legs, hips / 2));
        p.Add("torso", PartShape.Cylinder, V(H * 0.035f * thick, torso, H * 0.035f * thick), V(-depth * 0.3f, legs + torso / 2, 0), boneColor, segments: 6);
        p.Add("hips", PartShape.Box, V(depth * 0.72f, H * 0.055f, hips), V(0, legs, 0), boneColor);
        var ribs = Math.Clamp((int)b.S("ribs", 4), 2, 8);
        for (var i = 0; i < ribs; i++)
        {
            var y = legs + torso * (0.25f + i * 0.65f / ribs);
            var span = shoulders * (0.7f + 0.15f * i / ribs);
            p.Add("torso", PartShape.Box, V(depth * 0.25f, H * 0.018f * MathF.Min(thick, 1.3f), span), V(depth * 0.38f, y, 0), boneColor);
            foreach (var side in new[] { -1f, 1f })
            {
                p.Add("torso", PartShape.Box, V(depth, H * 0.018f * MathF.Min(thick, 1.3f), H * 0.022f * thick), V(0, y, side * span / 2), boneColor);
            }
        }
        p.Add("torso", PartShape.Box, V(depth * 0.18f, torso * 0.67f, H * 0.035f * thick), V(depth * 0.47f, legs + torso * 0.57f, 0), boneColor);
        if (flesh)
        {
            p.Add("torso", PartShape.Ellipsoid, V(depth * 1.7f, torso * 1.15f, shoulders * 1.1f), V(-depth * 0.25f, legs + torso * 0.45f, 0), skin, segments: 8);
            p.Add("hips", PartShape.Ellipsoid, V(depth * 1.2f, H * 0.15f, hips * 1.35f), V(0, legs, 0), skin, segments: 6);
            // The stitches cross the front, and a diagonal burial harness binds the grafts.
            var stitches = Math.Clamp((int)b.S("stitches", 5), 0, 9);
            for (var i = 0; i < stitches; i++)
            {
                p.Add("torso", PartShape.Box, V(H * 0.018f, H * 0.014f, shoulders * 0.23f), V(depth * 0.62f, legs + torso * (0.18f + i * 0.7f / Math.Max(1, stitches)), shoulders * 0.12f), binding, V(-25, 0, 0));
            }
            if (b.Has("harness")) p.Add("torso", PartShape.Box, V(H * 0.023f, torso * 1.05f, H * 0.045f), V(depth * 0.64f, legs + torso * 0.48f, 0), binding, V(24, 0, 0));
        }
        Skull(b, "head", 0, chest + H * 0.025f, 0, head);
        foreach (var side in new[] { -1f, 1f })
        {
            var armBone = side < 0 ? "arm_l" : "arm_r";
            var legBone = side < 0 ? "leg_l" : "leg_r";
            var z = side * shoulders / 2;
            var armScale = 1 + (side > 0 ? b.S("asymmetry", 0) : 0);
            var ar = H * (flesh ? 0.075f : 0.025f) * thick * armScale;
            var lr = H * (flesh ? 0.07f : 0.03f) * thick;
            var color = flesh ? skin : boneColor;
            p.Add(armBone, PartShape.Cylinder, V(ar, arms * 0.45f, ar), V(0, chest - arms * 0.24f, z), color, segments: 6);
            p.Add(armBone, PartShape.Ellipsoid, V(ar * 1.5f, ar * 1.5f, ar * 1.5f), V(0, chest - arms * 0.5f, z), boneColor, segments: 6);
            p.Add(armBone, PartShape.Cylinder, V(ar * 0.9f, arms * 0.43f, ar), V(H * 0.01f, chest - arms * 0.75f, z), color, segments: 6);
            p.Add(armBone, PartShape.Box, V(ar * 1.6f, H * 0.06f * armScale, ar * 1.7f), V(H * 0.02f, chest - arms, z), color);
            if (!b.Has("legless"))
            {
                for (var segment = 0; segment < 2; segment++)
                {
                    p.Add(legBone, PartShape.Cylinder, V(lr, legs * 0.43f, lr), V(0, legs * (0.25f + segment * 0.5f), side * hips / 2), color, segments: 6);
                }
                p.Add(legBone, PartShape.Ellipsoid, V(lr * 1.6f, lr * 1.6f, lr * 1.6f), V(0, legs * 0.5f, side * hips / 2), boneColor, segments: 6);
                p.Add(legBone, PartShape.Box, V(H * 0.12f, H * 0.035f, lr * 1.8f), V(H * 0.03f, H * 0.02f, side * hips / 2), color);
            }
            p.Sockets[side < 0 ? "hand_l" : "hand_r"] = new BodySocket(armBone, V(H * 0.03f, chest - arms, z), Vec3.Zero, H);
            if (b.Has("bound")) p.Add(armBone, PartShape.Cylinder, V(ar * 1.5f, H * 0.04f, ar * 1.5f), V(0, chest - arms * 0.28f, z), binding, segments: 6);
            if (b.Has("spikes")) p.Add("torso", PartShape.Cone, V(H * 0.055f, H * 0.2f, H * 0.055f), V(0, chest + H * 0.06f, side * shoulders * 0.56f), boneColor, V(side * 25, 0, 0), segments: 5);
            if (b.Has("mantle")) p.Add("torso", PartShape.Wedge, V(depth * 0.4f, torso * 1.2f, shoulders * 0.45f), V(-depth * 0.55f, legs + torso * 0.4f, side * shoulders * 0.32f), cloth, V(side * 12, 0, 0));
            if (b.Has("yoke")) p.Add("torso", PartShape.Cylinder, V(H * 0.035f, H * 0.3f, H * 0.035f), V(-depth * 0.5f, chest + H * 0.04f, side * shoulders * 0.7f), accent, segments: 5);
            if (b.Has("crystals")) p.Add("torso", PartShape.Cone, V(H * 0.08f, H * 0.27f, H * 0.08f), V(-depth * 0.5f, chest, side * shoulders * 0.65f), accent, V(side * 35, 0, 0), segments: 4);
            if (b.Has("vials")) p.Add("hips", PartShape.Cylinder, V(H * 0.06f, H * 0.13f, H * 0.06f), V(0, legs, side * shoulders * 0.6f), accent, segments: 6);
        }
        if (b.Has("cowl")) p.Add("head", PartShape.Wedge, V(head, head * 0.55f, head * 1.3f), V(-head * 0.2f, chest + head * 0.9f, 0), cloth);
        if (b.Has("beak")) p.Add("head", PartShape.Cone, V(head * 0.28f, head * 0.65f, head * 0.3f), V(head * 0.65f, chest + head * 0.4f, 0), binding, V(0, -90, 0), segments: 4);
        if (b.Has("crown"))
        {
            for (var i = -1; i <= 1; i++) p.Add("head", PartShape.Cone, V(head * 0.16f, head * (i == 0 ? 0.8f : 0.55f), head * 0.16f), V(0, chest + head * 1.2f, i * head * 0.38f), accent, segments: 4);
        }
        if (b.Has("graft_head")) Skull(b, "torso", 0, chest - head * 0.2f, shoulders * 0.42f, head * 0.6f);
        p.Sockets["head"] = new BodySocket("head", V(0, chest + head, 0), Vec3.Zero, head);
        p.Sockets["face"] = new BodySocket("head", V(head * 0.45f, chest + head * 0.5f, 0), Vec3.Zero, head);
        p.Sockets["chest"] = new BodySocket("torso", V(0, legs + torso * 0.5f, 0), Vec3.Zero, H);
        p.Sockets["belt"] = new BodySocket("hips", V(0, legs, 0), Vec3.Zero, H);
        p.Sockets["feet"] = new BodySocket("root", Vec3.Zero, Vec3.Zero, H);
        p.Sockets["back"] = new BodySocket("torso", V(-depth, chest, 0), Vec3.Zero, H);
        p.Sockets["off_hand"] = p.Sockets["hand_l"];
        p.Height = chest + head * (b.Has("crown") ? 1.6f : 1.05f);
        p.Width = shoulders + H * 0.12f;
    }

    public static void BoneQuadruped(BodyBuild b)
    {
        var p = b.Plan;
        p.Rig = "quadruped";
        var H = b.Height;
        var length = H * 1.2f * b.S("length", 1);
        var width = H * 0.38f * b.S("width", 1);
        var legs = H * 0.58f;
        var ivory = b.C("bone", "#cfc8b8");
        p.Bone("torso", "root", V(0, legs, 0));
        p.Bone("head", "torso", V(length * 0.5f, legs, 0));
        p.Bone("tail", "torso", V(-length * 0.55f, legs, 0));
        p.Add("torso", PartShape.Box, V(length, H * 0.055f, H * 0.055f), V(0, legs + H * 0.28f, 0), ivory);
        for (var i = 0; i < 5; i++)
        {
            var x = length * (-0.3f + i * 0.14f);
            foreach (var side in new[] { -1f, 1f })
            {
                p.Add("torso", PartShape.Box, V(H * 0.035f, H * 0.3f, H * 0.035f), V(x, legs + H * 0.12f, side * width / 2), ivory, V(side * 18, 0, 0));
            }
            p.Add("torso", PartShape.Box, V(H * 0.035f, H * 0.035f, width), V(x, legs + H * 0.25f, 0), ivory);
        }
        foreach (var end in new[] { -1f, 1f })
        {
            foreach (var side in new[] { -1f, 1f })
            {
                var name = "leg_" + (end > 0 ? "f" : "b") + (side < 0 ? "l" : "r");
                p.Bone(name, "torso", V(end * length * 0.4f, legs, side * width * 0.5f));
                p.Add(name, PartShape.Cylinder, V(H * 0.055f, legs, H * 0.055f), V(end * length * 0.4f, legs / 2, side * width * 0.5f), ivory, segments: 5);
                p.Add(name, PartShape.Box, V(H * 0.19f, H * 0.05f, H * 0.12f), V(end * length * 0.4f + H * 0.04f, H * 0.025f, side * width * 0.5f), ivory);
            }
        }
        Skull(b, "head", length * 0.57f, legs - H * 0.08f, 0, H * 0.34f);
        p.Add("head", PartShape.Box, V(H * 0.25f, H * 0.11f, H * 0.16f), V(length * 0.76f, legs + H * 0.02f, 0), ivory);
        p.Add("tail", PartShape.Cone, V(H * 0.05f, H * 0.65f, H * 0.05f), V(-length * 0.72f, legs + H * 0.16f, 0), ivory, V(0, 65, 0), segments: 5);
        p.Height = H;
        p.Width = length * 1.8f;
    }
}
