using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Effects;

/// <summary>The built-in effect vocabulary. Each handler compiles its parameters once and executes cheaply.</summary>
public static class BuiltinEffects
{
    public static void RegisterAll(EffectRegistry r)
    {
        r.Register(new DamageHandler());
        r.Register(new HealHandler());
        r.Register(new ResourceHandler());
        r.Register(new ApplyStatusHandler());
        r.Register(new RemoveStatusHandler());
        r.Register(new ModifyHandler());
        r.Register(new AreaHandler());
        r.Register(new ProjectileHandler());
        r.Register(new ChainHandler());
        r.Register(new SummonHandler());
        r.Register(new SpawnHandler());
        r.Register(new FieldHandler());
        r.Register(new MoveHandler());
        r.Register(new ConditionHandler());
        r.Register(new RandomHandler());
        r.Register(new RepeatHandler());
        r.Register(new SequenceHandler());
        r.Register(new EventHandler());
        r.Register(new InterruptHandler());
        r.Register(new TauntHandler());
        r.Register(new GrantItemHandler());
        r.Register(new GrantXpHandler());
        r.Register(new LogHandler());
    }

    // ------------------------------------------------------------ helpers

    private static IEnumerable<Entity> Filter(EffectContext ctx, IEnumerable<Entity> candidates, string filter)
    {
        foreach (var e in candidates)
        {
            if (!ctx.Sim.World.IsAlive(e) || ctx.Sim.World.Has<Dead>(e))
            {
                continue;
            }
            if (!ctx.Sim.World.Has<Stats>(e))
            {
                continue;
            }
            switch (filter)
            {
                case "enemy":
                    if (Relations.IsHostile(ctx.Sim, ctx.Origin, e))
                    {
                        yield return e;
                    }
                    break;
                case "ally":
                    if (!Relations.IsHostile(ctx.Sim, ctx.Origin, e) && e != ctx.Origin)
                    {
                        yield return e;
                    }
                    break;
                case "ally_or_self":
                    if (!Relations.IsHostile(ctx.Sim, ctx.Origin, e))
                    {
                        yield return e;
                    }
                    break;
                case "self":
                    if (e == ctx.Source)
                    {
                        yield return e;
                    }
                    break;
                case "not_self":
                    if (e != ctx.Source)
                    {
                        yield return e;
                    }
                    break;
                default:
                    yield return e;
                    break;
            }
        }
    }

    private static Vector3 Center(EffectContext ctx, string centerMode)
    {
        return centerMode switch
        {
            "source" => ctx.SourceTransform?.Position ?? ctx.Point,
            "point" => ctx.Point,
            _ => ctx.Focus,
        };
    }

    // ------------------------------------------------------------ damage / heal / resource

    private sealed class DamageHandler : IEffectHandler
    {
        public string Type => "damage";

        public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
        {
            Type = Type,
            Amount = ctx.ExprOf(p["amount"], Expr.Constant(10))!,
            Threads = ctx.ResolveThreads(p["threads"]),
            Tags = ctx.TagsOf(p["tags"]),
            CanCrit = ctx.BoolOf(p["can_crit"], true),
            Effectiveness = ctx.ExprOf(p["effectiveness"]),
        };

        private sealed class Effect : CompiledEffect
        {
            public required Expr Amount;
            public required List<string> Threads;
            public TagSet Tags = TagSet.Empty;
            public bool CanCrit = true;
            public Expr? Effectiveness;

            public override void Execute(EffectContext ctx)
            {
                if (ctx.Target.IsNone)
                {
                    return;
                }
                var amount = ctx.Eval(Amount) * ctx.Potency;
                if (Effectiveness is not null)
                {
                    amount *= ctx.Eval(Effectiveness, 1);
                }
                var threads = Threads.Count > 0 ? Threads : new List<string> { ctx.Sim.Content.Rules.Text("combat.default_thread", "stone") };
                var packet = DamagePipeline.Build(ctx, amount, threads, Tags, CanCrit);
                var result = DamagePipeline.Apply(ctx, packet);
                ctx.Vars["last_damage"] = result.Total;
                ctx.Vars["amount"] = result.Total;
            }
        }
    }

    private sealed class HealHandler : IEffectHandler
    {
        public string Type => "heal";

        public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
        {
            Type = Type,
            Amount = ctx.ExprOf(p["amount"], Expr.Constant(10))!,
            Resource = ctx.StringOf(p["resource"]) ?? "life",
            Tags = ctx.TagsOf(p["tags"]),
        };

        private sealed class Effect : CompiledEffect
        {
            public required Expr Amount;
            public string Resource = "life";
            public TagSet Tags = TagSet.Empty;

