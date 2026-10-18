using System.Numerics;
using HollowWake.Core.Combat;

namespace HollowWake.Core.Effects;

/// <summary>Authored launch elevation choices. These choose a point, never acquire or lock a victim.</summary>
public static class ProjectileAims
{
    private static readonly Dictionary<string, Func<EffectContext, Vector3?>> Registry = new(StringComparer.Ordinal)
    {
        ["horizontal"] = _ => null,
        ["target"] = BodyAim,
        ["point"] = c => BodyAim(c) ?? c.Point,
    };
    private static Vector3? BodyAim(EffectContext c) => c.TargetTransform is not null ? HitBodies.AimPoint(c.Sim, c.Target) :
        c.SampledTargetPoint is { } point ? point + c.AimOffset :
        c.AimOffset != Vector3.Zero ? c.Point + c.AimOffset : null;
    public static IEnumerable<string> Names => Registry.Keys;
    public static void Register(string name, Func<EffectContext, Vector3?> resolve) => Registry[name] = resolve;
    public static Func<EffectContext, Vector3?> Get(string name) => Registry.TryGetValue(name, out var resolve) ? resolve :
        throw new InvalidOperationException($"Unknown projectile aim '{name}'.");
}
