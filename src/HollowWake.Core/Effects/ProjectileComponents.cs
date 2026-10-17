using System.Numerics;
using HollowWake.Core.Attributes;
using HollowWake.Core.Ecs;
using HollowWake.Core.Maths;

namespace HollowWake.Core.Effects;

/// <summary>Where a returning projectile heads once its outward flight is spent.</summary>
public enum ReturnMode
{
    None,
    /// <summary>Back to the point it was launched from.</summary>
    Origin,
    /// <summary>Back to its caster, wherever the caster now stands.</summary>
    Caster,
}

/// <summary>
/// What every projectile of one compiled <c>projectile</c> effect shares: its hook lists, its hit shape and
/// the settings that never change in flight. Built once when the effect compiles.
/// </summary>
public sealed class ProjectileSpec
{
    public static readonly ProjectileSpec Plain = new();

    /// <summary>Each body the flight strikes.</summary>
    public CompiledEffect[] OnHit = Array.Empty<CompiledEffect>();
    /// <summary>The first body a projectile strikes; its fork children never run it again.</summary>
    public CompiledEffect[] OnFirstHit = Array.Empty<CompiledEffect>();
    /// <summary>The flight running out of range or time, or meeting the ground.</summary>
    public CompiledEffect[] OnExpire = Array.Empty<CompiledEffect>();
    /// <summary>The flight ending for any reason except splitting into fork children.</summary>
    public CompiledEffect[] OnEnd = Array.Empty<CompiledEffect>();
    /// <summary>Every <see cref="TickInterval"/> seconds in flight, where the projectile is.</summary>
    public CompiledEffect[] OnTick = Array.Empty<CompiledEffect>();
    /// <summary>Every <see cref="TravelEvery"/> units of flight, where the projectile is.</summary>
    public CompiledEffect[] OnTravel = Array.Empty<CompiledEffect>();
    /// <summary>A lobbed projectile coming down on the ground.</summary>
    public CompiledEffect[] OnLand = Array.Empty<CompiledEffect>();
    /// <summary>A returning projectile arriving back at its caster or its origin.</summary>
    public CompiledEffect[] OnReturn = Array.Empty<CompiledEffect>();

    public double TickInterval = 0.5;
    public float TravelEvery = 2f;
    public ReturnMode Return;
    public bool Gravity;
    public Shape Shape = Shapes.Circle;

    /// <summary>True when the projectile runs effects of its own while it flies, so it needs its own variables.</summary>
    public bool Emits => OnTick.Length > 0 || OnTravel.Length > 0;
}

/// <summary>
/// A projectile in flight. The projectile system steers it along its trajectory axes, tests its hit shape
/// against hostile bodies, and runs its hooks: forks, chains, rehits and returns decide what a spent flight
/// does next. Angular rates are radians per second.
/// </summary>
public sealed class Projectile : IComponent
{
    public required EffectContext Context;
    public ProjectileSpec Spec = ProjectileSpec.Plain;

    // ---- motion
    /// <summary>The world velocity of the last step, vertical speed included.</summary>
    public Vector3 Velocity;
    /// <summary>Speed along the ground in units per second.</summary>
    public float Speed;
    /// <summary>The ground heading in radians.</summary>
    public float Heading;
    /// <summary>The path the flight follows before any weaving; the body sits on it plus the weave offset.</summary>
    public Vector3 Carrier;
    /// <summary>Where the flight began: the return point, the spiral's center.</summary>
    public Vector3 Origin;
    public double BornAt;
    public double Duration = double.PositiveInfinity;
    public float Range;
    /// <summary>Units flown on the current leg: since launch, the last chain or the fork that made this flight.</summary>
    public float Traveled;
    /// <summary>Units flown in all, for travel hooks and zigzags; never reset.</summary>
    public float Odometer;

    // ---- the hit shape
    /// <summary>The collision radius of a circle; the size of every other shape is <see cref="Size"/>.</summary>
    public float Radius = 0.5f;
    public ShapeSize Size;
    /// <summary>How far the shape has turned from the heading by spinning, in radians.</summary>
    public float SpinAngle;

    // ---- lifecycle
    public int Pierce;
    /// <summary>Bodies struck since the flight began, or since it last chained or turned.</summary>
    public int HitsTaken;
    public int Forks;
    public int Chains;
    /// <summary>Seconds a struck body is safe from this projectile; zero means each body is struck once.</summary>
    public float Rehit;
    public int Bounces;
    /// <summary>Flies through walls: launched with <c>occlusion: free</c> or by a weave that phases.</summary>
    public bool Phases;
    public bool Returning;
    /// <summary>When a returning flight turned back.</summary>
    public double TurnedAt;
    public bool FirstHitDone;
    /// <summary>Set when the flight ended this tick; the entity is gone at the end of the tick.</summary>
    public bool Ended;
    public HitLedger Hits = new();
    /// <summary>The body the cast aimed at, for homing.</summary>
    public Entity CastTarget = Entity.None;

