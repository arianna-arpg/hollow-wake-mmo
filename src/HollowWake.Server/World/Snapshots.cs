using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Items;
using HollowWake.Core.Progression;
using HollowWake.Core.Quests;
using HollowWake.Core.Sim;
using HollowWake.Core.Territory;
using HollowWake.Core.Weaves;
using HollowWake.Protocol;
using HollowWake.Server.Config;
using EcsWorld = HollowWake.Core.Ecs.World;

namespace HollowWake.Server.World;

/// <summary>
/// Turns simulation state into wire state. Nothing here mutates the simulation. <see cref="Capture(Simulation, Entity)"/>
/// and <see cref="Self(Simulation, Entity)"/> build what goes on the wire; <see cref="Fingerprint"/> and
/// <see cref="SelfFingerprint"/> hash the same inputs, quantized the same way, without allocating, so the
/// <see cref="SnapshotBuilder"/> can tell whether anything a client would see has changed before it
/// captures or serializes anything.
/// </summary>
public static class Snapshots
{
    // ------------------------------------------------------------ static or dynamic

    private static readonly object Gate = new();

    private static Func<EcsWorld, Entity, bool>[] _dynamicWhen =
    {
        static (w, e) => w.Has<Stats>(e),
        static (w, e) => w.Has<Motion>(e),
        static (w, e) => w.Has<Projectile>(e),
        static (w, e) => w.Has<Field>(e),
        static (w, e) => w.Has<Tether>(e),
    };

    /// <summary>
    /// Registers a component that makes an entity dynamic: something that moves it or changes what a
    /// client sees of it. Stats, motion, projectiles, fields and tethers are registered by default.
    /// </summary>
    public static void MarkDynamic<T>() where T : class, IComponent
    {
        lock (Gate)
        {
            _dynamicWhen = [.. _dynamicWhen, static (w, e) => w.Has<T>(e)];
        }
    }

    /// <summary>
    /// Static entities carry none of the dynamic components: flora, props, markers, founts and drops.
    /// They are sent once as a spawn with <see cref="EntityState.Static"/> set and never in updates.
    /// </summary>
    public static bool IsStatic(Simulation sim, Entity e)
    {
        var checks = Volatile.Read(ref _dynamicWhen);
        for (var i = 0; i < checks.Length; i++)
        {
            if (checks[i](sim.World, e))
            {
                return false;
            }
        }
        return true;
    }

    // ------------------------------------------------------------ quantization (shared by capture and fingerprints)

    /// <summary>Default cast progress steps per cast; see <see cref="SnapshotSettings.CastSteps"/>.</summary>
    public const int DefaultCastSteps = SnapshotSettings.DefaultCastSteps;

    /// <summary>Default resource step as a fraction of the maximum; see <see cref="SnapshotSettings.ResourceStep"/>.</summary>
    public const double DefaultResourceStep = SnapshotSettings.DefaultResourceStep;

    /// <summary>Hundredths: centimetres for positions and sizes, hundredths of a radian for yaw, cm/s for velocity.</summary>
    private static float Hundredths(float v) => MathF.Round(v * 100f) / 100f + 0f;

    private static long QHundredths(float v) => (long)MathF.Round(v * 100f);

    private static float Tenths(double v) => (float)(Math.Round(v * 10) / 10) + 0f;

    private static long QTenths(double v) => (long)Math.Round(v * 10);

    /// <summary>
    /// A resource in steps of <paramref name="step"/> of its maximum, no finer than a tenth of a point and no
    /// coarser than <paramref name="step"/> times a hundred points (one point at the default step).
    /// </summary>
    private static long QResource(double current, double max, double step) => (long)Math.Round(current / Math.Clamp(max * step, 0.1, Math.Max(0.1, step * 100)));

    private static float CastProgress(Simulation sim, CastState? casting) =>
        casting is null || casting.Duration <= 0 ? 0f : (float)Math.Clamp((sim.Clock.Time - casting.StartedAt) / casting.Duration, 0, 1);

    // ------------------------------------------------------------ deliveries as drawn

    private static readonly Shape Bar = Shapes.Get("bar");

