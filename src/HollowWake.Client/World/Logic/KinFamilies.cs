using HollowWake.Client.UI.Logic;

namespace HollowWake.Client.World.Logic;

/// <summary>Canine packfolk and hoofed hornfolk, with independent anatomy on the shared biped rig.</summary>
public static class KinFamilies
{
    private static Vec3 V(float x, float y, float z) => new(x, y, z);

    public static void Gnoll(BodyBuild b)
    {
        var p = b.Plan;
        p.Rig = "biped";
        var H = b.Height;
        var W = H * b.S("breadth", .45f);
        var D = H * b.S("depth", .45f);
        var muscle = b.S("muscle", 1);
        var hx = D * .12f * b.S("hunch", 1);
        var snout = b.S("snout", 1);
        var hide = b.C("hide", "#ae8951");
        var dark = b.C("dark", "#332e28");
        var bone = b.C("bone", "#ddd0aa");
        var cloth = b.C("cloth", "#554239");
        var metal = b.C("metal", "#777b78");
        var belly = b.C("belly", "#c9b894");
        var accent = b.C("accent", "#bd6848");
        p.Bone("hips", "root", V(-D * .12f, H * .40f, 0));
        p.Bone("torso", "hips", V(-D * .12f, H * .40f, 0));
        p.Bone("head", "torso", V(hx, H * .78f, 0));
        p.Bone("tail", "hips", V(-D * .33f, H * .40f, 0));
        // High scapula and forward low skull make the posture legible from both cameras.
        p.Add("torso", PartShape.Ellipsoid, V(D * .59f, H * .43f, W * .61f), V(-D * .11f, H * .63f, 0), hide, V(0, 0, -15), segments: 7);
        p.Add("torso", PartShape.Ellipsoid, V(D * .59f, H * .27f, W * .72f), V(-D * .17f, H * .80f, 0), hide, segments: 6);
        p.Add("torso", PartShape.Ellipsoid, V(D * .15f, H * .30f, W * .40f), V(D * .19f, H * .62f, 0), belly, segments: 6);
        p.Add("hips", PartShape.Wedge, V(D * .47f, H * .16f, W * .53f), V(-D * .10f, H * .41f, 0), cloth);
        p.Add("tail", PartShape.Ellipsoid, V(D * .50f, H * .065f, W * .12f), V(-D * .54f, H * .36f, 0), hide, V(0, 0, 22), segments: 5);
        p.Add("tail", PartShape.Cone, V(D * .22f, H * .09f, W * .16f), V(-D * .76f, H * .30f, 0), dark, V(0, 0, 110), segments: 5);
        p.Add("head", PartShape.Ellipsoid, V(D * .42f, H * .21f, W * .39f), V(hx, H * .80f, 0), hide, segments: 6);
        p.Add("head", PartShape.Wedge, V(D * .40f * snout, H * .095f, W * .29f), V(hx + D * .20f, H * .77f, 0), hide, V(0, 0, -8));
        p.Add("head", PartShape.Wedge, V(D * .35f * snout, H * .045f, W * .25f), V(hx + D * .20f, H * .715f, 0), belly, V(0, 0, -3));
        p.Add("head", PartShape.Box, V(D * .35f * snout, H * .025f, W * .26f), V(hx + D * .21f, H * .743f, 0), dark);
        p.Add("head", PartShape.Ellipsoid, V(D * .10f, H * .05f, W * .22f), V(hx + D * .40f * snout, H * .78f, 0), dark, segments: 5);
        foreach (var side in new[] { -1f, 1f })
        {
            var z = side * W * .36f;
            var arm = side < 0 ? "arm_l" : "arm_r";
            var leg = side < 0 ? "leg_l" : "leg_r";
            p.Bone(arm, "torso", V(-D * .03f, H * .76f, z));
            p.Bone(leg, "hips", V(-D * .12f, H * .40f, side * W * .20f));
            p.Add(arm, PartShape.Ellipsoid, V(D * .30f, H * .27f, W * .20f * muscle), V(-D * .01f, H * .64f, z), hide, segments: 6);
            p.Add(arm, PartShape.Ellipsoid, V(D * .23f, H * .24f, W * .15f * muscle), V(D * .08f, H * .46f, z), hide, V(0, 0, -15), segments: 6);
            p.Add(arm, PartShape.Ellipsoid, V(D * .26f, H * .07f, W * .21f), V(D * .28f, H * .34f, z), dark, segments: 5);
            p.Add(leg, PartShape.Ellipsoid, V(D * .30f, H * .24f, W * .26f), V(-D * .04f, H * .30f, side * W * .20f), hide, V(0, 0, -20), segments: 6);
            p.Add(leg, PartShape.Ellipsoid, V(D * .16f, H * .20f, W * .15f), V(-D * .11f, H * .15f, side * W * .20f), hide, V(0, 0, 20), segments: 5);
            p.Add(leg, PartShape.Ellipsoid, V(D * .36f, H * .075f, W * .28f), V(D * .02f, H * .0375f, side * W * .20f), dark, segments: 6);
            for (var toe = -1; toe <= 1; toe++) p.Add(leg, PartShape.Cone, V(W * .045f, D * .12f, W * .045f), V(D * .22f, H * .025f, side * W * .20f + toe * W * .07f), bone, V(0, 0, -90), segments: 4);
            p.Add("head", PartShape.Ellipsoid, V(D * .07f, H * .045f, W * .06f), V(hx + D * .15f, H * .837f, side * W * .165f), b.C("eye", "#edba64"), segments: 5);
            p.Add("head", PartShape.Wedge, V(D * .19f, H * .035f, W * .07f), V(hx + D * .10f, H * .867f, side * W * .17f), dark, V(side * 18, 0, -9));
            if (b.Has("pointed_ears"))
            {
                p.Add("head", PartShape.Cone, V(D * .15f, H * .19f, W * .18f), V(hx - D * .05f, H * .935f, side * W * .18f), hide, V(side * 17, 0, 10), segments: 4);
            }
            else
            {
                p.Add("head", PartShape.Ellipsoid, V(D * .12f, H * .115f, W * .19f), V(hx - D * .06f, H * .92f, side * W * .19f), hide, segments: 6);
                p.Add("head", PartShape.Ellipsoid, V(D * .025f, H * .067f, W * .12f), V(hx + D * .01f, H * .923f, side * W * .19f), dark, segments: 5);
            }
            for (var tooth = 0; tooth < 4; tooth++) p.Add("head", PartShape.Cone, V(D * .045f, H * (tooth == 3 ? .055f : .027f), W * .038f), V(hx + D * (.04f + tooth * .09f), H * .737f, side * W * .135f), bone, V(0, 0, 180), segments: 4);
            if (b.Has("spots")) for (var i = 0; i < 4; i++) p.Add("torso", PartShape.Ellipsoid, V(D * .12f, H * .055f, W * .025f), V(-D * .22f + i % 2 * D * .20f, H * (.65f + i / 2f * .075f), side * W * .32f), dark, segments: 5);
            if (b.Has("bracers")) p.Add(arm, PartShape.Cylinder, V(W * .20f, H * .09f, W * .20f), V(D * .10f, H * .41f, z), metal, segments: 6);
            if (b.Has("armor")) p.Add("torso", PartShape.Wedge, V(D * .48f, H * .10f, W * .30f), V(-D * .06f, H * .79f, z), metal);
            p.Sockets[side < 0 ? "hand_l" : "hand_r"] = new BodySocket(arm, V(D * .28f, H * .34f, z), Vec3.Zero, H * b.S("grip", .95f));
        }
        if (b.Has("ridge")) for (var i = 0; i < 5; i++) p.Add("torso", PartShape.Cone, V(D * .17f, H * (.10f + i * .012f), W * .15f), V(-D * .35f + i * D * .055f, H * (.66f + i * .06f), 0), dark, V(0, 0, 30), segments: 4);
        if (b.Has("throat")) p.Add("head", PartShape.Ellipsoid, V(D * .28f, H * .20f, W * .36f), V(D * .22f, H * .70f, 0), belly, segments: 6);
        if (b.Has("apron")) p.Add("hips", PartShape.Wedge, V(D * .06f, H * .28f, W * .45f), V(D * .15f, H * .35f, 0), accent);
        if (b.Has("scars")) for (var i = 0; i < 3; i++) p.Add("torso", PartShape.Box, V(D * .025f, H * .08f, W * .025f), V(D * .22f, H * .69f, W * (.05f + i * .08f)), accent, V(25, 0, 0));
        KinAdornments(b);
        p.Swings = true;
    }

