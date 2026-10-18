namespace HollowWake.Client.World.Logic;

/// <summary>Reusable carried supplies and armor, fitted to chest/belt sockets on any compatible body.</summary>
public static class FieldGearFamilies
{
    private static Vec3 V(float x, float y, float z) => new(x, y, z);

    public static void FieldKit(BodyBuild b, GearFit f)
    {
        var W = b.S("breadth", 0.4f);
        var D = b.S("depth", 0.37f);
        var cloth = b.C("cloth", "#514d47");
        var metal = b.C("metal", "#929897");
        var bone = b.C("bone", "#e6d5ad");
        var wood = b.C("wood", "#765942");
        var accent = b.C("accent", "#c7a36a");
        // A complete field kit may include belt pieces. Each follows its own animated bone.
        var belt = f.Plan.Sockets.GetValueOrDefault("belt", f.Socket);
        if (b.Has("crown") && f.Plan.Sockets.TryGetValue("head", out var head)) for (var i = 0; i < 3; i++)
            f.Add(PartShape.Wedge, V(D * 0.18f, 0.17f, W * 0.08f), V(-D * 0.26f + i * D * 0.19f, 0, 0), metal, socket: head);
        if (b.Has("armor"))
        {
            foreach (var side in new[] { -1f, 1f })
                f.Add(PartShape.Wedge, V(D * 0.44f, 0.11f, W * 0.30f), V(-D * 0.03f, 0.11f, side * W * 0.37f), metal);
            f.Add(PartShape.Wedge, V(D * 0.17f, 0.28f, W * 0.58f), V(D * 0.31f, 0.01f, 0), metal);
        }
        if (b.Has("backplates")) for (var i = 0; i < 3; i++)
            f.Add(PartShape.Wedge, V(D * 0.35f, 0.16f, W * 0.77f), V(-D * 0.38f, -0.11f + i * 0.13f, 0), metal);
        if (b.Has("robe")) f.Add(PartShape.Cone, V(D * 0.80f, 0.32f, W * 0.84f), V(0, -0.09f, 0), cloth, segments: 7, socket: belt);
        if (b.Has("satchel")) f.Add(PartShape.Box, V(D * 0.35f, 0.27f, W * 0.61f), V(-D * 0.48f, -0.03f, 0), cloth);
        if (b.Has("stonepack")) for (var i = -1; i <= 1; i++)
            f.Add(PartShape.Ellipsoid, V(D * 0.43f, 0.22f, W * 0.29f), V(-D * 0.48f, 0.08f * (i == 0 ? 1 : 0), i * W * 0.24f), metal, segments: 5);
        if (b.Has("quiver")) for (var i = -1; i <= 1; i++)
        {
            f.Add(PartShape.Cylinder, V(0.018f, 0.53f, 0.018f), V(-D * 0.43f, 0.10f, i * W * 0.14f), wood, segments: 4);
            f.Add(PartShape.Wedge, V(0.06f, 0.09f, 0.03f), V(-D * 0.43f, 0.37f, i * W * 0.14f), bone);
        }
        if (b.Has("vials")) for (var i = -1; i <= 1; i++)
            f.Add(PartShape.Cylinder, V(0.065f, 0.15f, 0.065f), V(D * 0.39f, 0, i * W * 0.26f), accent, segments: 6, socket: belt);
        if (b.Has("censer")) f.Add(PartShape.Ellipsoid, V(D * 0.37f, 0.22f, W * 0.36f), V(-D * 0.47f, 0.18f, 0), accent, glow: true, segments: 6);
        if (b.Has("coil_yoke")) for (var i = -1; i <= 1; i += 2)
        {
            f.Add(PartShape.Cylinder, V(0.035f, 0.46f, 0.035f), V(-D * 0.38f, 0.24f, i * W * 0.40f), metal, segments: 5);
            f.Add(PartShape.Ellipsoid, V(0.12f, 0.12f, 0.12f), V(-D * 0.38f, 0.48f, i * W * 0.40f), accent, segments: 6);
        }
        if (b.Has("drum")) f.Add(PartShape.Cylinder, V(W * 0.52f, 0.21f, W * 0.52f), V(D * 0.47f, 0.04f, 0), wood, segments: 8, socket: belt);
        if (b.Has("banner"))
        {
            f.Add(PartShape.Cylinder, V(0.022f, 0.65f, 0.022f), V(-D * 0.48f, 0.32f, W * 0.25f), wood, segments: 4);
            f.Add(PartShape.Wedge, V(0.018f, 0.23f, W * 0.62f), V(-D * 0.48f, 0.53f, W * 0.49f), accent);
        }
        // Card framing must include the worn kit, including a banner above the anatomical head.
        var (min, max) = f.Plan.Bounds();
        f.Plan.Height = MathF.Max(f.Plan.Height, max.Y);
        f.Plan.Width = MathF.Max(f.Plan.Width, MathF.Max(max.X - min.X, max.Z - min.Z));
    }
}
