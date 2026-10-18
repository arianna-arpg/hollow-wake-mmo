using HollowWake.Client.UI.Logic;

namespace HollowWake.Client.World.Logic;

/// <summary>Material assemblies on the shared biped rig; twin of golemFamilies.ts.</summary>
public static class GolemFamilies
{
    private static Vec3 V(float x, float y, float z) => new(x, y, z);

    public static void Assembled(BodyBuild b)
    {
        var p = b.Plan;
        p.Rig = "biped";
        var H = b.Height;
        var W = H * b.S("breadth", .6f);
        var D = H * b.S("depth", .4f);
        var hip = H * b.S("legs", .32f);
        var shoulder = H * .76f;
        var reach = H * .36f * b.S("arms", 1);
        var head = H * .18f * b.S("head", 1);
        var fist = b.S("fists", 1);
        var body = b.C("body", "#8e968b");
        var edge = b.C("edge", "#d5c8a4");
        var joint = b.C("joint", "#302f2d");
        var metal = b.C("metal", "#645d4d");
        var core = b.C("core", "#e9dba2");
        var round = b.Has("round") || b.Has("vessel") || b.Has("clotted") || b.Has("grafted");
        var logs = b.Has("logs");
        var prisms = b.Has("prisms");
        var limb = b.Has("kindled") ? PartShape.Cone : round ? PartShape.Ellipsoid : logs ? PartShape.Cylinder : prisms ? PartShape.Cone : PartShape.Box;
        p.Bone("hips", "root", V(0, hip, 0));
        p.Bone("torso", "hips", V(0, hip, 0));
        p.Bone("head", "torso", V(D * .1f, H - head, 0));
        p.Add("hips", PartShape.Box, V(D * .6f, H * .09f, W * .52f), V(0, hip, 0), joint);
        if (b.Has("kindled"))
        {
            p.Add("torso", PartShape.Ellipsoid, V(D * .72f, H * .38f, W * .68f), V(0, H * .6f, 0), body, segments: 6);
            for (var i = -1; i <= 1; i++)
                p.Add("torso", PartShape.Cone, V(D * .64f, H * (.42f - MathF.Abs(i) * .07f), W * .32f), V(0, H * .72f, i * W * .24f), edge, turn: V(i * 19, 0, 0), segments: 5);
        }
        else if (b.Has("conductors"))
        {
            p.Add("torso", PartShape.Ellipsoid, V(D * .55f, H * .23f, W * .34f), V(0, H * .64f, 0), core, glow: true, segments: 6);
            foreach (var side in new[] { -1, 1 })
            {
                p.Add("torso", PartShape.Cylinder, V(D * .18f, H * .44f, W * .13f), V(0, H * .63f, side * W * .28f), metal, turn: V(side * 12, 0, 0), segments: 6);
                for (var i = 0; i < 3; i++)
                    p.Add("torso", PartShape.Box, V(D * .7f, H * .025f, W * .17f), V(0, H * (.49f + i * .12f), side * W * .27f), edge);
            }
        }
        else if (b.Has("grafted"))
        {
            p.Add("torso", PartShape.Ellipsoid, V(D * .93f, H * .45f, W * .74f), V(0, H * .6f, W * .07f), body, turn: V(9, 0, 0), segments: 7);
            p.Add("torso", PartShape.Ellipsoid, V(D * .78f, H * .30f, W * .43f), V(D * .06f, H * .72f, -W * .22f), edge, segments: 6);
        }
        else if (round)
        {
            p.Add("torso", PartShape.Ellipsoid, V(D * .94f, H * .46f, W * .76f), V(0, H * .58f, 0), body, segments: 8);
            p.Add("torso", PartShape.Cylinder, V(D * .62f, H * .10f, W * .5f), V(0, H * .8f, 0), edge, segments: 8);
        }
        else if (logs)
        {
            for (var i = -1; i <= 1; i++)
                p.Add("torso", PartShape.Cylinder, V(D * .7f, H * .43f, W * .24f), V(0, H * .64f, i * W * .2f), body, segments: 7);
        }
        else if (prisms)
        {
            p.Add("torso", PartShape.Cone, V(D * .95f, H * .5f, W * .72f), V(0, H * .64f, 0), body, segments: 5);
            p.Add("torso", PartShape.Cone, V(D * .85f, H * .27f, W * .66f), V(0, H * .43f, 0), edge, turn: V(180, 0, 0), segments: 5);
        }
        else
        {
            p.Add("torso", PartShape.Wedge, V(D * .88f, H * .36f, W * .7f), V(0, H * .64f, 0), body);
            p.Add("torso", PartShape.Box, V(D * .7f, H * .10f, W * .5f), V(0, H * .41f, 0), edge);
        }
        p.Add("head", prisms ? PartShape.Cone : logs ? PartShape.Cylinder : round ? PartShape.Ellipsoid : PartShape.Box,
            V(D * .48f, head, W * .28f), V(D * .1f, H - head * .5f, 0), body, segments: 6);
        // Dark face recess stays readable even when emissive accents lose their colour.
        p.Add("head", PartShape.Box, V(D * .035f, head * .22f, W * .23f), V(D * .35f, H - head * .48f, 0), joint);
        foreach (var side in new[] { -1, 1 })
        {
            var arm = side < 0 ? "arm_l" : "arm_r";
            var leg = side < 0 ? "leg_l" : "leg_r";
            var z = side * W * .38f;
            var armScale = 1 + side * b.S("arm_asymmetry", 0);
            p.Bone(arm, "torso", V(0, shoulder, z));
            p.Bone(leg, "hips", V(0, hip, side * W * .22f));
            p.Add(arm, PartShape.Ellipsoid, V(D * .32f, H * .10f, W * .18f), V(0, shoulder, z), joint, segments: 6);
            p.Add(arm, limb, V(D * .58f, reach * .47f, W * .25f), V(0, shoulder - reach * .23f, z), body, segments: 6);
            p.Add(arm, limb, V(D * .60f * fist * armScale, reach * .44f, W * .28f * fist * armScale), V(D * .09f, shoulder - reach * .79f, z), body, segments: 6);
            p.Add(arm, PartShape.Box, V(D * .20f, reach * .08f, W * .22f), V(D * .39f, shoulder - reach * .91f, z), edge);
            p.Add(leg, limb, V(D * .53f, hip * .8f, W * .26f), V(0, hip * .54f, side * W * .22f), body, segments: 6);
            p.Add(leg, PartShape.Box, V(D * .68f, H * .07f, W * .30f), V(D * .10f, H * .035f, side * W * .22f), body);
            p.Add("head", PartShape.Box, V(D * .04f, head * .10f, W * .055f), V(D * .375f, H - head * .48f, side * W * .07f), core, glow: true);
            if (b.Has("blocks"))
            {
                p.Add(arm, PartShape.Wedge, V(D * .72f, H * .14f, W * .33f), V(0, shoulder + H * .06f, z), edge, turn: V(side * 12, 0, 0));
                p.Add("torso", PartShape.Box, V(D * .1f, H * .17f, W * .27f), V(D * .46f, H * .67f, side * W * .18f), edge);
            }
            if (b.Has("plates"))
            {
                p.Add(arm, PartShape.Wedge, V(D * .7f, H * .18f, W * .34f), V(0, shoulder, z), edge);
                p.Add(leg, PartShape.Box, V(D * .12f, hip * .55f, W * .24f), V(D * .31f, hip * .53f, side * W * .22f), edge);
            }
            if (prisms) for (var i = 0; i < 3; i++)
                p.Add(arm, PartShape.Cone, V(D * .29f, H * (.23f + i * .035f), W * .13f), V(-D * .1f, shoulder + H * .04f, z + side * i * W * .075f), edge, turn: V(side * (18 + i * 15), 0, 0), segments: 4);
            if (logs)
            {
                p.Add(arm, PartShape.Cylinder, V(D * .64f, H * .035f, W * .29f), V(0, shoulder - reach * .25f, z), metal, segments: 7);
                p.Add(arm, PartShape.Cylinder, V(D * .66f, H * .035f, W * .31f), V(D * .09f, shoulder - reach * .77f, z), metal, segments: 7);
            }
            if (b.Has("handles"))
            {
                p.Add("torso", PartShape.Box, V(D * .24f, H * .04f, W * .25f), V(0, H * .76f, side * W * .42f), edge);
                p.Add("torso", PartShape.Box, V(D * .24f, H * .18f, W * .055f), V(0, H * .68f, side * W * .54f), edge);
                p.Add("torso", PartShape.Box, V(D * .24f, H * .04f, W * .25f), V(0, H * .60f, side * W * .42f), edge);
            }
            if (b.Has("rivets")) for (var i = 0; i < 3; i++)
                p.Add("torso", PartShape.Ellipsoid, V(H * .025f, H * .025f, H * .025f), V(D * .46f, H * (.52f + i * .09f), side * W * .27f), edge, segments: 5);
            if (b.Has("chimneys"))
                p.Add("torso", PartShape.Cylinder, V(H * .1f, H * .32f, H * .1f), V(-D * .34f, H * .88f, side * W * .21f), metal, segments: 6);
            p.Sockets[side < 0 ? "hand_l" : "hand_r"] = new BodySocket(arm, V(D * .10f, shoulder - reach, z), Vec3.Zero, H * .55f);
        }
        if (b.Has("plates"))
        {
            p.Add("torso", PartShape.Box, V(D * .14f, H * .32f, W * .62f), V(D * .45f, H * .64f, 0), metal);
            for (var i = -1; i <= 1; i++)
                p.Add("torso", PartShape.Box, V(D * .025f, H * .17f, W * .055f), V(D * .535f, H * .64f, i * W * .12f), joint);
        }
        if (b.Has("vessel"))
        {
            p.Add("torso", PartShape.Cylinder, V(D * .69f, H * .035f, W * .55f), V(0, H * .855f, 0), joint, segments: 8);
            p.Add("torso", PartShape.Cylinder, V(D * .75f, H * .04f, W * .61f), V(0, H * .83f, 0), edge, segments: 8);
        }
        if (b.Has("glaze")) for (var i = 0; i < 3; i++)
            p.Add("torso", PartShape.Box, V(D * .025f, H * (.07f + i * .025f), W * .1f), V(D * .44f, H * .66f, (i - 1) * W * .17f), edge);
        if (b.Has("thumbprints")) for (var i = -1; i <= 1; i++)
            p.Add("torso", PartShape.Ellipsoid, V(D * .035f, H * .06f, W * .09f), V(D * .45f, H * .58f, i * W * .16f), joint, segments: 5);
        if (b.Has("runestones")) for (var i = -1; i <= 1; i++)
            p.Add("torso", PartShape.Box, V(D * .03f, H * .11f, W * .025f), V(D * .53f, H * .67f, i * W * .15f), core, glow: true);
        if (b.Has("crest"))
            p.Add("head", PartShape.Wedge, V(D * .55f, H * .09f, W * .38f), V(D * .08f, H * 1.02f, 0), edge);
        if (b.Has("crown")) for (var i = -1; i <= 1; i++)
            p.Add("head", PartShape.Cone, V(D * .18f, H * (.2f - MathF.Abs(i) * .07f), W * .1f), V(D * .02f, H * 1.01f, i * W * .14f), edge, segments: 4);
        if (b.Has("bindings")) for (var i = 0; i < 3; i++)
            p.Add("torso", PartShape.Box, V(D * .75f, H * .025f, W * .72f), V(0, H * (.49f + i * .13f), 0), metal);
        if (b.Has("branches")) foreach (var side in new[] { -1, 1 })
        {
            p.Add("torso", PartShape.Cylinder, V(H * .055f, H * .45f, H * .055f), V(-D * .2f, H * .95f, side * W * .32f), body, turn: V(side * 30, 0, 0), segments: 5);
            p.Add("torso", PartShape.Cylinder, V(H * .04f, H * .24f, H * .04f), V(-D * .2f, H * 1.05f, side * W * .42f), edge, turn: V(side * 65, 0, 0), segments: 5);
        }
        if (b.Has("vault"))
        {
            p.Add("torso", PartShape.Box, V(D * .16f, H * .33f, W * .62f), V(D * .48f, H * .61f, 0), metal);
            p.Add("torso", PartShape.Box, V(D * .04f, H * .26f, W * .51f), V(D * .58f, H * .61f, 0), body);
            p.Add("torso", PartShape.Cylinder, V(H * .19f, H * .05f, H * .19f), V(D * .63f, H * .62f, 0), edge, turn: V(0, 90, 0), segments: 8);
            p.Add("torso", PartShape.Box, V(D * .04f, H * .025f, W * .39f), V(D * .67f, H * .62f, 0), joint);
        }
        if (b.Has("scrap"))
        {
            p.Add("torso", PartShape.Box, V(D * .95f, H * .24f, W * .44f), V(D * .1f, H * .69f, -W * .16f), metal, turn: V(-12, 0, 0));
            p.Add("torso", PartShape.Ellipsoid, V(D * .7f, H * .29f, W * .41f), V(0, H * .61f, W * .27f), edge, segments: 5);
            p.Add("arm_l", PartShape.Box, V(D * .85f, H * .2f, W * .35f), V(D * .1f, shoulder - reach * .7f, -W * .4f), metal);
        }
        if (b.Has("chains")) for (var i = 0; i < 6; i++)
            p.Add("torso", PartShape.Box, V(H * .035f, H * .065f, H * .06f), V(D * .5f, H * (.65f - i * .045f), W * .24f), metal, turn: V(i % 2 * 40, 0, 0));
        if (b.Has("trinkets")) for (var i = -1; i <= 1; i++)
            p.Add("torso", PartShape.Ellipsoid, V(H * .08f, H * .1f, H * .08f), V(D * .55f, H * (.65f + MathF.Abs(i) * .1f), i * W * .17f), core, segments: 5);
        if (b.Has("kindled"))
        {
            foreach (var side in new[] { -1, 1 })
            {
                var arm = side < 0 ? "arm_l" : "arm_r";
                for (var i = 0; i < 2; i++)
                    p.Add(arm, PartShape.Cone, V(D * .32f, H * (.27f + i * .08f), W * .16f), V(0, shoulder - reach * .55f, side * W * (.33f + i * .1f)), core, turn: V(side * 14, 0, 0), glow: true, segments: 5);
            }
            p.Add("head", PartShape.Cone, V(D * .38f, H * .25f, W * .24f), V(D * .06f, H * .99f, 0), edge, segments: 5);
        }
        if (b.Has("frost"))
        {
            foreach (var side in new[] { -1, 1 }) for (var i = 0; i < 3; i++)
                p.Add("torso", PartShape.Cone, V(D * .22f, H * (.16f + i * .035f), W * .10f), V(D * .20f, H * .47f, side * W * (.12f + i * .1f)), edge, turn: V(180, 0, 0), segments: 4);
            p.Add("head", PartShape.Wedge, V(D * .40f, H * .18f, W * .30f), V(D * .07f, H * .99f, 0), edge, turn: V(0, 0, 18));
        }
        if (b.Has("conductors"))
        {
            foreach (var side in new[] { -1, 1 })
            {
                p.Add("torso", PartShape.Cylinder, V(H * .035f, H * .42f, H * .035f), V(-D * .12f, H * .95f, side * W * .29f), metal, segments: 6);
                p.Add("torso", PartShape.Ellipsoid, V(H * .1f, H * .1f, H * .1f), V(-D * .12f, H * 1.17f, side * W * .29f), core, glow: true, segments: 6);
                for (var i = 0; i < 3; i++)
                    p.Add(side < 0 ? "arm_l" : "arm_r", PartShape.Cylinder, V(D * .66f, H * .035f, W * .3f), V(0, shoulder - reach * (.25f + i * .22f), side * W * .38f), metal, segments: 8);
            }
        }
        if (b.Has("clotted"))
        {
            p.Add("arm_r", PartShape.Ellipsoid, V(D * .84f, H * .28f, W * .37f), V(D * .06f, shoulder - reach * .65f, W * .41f), body, segments: 7);
            p.Add("torso", PartShape.Ellipsoid, V(D * .20f, H * .16f, W * .23f), V(D * .45f, H * .64f, 0), core, segments: 7);
            for (var i = 0; i < 3; i++)
                p.Add("hips", PartShape.Ellipsoid, V(H * .05f, H * (.09f + i * .025f), H * .05f), V(D * .2f, hip * .72f, (i - 1) * W * .16f), edge, segments: 5);
        }
        if (b.Has("sinews")) foreach (var side in new[] { -1, 1 })
        {
            p.Add("torso", PartShape.Cylinder, V(H * .035f, H * .45f, H * .035f), V(D * .42f, H * .59f, side * W * .2f), edge, turn: V(side * 20, 0, 0), segments: 5);
            p.Add(side < 0 ? "arm_l" : "arm_r", PartShape.Cylinder, V(H * .03f, reach * .8f, H * .03f), V(D * .35f, shoulder - reach * .52f, side * W * .38f), edge, segments: 5);
        }
        if (b.Has("grafted"))
        {
            // Grafted limbs stay attached to the existing animated arm/leg bones.
            p.Add("arm_l", PartShape.Ellipsoid, V(D * .8f, H * .38f, W * .36f), V(0, shoulder - reach * .6f, -W * .42f), body, segments: 6);
            for (var i = 0; i < 3; i++)
                p.Add("arm_r", PartShape.Cone, V(D * .14f, H * .18f, W * .065f), V(D * .22f, shoulder - reach, W * (.3f + i * .075f)), metal, turn: V(180, 0, 0), segments: 4);
            // Exposed rib arcs, stitch seams, a second skull and an off-center belly maw.
            for (var i = 0; i < 4; i++)
            {
                p.Add("torso", PartShape.Box, V(D * .06f, H * .025f, W * .36f), V(D * .46f, H * (.53f + i * .065f), -W * .13f), metal, turn: V(-12, 0, 0));
                p.Add("torso", PartShape.Box, V(D * .055f, H * .02f, W * .08f), V(D * .47f, H * (.52f + i * .06f), W * .12f), joint, turn: V(25, 0, 0));
            }
            p.Add("torso", PartShape.Ellipsoid, V(D * .35f, H * .16f, W * .23f), V(D * .23f, H * .83f, -W * .33f), metal, turn: V(-24, 0, 0), segments: 6);
            p.Add("torso", PartShape.Box, V(D * .04f, H * .04f, W * .17f), V(D * .42f, H * .84f, -W * .33f), joint);
            p.Add("torso", PartShape.Ellipsoid, V(D * .07f, H * .16f, W * .27f), V(D * .50f, H * .53f, W * .18f), joint, segments: 6);
            for (var i = 0; i < 3; i++)
                p.Add("torso", PartShape.Cone, V(D * .06f, H * .045f, W * .04f), V(D * .55f, H * .58f, W * (.1f + i * .08f)), metal, turn: V(180, 0, 0), segments: 4);
            p.Add("torso", PartShape.Cone, V(D * .20f, H * .27f, W * .18f), V(-D * .1f, H * .90f, W * .28f), metal, turn: V(28, 0, 0), segments: 5);
        }
        if (b.Has("moss"))
            p.Add("torso", PartShape.Ellipsoid, V(D * .10f, H * .10f, W * .23f), V(D * .45f, H * .79f, -W * .16f), metal, segments: 5);
        p.Sockets["chest"] = new BodySocket("torso", V(D * .4f, H * .63f, 0), Vec3.Zero, H);
        p.Sockets["head"] = new BodySocket("head", V(D * .1f, H, 0), Vec3.Zero, head);
        p.Swings = true;
    }
}
