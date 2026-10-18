using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Combat;

public sealed class HitBody : IComponent
{
    public required HitBodyDef Definition { get; init; }
}

/// <summary>A solid of revolution about the vertical axis. Its horizontal slice is an exact disk.</summary>
public abstract class HitVolumeShape
{
    public abstract string Name { get; }
    /// <summary>Radius multiplier at normalized vertical distance from center, in [0,1].</summary>
    public abstract float Slice(float height);
    public virtual bool Convex => false;
}

public static class HitVolumeShapes
{
    private static readonly ConcurrentDictionary<string, HitVolumeShape> Registry = new(StringComparer.Ordinal);
    static HitVolumeShapes() { Register(new Ellipsoid()); Register(new Cylinder()); }
    public static void Register(HitVolumeShape shape) => Registry[shape.Name] = shape;
    public static bool Has(string name) => Registry.ContainsKey(name);
    public static HitVolumeShape Get(string name) => Registry.TryGetValue(name, out var shape) ? shape :
        throw new InvalidOperationException($"Unknown hit volume '{name}'.");
    private sealed class Ellipsoid : HitVolumeShape
    {
        public override string Name => "ellipsoid";
        public override bool Convex => true;
        public override float Slice(float height) => MathF.Sqrt(MathF.Max(0, 1 - height * height));
    }
    private sealed class Cylinder : HitVolumeShape
    {
        public override string Name => "cylinder";
        public override bool Convex => true;
        public override float Slice(float height) => 1;
    }
}

/// <summary>Pure anatomy queries. Navigation radius, material volume and cosmetic meshes are independent.</summary>
public static class HitBodies
{
    private static readonly HitVolumeSpec Legacy = new() { Shape = "cylinder", Up = 0.5f, HalfHeight = 0.5f, Radius = 1 };
    private sealed class BoundsCache
    {
        public long Tick = -1;
        public (float Radius, float Height) Value;
    }
    private static readonly ConditionalWeakTable<Simulation, BoundsCache> CachedBounds = new();
    /// <summary>Call after adding or resizing a body within a tick. Motion and facing do not change enclosing bounds.</summary>
    public static void InvalidateBounds(Simulation sim)
    {
        if (CachedBounds.TryGetValue(sim, out var cache)) cache.Tick = -1;
    }

    /// <summary>Continuous contact for disk-shaped flights against convex slices; other shapes keep their endpoint test.</summary>
    public static float? Sweep(Simulation sim, Entity e, Shape shape, in ShapeSize size, Vector3 from, Vector3 to,
        Vector2 facing, float halfHeight)
    {
        if (!shape.Circular || size.Inner > 0)
            return Touches(sim, e, shape, size, to, facing, to.Z - halfHeight, to.Z + halfHeight) ? 1 : null;
        if (sim.World.Get<Transform>(e) is not { } t) return null;
        var body = sim.World.Get<HitBody>(e);
        float? first = null;
        var count = body?.Definition.Volumes.Count ?? 1;
        for (var i = 0; i < count; i++)
        {
            var v = body is null ? Legacy : body.Definition.Volumes[i];
            var primitive = HitVolumeShapes.Get(v.Shape);
            if (!primitive.Convex)
            {
                if (Touches(sim, e, shape, size, to, facing, to.Z - halfHeight, to.Z + halfHeight)) first ??= 1;
                continue;
            }
            var c = Center(t, v);
            var h = v.HalfHeight * t.Height;
            var radius = v.Radius * (body is null ? t.Radius : t.Height);
            var start = from - c;
            var delta = to - from;
            float low = 0, high = first ?? 1;
            var flatStart = new Vector2(start.X, start.Y);
            var flatDelta = new Vector2(delta.X, delta.Y);
            var lengthSquared = flatDelta.LengthSquared();
            var nearest = lengthSquared > 0 ? Math.Clamp(-Vector2.Dot(flatStart, flatDelta) / lengthSquared, 0, high) : 0;
            var enclosingRadius = radius + size.Radius;
            if ((flatStart + flatDelta * nearest).LengthSquared() > enclosingRadius * enclosingRadius + 1e-6f) continue;
            if (MathF.Abs(delta.Z) < 1e-8f)
            {
                if (MathF.Abs(start.Z) > h + halfHeight) continue;
                // Constant-height disks have an analytic entry; no iterative minimization is needed.
                var r = size.Radius + radius * primitive.Slice(MathF.Max(0, MathF.Abs(start.Z) - halfHeight) / h);
                var c0 = flatStart.LengthSquared() - r * r;
                if (c0 <= 1e-6f) { first = 0; continue; }
                if (lengthSquared < 1e-12f) continue;
                var projection = Vector2.Dot(flatStart, flatDelta);
                var discriminant = projection * projection - lengthSquared * c0;
                if (discriminant < 0) continue;
                var entry = (-projection - MathF.Sqrt(discriminant)) / lengthSquared;
                if (entry >= 0 && entry <= high) first = entry;
                continue;
            }
            else
            {
                var a = (-h - halfHeight - start.Z) / delta.Z;
                var b = (h + halfHeight - start.Z) / delta.Z;
                low = MathF.Max(low, MathF.Min(a, b)); high = MathF.Min(high, MathF.Max(a, b));
                if (low > high) continue;
            }
            var flightRadius = size.Radius;
            float Gap(float time)
            {
                var p = start + delta * time;
                var dz = MathF.Max(0, MathF.Abs(p.Z) - halfHeight) / h;
                return MathF.Sqrt(p.X * p.X + p.Y * p.Y) - flightRadius - radius * primitive.Slice(dz);
            }
            // The gap is convex on the vertically clipped interval. Minimize, then find the entry root.
            var a0 = low; var b0 = high;
            for (var n = 0; n < 32; n++)
            {
                var a = (2 * a0 + b0) / 3; var b = (a0 + 2 * b0) / 3;
                if (Gap(a) < Gap(b)) b0 = b; else a0 = a;
            }
            var minimum = (a0 + b0) * 0.5f;
            if (Gap(minimum) > 1e-6f && Gap(low) > 1e-6f && Gap(high) > 1e-6f) continue;
            if (Gap(low) <= 1e-6f) { first = low; continue; }
            var end = Gap(minimum) <= 1e-6f ? minimum : high;
            for (var n = 0; n < 28; n++)
            {
                var mid = (low + end) * 0.5f;
                if (Gap(mid) <= 1e-6f) end = mid; else low = mid;
            }
            first = end;
        }
        return first;
    }

