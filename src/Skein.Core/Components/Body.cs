using System.Numerics;
using Skein.Core.Ecs;
using Skein.Core.Tags;

namespace Skein.Core.Components;

/// <summary>Where a thing is. X east, Y north, Z up, in world units (one cell is one unit).</summary>
public sealed class Transform : IComponent
{
    public Vector3 Position;
    /// <summary>Facing on the ground plane, radians, 0 along +X.</summary>
    public float Yaw;
    public float Radius = 0.4f;
    public float Height = 1.8f;

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

    public void Stop()
    {
        MoveDir = Vector2.Zero;
        MoveTarget = null;
        Path.Clear();
    }

    public bool WantsToMove => MoveDir.LengthSquared() > 0.0001f || MoveTarget is not null || Path.Count > 0;
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