    /// <summary>
    /// What a body is drawn as beyond its transform. A projectile or a field: its registered shape and size, facing its
    /// heading or facing, a flight turned by its spin. A tether: a bar laid along its line (its transform sits at the
    /// midpoint with half the length as radius and its yaw along the line; a bar lies across its facing, so the facing
    /// is a quarter turn from the yaw). A telegraph mark: a circle of its radius. A flight's velocity, which it keeps
    /// outside <see cref="Motion"/>. And for a body with a lifetime (a telegraph mark, a timed summon), when it ends;
    /// NaN without one.
    /// </summary>
    private readonly record struct Drawn(Shape? Shape, ShapeSize Size, float Facing, float? Spin, bool Flies, Vector3 Velocity, double Until);

    private static Drawn DrawnOf(EcsWorld world, Entity e, Transform? t)
    {
        Shape? shape = null;
        var size = default(ShapeSize);
        var facing = t?.Yaw ?? 0f;
        float? spin = null;
        var flies = false;
        var velocity = Vector3.Zero;
        if (world.TryGet<Projectile>(e, out var p))
        {
            flies = true;
            velocity = p.Velocity;
            facing = p.Heading;
            shape = p.Spec.Shape;
            // A circle's size is its collision radius; every other shape keeps its own.
            size = ReferenceEquals(shape, Shapes.Circle) ? p.Size with { Radius = p.Radius } : p.Size;
            spin = p.SpinAngle;
        }
        else if (world.TryGet<Field>(e, out var f))
        {
            facing = f.Facing;
            shape = f.Spec.Shape;
            size = f.Size;
        }
        else if (t is not null && world.TryGet<Tether>(e, out var tether))
        {
            shape = Bar;
            size = new ShapeSize(t.Radius, 0f, tether.Width, 0f);
            facing = t.Yaw + MathF.PI / 2f;
        }
        else if (t is not null && world.TryGet<Identity>(e, out var id) && id.Tags.Has("telegraph"))
        {
            shape = Shapes.Circle;
            size = new ShapeSize(t.Radius, 0f, 0f, 0f);
        }
        if (shape is not null)
        {
            size = Resolved(shape, size);
        }
        var until = world.TryGet<Lifetime>(e, out var life) ? life.ExpiresAt : double.NaN;
        return new Drawn(shape, size, facing, spin, flies, velocity, until);
    }

    /// <summary>
    /// A size with the shape's defaults where it has none (Effects/Shapes.cs), so a client never needs that table: a
    /// sector's or crescent's angle, a crescent's hollow, an arc's or wave's angle, a width for every shape with one (a
    /// band as wide as it is long). Sizes that went through delivery content are resolved already and pass unchanged.
    /// </summary>
    private static ShapeSize Resolved(Shape shape, in ShapeSize size)
    {
        var unresolved = shape.HasAngle && size.Angle <= 0;
        var angle = shape.HasAngle ? (size.Angle > 0 ? size.Angle : shape.DefaultAngle) : 0f;
        // The weave's area width is not known here; the radius stands in for it, keeping a line's proportions.
        var width = shape.HasWidth ? (size.Width > 0 ? size.Width : shape.DefaultWidth(size.Radius, size.Radius)) : 0f;
        var inner = size.Inner > 0 ? size.Inner : unresolved ? shape.DefaultInner : 0f;
        return new ShapeSize(size.Radius, angle, width, inner);
    }

    /// <summary>
    /// The size as the wire carries it (see <see cref="ShapeSizeInfo"/>): radius, width and inner in world units (the
    /// inner fraction times the radius), the angle in radians, and the curvature of a bowed shape in radii.
    /// </summary>
    private static ShapeSizeInfo SizeInfo(Shape shape, in ShapeSize size) => new()
    {
        Radius = Hundredths(size.Radius),
        Angle = shape.HasAngle ? Hundredths(size.Angle * MathF.PI / 180f) : null,
        Width = shape.HasWidth ? Hundredths(size.Width) : null,
        Inner = size.Inner > 0 ? Hundredths(size.Inner * size.Radius) : null,
        Curvature = shape.Curvature > 0 ? Hundredths(shape.Curvature) : null,
    };