    // ---- trajectory axes
    public float Homing;
    public float Guide;
    public float Erratic;
    public float Spiral;
    public float Orbit;
    public float Spin;
    /// <summary>Weaving frequency in cycles per second; <see cref="Amplitude"/> is how far it sways.</summary>
    public float Wave;
    public float Amplitude;
    /// <summary>Change of speed in units per second squared.</summary>
    public float Accel;
    public float Recurve;
    public float Converge;
    public float OrbitRadius;
    /// <summary>Radians the heading swings at each zigzag, every <see cref="ZigzagEvery"/> units.</summary>
    public float Zigzag;
    public float ZigzagEvery;

    // ---- steering state
    public Entity SeekTarget = Entity.None;
    /// <summary>True while the flight steers straight at <see cref="SeekTarget"/> after a chain.</summary>
    public bool Chasing;
    public double NextSeekAt;
    public Vector3 AimPoint;
    public bool Converging;
    public float SpiralAngle;
    public float SpiralRadius;
    public float ZigzagAt;
    public int ZigzagCount;
    /// <summary>Plus or minus one: which way a recurving flight bends, mirrored across a fan.</summary>
    public float Side = 1;
    public double NextTickAt;
    public float NextTravelAt;
    /// <summary>The flight's own random stream, for erratic steering; null when nothing in flight rolls.</summary>
    public Rng? Rng;

    /// <summary>The unit ground direction of the heading.</summary>
    public Vector2 Direction => new(MathF.Cos(Heading), MathF.Sin(Heading));

    /// <summary>True when anything steers the heading after launch.</summary>
    public bool Steers => Homing != 0 || Guide != 0 || Erratic != 0 || Recurve != 0 || Converging || Zigzag != 0 || Chasing;

    /// <summary>A copy of this flight for a fork child: every number and setting, its own ledger holding the same bodies.</summary>
    public Projectile CloneFlight()
    {
        var child = (Projectile)MemberwiseClone();
        child.Hits = new HitLedger();
        child.Hits.CopyFrom(Hits);
        child.Rng = null;
        child.Ended = false;
        return child;
    }
}

/// <summary>
/// Who a delivery has struck and when, so a body is not struck twice, or not again until a lockout passes.
/// A short array: most flights strike a handful of bodies.
/// </summary>
public sealed class HitLedger
{
    private Entity[] _bodies = new Entity[4];
    private double[] _times = new double[4];

    public int Count { get; private set; }

    /// <summary>True when the body was struck and is still locked out: forever with no lockout, else for <paramref name="lockout"/> seconds.</summary>
    public bool Holds(Entity body, double now, float lockout)
    {
        for (var i = 0; i < Count; i++)
        {
            if (_bodies[i] == body)
            {
                return lockout <= 0 || now - _times[i] < lockout;
            }
        }
        return false;
    }

    public bool Contains(Entity body)
    {
        for (var i = 0; i < Count; i++)
        {
            if (_bodies[i] == body)
            {
                return true;
            }
        }
        return false;
    }

    public void Record(Entity body, double now)
    {
        for (var i = 0; i < Count; i++)
        {
            if (_bodies[i] == body)
            {
                _times[i] = now;
                return;
            }
        }
        if (Count == _bodies.Length)
        {
            Array.Resize(ref _bodies, Count * 2);
            Array.Resize(ref _times, Count * 2);
        }
        _bodies[Count] = body;
        _times[Count] = now;
        Count++;
    }

    public void Clear() => Count = 0;

    public void CopyFrom(HitLedger other)
    {
        if (_bodies.Length < other.Count)
        {
            _bodies = new Entity[other._bodies.Length];
            _times = new double[other._times.Length];
        }
        Array.Copy(other._bodies, _bodies, other.Count);
        Array.Copy(other._times, _times, other.Count);
        Count = other.Count;
    }
}

/// <summary>
/// What every field of one compiled <c>field</c> effect shares: its shape, its hooks, its domain and how it
/// treats the bodies inside it.
/// </summary>
public sealed class FieldSpec
{
    public static readonly FieldSpec Plain = new();

