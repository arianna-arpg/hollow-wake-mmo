using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Maths;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Effects.Handlers;

/// <summary>
/// Effects about casting: casting another weave from inside an effect graph (<c>cast</c>), running a list
/// later (<c>delay</c>), banking a list behind a fuse that resolves at live stats (<c>fuse</c>), shaving
/// running cooldowns (<c>cooldown</c>), putting use charges back into banks (<c>restore_uses</c>) and
/// surging a raised guard's pool (<c>guard</c>).
/// </summary>
public static class CastHandlers
{
    public static void Register(EffectRegistry r)
    {
        r.Register(new CastHandler());
        r.Register(new DelayHandler());
        r.Register(new FuseHandler());
        r.Register(new CooldownHandler());
        r.Register(new RestoreUsesHandler());
        r.Register(new GuardSurgeHandler());
    }
}

/// <summary>
/// <c>guard {amount, self}</c>: surges a raised guard, adding <c>amount</c> to its pool, past the pool it was
/// raised with if need be (row C18: mana poured into a guard). It acts on the target's guard, or on the
/// source's with <c>self</c> or without a target. A body holding no guard, or a broken one, is untouched.
/// </summary>
internal sealed class GuardSurgeHandler : IEffectHandler
{
    public string Type => "guard";

    public EffectSignature Signature { get; } = EffectSignature.Of("amount", "self");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Amount = ctx.ExprOf(p["amount"]) ?? throw new InvalidOperationException($"guard needs 'amount' in {ctx.Owner}."),
        Self = ctx.BoolOf(p["self"], false),
    };

    private sealed class Effect : CompiledEffect
    {
        public required Expr Amount;
        public bool Self;

        public override void Execute(EffectContext ctx)
        {
            var world = ctx.Sim.World;
            var bearer = Self || ctx.Target.IsNone ? ctx.Source : ctx.Target;
            if (!world.TryGet<Guard>(bearer, out var guard) || guard.Broken
                || !world.TryGet<WeaveBook>(bearer, out var book) || !ReferenceEquals(book.Casting, guard.Cast))
            {
                return;
            }
            var amount = Math.Max(0, ctx.Eval(Amount) * ctx.Potency);
            if (amount <= 0)
            {
                return;
            }
            guard.Pool += amount;
            ctx.Sim.Events.Publish(new GuardSurged(bearer, guard.Cast.Weave?.Key ?? guard.Slot.Key, amount, guard.Pool) { Tick = ctx.Sim.Clock.Tick });
        }
    }
}

/// <summary>
/// <c>fuse {delay, power, effects}</c>: a wrapper that banks each run of its list and resolves it after a
/// delay (attribute <c>fuse.delay</c> in the weave's scope unless given), one level deeper, with a detached
/// context that reads the caster's stats of the moment it fires, its damage scaled by one plus
/// <c>power</c> (attribute <c>fuse.power</c> unless given). Glyphs wrap a delivery's hits in it.
/// </summary>
internal sealed class FuseHandler : IEffectHandler
{
    public string Type => "fuse";

    public EffectSignature Signature { get; } = EffectSignature.Of("delay", "power").Wrapping("effects");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Delay = ctx.ExprOf(p["delay"]),
        Power = ctx.ExprOf(p["power"]),
        Effects = ctx.CompileList(p["effects"], "effects"),
    };

    private sealed class Effect : CompiledEffect
    {
        public Expr? Delay;
        public Expr? Power;
        public CompiledEffect[] Effects = Array.Empty<CompiledEffect>();

        public override void Execute(EffectContext ctx)
        {
            if (Effects.Length == 0)
            {
                return;
            }
            var stats = ctx.SourceStats;
            var weave = ctx.Weave;
            var delay = Delay is not null ? ctx.Eval(Delay, 1) : weave is not null && stats is not null ? weave.Attr(stats, "fuse.delay", 1) : 1;
            var power = Power is not null ? ctx.Eval(Power, 0) : weave is not null && stats is not null ? weave.Attr(stats, "fuse.power", 0) : 0;
            var job = new Job(Effects, ctx.Fork(1, Math.Max(0, 1 + power)));
            ctx.Sim.Scheduler.After(Math.Max(0, delay), job.Fire);
        }
    }

    private sealed class Job
    {
        private readonly CompiledEffect[] _effects;
        private readonly EffectContext _later;

        public Job(CompiledEffect[] effects, EffectContext later)
        {
            _effects = effects;
            _later = later;
        }

        public void Fire()
        {
            if (!_later.Source.IsNone && !_later.Sim.World.IsAlive(_later.Source))
            {
                return;
            }
            CompiledEffect.ExecuteAll(_effects, _later);
        }
    }
}