    private static void AddDrawn(ref FingerprintHash h, in Drawn drawn)
    {
        if (drawn.Shape is { } shape)
        {
            h.Add(shape.Name);
            h.Add(QHundredths(drawn.Size.Radius));
            h.Add(QHundredths(drawn.Size.Angle * MathF.PI / 180f));
            h.Add(QHundredths(drawn.Size.Width));
            h.Add(QHundredths(drawn.Size.Inner * drawn.Size.Radius));
            h.Add(QHundredths(drawn.Facing));
            h.Add(drawn.Spin is { } spin ? QHundredths(spin) : long.MinValue);
        }
        if (drawn.Flies)
        {
            h.Add(QHundredths(drawn.Velocity.X));
            h.Add(QHundredths(drawn.Velocity.Y));
            h.Add(QHundredths(drawn.Velocity.Z));
        }
        if (!double.IsNaN(drawn.Until))
        {
            h.Add(Math.Round(drawn.Until, 2));
        }
    }

    // ------------------------------------------------------------ entity state

    public static EntityState Capture(Simulation sim, Entity e) => Capture(sim, e, IsStatic(sim, e));

    /// <summary>
    /// The wire state of one entity, positions to the centimetre and resources to a tenth. Allocates the
    /// state and its collections only; flags and statuses are skipped when an entity has none.
    /// </summary>
    public static EntityState Capture(Simulation sim, Entity e, bool isStatic, bool hitBodies = true)
    {
        var world = sim.World;
        var t = world.Get<Transform>(e);
        var id = world.Get<Identity>(e);
        var stats = world.Get<Stats>(e);
        var motion = world.Get<Motion>(e);
        var statuses = world.Get<Statuses>(e);
        var casting = world.Get<WeaveBook>(e)?.Casting;
        var interactable = world.Get<Interactable>(e);
        var drawn = DrawnOf(world, e, t);
        var state = new EntityState
        {
            Key = e.Key,
            Name = id?.Name ?? "",
            Pattern = id?.Pattern,
            Look = id?.Look,
            X = t is null ? 0 : Hundredths(t.Position.X),
            Y = t is null ? 0 : Hundredths(t.Position.Y),
            Z = t is null ? 0 : Hundredths(t.Position.Z),
            Yaw = t is null ? 0 : Hundredths(t.Yaw),
            Radius = t is null ? 0.4f : Hundredths(t.Radius),
            Height = t is null ? 1.8f : Hundredths(t.Height),
            HitBody = hitBodies ? world.Get<HollowWake.Core.Combat.HitBody>(e)?.Definition.Id : null,
            Faction = HollowWake.Core.Combat.Relations.FactionOf(sim, e),
            Rarity = id?.Rarity,
            IsPlayer = id?.IsPlayer ?? false,
            Level = stats?.Level ?? 0,
            Interactable = interactable?.Kind,
            Dead = world.Has<Dead>(e),
            Casting = casting?.Slot.Compiled?.Name ?? casting?.Slot.Key,
            CastProgress = Hundredths(CastProgress(sim, casting)),
            Grounded = motion?.Grounded ?? true,
            VX = motion is not null ? Hundredths(motion.Velocity.X) : drawn.Flies ? Hundredths(drawn.Velocity.X) : 0,
            VY = motion is not null ? Hundredths(motion.Velocity.Y) : drawn.Flies ? Hundredths(drawn.Velocity.Y) : 0,
            VZ = motion is not null ? Hundredths(motion.Velocity.Z) : drawn.Flies ? Hundredths(drawn.Velocity.Z) : 0,
            Static = isStatic,
            Shape = drawn.Shape?.Name,
            ShapeSize = drawn.Shape is { } shape ? SizeInfo(shape, drawn.Size) : null,
            Heading = drawn.Shape is not null ? Hundredths(drawn.Facing) : null,
            Spin = drawn.Spin is { } spin ? Hundredths(spin) : null,
            Until = double.IsNaN(drawn.Until) ? null : Math.Round(drawn.Until, 2),
        };
        if (stats is not null)
        {
            if (stats.Flags.Count > 0)
            {
                var flags = state.Flags;
                foreach (var f in stats.Flags)
                {
                    if (!f.StartsWith("status:", StringComparison.Ordinal))
                    {
                        flags.Add(f);
                    }
                }
            }
            foreach (var (res, cur) in stats.Resources)
            {
                state.Resources[res] = Tenths(cur);
                state.ResourceMax[res] = Tenths(stats.ResourceMax(res));
            }
        }
        if (statuses is not null && statuses.Active.Count > 0)
        {
            var list = state.Statuses;
            foreach (var s in statuses.Active)
            {
                if (!list.Contains(s.Id))
                {
                    list.Add(s.Id);
                }
            }
        }
        return state;
    }