            public override void Execute(EffectContext ctx)
            {
                var target = ctx.Target.IsNone ? ctx.Source : ctx.Target;
                if (ctx.Sim.World.Get<Stats>(target) is not { } stats || ctx.Sim.World.Has<Dead>(target))
                {
                    return;
                }
                var amount = ctx.Eval(Amount) * ctx.Potency;
                if (ctx.SourceStats is { } src)
                {
                    amount = src.Attributes.Apply("healing", amount, src, ctx.Tags.Union(Tags), ctx.Weave?.Modifiers);
                }
                var healed = stats.AddResource(Resource, amount);
                if (healed > 0)
                {
                    stats.Stamp("healed", ctx.Sim.Clock.Time);
                    ctx.Sim.Events.Publish(new Healed(ctx.Source, target, healed, Resource) { Tick = ctx.Sim.Clock.Tick });
                }
            }
        }
    }

    private sealed class ResourceHandler : IEffectHandler
    {
        public string Type => "resource";

        public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
        {
            Type = Type,
            Amount = ctx.ExprOf(p["amount"], Expr.Constant(10))!,
            Resource = ctx.StringOf(p["resource"]) ?? "mana",
        };

        private sealed class Effect : CompiledEffect
        {
            public required Expr Amount;
            public string Resource = "mana";

            public override void Execute(EffectContext ctx)
            {
                var target = ctx.Target.IsNone ? ctx.Source : ctx.Target;
                ctx.Sim.World.Get<Stats>(target)?.AddResource(Resource, ctx.Eval(Amount) * ctx.Potency);
            }
        }
    }

    // ------------------------------------------------------------ statuses & modifiers

    private sealed class ApplyStatusHandler : IEffectHandler
    {
        public string Type => "apply_status";

        public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
        {
            Type = Type,
            Status = ctx.StringOf(p["status"]) ?? "$thread_status",
            Duration = ctx.ExprOf(p["duration"]),
            Chance = ctx.ExprOf(p["chance"], Expr.Constant(1))!,
            Stacks = ctx.ExprOf(p["stacks"], Expr.Constant(1))!,
            Magnitude = ctx.ExprOf(p["magnitude"]),
            Threads = ctx.ResolveThreads(p["threads"]),
            OnSelf = ctx.BoolOf(p["self"], false),
        };

        private sealed class Effect : CompiledEffect
        {
            public required string Status;
            public Expr? Duration;
            public required Expr Chance;
            public required Expr Stacks;
            public Expr? Magnitude;
            public List<string> Threads = new();
            public bool OnSelf;

            /// <summary>Damaging statuses take their magnitude from the damage just dealt through the ailment rules.</summary>
            private static double DefaultMagnitude(EffectContext ctx, string statusId)
            {
                var amount = ctx.Vars.GetValueOrDefault("last_damage", ctx.Vars.GetValueOrDefault("amount", 0));
                var scope = new ScopeOverlay(ctx.Scope).Set("amount", amount);
                return ctx.Sim.Content.Rules.Eval("combat.ailment_magnitude." + statusId, "1", scope);
            }

            public override void Execute(EffectContext ctx)
            {
                var target = OnSelf || ctx.Target.IsNone ? ctx.Source : ctx.Target;
                var statusId = Status;
                if (statusId == "$thread_status")
                {
                    var thread = Threads.FirstOrDefault();
                    statusId = thread is null ? null : ctx.Sim.Content.Get<ThreadDef>(thread)?.Status;
                    if (statusId is null)
                    {
                        return;
                    }
                }
                if (!ctx.Rng.Chance(ctx.Eval(Chance, 1)))
                {
                    return;
                }
                var service = ctx.Sim.Service<StatusService>();
                service.Apply(ctx.Sim, target, statusId, ctx.Source, new StatusApplication
                {
                    Duration = Duration is null ? null : ctx.Eval(Duration),
                    Stacks = Math.Max(1, (int)Math.Round(ctx.Eval(Stacks, 1))),
                    Magnitude = (Magnitude is null ? DefaultMagnitude(ctx, statusId) : ctx.Eval(Magnitude)) * ctx.Potency,
                    Potency = ctx.Potency,
                    SourceKey = ctx.Weave?.Key ?? "effect",
                    Tags = ctx.Tags,
                });
            }
        }
    }

    private sealed class RemoveStatusHandler : IEffectHandler
    {
        public string Type => "remove_status";

        public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
        {
            Type = Type,
            Status = ctx.StringOf(p["status"]),
            Tags = ctx.TagsOf(p["tags"]),
            Count = ctx.IntOf(p["count"], int.MaxValue),
            OnSelf = ctx.BoolOf(p["self"], false),
        };

        private sealed class Effect : CompiledEffect
        {
            public string? Status;
            public TagSet Tags = TagSet.Empty;
            public int Count = int.MaxValue;
            public bool OnSelf;

