using System.Numerics;
using HollowWake.Core.Ai;
using HollowWake.Core.Characters;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Intents;
using HollowWake.Core.Items;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Systems;
using HollowWake.Core.Weaves;
using HollowWake.Core.World;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>
/// The intent seam: AI bodies act through the intents a player sends, dispatched at once during the think
/// through the same handlers, and hear refusals directly; constructs cast through the runtime like anyone;
/// a player's click plans as a chase does.
/// </summary>
public class AiIntentTests
{
    /// <summary>A hunter and a boar that stands still, <paramref name="apart"/> units east of it.</summary>
    private static (Simulation Sim, Entity Hunter, Entity Prey) Duel(float apart, string hunter = "wolf")
    {
        var sim = ZoneBuilder.CreateArena(CoreContent.Registry, 5);
        var factory = sim.Service<PatternFactory>();
        var h = factory.Spawn(sim, hunter, new Vector3(10.5f, 10.5f, 0), new SpawnOptions { Faction = "red" });
        var p = factory.Spawn(sim, "boar", new Vector3(10.5f + apart, 10.5f, 0), new SpawnOptions { Faction = "blue" });
        sim.World.Remove<Behavior>(p);
        return (sim, h, p);
    }

    /// <summary>The default handlers behind a registry that counts what it passes on, by intent type and per tick.</summary>
    private sealed class Counting
    {
        private readonly IntentHandlers _real = IntentHandlers.CreateDefault();

        public readonly IntentHandlers Handlers = new();
        public readonly Dictionary<Type, int> Handled = new();
        public int ThisTick;

        public Counting()
        {
            Pass<MoveToIntent>();
            Pass<MoveDirIntent>();
            Pass<StopIntent>();
            Pass<JumpIntent>();
            Pass<FaceIntent>();
            Pass<UseWeaveIntent>();
        }

        public int Of<T>() where T : IIntent => Handled.GetValueOrDefault(typeof(T));

        private void Pass<T>() where T : IIntent =>
            Handlers.Register<T>((in IntentContext ctx, T _) =>
            {
                Handled[typeof(T)] = Handled.GetValueOrDefault(typeof(T)) + 1;
                ThisTick++;
                _real.Dispatch(ctx);
            });
    }

    /// <summary>A think's context for a body, built the way the behavior system builds one.</summary>
    private static AiContext ContextFor(Simulation sim, Entity e)
    {
        var behavior = sim.World.Get<Behavior>(e)!;
        return new AiContext
        {
            Sim = sim,
            Entity = e,
            Behavior = behavior,
            Def = sim.Content.Get<BehaviorDef>(behavior.BehaviorId)!,
            Transform = sim.World.Get<Transform>(e)!,
            Motion = sim.World.Get<Motion>(e)!,
            Stats = sim.World.Get<Stats>(e)!,
        };
    }

    [Fact]
    public void AnAiBodysStopsMovesFacingAndCastsLeaveAsIntentsAndTheHandlersRunThem()
    {
        var (sim, wolf, boar) = Duel(apart: 8);
        var counting = new Counting();
        sim.AddService(counting.Handlers);
        var b = sim.World.Get<Behavior>(wolf)!;
        var bites = 0;
        sim.Events.Subscribe<DamageDealt>(d => bites += d.Source == wolf && d.Target == boar ? 1 : 0);
        var chaseThinks = 0;
        var silentThinks = 0;
        for (var i = 0; i < 6 * sim.Clock.TicksPerSecond; i++)
        {
            var thinkAt = b.NextThinkAt;
            counting.ThisTick = 0;
            sim.Tick();
            if (b.NextThinkAt != thinkAt && b.State == "chase")
            {
                chaseThinks++;
                silentThinks += counting.ThisTick == 0 ? 1 : 0;
            }
        }
        Assert.True(counting.Of<MoveToIntent>() > 0, "the wolf never walked by intent");
        Assert.True(counting.Of<StopIntent>() > 0, "the wolf never stopped by intent");
        Assert.True(counting.Of<FaceIntent>() > 0, "the wolf never turned by intent");
        Assert.True(counting.Of<UseWeaveIntent>() > 0, "the wolf never cast by intent");
        // Every think of the chase acts, and acts by intent: a move toward the boar or a stop beside it.
        Assert.True(chaseThinks > 0);
        Assert.Equal(0, silentThinks);
        Assert.True(bites > 0, "the handlers never carried a bite home");
        Assert.False(AiActions.TryGet("heal_self", out _), "a free heal is nothing an intent can ask for");
    }