/// <summary>
/// <c>cooldown {seconds, fraction, scope}</c>: shaves the running cooldowns of the source's weaves, first
/// <c>seconds</c> off, then <c>fraction</c> of what remains. <c>scope</c> is <c>others</c> (default: every
/// weave but the one running), <c>all</c>, or a list of tags (weaves carrying one of them).
/// </summary>
internal sealed class CooldownHandler : IEffectHandler
{
    public string Type => "cooldown";

    public EffectSignature Signature { get; } = EffectSignature.Of("seconds", "fraction", "scope");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx)
    {
        var scope = p["scope"];
        var word = ctx.StringOf(scope);
        TagSet? tags = null;
        if (word is null && scope is not null)
        {
            tags = ctx.TagsOf(scope);
        }
        else if (word is not null and not ("others" or "all"))
        {
            tags = TagSet.Parse(word);
        }
        return new Effect
        {
            Type = Type,
            Seconds = ctx.ExprOf(p["seconds"]),
            Fraction = ctx.ExprOf(p["fraction"]),
            All = word == "all",
            Tags = tags,
        };
    }

    private sealed class Effect : CompiledEffect
    {
        public Expr? Seconds;
        public Expr? Fraction;
        public bool All;
        public TagSet? Tags;

        public override void Execute(EffectContext ctx)
        {
            if (!ctx.Sim.World.TryGet<WeaveBook>(ctx.Source, out var book) || book.ReadyAt.Count == 0)
            {
                return;
            }
            var seconds = Seconds is null ? 0 : Math.Max(0, ctx.Eval(Seconds));
            var fraction = Fraction is null ? 0 : Math.Clamp(ctx.Eval(Fraction), 0, 1);
            var now = ctx.Sim.Clock.Time;
            var host = (ctx.Host ?? ctx.Weave)?.Key;
            foreach (var slot in book.Slots)
            {
                var key = slot.Key;
                if (!book.ReadyAt.TryGetValue(key, out var at) || at <= now)
                {
                    continue;
                }
                if (Tags is { } tags ? !(slot.Compiled?.Tags.HasAny(tags) ?? false) : !All && key == host)
                {
                    continue;
                }
                var cut = seconds + fraction * Math.Max(0, at - now - seconds);
                book.ReadyAt[key] = Math.Max(now, at - cut);
            }
        }
    }
}

/// <summary>
/// <c>restore_uses {amount, scope}</c>: puts use charges back into banks: the host slot's (default), every
/// bank (<c>all</c>), or the banks that recover only while still (<c>still</c>). Without an amount a bank
/// is filled.
/// </summary>
internal sealed class RestoreUsesHandler : IEffectHandler
{
    public string Type => "restore_uses";

    public EffectSignature Signature { get; } = EffectSignature.Of("amount", "scope");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Amount = ctx.ExprOf(p["amount"]),
        Scope = ctx.ChoiceOf(p["scope"], "scope", "host", "host", "all", "still"),
    };

    private sealed class Effect : CompiledEffect
    {
        public Expr? Amount;
        public required string Scope;

        public override void Execute(EffectContext ctx)
        {
            var sim = ctx.Sim;
            if (!sim.World.TryGet<WeaveBook>(ctx.Source, out var book) || !sim.World.TryGet<Stats>(ctx.Source, out var stats) || !sim.TryService<WeaveRuntime>(out var runtime))
            {
                return;
            }
            var amount = Amount is null ? double.PositiveInfinity : ctx.Eval(Amount);
            if (Scope == "host")
            {
                if (ctx.Slot is { Compiled: { } weave } host && weave.Casting.Uses is { } spec)
                {
                    runtime.RestoreUses(sim, ctx.Source, host, weave, spec, stats, amount);
                }
                return;
            }
            foreach (var slot in book.Slots)
            {
                if (slot.Compiled is { } weave && weave.Casting.Uses is { } spec && (Scope == "all" || spec.Still is not null))
                {
                    runtime.RestoreUses(sim, ctx.Source, slot, weave, spec, stats, amount);
                }
            }
        }
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
            WeaveHash = Rng.Hash64(weave),
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
        public ulong WeaveHash;

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
            var rng = new Rng(Rng.Combine(ctx.Rng.NextUInt64(), WeaveHash));
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
                Slot = ctx.Slot,
                Host = ctx.Host ?? ctx.Weave,
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