    /// <summary>
    /// A hash of everything <see cref="Capture(Simulation, Entity, bool)"/> puts on the wire, at the same
    /// quantization, except that resources count in steps of <paramref name="resourceStep"/> of their
    /// maximum and cast progress in <paramref name="castSteps"/> steps. Equal fingerprints mean a client
    /// would see nothing new. Allocation-free; in-process only, never persisted or sent.
    /// </summary>
    public static ulong Fingerprint(Simulation sim, Entity e, double resourceStep = DefaultResourceStep, int castSteps = DefaultCastSteps)
    {
        var world = sim.World;
        var h = new FingerprintHash(1);
        if (world.TryGet<Transform>(e, out var t))
        {
            h.Add(QHundredths(t.Position.X));
            h.Add(QHundredths(t.Position.Y));
            h.Add(QHundredths(t.Position.Z));
            h.Add(QHundredths(t.Yaw));
            h.Add(QHundredths(t.Radius));
            h.Add(QHundredths(t.Height));
        }
        if (world.TryGet<Identity>(e, out var id))
        {
            h.Add(id.Name);
            h.Add(id.Pattern);
            h.Add(id.Look);
            h.Add(HollowWake.Core.Combat.Relations.FactionOf(sim, e));
            h.Add(id.Rarity);
            h.Add(id.IsPlayer);
        }
        h.Add(world.TryGet<Interactable>(e, out var interactable) ? interactable.Kind : null);
        h.Add(world.Has<Dead>(e));
        h.Add(world.Get<HollowWake.Core.Combat.HitBody>(e)?.Definition.Id);
        if (world.TryGet<Stats>(e, out var stats))
        {
            h.Add(stats.Level);
            foreach (var (res, cur) in stats.Resources)
            {
                var max = stats.ResourceMax(res);
                h.Add(res);
                h.Add(QResource(cur, max, resourceStep));
                h.Add(QTenths(max));
            }
            if (stats.Flags.Count > 0)
            {
                ulong set = 0;
                var count = 0;
                foreach (var f in stats.Flags)
                {
                    if (!f.StartsWith("status:", StringComparison.Ordinal))
                    {
                        set += FingerprintHash.Mix((ulong)(uint)f.GetHashCode());
                        count++;
                    }
                }
                h.Add(set);
                h.Add(count);
            }
        }
        if (world.TryGet<Statuses>(e, out var statuses) && statuses.Active.Count > 0)
        {
            ulong set = 0;
            foreach (var s in statuses.Active)
            {
                set += FingerprintHash.Mix((ulong)(uint)s.Id.GetHashCode());
            }
            h.Add(set);
        }
        if (world.TryGet<WeaveBook>(e, out var book) && book.Casting is { } casting)
        {
            h.Add(casting.Slot.Compiled?.Name ?? casting.Slot.Spec.Preset ?? casting.Slot.Spec.Form);
            h.Add(casting.StartedAt);
            h.Add((long)(CastProgress(sim, casting) * Math.Max(1, castSteps)));
        }
        if (world.TryGet<Motion>(e, out var motion))
        {
            h.Add(motion.Grounded);
            h.Add(QHundredths(motion.Velocity.X));
            h.Add(QHundredths(motion.Velocity.Y));
            h.Add(QHundredths(motion.Velocity.Z));
        }
        var body = world.Get<Transform>(e);
        AddDrawn(ref h, DrawnOf(world, e, body));
        return h.Value;
    }