    [Fact]
    public void WithNoHandlersNeitherABeastNorATotemWalksTurnsOrCasts()
    {
        var (sim, wolf, _) = Duel(apart: 8);
        sim.AddService(new IntentHandlers());
        var t = sim.World.Get<Transform>(wolf)!;
        var (x, y, yaw) = (t.Position.X, t.Position.Y, t.Yaw);
        var casts = 0;
        sim.Events.Subscribe<WeaveStarted>(w => casts += w.Caster == wolf ? 1 : 0);
        sim.Events.Subscribe<WeaveCast>(w => casts += w.Caster == wolf ? 1 : 0);
        sim.RunSeconds(4);
        Assert.Equal("chase", sim.World.Get<Behavior>(wolf)!.State);
        Assert.Equal(x, t.Position.X);
        Assert.Equal(y, t.Position.Y);
        Assert.Equal(yaw, t.Yaw);
        Assert.Equal(0, casts);
        Assert.False(sim.World.Get<Motion>(wolf)!.WantsToMove);

        // A construct's casts leave the same way.
        var arena = MinionArena.Create(9);
        arena.Foe("tangle", arena.OwnerAt.Position + new Vector3(6, 0, 0));
        arena.Cast(arena.Learn("plant_ember_totem"), point: arena.OwnerAt.Position + new Vector3(2, 2, 0));
        var totem = arena.Minions("ember_totems").Single();
        arena.Sim.AddService(new IntentHandlers());
        arena.Events.Clear();
        arena.Sim.RunSeconds(3);
        Assert.DoesNotContain(arena.Events.OfType<WeaveCast>(), w => w.Caster == totem);
    }

    [Fact]
    public void ARefusedIntentReportsItsReasonAndTheAiWritesNothingInItsPlace()
    {
        var (sim, wolf, boar) = Duel(apart: 8);
        var asked = 0;
        var handlers = IntentHandlers.CreateDefault();
        handlers.Register<MoveToIntent>((in IntentContext ctx, MoveToIntent _) =>
        {
            asked++;
            ctx.Reject("held_fast");
        });
        sim.AddService(handlers);
        var published = 0;
        sim.Events.Subscribe<IntentRejected>(_ => published++);
        var t = sim.World.Get<Transform>(wolf)!;
        var (x, y) = (t.Position.X, t.Position.Y);
        sim.RunSeconds(3);
        var m = sim.World.Get<Motion>(wolf)!;
        Assert.Equal("chase", sim.World.Get<Behavior>(wolf)!.State);
        Assert.True(asked > 0, "the wolf never asked to walk");
        Assert.True(m.Path.Count == 0 && m.MoveTarget is null, "the wolf planned a walk past its refusal");
        Assert.Equal(x, t.Position.X);
        Assert.Equal(y, t.Position.Y);
        // An AI's refusals are its own: no client hears of them.
        Assert.Equal(0, published);

        // The seam hands back the handler's reason and keeps it on the think, in order.
        var ctx = ContextFor(sim, wolf);
        var walk = AiIntents.Send(ctx, new MoveToIntent(new Vector3(20, 10, 0)));
        Assert.False(walk.Accepted);
        Assert.Equal("held_fast", walk.Refusal);
        Assert.Equal("no_slot", AiIntents.Send(ctx, new UseWeaveIntent(99, boar, Vector3.Zero, Vector2.Zero)).Refusal);
        Assert.Equal(IntentHandlers.PlayersOnly, AiIntents.Send(ctx, new PickUpIntent(boar)).Refusal);
        Assert.True(AiIntents.Send(ctx, new FaceIntent(new Vector3(10.5f, 20, 0))).Accepted);
        Assert.Equal(new[] { "held_fast", "no_slot", IntentHandlers.PlayersOnly }, ctx.Refusals.Select(r => r.Reason));
        Assert.IsType<MoveToIntent>(ctx.Refusals[0].Intent);
        Assert.Equal(0, published);
    }

