using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Maths;
using HollowWake.Core.Tags;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Effects.Handlers;

/// <summary>
/// Effects about casting: casting another weave from inside an effect graph (<c>cast</c>) and running a
/// list later (<c>delay</c>); fuses, cooldown shaving and use restores join this family.
/// </summary>
public static class CastHandlers
{
    public static void Register(EffectRegistry r)
    {
        r.Register(new CastHandler());
        r.Register(new DelayHandler());
    }
}

/// <summary>
/// <c>cast {weave, at, aim, turn, level, damage_scale}</c>: runs a preset weave's effect graph from inside
/// another graph, free of cost and cooldown, one level deeper, with the host's weave level, tags and
/// origin. <c>at</c> places it at the context's point (default), its target or its caster; <c>aim</c>
/// points it onward along the host's direction (default), out from the host's center, at a random bearing,
/// or at a bearing that turns by <c>turn</c> degrees (default 45) each time this effect runs for the host.
/// The named weave compiles once per level. Sequels, emitters, follow-ups and contagion releases are this.
/// </summary>
internal sealed class CastHandler : IEffectHandler
{
    public string Type => "cast";

    public EffectSignature Signature { get; } = EffectSignature.Of("weave", "at", "aim", "turn", "level", "damage_scale");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx)
    {
        var weave = ctx.StringOf(p["weave"]) ?? throw new InvalidOperationException($"cast needs 'weave' in {ctx.Owner}.");
        if (!ctx.Content.Has<WeaveDef>(weave))
        {
            throw new InvalidOperationException($"cast names unknown preset weave '{weave}' in {ctx.Owner}.");
        }
        var effect = new Effect
        {
            Type = Type,
            Weave = weave,
            At = ctx.ChoiceOf(p["at"], "at", "point", "point", "target", "caster"),
            Aim = ctx.ChoiceOf(p["aim"], "aim", "onward", "onward", "out", "random", "rotating"),
            TurnRadians = ctx.NumberOf(p["turn"], 45) * Math.PI / 180.0,
            Level = ctx.ExprOf(p["level"]),
            DamageScale = ctx.ExprOf(p["damage_scale"]),
            Compiler = ctx.Compiler,
            TurnKey = "cast.turns." + weave,
        };
        // Compile the named weave now unless it is already being compiled up this chain (weaves that cast
        // each other), so a broken target fails its host at compile time rather than mid-fight.
        if (ctx.Compiler is { } compiler && !compiler.IsBuilding(weave))
        {
            if (!compiler.TryCompile(new WeaveSpec { Preset = weave }, out var compiled, out var error, GlyphFit.Dormant))
            {
                throw new InvalidOperationException($"cast target '{weave}' does not compile: {error}");
            }
            effect.Remember(1, compiled!);
        }
        return effect;
    }

    private sealed class Effect : CompiledEffect
    {
        public required string Weave;
        public required string At;
        public required string Aim;
        public double TurnRadians;
        public Expr? Level;
        public Expr? DamageScale;
        public WeaveCompiler? Compiler;
        public required string TurnKey;

        // The named weave compiled at each level it was cast at; levels are small and few.
        private readonly Dictionary<int, CompiledWeave> _byLevel = new();
        // The last host tag set and the union it produced, since a host's tags rarely change.
        private TagSet? _hostTags;
        private CompiledWeave? _taggedWeave;
        private TagSet _tags = TagSet.Empty;

        public void Remember(int level, CompiledWeave weave) => _byLevel[level] = weave;

        public override void Execute(EffectContext ctx)
        {
            var level = Level is null ? ctx.Weave?.Level ?? 1 : Math.Max(1, (int)Math.Round(ctx.Eval(Level, 1)));
            var weave = Resolve(ctx, level);
            if (weave is null)
            {
                return;
            }
            var hostCenter = ctx.From ?? ctx.SourceTransform?.Position ?? ctx.Point;
            var target = Entity.None;
            Vector3 point;
            Vector3? from;
            switch (At)
            {
                case "target":
                    if (!ctx.Target.IsNone && ctx.Sim.World.IsAlive(ctx.Target) && !ctx.Sim.World.Has<Dead>(ctx.Target))
                    {
                        target = ctx.Target;
                    }
                    point = ctx.Focus;
                    from = point;
                    break;
                case "caster":
                    point = ctx.SourceTransform?.Position ?? ctx.Point;
                    from = null;
                    break;
                default:
                    point = ctx.Point;
                    from = point;
                    break;
            }
            var rng = new Rng(Rng.Combine(ctx.Rng.NextUInt64(), Rng.Hash64(Weave)));
            var direction = AimFor(ctx, point, hostCenter, rng);
            var scale = DamageScale is null ? 1 : ctx.Eval(DamageScale, 1);
            var cast = new EffectContext
            {
                Sim = ctx.Sim,
                Origin = ctx.Origin,
                Source = ctx.Source,
                Target = target,
                Point = point,
                Direction = direction,
                Weave = weave,
                Tags = TagsFor(weave, ctx.Tags),
                Depth = ctx.Depth + 1,
                Rng = rng,
                Potency = ctx.Potency,
                DamageScale = ctx.DamageScale * scale,
                From = from,
                Vars = new Dictionary<string, double>(ctx.Vars, StringComparer.Ordinal),
            };
            ExecuteAll(weave.Effects, cast);
        }

        private CompiledWeave? Resolve(EffectContext ctx, int level)
        {
            if (_byLevel.TryGetValue(level, out var cached))
            {
                return cached;
            }
            var compiler = Compiler ?? (ctx.Sim.TryService<WeaveCompiler>(out var service) ? service : null);
            if (compiler is null)
            {
                return null;
            }
            if (!compiler.TryCompile(new WeaveSpec { Preset = Weave, Level = level }, out var weave, out var error, GlyphFit.Dormant))
            {
                ctx.Sim.Log.Warn($"cast of '{Weave}' failed to compile: {error}");
                return null;
            }
            _byLevel[level] = weave!;
            return weave;
        }

        private TagSet TagsFor(CompiledWeave weave, TagSet host)
        {
            if (!ReferenceEquals(_hostTags, host) || !ReferenceEquals(_taggedWeave, weave))
            {
                _hostTags = host;
                _taggedWeave = weave;
                _tags = weave.Tags.Union(host);
            }
            return _tags;
        }

        private Vector2 AimFor(EffectContext ctx, Vector3 point, Vector3 hostCenter, Rng rng)
        {
            switch (Aim)
            {
                case "random":
                {
                    var angle = rng.NextDouble() * Math.PI * 2;
                    return new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                }
                case "out":
                    return Outward(ctx, point, hostCenter);
                case "rotating":
                {
                    var onward = Onward(ctx, point, hostCenter);
                    var turns = ctx.Vars.GetValueOrDefault(TurnKey);
                    ctx.Vars[TurnKey] = turns + 1;
                    var angle = Math.Atan2(onward.Y, onward.X) + turns * TurnRadians;
                    return new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                }
                default:
                    return Onward(ctx, point, hostCenter);
            }
        }

        private static Vector2 Onward(EffectContext ctx, Vector3 point, Vector3 hostCenter) =>
            ctx.Direction.LengthSquared() > 0.001f ? Vector2.Normalize(ctx.Direction) : Outward(ctx, point, hostCenter);

        private static Vector2 Outward(EffectContext ctx, Vector3 point, Vector3 hostCenter)
        {
            var away = new Vector2(point.X - hostCenter.X, point.Y - hostCenter.Y);
            if (away.LengthSquared() > 0.0001f)
            {
                return Vector2.Normalize(away);
            }
            return ctx.SourceTransform?.Forward ?? Vector2.UnitX;
        }
    }
}

