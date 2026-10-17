using System.Numerics;
using HollowWake.Core.Ecs;

namespace HollowWake.Core.Effects;

/// <summary>A projectile in flight. The projectile system moves it, tests hits and runs its effects.</summary>
public sealed class Projectile : IComponent
{
    public required EffectContext Context;
    public Vector3 Velocity;
    public float Range;
    public float Traveled;
    public int Pierce;
    public HashSet<Entity> Hit = new();
    public CompiledEffect[] OnHit = Array.Empty<CompiledEffect>();
    public CompiledEffect[] OnExpire = Array.Empty<CompiledEffect>();
    public float Radius = 0.5f;
    public bool Gravity;
}

/// <summary>A standing area that pulses effects on whoever is inside until it expires.</summary>
public sealed class Field : IComponent
{
    public required EffectContext Context;
    public float Radius;
    public double Interval;
    public double NextPulseAt;
    public double ExpiresAt;
    public string Filter = "enemy";
    public CompiledEffect[] Pulse = Array.Empty<CompiledEffect>();
    public Entity Follow = Entity.None;
}