    public static void Beastkin(BodyBuild b)
    {
        var p = b.Plan;
        p.Rig = "biped";
        var H = b.Height;
        var W = H * b.S("breadth", .48f);
        var D = H * b.S("depth", .38f);
        var hip = H * b.S("legs", .50f);
        var muscle = b.S("muscle", 1);
        var horn = b.S("horns", 1);
        var bull = b.Has("bull");
        var hide = b.C("hide", "#95714b");
        var belly = b.C("belly", "#c9b894");
        var dark = b.C("dark", "#332e28");
        var bone = b.C("bone", "#ddd0aa");
        var metal = b.C("metal", "#777b78");
        p.Bone("hips", "root", V(0, hip, 0));
        p.Bone("torso", "hips", V(0, hip, 0));
        p.Bone("head", "torso", V(D * .06f, H * .82f, 0));
        p.Add("torso", PartShape.Ellipsoid, V(D * .70f, H * .35f, W * .71f), V(-D * .05f, H * .66f, 0), hide, segments: 7);
        p.Add("torso", PartShape.Ellipsoid, V(D * .15f, H * .23f, W * .51f), V(D * .30f, H * .68f, 0), belly, segments: 6);
        p.Add("hips", PartShape.Ellipsoid, V(D * .60f, H * .15f, W * .51f), V(0, hip, 0), dark, segments: 6);
        p.Add("hips", PartShape.Wedge, V(D * .10f, H * .18f, W * .27f), V(D * .31f, hip - H * .05f, 0), b.C("cloth", "#554239"));
        p.Add("head", PartShape.Ellipsoid, V(D * (bull ? .50f : .35f), H * .20f, W * (bull ? .43f : .32f)), V(D * .09f, H * .86f, 0), hide, segments: 6);
        p.Add("head", PartShape.Ellipsoid, V(D * .35f, H * (bull ? .09f : .13f), W * (bull ? .38f : .22f)), V(D * .32f, H * .81f, 0), belly, segments: 6);
        p.Add("head", PartShape.Ellipsoid, V(D * .06f, H * .04f, W * (bull ? .30f : .14f)), V(D * .51f, H * .81f, 0), dark, segments: 5);
        foreach (var side in new[] { -1f, 1f })
        {
            var z = side * W * .36f;
            var arm = side < 0 ? "arm_l" : "arm_r";
            var leg = side < 0 ? "leg_l" : "leg_r";
            p.Bone(arm, "torso", V(0, H * .77f, z));
            p.Bone(leg, "hips", V(0, hip, side * W * .22f));
            p.Add(arm, PartShape.Ellipsoid, V(D * .34f, H * .23f, W * .20f * muscle), V(0, H * .66f, z), hide, segments: 6);
            p.Add(arm, PartShape.Ellipsoid, V(D * .28f, H * .20f, W * .16f * muscle), V(D * .09f, H * .50f, z), hide, V(0, 0, -12), segments: 6);
            p.Add(arm, PartShape.Box, V(D * .25f, H * .07f, W * .20f), V(D * .28f, H * .39f, z), dark);
            // Thigh, backward shin/hock, then forward pastern: a readable ungulate leg.
            p.Add(leg, PartShape.Ellipsoid, V(D * .36f, hip * .49f, W * .27f), V(D * .06f, hip * .80f, side * W * .22f), hide, V(0, 0, -16), segments: 6);
            p.Add(leg, PartShape.Ellipsoid, V(D * .19f, hip * .45f, W * .17f), V(-D * .04f, hip * .49f, side * W * .22f), dark, V(0, 0, 28), segments: 5);
            p.Add(leg, PartShape.Ellipsoid, V(D * .22f, hip * .14f, W * .19f), V(-D * .17f, hip * .30f, side * W * .22f), hide, segments: 5);
            p.Add(leg, PartShape.Ellipsoid, V(D * .14f, hip * .30f, W * .13f), V(-D * .08f, hip * .18f, side * W * .22f), dark, V(0, 0, -25), segments: 5);
            foreach (var toe in new[] { -1f, 1f }) p.Add(leg, PartShape.Wedge, V(D * .34f, H * .07f, W * .11f), V(D * .06f, H * .035f, side * W * .22f + toe * W * .061f), b.C("hoof", "#4a4540"));
            p.Add("head", PartShape.Ellipsoid, V(D * .12f, H * .05f, W * .26f), V(-D * .02f, H * .875f, side * W * .26f), hide, V(side * 15, 0, 0), segments: 5);
            p.Add("head", PartShape.Ellipsoid, V(D * .04f, H * .025f, W * .065f), V(D * .25f, H * .885f, side * W * .15f), b.C("eye", "#edba64"), segments: 5);
            p.Add("head", PartShape.Wedge, V(D * .16f, H * .035f, W * .08f), V(D * .18f, H * .91f, side * W * .15f), dark, V(side * 10, 0, 0));
            if (b.Has("bull_horns"))
            {
                p.Add("head", PartShape.Cylinder, V(W * .10f, W * .30f, W * .10f), V(-D * .03f, H * .94f, side * W * .29f), bone, V(side * 70, 0, 0), taper: .65f, segments: 6);
                p.Add("head", PartShape.Cone, V(W * .075f, H * .16f, W * .075f), V(-D * .03f, H * 1.01f, side * W * .43f), bone, V(side * 20, 0, -15), segments: 5);
            }
            if (b.Has("swept_horns")) p.Add("head", PartShape.Cone, V(W * .10f, H * .27f * horn, W * .10f), V(-D * .09f, H * 1.02f, side * W * .15f), bone, V(side * 12, 0, 28), segments: 6);
            if (b.Has("ram_horns"))
            {
                p.Add("head", PartShape.Ellipsoid, V(D * .43f * horn, H * .10f * horn, W * .14f), V(-D * .04f, H * .985f, side * W * .25f), bone, segments: 6);
                p.Add("head", PartShape.Ellipsoid, V(D * .14f * horn, H * .18f * horn, W * .14f), V(-D * .23f * horn, H * .92f, side * W * .28f), bone, V(0, 0, -12), segments: 6);
                p.Add("head", PartShape.Ellipsoid, V(D * .35f * horn, H * .07f * horn, W * .12f), V(-D * .09f, H * .84f, side * W * .28f), bone, V(0, 0, 15), segments: 6);
                p.Add("head", PartShape.Cone, V(W * .095f, H * .12f * horn, W * .095f), V(D * .08f, H * .87f, side * W * .28f), bone, V(0, 0, -25), segments: 5);
            }
            if (b.Has("armor")) p.Add("torso", PartShape.Wedge, V(D * .44f, H * .10f, W * .30f), V(0, H * .79f, z), metal);
            if (b.Has("bracers")) p.Add(arm, PartShape.Cylinder, V(W * .21f, H * .10f, W * .21f), V(D * .09f, H * .46f, z), metal, segments: 6);
            p.Sockets[side < 0 ? "hand_l" : "hand_r"] = new BodySocket(arm, V(D * .28f, H * .39f, z), Vec3.Zero, H * b.S("grip", .95f));
        }
        if (b.Has("beard") || bull) p.Add("head", PartShape.Cone, V(D * .17f, H * .18f, W * .24f), V(D * .20f, H * .73f, 0), dark, V(0, 0, 180), segments: 5);
        if (b.Has("armor")) p.Add("torso", PartShape.Wedge, V(D * .10f, H * .24f, W * .57f), V(D * .32f, H * .66f, 0), metal);
        if (b.Has("warpaint")) for (var i = -1; i <= 1; i++) p.Add("torso", PartShape.Box, V(D * .025f, H * .15f, W * .05f), V(D * .39f, H * .68f, i * W * .16f), b.C("accent", "#7d9e95"), V(20, 0, 0));
        KinAdornments(b);
        p.Swings = true;
    }