/// <summary>
/// <c>delay {seconds, effects}</c>: runs its list after a delay through the simulation scheduler (at least
/// one tick later), one level deeper, with a detached copy of the context so it reads the stats of the
/// moment it fires. Nothing fires once its source no longer exists.
/// </summary>
internal sealed class DelayHandler : IEffectHandler
{
    public string Type => "delay";

    public EffectSignature Signature { get; } = EffectSignature.Of("seconds").Wrapping("effects");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Seconds = ctx.ExprOf(p["seconds"], Expr.Constant(1))!,
        Effects = ctx.CompileList(p["effects"], "effects"),
    };

    private sealed class Effect : CompiledEffect
    {
        public required Expr Seconds;
        public CompiledEffect[] Effects = Array.Empty<CompiledEffect>();

        public override void Execute(EffectContext ctx)
        {
            if (Effects.Length == 0)
            {
                return;
            }
            var seconds = Math.Max(0, ctx.Eval(Seconds, 1));
            var later = ctx.Fork(deeper: 1);
            ctx.Sim.Scheduler.After(seconds, () => Fire(later));
        }

        private void Fire(EffectContext later)
        {
            if (!later.Source.IsNone && !later.Sim.World.IsAlive(later.Source))
            {
                return;
            }
            ExecuteAll(Effects, later);
        }
    }
}