    /// <summary>
    /// The fields of <paramref name="next"/> that differ from <paramref name="last"/>, or null when a delta
    /// cannot express the change (the resource set or the player flag changed) and the full state must go
    /// instead. <paramref name="empty"/> is set when nothing a client sees differs.
    /// </summary>
    public static EntityDelta? Delta(EntityState last, EntityState next, out bool empty)
    {
        empty = false;
        if (last.Key != next.Key || last.IsPlayer != next.IsPlayer || last.Static != next.Static
            || !SameKeys(last.Resources, next.Resources) || !SameKeys(last.ResourceMax, next.ResourceMax))
        {
            return null;
        }
        var changes = 0;
        float? F(float a, float b)
        {
            if (a == b)
            {
                return null;
            }
            changes++;
            return b;
        }
        string? S(string? a, string? b)
        {
            if (a == b)
            {
                return null;
            }
            changes++;
            return b ?? "";
        }
        bool? B(bool a, bool b)
        {
            if (a == b)
            {
                return null;
            }
            changes++;
            return b;
        }
        // A shaped body's heading or spin can be set or changed by a delta, never cleared, and a lifetime's end never
        // changes by one: those need the full state.
        if ((last.Heading is not null && next.Heading is null) || (last.Spin is not null && next.Spin is null) || (last.ShapeSize is not null && next.ShapeSize is null && next.Shape is not null)
            || last.Until != next.Until)
        {
            return null;
        }
        float? NF(float? a, float? b)
        {
            if (a == b)
            {
                return null;
            }
            changes++;
            return b;
        }
        ShapeSizeInfo? size = null;
        if (!Equals(last.ShapeSize, next.ShapeSize) && next.ShapeSize is not null)
        {
            size = next.ShapeSize;
            changes++;
        }
        var resources = ChangedEntries(last.Resources, next.Resources);
        var maxes = ChangedEntries(last.ResourceMax, next.ResourceMax);
        var flags = SameSet(last.Flags, next.Flags) ? null : next.Flags;
        var statuses = SameSequence(last.Statuses, next.Statuses) ? null : next.Statuses;
        changes += (resources is null ? 0 : 1) + (maxes is null ? 0 : 1) + (flags is null ? 0 : 1) + (statuses is null ? 0 : 1);
        int? level = null;
        if (last.Level != next.Level)
        {
            level = next.Level;
            changes++;
        }
        var delta = new EntityDelta
        {
            Key = next.Key,
            Name = S(last.Name, next.Name),
            Pattern = S(last.Pattern, next.Pattern),
            Look = S(last.Look, next.Look),
            X = F(last.X, next.X),
            Y = F(last.Y, next.Y),
            Z = F(last.Z, next.Z),
            Yaw = F(last.Yaw, next.Yaw),
            Radius = F(last.Radius, next.Radius),
            Height = F(last.Height, next.Height),
            HitBody = S(last.HitBody, next.HitBody),
            Faction = S(last.Faction, next.Faction),
            Rarity = S(last.Rarity, next.Rarity),
            Level = level,
            Resources = resources,
            ResourceMax = maxes,
            Flags = flags,
            Statuses = statuses,
            Interactable = S(last.Interactable, next.Interactable),
            Dead = B(last.Dead, next.Dead),
            Casting = S(last.Casting, next.Casting),
            CastProgress = F(last.CastProgress, next.CastProgress),
            Grounded = B(last.Grounded, next.Grounded),
            VX = F(last.VX, next.VX),
            VY = F(last.VY, next.VY),
            VZ = F(last.VZ, next.VZ),
            Shape = S(last.Shape, next.Shape),
            ShapeSize = size,
            Heading = NF(last.Heading, next.Heading),
            Spin = NF(last.Spin, next.Spin),
        };
        empty = changes == 0;
        return delta;
    }