            public override void Execute(EffectContext ctx)
            {
                var target = OnSelf || ctx.Target.IsNone ? ctx.Source : ctx.Target;
                ctx.Sim.Service<StatusService>().Remove(ctx.Sim, target, Status, Tags, Count);
            }
        }
    }

    private sealed class ModifyHandler : IEffectHandler
    {
        public string Type => "modify";

        public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx)
        {
            var form = ctx.StringOf(p["form"]) ?? "flat";
            return new Effect
            {
                Type = Type,
                Attribute = ctx.StringOf(p["attribute"]) ?? throw new InvalidOperationException("modify needs 'attribute'"),
                Form = Enum.Parse<Attributes.ModifierForm>(form, ignoreCase: true),
                Value = ctx.ExprOf(p["value"], Expr.Constant(0))!,
                Duration = ctx.ExprOf(p["duration"], Expr.Constant(5))!,
                Scope = ctx.TagsOf(p["scope"]),
                OnSelf = ctx.BoolOf(p["self"], false),
                Key = ctx.StringOf(p["key"]),
            };
        }

        private sealed class Effect : CompiledEffect
        {
            public required string Attribute;
            public Attributes.ModifierForm Form;
            public required Expr Value;
            public required Expr Duration;
            public TagSet Scope = TagSet.Empty;
            public bool OnSelf;
            public string? Key;