    public Shape Shape = Shapes.Circle;
    /// <summary>Once per pulse at the field's center, before the pulse reaches the bodies inside.</summary>
    public CompiledEffect[] OnTick = Array.Empty<CompiledEffect>();
    /// <summary>A delayed field arriving.</summary>
    public CompiledEffect[] OnLand = Array.Empty<CompiledEffect>();
    public CompiledEffect[] OnEnter = Array.Empty<CompiledEffect>();
    public CompiledEffect[] OnExit = Array.Empty<CompiledEffect>();
    public CompiledEffect[] OnEnd = Array.Empty<CompiledEffect>();
    /// <summary>Modifiers every body inside that passes <see cref="DomainFilter"/> carries while it stays.</summary>
    public Modifier[] Domain = Array.Empty<Modifier>();
    public string DomainFilter = "enemy";
    /// <summary>Each body is pulsed once; with <see cref="RearmOnExit"/>, again after leaving and returning.</summary>
    public bool HitOnce;
    public bool RearmOnExit;
    /// <summary>A named curve the radius follows over the field's life, from <see cref="SizeFrom"/> to <see cref="SizeTo"/> times its cast size.</summary>
    public string? SizeCurve;
    public float SizeFrom = 1;
    public float SizeTo = 1;

    /// <summary>True when the field must know who is inside from tick to tick, not only at pulses.</summary>
    public bool TracksOccupants(Field f) => OnEnter.Length > 0 || OnExit.Length > 0 || Domain.Length > 0 || HitOnce || f.Exposure > 0 || f.Pull != 0;
}

/// <summary>A standing area that pulses effects on whoever is inside until it expires.</summary>
public sealed class Field : IComponent
{
    public required EffectContext Context;
    /// <summary>The radius the field was cast at, before its size envelope.</summary>
    public float Radius;
    public double Interval;
    public double NextPulseAt;
    public double ExpiresAt;
    public string Filter = "enemy";
    public CompiledEffect[] Pulse = Array.Empty<CompiledEffect>();
    public Entity Follow = Entity.None;

    public FieldSpec Spec = FieldSpec.Plain;
    /// <summary>The shape's proportions; its radius is the current radius.</summary>
    public ShapeSize Size;
    /// <summary>The facing of a shaped field, in radians.</summary>
    public float Facing;
    public double BornAt;
    /// <summary>When a delayed field arrives; it does nothing before.</summary>
    public double LandsAt;
    public bool Landed = true;
    public int PulsesLeft = int.MaxValue;
    /// <summary>Seconds a body must stand inside before pulses reach it.</summary>
    public double Exposure;
    /// <summary>Units per second the field draws the bodies inside toward its center.</summary>
    public float Pull;
    /// <summary>Units per second the field drifts along its facing.</summary>
    public float Drift;
    /// <summary>Units per second the field moves toward the nearest enemy.</summary>
    public float Seek;
    /// <summary>Radians per second the field's shape turns.</summary>
    public float Rotate;
    /// <summary>One per caster and key: a new field with the key ends the caster's older one.</summary>
    public string? Key;
    /// <summary>Reaches only bodies with a clear line from its center, past no wall or rising ground.</summary>
    public bool Occluded;
    /// <summary>The modifier source of the field's domain on the bodies inside.</summary>
    public string DomainSource = "";
    /// <summary>The domain's modifiers with this field as their source, built when the field is placed.</summary>
    public Modifier[] DomainModifiers = Array.Empty<Modifier>();
    /// <summary>The bodies inside, when the field tracks them.</summary>
    public Dictionary<Entity, Occupant>? Occupants;
    /// <summary>Set when the field ended this tick; the entity is gone at the end of the tick.</summary>
    public bool Ended;
}

/// <summary>
/// A live line between two ends (bodies or a point): every interval it strikes the bodies it passes within
/// half its width of. It ends when its time runs out, when an end it is bound to is gone, or when it is
/// stretched past its length.
/// </summary>
public sealed class Tether : IComponent
{
    public required EffectContext Context;
    /// <summary>The body the line starts from.</summary>
    public Entity From = Entity.None;
    /// <summary>The body the line runs to; none when it runs to <see cref="Point"/>.</summary>
    public Entity To = Entity.None;
    public Vector3 Point;
    public float Width = 1f;
    /// <summary>Stretched further than this, the line snaps; infinite when it cannot.</summary>
    public float MaxLength = float.PositiveInfinity;
    public double Interval = 0.5;
    public double NextPulseAt;
    public double ExpiresAt = double.PositiveInfinity;
    public string Filter = "enemy";
    public CompiledEffect[] Pulse = Array.Empty<CompiledEffect>();
    public CompiledEffect[] OnEnd = Array.Empty<CompiledEffect>();
    public bool Ended;
}

/// <summary>
/// A body a field knows: when it entered, whether it passes the field's filter, whether it carries the
/// field's domain, whether a pulse already struck it, whether it is inside now and the tick it was last
/// seen inside.
/// </summary>
public struct Occupant
{
    public double EnteredAt;
    public bool Passes;
    public bool InDomain;
    public bool Struck;
    public bool Inside;
    public long SeenAt;
}