    [Fact]
    public void AConstructsCastPaysItsCostAndWaitsItsCastTime()
    {
        var arena = MinionArena.Create(9);
        arena.Foe("tangle", arena.OwnerAt.Position + new Vector3(8, 0, 0));
        arena.Cast(arena.Learn("plant_ember_totem"), point: arena.OwnerAt.Position + new Vector3(2, 0, 0));
        var totem = arena.Minions("ember_totems").Single();
        var stats = arena.Sim.World.Get<Stats>(totem)!;
        var book = arena.Sim.World.Get<WeaveBook>(totem)!;
        var runtime = arena.Sim.Service<WeaveRuntime>();
        var weave = runtime.Resolve(arena.Sim, book.Slots[0], out _)!;
        // The shipped devices cast free: their pattern folds every cost to nothing, through the same costs.
        Assert.Equal(0, runtime.Costs(weave, stats)["mana"]);
        // Without that waiver the totem pays as anyone would.
        stats.Attributes.RemoveSource("pattern");
        var cost = runtime.Costs(weave, stats)["mana"];
        Assert.True(cost > 0);
        double? manaAtStart = null, paid = null, startedAt = null, castAt = null, duration = null;
        arena.Sim.Events.Subscribe<WeaveStarted>(w =>
        {
            if (w.Caster == totem && startedAt is null)
            {
                startedAt = arena.Sim.Clock.Time;
                duration = w.CastTime;
                paid = manaAtStart - stats.Resource("mana");
            }
        });
        arena.Sim.Events.Subscribe<WeaveCast>(w =>
        {
            if (w.Caster == totem && castAt is null)
            {
                castAt = arena.Sim.Clock.Time;
            }
        });
        for (var i = 0; i < 3 * arena.Sim.Clock.TicksPerSecond && castAt is null; i++)
        {
            manaAtStart = stats.Resource("mana");
            arena.Sim.Tick();
        }
        Assert.NotNull(startedAt);
        Assert.NotNull(castAt);
        Assert.Equal(cost, paid!.Value, 6);
        Assert.Equal(runtime.CastDuration(weave, stats), duration!.Value, 6);
        Assert.True(duration > 0);
        Assert.True(castAt - startedAt >= duration - 1e-9, $"the bolt flew {castAt - startedAt}s into a {duration}s cast");
    }

    [Fact]
    public void APlayersClickPlansAsAChaseDoesButAfreshEachTime()
    {
        var sim = ZoneBuilder.CreateArena(CoreContent.Registry, 1);
        var characters = sim.Service<CharacterFactory>();
        var sheet = characters.Create(sim, "c-click", "a-click", "Clicker", "holdfast", sim.Service<ItemGenerator>());
        sheet.Position = new Vector3(5, 5, 0);
        var e = characters.Materialize(sim, sheet);
        var player = sim.World.Get<Player>(e)!;
        var m = sim.World.Get<Motion>(e)!;
        var t = sim.World.Get<Transform>(e)!;
        Assert.False(sim.Service<IntentHandlers>().IsPlayerOnly<MoveToIntent>());
        Assert.True(sim.Service<IntentHandlers>().IsPlayerOnly<PickUpIntent>());

        // Over clear ground a click walks straight at the point, as a chase does.
        player.Intents.Enqueue(new MoveToIntent(new Vector3(15, 5, 0)));
        sim.Tick();
        Assert.Empty(m.Path);
        Assert.Equal(15, m.MoveTarget!.Value.X, 3);
        // A second click beside the first plans afresh: a click never keeps a plan.
        player.Intents.Enqueue(new MoveToIntent(new Vector3(15, 6, 0)));
        sim.Tick();
        Assert.Equal(6, m.MoveTarget!.Value.Y, 3);
        // A destination sent again every think keeps the plan that still serves it.
        player.Intents.Enqueue(new MoveToIntent(new Vector3(15, 6.5f, 0)) { KeepPlan = true });
        sim.Tick();
        Assert.Equal(6, m.MoveTarget!.Value.Y, 3);
        sim.RunSeconds(4);
        Assert.True(t.GroundDistanceTo(new Vector3(15, 6, 0)) < 0.5f, $"the click ended at {t.Position}");

        // Around a wall the click searches, on the pathfinder's own budget, and gets there.
        for (var y = 2; y <= 12; y++)
        {
            var (chunk, i) = sim.Terrain.Locate(new CellCoord(17, y));
            chunk.Flags[i] |= CellFlags.Blocked;
        }
        player.Intents.Enqueue(new MoveToIntent(new Vector3(20.5f, 6.5f, 0)));
        sim.Tick();
        Assert.NotEmpty(m.Path);
        sim.RunSeconds(5);
        Assert.True(t.GroundDistanceTo(new Vector3(20.5f, 6.5f, 0)) < 0.5f, $"the click around the wall ended at {t.Position}");

        // Facing is an intent any actor sends.
        player.Intents.Enqueue(new FaceIntent(t.Position + new Vector3(0, -3, 0)));
        sim.Tick();
        Assert.Equal(-MathF.PI / 2, t.Yaw, 3);
    }
}