    private static bool SameKeys(Dictionary<string, float> a, Dictionary<string, float> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }
        foreach (var key in a.Keys)
        {
            if (!b.ContainsKey(key))
            {
                return false;
            }
        }
        return true;
    }

    private static Dictionary<string, float>? ChangedEntries(Dictionary<string, float> last, Dictionary<string, float> next)
    {
        Dictionary<string, float>? changed = null;
        foreach (var (key, value) in next)
        {
            if (!last.TryGetValue(key, out var before) || before != value)
            {
                (changed ??= new Dictionary<string, float>(StringComparer.Ordinal))[key] = value;
            }
        }
        return changed;
    }

    private static bool SameSet(List<string> a, List<string> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }
        foreach (var s in a)
        {
            if (!b.Contains(s))
            {
                return false;
            }
        }
        return true;
    }

    private static bool SameSequence(List<string> a, List<string> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }
        for (var i = 0; i < a.Count; i++)
        {
            if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    // ------------------------------------------------------------ items and quests

    public static ItemInfo Item(ItemResolver resolver, ItemInstance item)
    {
        var b = resolver.BaseOf(item);
        var local = resolver.LocalAttributes(item);
        var locals = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (attr, _) in local.BaseOverrides)
        {
            locals[attr] = Math.Round(ItemResolver.LocalValue(local, attr), 2);
        }
        foreach (var attr in local.ModifiedAttributes)
        {
            locals[attr] = Math.Round(ItemResolver.LocalValue(local, attr), 2);
        }
        var lines = new List<string>();
        foreach (var m in resolver.WearerModifiers(item))
        {
            lines.Add(m.ToString());
        }
        return new ItemInfo(item.Uid, item.Base, item.Name, item.Rarity, item.Count, item.Level, b.Slot, lines, locals);
    }

    public static QuestInfo Quest(Simulation sim, QuestProgress progress)
    {
        var def = progress.Generated ?? sim.Service<QuestService>().Find(sim, progress.QuestId);
        if (def is null)
        {
            return new QuestInfo(progress.QuestId, progress.QuestId, progress.State.ToString().ToLowerInvariant(), new List<string>(), new List<int>(), new List<int>(), null, 0);
        }
        var texts = new List<string>(def.Objectives.Count);
        var counts = new List<int>(def.Objectives.Count);
        var required = new List<int>(def.Objectives.Count);
        foreach (var o in def.Objectives)
        {
            texts.Add(o.Text);
            counts.Add(progress.Counts.GetValueOrDefault(o.Id));
            required.Add(o.Count);
        }
        return new QuestInfo(def.Id, def.Name, progress.State.ToString().ToLowerInvariant(), texts, counts, required, def.Giver, def.Level);
    }

    public static QuestInfo QuestOffer(Core.Definitions.QuestDef def) => new(def.Id, def.Name, "offered",
        def.Objectives.Select(o => o.Text).ToList(), def.Objectives.Select(_ => 0).ToList(), def.Objectives.Select(o => o.Count).ToList(), def.Giver, def.Level);

    // ------------------------------------------------------------ self state

    /// <summary>
    /// What a session keeps between self states so an unchanged attribute set is not folded again: the
    /// attribute values from the last build and the stats version they were read at.
    /// </summary>
    public sealed class SelfCache
    {
        internal Stats? Stats;
        internal int Version = -1;
        internal Dictionary<string, double>? Attributes;

        public void Clear()
        {
            Stats = null;
            Version = -1;
            Attributes = null;
        }
    }

    public static SelfStateMsg Self(Simulation sim, Entity e) => Self(sim, e, null);

    /// <summary>
    /// The owner's view of their character. With a cache, the attribute map is reused while the stats
    /// version is unchanged (attribute values are cached by the attribute set until it changes, so
    /// nothing is lost); the map is never mutated once built, so sharing it between messages is safe.
    /// </summary>
    public static SelfStateMsg Self(Simulation sim, Entity e, SelfCache? cache)
    {
        var stats = sim.World.Require<Stats>(e);
        var resolver = sim.Service<ItemResolver>();
        var runtime = sim.Service<WeaveRuntime>();
        var progression = sim.Service<ProgressionService>();
        var book = sim.World.Get<WeaveBook>(e);
        var inv = sim.World.Get<Inventory>(e);
        var eq = sim.World.Get<Equipment>(e);
        var log = sim.World.Get<QuestLog>(e);
        var player = sim.World.Get<Player>(e);
        var world = sim.Service<WorldState>();

        Dictionary<string, double> attributes;
        if (cache is not null && cache.Attributes is not null && ReferenceEquals(cache.Stats, stats) && cache.Version == stats.Attributes.Version)
        {
            attributes = cache.Attributes;
        }
        else
        {
            attributes = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var def in sim.Content.Attributes.All)
            {
                if (def.Hidden || def.Category is "weave")
                {
                    continue;
                }
                try
                {
                    attributes[def.Id] = Math.Round(stats.Get(def.Id), 3);
                }
                catch (Exception)
                {
                    attributes[def.Id] = 0;
                }
            }
            if (cache is not null)
            {
                cache.Stats = stats;
                cache.Version = stats.Attributes.Version;
                cache.Attributes = attributes;
            }
        }
        var weaves = new List<WeaveSlotInfo>(book?.Slots.Count ?? 0);
        if (book is not null)
        {
            for (var i = 0; i < book.Slots.Count; i++)
            {
                var slot = book.Slots[i];
                var key = slot.Key;
                var compiled = runtime.Resolve(sim, slot, out _);
                var readyIn = book.ReadyAt.TryGetValue(key, out var at) ? Math.Max(0, at - sim.Clock.Time) : 0;
                weaves.Add(new WeaveSlotInfo(i, key, compiled?.Name ?? key, compiled?.Look, readyIn,
                    compiled is null ? new Dictionary<string, double>() : runtime.Costs(compiled, stats),
                    compiled?.Targeting.ToString().ToLowerInvariant() ?? "enemy",
                    compiled?.Range(stats) ?? 0));
            }
        }
        var resources = new Dictionary<string, double>(stats.Resources.Count, StringComparer.Ordinal);
        var maxes = new Dictionary<string, double>(stats.Resources.Count, StringComparer.Ordinal);
        foreach (var (res, cur) in stats.Resources)
        {
            resources[res] = Math.Round(cur, 1);
            maxes[res] = Math.Round(stats.ResourceMax(res), 1);
        }
        var bag = new List<ItemInfo>();
        if (inv is not null)
        {
            foreach (var item in inv.Items)
            {
                bag.Add(Item(resolver, item));
            }
        }
        var equipment = new Dictionary<string, ItemInfo>(StringComparer.Ordinal);
        if (eq is not null)
        {
            foreach (var (slotName, item) in eq.BySlot)
            {
                equipment[slotName] = Item(resolver, item);
            }
        }
        var quests = new List<QuestInfo>();
        if (log is not null)
        {
            foreach (var q in log.Quests.Values)
            {
                quests.Add(Quest(sim, q));
            }
        }
        var holdings = new List<string>();
        if (player is not null)
        {
            foreach (var h in world.Holdings.Values)
            {
                if (h.OwnerCharacterId == player.CharacterId)
                {
                    holdings.Add(h.Id);
                }
            }
        }
        return new SelfStateMsg(
            e.Key,
            stats.Level,
            Math.Round(stats.Xp, 1),
            progression.XpToNext(sim, stats.Level),
            progression.AvailablePoints(sim, stats),
            new List<string>(stats.Passives),
            attributes,
            resources,
            maxes,
            weaves,
            bag,
            inv?.Capacity ?? 0,
            equipment,
            inv is null ? new Dictionary<string, double>() : new Dictionary<string, double>(inv.Currency, StringComparer.Ordinal),
            quests,
            log is null ? new Dictionary<string, double>() : new Dictionary<string, double>(log.Reputation, StringComparer.Ordinal),
            holdings);
    }

    /// <summary>
    /// A hash of what <see cref="Self(Simulation, Entity, SelfCache?)"/> reads: level, xp, passives, the
    /// attribute set's version, resources (in steps of <paramref name="resourceStep"/> of their maximum),
    /// weave slots and their cooldowns (counted down in steps of <paramref name="cooldownStep"/> seconds,
    /// so a client watching a cooldown still sees it tick), bag, equipment, currency, quests, reputation
    /// and holdings. Allocation-free.
    /// </summary>
    public static ulong SelfFingerprint(Simulation sim, Entity e, double cooldownStep, double resourceStep = DefaultResourceStep)
    {
        var world = sim.World;
        var h = new FingerprintHash(2);
        if (!world.TryGet<Stats>(e, out var stats))
        {
            return h.Value;
        }
        h.Add(stats.Level);
        h.Add(Math.Round(stats.Xp, 1));
        h.Add(stats.PassivePointsSpent);
        foreach (var p in stats.Passives)
        {
            h.Add(p);
        }
        h.Add(stats.Attributes.Version);
        h.Add((long)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(stats));
        foreach (var (res, cur) in stats.Resources)
        {
            var max = stats.ResourceMax(res);
            h.Add(res);
            h.Add(QResource(cur, max, resourceStep));
            h.Add(QTenths(max));
        }
        if (world.TryGet<WeaveBook>(e, out var book))
        {
            var now = sim.Clock.Time;
            var step = Math.Max(1e-3, cooldownStep);
            foreach (var slot in book.Slots)
            {
                h.Add((long)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(slot));
                h.Add(slot.Compiled is null ? 0L : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(slot.Compiled));
            }
            foreach (var (key, at) in book.ReadyAt)
            {
                h.Add(key);
                h.Add((long)Math.Ceiling(Math.Max(0, at - now) / step));
            }
        }
        if (world.TryGet<Inventory>(e, out var inv))
        {
            AddBag(ref h, inv);
        }
        if (world.TryGet<Equipment>(e, out var eq))
        {
            foreach (var (slotName, item) in eq.BySlot)
            {
                h.Add(slotName);
                AddItem(ref h, item);
            }
        }
        if (world.TryGet<QuestLog>(e, out var log))
        {
            foreach (var (questId, progress) in log.Quests)
            {
                h.Add(questId);
                h.Add((int)progress.State);
                foreach (var (objective, count) in progress.Counts)
                {
                    h.Add(objective);
                    h.Add(count);
                }
            }
            foreach (var (faction, standing) in log.Reputation)
            {
                h.Add(faction);
                h.Add(standing);
            }
        }
        if (world.TryGet<Player>(e, out var player) && sim.TryService<WorldState>(out var state))
        {
            foreach (var holding in state.Holdings.Values)
            {
                if (holding.OwnerCharacterId == player.CharacterId)
                {
                    h.Add(holding.Id);
                }
            }
        }
        return h.Value;
    }

    /// <summary>The bag's shape, each item where it lies, the items without a cell, and the wallet.</summary>
    internal static void AddBag(ref FingerprintHash h, Inventory inv)
    {
        h.Add(inv.Capacity);
        var placed = inv.Bag.Placed;
        for (var i = 0; i < placed.Count; i++)
        {
            var p = placed[i];
            h.Add(p.X);
            h.Add(p.Y);
            AddItem(ref h, p.Item);
        }
        foreach (var item in inv.Overflow)
        {
            h.Add(-1);
            AddItem(ref h, item);
        }
        foreach (var (currency, amount) in inv.Currency)
        {
            h.Add(currency);
            h.Add(amount);
        }
    }

    internal static void AddItem(ref FingerprintHash h, ItemInstance item)
    {
        h.Add(item.Uid);
        h.Add(item.Count);
        h.Add(item.Name);
        h.Add(item.Rarity);
        h.Add(item.Quality);
        h.Add(item.Affixes.Count);
        h.Add(item.Locked);
        h.Add(item.Payload?.Level ?? 0);
        if (item.Sockets is { } sockets)
        {
            h.Add(sockets.Count);
            foreach (var stone in sockets)
            {
                h.Add(stone?.Uid ?? 0);
            }
        }
    }

    // ------------------------------------------------------------ chunks

    public static ChunkDataMsg Chunk(Core.World.ChunkData c)
    {
        var layers = c.Layers.SelectMany(kv => kv.Value.Select(s => new LayerInfo(kv.Key, s.Height, s.Walkable, string.Join(" ", s.Tags)))).ToList();
        return new ChunkDataMsg(c.Coord.X, c.Coord.Y, c.Size, c.Height, c.Flags.Select(f => (byte)f).ToArray(), c.TagTable.Select(t => string.Join(" ", t)).ToList(), c.SurfaceTag, layers, c.GenVersion,
            float.IsFinite(c.WaterLevel) ? c.WaterLevel : null);
    }
}

