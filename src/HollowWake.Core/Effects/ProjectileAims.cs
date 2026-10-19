using System.Numerics;
using HollowWake.Core.Combat;

namespace HollowWake.Core.Effects;

/// <summary>A registered launch aim and optional terrain-relative floor for its outward flight.</summary>
public sealed record ProjectileAim(Func<EffectContext, Vector3?> Point, Func<EffectContext, float?>? GroundFloor = null, bool ReturnGroundFloor = false);

/// <summary>Authored launch elevation choices. These choose a point, never acquire or lock a victim.</summary>
public static class ProjectileAims
{
    private static readonly Dictionary<string, ProjectileAim> Registry = new(StringComparer.Ordinal)
    {
        ["horizontal"] = new(_ => null),
        ["target"] = new(c => BodyAim(c) ?? (FreeGroundAim(c) ? c.Point : null),
            c => FreeGroundAim(c) ? (float)c.Sim.Content.Rules.Number("delivery.projectile_ground_floor", 0) : null, ReturnGroundFloor: true),
        ["point"] = new(c => BodyAim(c) ?? c.Point),
    };
    private static bool FreeGroundAim(EffectContext c) => c.LaunchLift is null && BodyAim(c) is null;
    private static Vector3? BodyAim(EffectContext c) => c.SampledTargetPoint is { } point ? point + c.AimOffset :
        c.TargetTransform is not null ? HitBodies.AimPoint(c.Sim, c.Target) :
        c.AimOffset != Vector3.Zero ? c.Point + c.AimOffset : null;
    public static IEnumerable<string> Names => Registry.Keys;
    public static void Register(string name, Func<EffectContext, Vector3?> resolve) => Register(name, new ProjectileAim(resolve));
    public static void Register(string name, ProjectileAim aim) => Registry[name] = aim;
    public static Func<EffectContext, Vector3?> Get(string name) => Policy(name).Point;
    public static ProjectileAim Policy(string name) => Registry.TryGetValue(name, out var aim) ? aim :
        throw new InvalidOperationException($"Unknown projectile aim '{name}'.");
}