    public static Vector3 Center(Transform t, HitVolumeSpec v) => t.Position + new Vector3(
        (t.Forward.X * v.Forward - t.Forward.Y * v.Left) * t.Height,
        (t.Forward.Y * v.Forward + t.Forward.X * v.Left) * t.Height, v.Up * t.Height);

    public static Vector3 AimPoint(Simulation sim, Entity e)
    {
        if (sim.World.Get<Transform>(e) is not { } t) return default;
        if (sim.World.Get<HitBody>(e) is { } body) return Center(t, body.Definition.Volumes[body.Definition.Aim]);
        return t.Position + new Vector3(0, 0, t.Height * 0.5f);
    }

    /// <summary>The damage solid intersected by a planar delivery extruded between low and high Z.</summary>
    public static bool Touches(Simulation sim, Entity e, Shape shape, in ShapeSize size,
        Vector3 anchor, Vector2 facing, float low, float high, Vector3? position = null)
    {
        if (sim.World.Get<Transform>(e) is not { } t) return false;
        // A pre-commit observation may place the same anatomy at an estimated position.
        // Never move the live body or substitute its navigation radius for its damage profile.
        var origin = position ?? t.Position;
        if (sim.World.Get<HitBody>(e) is not { } body)
            return high >= origin.Z && low <= origin.Z + t.Height &&
                shape.Contains(size, new(origin.X - anchor.X, origin.Y - anchor.Y), facing, t.Radius);
        foreach (var v in body.Definition.Volumes)
        {
            var center = Center(t, v) + (origin - t.Position);
            var h = t.Height * v.HalfHeight;
            var dz = MathF.Max(0, MathF.Max(low - center.Z, center.Z - high));
            if (dz > h) continue;
            var radius = t.Height * v.Radius * HitVolumeShapes.Get(v.Shape).Slice(dz / h);
            if (shape.Contains(size, new(center.X - anchor.X, center.Y - anchor.Y), facing, radius)) return true;
        }
        return false;
    }

    /// <summary>Horizontal extent along a world direction, used for source reach, never movement clearance.</summary>
    public static float Reach(Simulation sim, Entity e, Vector2 direction)
    {
        if (sim.World.Get<Transform>(e) is not { } t) return 0;
        if (sim.World.Get<HitBody>(e) is not { } body) return t.Radius;
        var reach = 0f;
        foreach (var v in body.Definition.Volumes)
        {
            var c = Center(t, v) - t.Position;
            reach = MathF.Max(reach, Vector2.Dot(new(c.X, c.Y), direction) + v.Radius * t.Height);
        }
        return reach;
    }

    /// <summary>Enclosing bounds sampled once per tick, invalidated by body creation/resizing within a tick.</summary>
    public static (float Radius, float Height) Bounds(Simulation sim)
    {
        var cache = CachedBounds.GetOrCreateValue(sim);
        if (cache.Tick == sim.Clock.Tick) return cache.Value;
        float radius = 0, height = 0;
        foreach (var e in sim.Spatial.LiveEntities)
        {
            if (!sim.World.Has<Stats>(e) || sim.World.Get<Transform>(e) is not { } t) continue;
            if (sim.World.Get<HitBody>(e) is { } body)
            {
                foreach (var v in body.Definition.Volumes)
                {
                    radius = MathF.Max(radius, (MathF.Sqrt(v.Forward * v.Forward + v.Left * v.Left) + v.Radius) * t.Height);
                    height = MathF.Max(height, (MathF.Abs(v.Up) + v.HalfHeight) * t.Height);
                }
            }
            else { radius = MathF.Max(radius, t.Radius); height = MathF.Max(height, t.Height); }
        }
        cache.Tick = sim.Clock.Tick;
        return cache.Value = (radius, height);
    }
}