/// <summary>
/// A small allocation-free 64-bit hash (xxHash64 rounds) for snapshot fingerprints. Strings hash by
/// content with the runtime's per-process string hash, so values are only comparable within a process.
/// </summary>
internal struct FingerprintHash
{
    private const ulong P1 = 11400714785074694791UL;
    private const ulong P2 = 14029467366897019727UL;
    private const ulong P3 = 1609587929392839161UL;
    private const ulong P4 = 9650029242287828579UL;
    private const ulong P5 = 2870177450012600261UL;

    private ulong _acc;

    public FingerprintHash(ulong seed)
    {
        _acc = seed + P5;
    }

    public void Add(ulong value)
    {
        var k = BitOperations.RotateLeft(value * P2, 31) * P1;
        _acc ^= k;
        _acc = BitOperations.RotateLeft(_acc, 27) * P1 + P4;
    }

    public void Add(long value) => Add((ulong)value);

    public void Add(int value) => Add((ulong)(uint)value);

    public void Add(bool value) => Add(value ? 0x9E3779B97F4A7C15UL : 0x632BE59BD9B4E019UL);

    public void Add(double value) => Add((ulong)BitConverter.DoubleToInt64Bits(value));

    public void Add(string? value) => Add(value is null ? 0x5BD1E9955BD1E995UL : (ulong)(uint)value.GetHashCode() | ((ulong)(uint)value.Length << 32));

    public ulong Value => Mix(_acc);

    /// <summary>The xxHash64 avalanche; also used to combine unordered sets by summing mixed members.</summary>
    public static ulong Mix(ulong h)
    {
        h ^= h >> 33;
        h *= P2;
        h ^= h >> 29;
        h *= P3;
        h ^= h >> 32;
        return h;
    }
}