            public override void Execute(EffectContext ctx)
            {
                var target = OnSelf || ctx.Target.IsNone ? ctx.Source : ctx.Target;
                if (ctx.Sim.World.Get<Stats>(target) is not { } stats)
                {
                    return;
                }
                var source = "temp:" + (Key ?? $"{ctx.Weave?.Key ?? "effect"}:{Attribute}");
                stats.Attributes.RemoveSource(source);
                stats.Attributes.Add(new Attributes.Modifier { Attribute = Attribute, Form = Form, Value = ctx.Eval(Value) * ctx.Potency, Scope = Scope, Source = source });
                var duration = ctx.Eval(Duration, 5);
                var version = stats.Attributes.Version;
                ctx.Sim.Scheduler.After(duration, () =>
                {
                    if (ctx.Sim.World.IsAlive(target))
                    {
                        stats.Attributes.RemoveSource(source);
                    }
                });
                _ = version;
            }
        }
    }

    // ------------------------------------------------------------ delivery: area, projectile, chain, field

    private sealed class AreaHandler : IEffectHandler
    {
        public string Type => "area";

        public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
        {
            Type = Type,
            Shape = ctx.StringOf(p["shape"]) ?? "circle",
            Radius = ctx.ExprOf(p["radius"], Expr.Parse("weave.area.radius"))!,
            Angle = ctx.ExprOf(p["angle"], Expr.Constant(90))!,
            Filter = ctx.StringOf(p["filter"]) ?? "enemy",
            CenterMode = ctx.StringOf(p["center"]) ?? "focus",
            MaxTargets = ctx.ExprOf(p["max_targets"]),
            Effects = ctx.CompileList(p["effects"], "effects"),
            IncludeCenter = ctx.BoolOf(p["include_center"], true),
        };

        private sealed class Effect : CompiledEffect
        {
            public string Shape = "circle";
            public required Expr Radius;
            public required Expr Angle;
            public string Filter = "enemy";
            public string CenterMode = "focus";
            public Expr? MaxTargets;
            public CompiledEffect[] Effects = Array.Empty<CompiledEffect>();
            public bool IncludeCenter = true;

            public override void Execute(EffectContext ctx)
            {
                var center = Center(ctx, CenterMode);
                var radius = (float)Math.Max(0.1, ctx.Eval(Radius, 2));
                var candidates = ctx.Sim.Spatial.Within(center, radius).Select(w => w.Entity);
                if (Shape == "cone")
                {
                    var origin = ctx.SourceTransform?.Position ?? center;
                    var dir = ctx.Direction.LengthSquared() > 0.001f ? Vector2.Normalize(ctx.Direction) : (ctx.SourceTransform?.Forward ?? Vector2.UnitX);
                    var half = MathF.PI * (float)ctx.Eval(Angle, 90) / 360f;
                    candidates = ctx.Sim.Spatial.Within(origin, radius).Where(w =>
                    {
                        var to = new Vector2(w.Position.X - origin.X, w.Position.Y - origin.Y);
                        if (to.LengthSquared() < 0.01f)
                        {
                            return true;
                        }
                        var ang = MathF.Acos(Math.Clamp(Vector2.Dot(Vector2.Normalize(to), dir), -1f, 1f));
                        return ang <= half;
                    }).Select(w => w.Entity);
                }
                else if (Shape == "line")
                {
                    var origin = ctx.SourceTransform?.Position ?? center;
                    var dir = ctx.Direction.LengthSquared() > 0.001f ? Vector2.Normalize(ctx.Direction) : (ctx.SourceTransform?.Forward ?? Vector2.UnitX);
                    var width = 1.0f;
                    candidates = ctx.Sim.Spatial.Within(origin, radius).Where(w =>
                    {
                        var to = new Vector2(w.Position.X - origin.X, w.Position.Y - origin.Y);
                        var along = Vector2.Dot(to, dir);
                        if (along < 0 || along > radius)
                        {
                            return false;
                        }
                        var perp = MathF.Abs(to.X * dir.Y - to.Y * dir.X);
                        return perp <= width;
                    }).Select(w => w.Entity);
                }
                var targets = Filter(ctx, candidates, Filter).Where(e => IncludeCenter || e != ctx.Target).ToList();
                if (MaxTargets is not null)
                {
                    var max = (int)Math.Round(ctx.Eval(MaxTargets, targets.Count));
                    if (targets.Count > max)
                    {
                        ctx.Rng.Shuffle(targets);
                        targets = targets.Take(max).ToList();
                    }
                }
                var deeper = ctx.Deeper();
                foreach (var t in targets)
                {
                    ExecuteAll(Effects, deeper.WithTarget(t));
                }
            }
        }
    }

    private sealed class ProjectileHandler : IEffectHandler
    {
        public string Type => "projectile";

        public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
        {
            Type = Type,
            Count = ctx.ExprOf(p["count"], Expr.Parse("weave.projectile.count"))!,
            Speed = ctx.ExprOf(p["speed"], Expr.Parse("weave.projectile.speed"))!,
            Range = ctx.ExprOf(p["range"], Expr.Parse("weave.range"))!,
            Spread = ctx.ExprOf(p["spread"], Expr.Constant(15))!,
            Pierce = ctx.ExprOf(p["pierce"], Expr.Parse("weave.projectile.pierce"))!,
            Radius = ctx.NumberOf(p["radius"], 0.5),
            OnHit = ctx.CompileList(p["on_hit"], "on_hit"),
            OnExpire = ctx.CompileList(p["on_expire"], "on_expire"),
            Look = ctx.StringOf(p["look"]),
            Gravity = ctx.BoolOf(p["gravity"], false),
        };

        private sealed class Effect : CompiledEffect
        {
            public required Expr Count;
            public required Expr Speed;
            public required Expr Range;
            public required Expr Spread;
            public required Expr Pierce;
            public double Radius = 0.5;
            public CompiledEffect[] OnHit = Array.Empty<CompiledEffect>();
            public CompiledEffect[] OnExpire = Array.Empty<CompiledEffect>();
            public string? Look;
            public bool Gravity;

            public override void Execute(EffectContext ctx)
            {
                var origin = ctx.SourceTransform?.Position ?? ctx.Point;
                var count = Math.Clamp((int)Math.Round(ctx.Eval(Count, 1)), 1, 64);
                var speed = (float)Math.Max(0.5, ctx.Eval(Speed, 12));
                var range = (float)Math.Max(1, ctx.Eval(Range, 12));
                var pierce = (int)Math.Max(0, ctx.Eval(Pierce, 0));
                var spread = (float)(ctx.Eval(Spread, 15) * Math.PI / 180.0);
                Vector2 dir;
                if (!ctx.Target.IsNone && ctx.TargetTransform is { } tt)
                {
                    dir = new Vector2(tt.Position.X - origin.X, tt.Position.Y - origin.Y);
                }
                else if (ctx.Direction.LengthSquared() > 0.001f)
                {
                    dir = ctx.Direction;
                }
                else
                {
                    dir = new Vector2(ctx.Point.X - origin.X, ctx.Point.Y - origin.Y);
                }
                if (dir.LengthSquared() < 0.0001f)
                {
                    dir = ctx.SourceTransform?.Forward ?? Vector2.UnitX;
                }
                dir = Vector2.Normalize(dir);
                var baseAngle = MathF.Atan2(dir.Y, dir.X);
                var start = origin + new Vector3(0, 0, 1.0f);
                for (var i = 0; i < count; i++)
                {
                    var offset = count == 1 ? 0 : -spread / 2 + spread * i / (count - 1);
                    var ang = baseAngle + offset;
                    var vel = new Vector3(MathF.Cos(ang) * speed, MathF.Sin(ang) * speed, 0);
                    var e = ctx.Sim.World.Create();
                    ctx.Sim.World.Add(e, new Transform { Position = start, Yaw = ang, Radius = (float)Radius, Height = 0.5f });
                    ctx.Sim.World.Add(e, new Identity { Name = "projectile", Pattern = null, Tags = TagSet.Of("projectile"), Faction = ctx.Sim.World.Get<Identity>(ctx.Origin)?.Faction, Owner = ctx.Origin, Look = Look ?? ctx.Weave?.Look });
                    ctx.Sim.World.Add(e, new Projectile
                    {
                        Context = ctx.Deeper(),
                        Velocity = vel,
                        Range = range,
                        Pierce = pierce,
                        OnHit = OnHit,
                        OnExpire = OnExpire,
                        Radius = (float)Radius,
                        Gravity = Gravity,
                    });
                    ctx.Sim.Spatial.Update(e, start);
                    ctx.Sim.Events.Publish(new EntitySpawned(e, "projectile", start) { Tick = ctx.Sim.Clock.Tick });
                }
            }
        }
    }

    private sealed class ChainHandler : IEffectHandler
    {
        public string Type => "chain";

        public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
        {
            Type = Type,
            Count = ctx.ExprOf(p["count"], Expr.Parse("weave.chain.count"))!,
            Range = ctx.ExprOf(p["range"], Expr.Constant(6))!,
            Filter = ctx.StringOf(p["filter"]) ?? "enemy",
            Effects = ctx.CompileList(p["effects"], "effects"),
        };

        private sealed class Effect : CompiledEffect
        {
            public required Expr Count;
            public required Expr Range;
            public string Filter = "enemy";
            public CompiledEffect[] Effects = Array.Empty<CompiledEffect>();

            public override void Execute(EffectContext ctx)
            {
                if (ctx.Target.IsNone)
                {
                    return;
                }
                var remaining = (int)Math.Round(ctx.Eval(Count, 0));
                var range = (float)ctx.Eval(Range, 6);
                var hit = new HashSet<Entity> { ctx.Target };
                var current = ctx.Deeper();
                while (remaining-- > 0)
                {
                    var from = current.Focus;
                    var next = Filter(ctx, ctx.Sim.Spatial.Within(from, range).OrderBy(w => w.Distance).Select(w => w.Entity), Filter).FirstOrDefault(e => !hit.Contains(e));
                    if (next.IsNone)
                    {
                        break;
                    }
                    hit.Add(next);
                    current = current.WithTarget(next).Deeper();
                    ExecuteAll(Effects, current);
                }
            }
        }
    }

    private sealed class FieldHandler : IEffectHandler
    {
        public string Type => "field";

        public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
        {
            Type = Type,
            Radius = ctx.ExprOf(p["radius"], Expr.Parse("weave.area.radius"))!,
            Duration = ctx.ExprOf(p["duration"], Expr.Parse("weave.duration"))!,
            Interval = ctx.ExprOf(p["interval"], Expr.Constant(0.5))!,
            Filter = ctx.StringOf(p["filter"]) ?? "enemy",
            Pulse = ctx.CompileList(p["pulse"], "pulse"),
            Look = ctx.StringOf(p["look"]),
            Follow = ctx.BoolOf(p["follow"], false),
        };

        private sealed class Effect : CompiledEffect
        {
            public required Expr Radius;
            public required Expr Duration;
            public required Expr Interval;
            public string Filter = "enemy";
            public CompiledEffect[] Pulse = Array.Empty<CompiledEffect>();
            public string? Look;
            public bool Follow;

            public override void Execute(EffectContext ctx)
            {
                var center = ctx.Focus;
                var e = ctx.Sim.World.Create();
                ctx.Sim.World.Add(e, new Transform { Position = center, Radius = (float)ctx.Eval(Radius, 2) });
                ctx.Sim.World.Add(e, new Identity { Name = "field", Tags = TagSet.Of("field"), Faction = ctx.Sim.World.Get<Identity>(ctx.Origin)?.Faction, Owner = ctx.Origin, Look = Look ?? ctx.Weave?.Look });
                ctx.Sim.World.Add(e, new Field
                {
                    Context = ctx.Deeper(),
                    Radius = (float)ctx.Eval(Radius, 2),
                    Interval = Math.Max(ctx.Sim.Clock.Dt, ctx.Eval(Interval, 0.5)),
                    ExpiresAt = ctx.Sim.Clock.Time + Math.Max(ctx.Sim.Clock.Dt, ctx.Eval(Duration, 4)),
                    NextPulseAt = ctx.Sim.Clock.Time,
                    Filter = Filter,
                    Pulse = Pulse,
                    Follow = Follow ? ctx.Source : Entity.None,
                });
                ctx.Sim.Spatial.Update(e, center);
                ctx.Sim.Events.Publish(new EntitySpawned(e, "field", center) { Tick = ctx.Sim.Clock.Tick });
            }
        }
    }

    // ------------------------------------------------------------ summon / spawn

    private sealed class SummonHandler : IEffectHandler
    {
        public string Type => "summon";

        public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
        {
            Type = Type,
            Pattern = ctx.StringOf(p["pattern"]) ?? throw new InvalidOperationException("summon needs 'pattern'"),
            Count = ctx.ExprOf(p["count"], Expr.Parse("weave.summon.count"))!,
            Duration = ctx.ExprOf(p["duration"], Expr.Parse("weave.duration"))!,
            MaxActive = ctx.ExprOf(p["max_active"], Expr.Parse("weave.summon.max"))!,
            Group = ctx.StringOf(p["group"]),
        };

        private sealed class Effect : CompiledEffect
        {
            public required string Pattern;
            public required Expr Count;
            public required Expr Duration;
            public required Expr MaxActive;
            public string? Group;

            public override void Execute(EffectContext ctx)
            {
                var factory = ctx.Sim.Service<PatternFactory>();
                var count = Math.Clamp((int)Math.Round(ctx.Eval(Count, 1)), 1, 32);
                var duration = ctx.Eval(Duration, 20);
                var max = (int)Math.Round(ctx.Eval(MaxActive, 8));
                var group = Group ?? Pattern;
                var owned = ctx.Sim.World.Query<Minion>().Where(m => m.C1.Owner == ctx.Origin && m.C1.Group == group).Select(m => m.Entity).ToList();
                while (owned.Count + count > max && owned.Count > 0)
                {
                    ctx.Sim.World.DestroyDeferred(owned[0]);
                    owned.RemoveAt(0);
                }
                var center = ctx.Focus;
                for (var i = 0; i < count; i++)
                {
                    var ang = ctx.Rng.NextDouble() * Math.PI * 2;
                    var pos = center + new Vector3((float)Math.Cos(ang) * 1.2f, (float)Math.Sin(ang) * 1.2f, 0);
                    var e = factory.Spawn(ctx.Sim, Pattern, pos, new SpawnOptions { Owner = ctx.Origin, Level = ctx.SourceStats?.Level });
                    if (e.IsNone)
                    {
                        continue;
                    }
                    ctx.Sim.World.Add(e, new Minion { Owner = ctx.Origin, ExpiresAt = duration > 0 ? ctx.Sim.Clock.Time + duration : double.PositiveInfinity, Group = group });
                }
            }
        }
    }

    private sealed class SpawnHandler : IEffectHandler
    {
        public string Type => "spawn";

        public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
        {
            Type = Type,
            Pattern = ctx.StringOf(p["pattern"]) ?? throw new InvalidOperationException("spawn needs 'pattern'"),
            Count = ctx.ExprOf(p["count"], Expr.Constant(1))!,
            Lifetime = ctx.ExprOf(p["lifetime"]),
        };

        private sealed class Effect : CompiledEffect
        {
            public required string Pattern;
            public required Expr Count;
            public Expr? Lifetime;

            public override void Execute(EffectContext ctx)
            {
                var factory = ctx.Sim.Service<PatternFactory>();
                var count = Math.Clamp((int)Math.Round(ctx.Eval(Count, 1)), 1, 32);
                for (var i = 0; i < count; i++)
                {
                    var e = factory.Spawn(ctx.Sim, Pattern, ctx.Focus, new SpawnOptions { Owner = ctx.Origin });
                    if (!e.IsNone && Lifetime is not null)
                    {
                        ctx.Sim.World.Add(e, new Lifetime { ExpiresAt = ctx.Sim.Clock.Time + ctx.Eval(Lifetime, 10) });
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------ movement

    private sealed class MoveHandler : IEffectHandler
    {
        public string Type => "move";

        public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
        {
            Type = Type,
            Kind = ctx.StringOf(p["kind"]) ?? "teleport",
            Distance = ctx.ExprOf(p["distance"], Expr.Constant(4))!,
            Who = ctx.StringOf(p["who"]) ?? "source",
        };

        private sealed class Effect : CompiledEffect
        {
            public string Kind = "teleport";
            public required Expr Distance;
            public string Who = "source";

            public override void Execute(EffectContext ctx)
            {
                var mover = Who == "target" ? ctx.Target : ctx.Source;
                if (!ctx.Sim.World.TryGet<Transform>(mover, out var t) || !ctx.Sim.World.TryGet<Motion>(mover, out var motion))
                {
                    return;
                }
                var dist = (float)ctx.Eval(Distance, 4);
                var source = ctx.SourceTransform?.Position ?? ctx.Point;
                Vector3 destination;
                switch (Kind)
                {
                    case "knockback":
                    {
                        var away = new Vector2(t.Position.X - source.X, t.Position.Y - source.Y);
                        if (away.LengthSquared() < 0.001f)
                        {
                            away = Vector2.UnitX;
                        }
                        away = Vector2.Normalize(away);
                        destination = t.Position + new Vector3(away.X * dist, away.Y * dist, 0);
                        break;
                    }
                    case "pull":
                    {
                        var toward = new Vector2(source.X - t.Position.X, source.Y - t.Position.Y);
                        var len = toward.Length();
                        if (len < 0.001f)
                        {
                            return;
                        }
                        var step = Math.Min(dist, Math.Max(0, len - 1f));
                        toward /= len;
                        destination = t.Position + new Vector3(toward.X * step, toward.Y * step, 0);
                        break;
                    }
                    default:
                    {
                        var target = ctx.Target.IsNone ? ctx.Point : ctx.Focus;
                        var to = new Vector2(target.X - t.Position.X, target.Y - t.Position.Y);
                        var len = to.Length();
                        if (len < 0.001f)
                        {
                            var fwd = t.Forward;
                            to = fwd;
                            len = 1;
                        }
                        to /= len;
                        var step = Math.Min(dist, len);
                        destination = t.Position + new Vector3(to.X * step, to.Y * step, 0);
                        break;
                    }
                }
                destination = Movement.MovementSystem.ResolveDestination(ctx.Sim, t.Position, destination);
                var from = t.Position;
                t.Position = ctx.Sim.Terrain.SnapToGround(destination, 1.5);
                motion.Stop();
                ctx.Sim.Spatial.Update(mover, t.Position);
                ctx.Sim.Events.Publish(new EntityMoved(mover, from, t.Position) { Tick = ctx.Sim.Clock.Tick });
            }
        }
    }

    // ------------------------------------------------------------ control flow

    private sealed class ConditionHandler : IEffectHandler
    {
        public string Type => "condition";

        public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
        {
            Type = Type,
            If = ctx.ExprOf(p["if"], Expr.Constant(1))!,
            Then = ctx.CompileList(p["then"], "then"),
            Else = ctx.CompileList(p["else"], "else"),
        };

        private sealed class Effect : CompiledEffect
        {
            public required Expr If;
            public CompiledEffect[] Then = Array.Empty<CompiledEffect>();
            public CompiledEffect[] Else = Array.Empty<CompiledEffect>();

            public override void Execute(EffectContext ctx) => ExecuteAll(If.IsTrue(ctx.Scope) ? Then : Else, ctx);
        }
    }

    private sealed class RandomHandler : IEffectHandler
    {
        public string Type => "random";

        public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx)
        {
            var choices = new List<(double, CompiledEffect[])>();
            if (p["choices"] is JsonArray arr)
            {
                foreach (var c in arr.OfType<JsonObject>())
                {
                    choices.Add((ctx.NumberOf(c["weight"], 1), ctx.CompileList(c["effects"], "effects")));
                }
            }
            return new Effect { Type = Type, Choices = choices };
        }

        private sealed class Effect : CompiledEffect
        {
            public List<(double Weight, CompiledEffect[] Effects)> Choices = new();

            public override void Execute(EffectContext ctx)
            {
                if (Choices.Count == 0)
                {
                    return;
                }
                var idx = ctx.Rng.PickWeighted(Choices.Select(c => c.Weight).ToList());
                if (idx >= 0)
                {
                    ExecuteAll(Choices[idx].Effects, ctx);
                }
            }
        }
    }

    private sealed class RepeatHandler : IEffectHandler
    {
        public string Type => "repeat";

        public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
        {
            Type = Type,
            Times = ctx.ExprOf(p["times"], Expr.Constant(3))!,
            Interval = ctx.ExprOf(p["interval"], Expr.Constant(0.3))!,
            Effects = ctx.CompileList(p["effects"], "effects"),
        };

        private sealed class Effect : CompiledEffect
        {
            public required Expr Times;
            public required Expr Interval;
            public CompiledEffect[] Effects = Array.Empty<CompiledEffect>();

            public override void Execute(EffectContext ctx)
            {
                var times = Math.Clamp((int)Math.Round(ctx.Eval(Times, 3)), 1, 50);
                var interval = ctx.Eval(Interval, 0.3);
                var deeper = ctx.Deeper();
                ExecuteAll(Effects, deeper);
                for (var i = 1; i < times; i++)
                {
                    var n = i;
                    ctx.Sim.Scheduler.After(interval * n, () =>
                    {
                        if (ctx.Sim.World.IsAlive(ctx.Source))
                        {
                            ExecuteAll(Effects, deeper);
                        }
                    });
                }
            }
        }
    }

    private sealed class SequenceHandler : IEffectHandler
    {
        public string Type => "sequence";

        public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect { Type = Type, Effects = ctx.CompileList(p["effects"], "effects") };

        private sealed class Effect : CompiledEffect
        {
            public CompiledEffect[] Effects = Array.Empty<CompiledEffect>();
            public override void Execute(EffectContext ctx) => ExecuteAll(Effects, ctx);
        }
    }

    private sealed class EventHandler : IEffectHandler
    {
        public string Type => "event";

        public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
        {
            Type = Type,
            Name = ctx.StringOf(p["name"]) ?? "custom",
            Data = p["data"] is JsonObject d ? d.ToDictionary(kv => kv.Key, kv => ctx.ExprOf(kv.Value, Expr.Constant(0))!) : new Dictionary<string, Expr>(),
        };

        private sealed class Effect : CompiledEffect
        {
            public string Name = "custom";
            public Dictionary<string, Expr> Data = new();

            public override void Execute(EffectContext ctx)
            {
                var data = Data.ToDictionary(kv => kv.Key, kv => ctx.Eval(kv.Value));
                ctx.Sim.Events.Publish(new CustomEvent(Name, ctx.Source, ctx.Target, data) { Tick = ctx.Sim.Clock.Tick });
            }
        }
    }

    private sealed class InterruptHandler : IEffectHandler
    {
        public string Type => "interrupt";

        public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect { Type = Type };

        private sealed class Effect : CompiledEffect
        {
            public override void Execute(EffectContext ctx)
            {
                if (ctx.Sim.World.TryGet<WeaveBook>(ctx.Target, out var book) && book.Casting is not null)
                {
                    var key = book.Casting.Slot.Key;
                    book.Casting = null;
                    ctx.Sim.Events.Publish(new WeaveInterrupted(ctx.Target, key) { Tick = ctx.Sim.Clock.Tick });
                }
            }
        }
    }

    private sealed class TauntHandler : IEffectHandler
    {
        public string Type => "taunt";

        public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect { Type = Type, Duration = ctx.ExprOf(p["duration"], Expr.Constant(4))! };

        private sealed class Effect : CompiledEffect
        {
            public required Expr Duration;

            public override void Execute(EffectContext ctx)
            {
                if (ctx.Sim.World.TryGet<Behavior>(ctx.Target, out var b))
                {
                    b.Target = ctx.Source;
                    b.Memory["taunted_until"] = ctx.Sim.Clock.Time + ctx.Eval(Duration, 4);
                }
            }
        }
    }

    private sealed class GrantItemHandler : IEffectHandler
    {
        public string Type => "grant_item";

        public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
        {
            Type = Type,
            Item = ctx.StringOf(p["item"]),
            Table = ctx.StringOf(p["table"]),
            Count = ctx.IntOf(p["count"], 1),
            Rarity = ctx.StringOf(p["rarity"]),
        };

        private sealed class Effect : CompiledEffect
        {
            public string? Item;
            public string? Table;
            public int Count = 1;
            public string? Rarity;

            public override void Execute(EffectContext ctx)
            {
                var target = ctx.Target.IsNone ? ctx.Source : ctx.Target;
                var loot = ctx.Sim.Service<Loot.LootService>();
                var level = ctx.Sim.World.Get<Stats>(target)?.Level ?? 1;
                if (Item is not null)
                {
                    var gen = ctx.Sim.Service<Items.ItemGenerator>();
                    var item = gen.Roll(Item, level, Rarity, ctx.Rng);
                    item.Count = Math.Clamp(Count, 1, Math.Max(1, item.Stack));
                    loot.Give(ctx.Sim, target, item);
                }
                if (Table is not null)
                {
                    loot.GiveTable(ctx.Sim, target, Table, level, ctx.Rng);
                }
            }
        }
    }

    private sealed class GrantXpHandler : IEffectHandler
    {
        public string Type => "grant_xp";

        public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect { Type = Type, Amount = ctx.ExprOf(p["amount"], Expr.Constant(10))! };

        private sealed class Effect : CompiledEffect
        {
            public required Expr Amount;

            public override void Execute(EffectContext ctx)
            {
                var target = ctx.Target.IsNone ? ctx.Source : ctx.Target;
                ctx.Sim.Service<Progression.ProgressionService>().GrantXp(ctx.Sim, target, ctx.Eval(Amount, 10));
            }
        }
    }

    private sealed class LogHandler : IEffectHandler
    {
        public string Type => "log";

        public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect { Type = Type, Text = ctx.StringOf(p["text"]) ?? "" };

        private sealed class Effect : CompiledEffect
        {
            public string Text = "";
            public override void Execute(EffectContext ctx) => ctx.Sim.Log.Info($"[effect] {Text} source={ctx.Source} target={ctx.Target}");
        }
    }
}

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