    private static void KinAdornments(BodyBuild b)
    {
        var p = b.Plan;
        var H = b.Height;
        var W = H * b.S("breadth", .48f);
        var D = H * b.S("depth", .40f);
        var bone = b.C("bone", "#ddd0aa");
        var cloth = b.C("cloth", "#554239");
        var wood = b.C("wood", "#72583d");
        var metal = b.C("metal", "#777b78");
        if (b.Has("mantle")) p.Add("torso", PartShape.Wedge, V(D * .12f, H * .37f, W * .76f), V(-D * .40f, H * .62f, 0), cloth);
        if (b.Has("pack")) p.Add("torso", PartShape.Box, V(D * .30f, H * .24f, W * .48f), V(-D * .43f, H * .63f, 0), cloth);
        if (b.Has("trophies")) for (var i = -1; i <= 1; i++)
        {
            p.Add("torso", PartShape.Ellipsoid, V(H * .065f, H * .075f, H * .065f), V(D * .36f, H * .65f + i % 2 * H * .03f, i * W * .16f), bone, segments: 5);
            p.Add("torso", PartShape.Cone, V(H * .025f, H * .07f, H * .025f), V(D * .38f, H * .60f + i % 2 * H * .03f, i * W * .16f), bone, V(0, 0, 180), segments: 4);
        }
        if (b.Has("quiver")) for (var i = -1; i <= 1; i++)
        {
            p.Add("torso", PartShape.Cylinder, V(H * .014f, H * .48f, H * .014f), V(-D * .43f, H * .72f, i * W * .09f), wood, segments: 5);
            p.Add("torso", PartShape.Cone, V(H * .05f, H * .09f, H * .05f), V(-D * .43f, H * .97f, i * W * .09f), bone, segments: 4);
        }
        if (b.Has("warhorn"))
        {
            p.Add("torso", PartShape.Cone, V(H * .18f, H * .37f, H * .18f), V(-D * .43f, H * .72f, W * .17f), bone, V(25, 0, -20), segments: 7);
            p.Add("torso", PartShape.Cylinder, V(H * .19f, H * .025f, H * .19f), V(-D * .36f, H * .55f, W * .10f), metal, V(25, 0, -20), segments: 7);
        }
        if (b.Has("censer")) p.Add("torso", PartShape.Ellipsoid, V(H * .10f, H * .13f, H * .10f), V(D * .38f, H * .55f, W * .14f), b.C("accent", "#bd6848"), glow: true, segments: 6);
        if (b.Has("snares")) for (var i = 0; i < 3; i++) p.Add("hips", PartShape.Cylinder, V(H * .11f, H * .02f, H * .11f), V(-D * .1f, H * (.41f - i * .05f), W * .36f), metal, V(90, 0, 0), segments: 6);
    }
}
