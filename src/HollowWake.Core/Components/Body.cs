using System.Numerics;
using HollowWake.Core.Ecs;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Components;

/// <summary>Where a thing is. X east, Y north, Z up, in world units (one cell is one unit).</summary>
public sealed class Transform : IComponent
{
    public Vector3 Position;
    /// <summary>Facing on the ground plane, radians, 0 along +X.</summary>
    public float Yaw;
    private float _radius = 0.4f;
    private float _height = 1.8f;
    /// <summary>Geometry-dependent attribute inputs invalidate only when a dimension changes.</summary>
    public event Action? DimensionsChanged;
    public float Radius
    {
        get => _radius;
        set { if (_radius != value) { _radius = value; DimensionsChanged?.Invoke(); } }
    }
    public float Height
    {
        get => _height;
        set { if (_height != value) { _height = value; DimensionsChanged?.Invoke(); } }
    }

    public Vector2 Forward => new(MathF.Cos(Yaw), MathF.Sin(Yaw));

    public void Face(Vector3 target)
    {
        var dx = target.X - Position.X;
        var dy = target.Y - Position.Y;
        if (dx * dx + dy * dy > 1e-6f)
        {
            Yaw = MathF.Atan2(dy, dx);
        }
    }

    public float DistanceTo(Transform other) => Vector3.Distance(Position, other.Position);

    public float GroundDistanceTo(Vector3 p)
    {
        var dx = p.X - Position.X;
        var dy = p.Y - Position.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}

/// <summary>Movement state and intent: desired direction, a path to follow, a jump request, gravity.</summary>
public sealed class Motion : IComponent
{
    public Vector3 Velocity;
    public bool Grounded;
    /// <summary>Desired horizontal direction this tick, unit length or zero.</summary>
    public Vector2 MoveDir;
    public Vector3? MoveTarget;
    public List<Vector3> Path = new();
    public bool JumpRequested;
    public double LastMoveTime = double.NegativeInfinity;
    public double StationarySince;
    public bool Flying;
    public float StopDistance = 0.3f;

    /// <summary>Entities this cannot pass through are resolved by the movement system; a simple no-clip flag for tools.</summary>
    public bool NoClip;

    /// <summary>A shove on top of the body's own movement (knockback, pull, buffet), decaying as the displacement system integrates it.</summary>
    public Vector3 Impulse;

    /// <summary>Who gave the current <see cref="Impulse"/> and what happens if it slams the body into something; null when nothing rides it.</summary>
    public ShoveRider? Shove;

    /// <summary>A movement over time that overrides walking while it lasts (a dash, a leap); null when none.</summary>
    public Displacement? Displacement;

    public void Stop()
    {
        MoveDir = Vector2.Zero;
        MoveTarget = null;
        Path.Clear();
    }

    public bool WantsToMove => MoveDir.LengthSquared() > 0.0001f || MoveTarget is not null || Path.Count > 0;
}

/// <summary>A body carried along a path over time instead of walking: where it goes, how fast, and until when.</summary>
public sealed class Displacement
{
    /// <summary>What kind of displacement this is (dash, leap, knockback ...), for the systems and the client.</summary>
    public string Kind = "";
    public Vector3 From;
    public Vector3 To;
    public double StartedAt;
    public double EndsAt;
    /// <summary>The effect context the displacement's hits and landing run in, when it has any.</summary>
    public Effects.EffectContext? Context;
    /// <summary>Bodies the way passes within this many units of are struck by <see cref="OnContact"/>, each once.</summary>
    public float Width;
    /// <summary>Who <see cref="OnContact"/> strikes: <c>enemy</c> by default, as delivery filters read.</summary>
    public string Filter = "enemy";
    /// <summary>Peak height above the straight line between the ends, for leaps.</summary>
    public float Arc;
    /// <summary>Passes through bodies and the cells placed things block; walls still stop it.</summary>
    public bool Phase;
    public Effects.CompiledEffect[] OnContact = Array.Empty<Effects.CompiledEffect>();
    public Effects.CompiledEffect[] OnTravel = Array.Empty<Effects.CompiledEffect>();
    public Effects.CompiledEffect[] OnLand = Array.Empty<Effects.CompiledEffect>();
    public Effects.CompiledEffect[] OnEnd = Array.Empty<Effects.CompiledEffect>();
    /// <summary>Units between <see cref="OnTravel"/> beats.</summary>
    public float TravelEvery = 2f;
    public float Traveled;
    public float NextTravelAt;
    /// <summary>Flags the body wears while carried (dashing, airborne, untargetable), taken off on arrival.</summary>
    public string[] Flags = Array.Empty<string>();
    public Effects.HitLedger Struck = new();
}

/// <summary>Who shoved a body, and what happens when the shove slams it into something it cannot enter.</summary>
public sealed class ShoveRider
{
    public Entity Source = Entity.None;
    public Effects.EffectContext? Context;
    public Effects.CompiledEffect[] OnCollide = Array.Empty<Effects.CompiledEffect>();
}

/// <summary>Marks a body the displacement system is moving: an impulse is decaying on it or a displacement carries it.</summary>
public sealed class Displaced : IComponent
{
}

/// <summary>Points a body marked to come back to later, by key: what <c>mark</c> stores and <c>move</c> kind <c>recall</c> reads.</summary>
public sealed class Marks : IComponent
{
    public Dictionary<string, Vector3> Points { get; } = new(StringComparer.Ordinal);
}

/// <summary>Who or what this is: name, template, faction, tags, ownership.</summary>
public sealed class Identity : IComponent
{
    public string Name = "";
    public string? Pattern;
    public TagSet Tags = TagSet.Empty;
    public string? Faction;
    public string? Rarity;
    public bool IsPlayer;
    public string? CharacterId;
    public string? AccountId;
    /// <summary>Owner for summons, projectiles, fields and drops.</summary>
    public Entity Owner = Entity.None;
    public string? Look;

    public override string ToString() => Name.Length > 0 ? Name : Pattern ?? "entity";
}

/// <summary>Despawn at a time.</summary>
public sealed class Lifetime : IComponent
{
    public double ExpiresAt;
}

/// <summary>Marks a dead body awaiting loot, respawn or removal.</summary>
public sealed class Dead : IComponent
{
    public double DiedAt;
    public Entity Killer = Entity.None;
    public bool Looted;
}

/// <summary>Something players can use: a quest giver, a crafting station, a portal, a fount, a chest.</summary>
public sealed class Interactable : IComponent
{
    public string Kind = "";
    public string? Ref;
    public TagSet Tags = TagSet.Empty;
    public float Range = 2.5f;
    public Dictionary<string, string> Data = new();
}

/// <summary>A dropped item on the ground.</summary>
public sealed class LootDrop : IComponent
{
    public required Items.ItemInstance Item;
    public Entity Owner = Entity.None;
    public double OwnerUntil;
    public double ExpiresAt;
}

/// <summary>A spawn point that keeps its pack alive and respawns it.</summary>
public sealed class Spawner : IComponent
{
    public int? Level;
    public required string Pattern;
    public int PackSize = 1;
    public double RespawnSeconds = 120;
    public double RespawnAt = double.NegativeInfinity;
    public List<Entity> Alive = new();
    public string? Group;
    public bool RollRarity = true;
    public float Scatter = 2.5f;
    public bool Active = true;
}

/// <summary>Static scenery such as flora. Blocks movement when tagged blocking.</summary>
public sealed class Prop : IComponent
{
    public float Scale = 1f;
    public bool Blocking;
    public string? Resource;
}
