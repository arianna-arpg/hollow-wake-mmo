using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Ai;
using HollowWake.Core.Characters;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Intents;
using HollowWake.Core.Items;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Systems;
using HollowWake.Core.World;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>
/// The intent seam: AI bodies act through the intents a player sends, dispatched at once during the think
/// through the same handlers, and hear refusals directly; a player's click plans as a chase does, on bounded
/// work; and the AI's walks follow the same ground, tick for tick, as before they were intents.
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

    /// <summary>The default handlers behind a registry that counts what it passes on, by intent type.</summary>
    private sealed class Counting
    {
        private readonly IntentHandlers _real = IntentHandlers.CreateDefault();

        public readonly IntentHandlers Handlers = new();
        public readonly Dictionary<Type, int> Handled = new();

        public Counting()
        {
            Pass<MoveToIntent>();
            Pass<MoveDirIntent>();
            Pass<StopIntent>();
            Pass<JumpIntent>();
            Pass<UseWeaveIntent>();
        }

        public int Of<T>() where T : IIntent => Handled.GetValueOrDefault(typeof(T));

        private void Pass<T>() where T : IIntent =>
            Handlers.Register<T>((in IntentContext ctx, T _) =>
            {
                Handled[typeof(T)] = Handled.GetValueOrDefault(typeof(T)) + 1;
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

    private static void Block(Simulation sim, int x, int y)
    {
        var (chunk, i) = sim.Terrain.Locate(new CellCoord(x, y));
        chunk.Flags[i] |= CellFlags.Blocked;
    }

    private static Entity Player(Simulation sim, string name, Vector3 at)
    {
        var characters = sim.Service<CharacterFactory>();
        var sheet = characters.Create(sim, "c-" + name, "a-" + name, name, "holdfast", sim.Service<ItemGenerator>());
        sheet.Position = at;
        return characters.Materialize(sim, sheet);
    }

    [Fact]
    public void AnAiBodysStopsWalksAndCastsLeaveAsIntentsAndTheHandlersRunThem()
    {
        var (sim, wolf, boar) = Duel(apart: 8);
        var counting = new Counting();
        sim.AddService(counting.Handlers);
        var bites = 0;
        sim.Events.Subscribe<DamageDealt>(d => bites += d.Source == wolf && d.Target == boar ? 1 : 0);
        sim.RunSeconds(6);
        Assert.True(counting.Of<MoveToIntent>() > 0, "the wolf never walked by intent");
        Assert.True(counting.Of<StopIntent>() > 0, "the wolf never stopped by intent");
        Assert.True(counting.Of<UseWeaveIntent>() > 0, "the wolf never cast by intent");
        Assert.True(bites > 0, "the handlers never carried a bite home");
        Assert.False(AiActions.TryGet("heal_self", out _), "a free heal is nothing an intent can ask for");
    }

    [Fact]
    public void WithNoHandlersABeastNeitherWalksNorCasts()
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
        // Out of reach the chase only walks, so nothing turns it either.
        Assert.Equal(yaw, t.Yaw);
        Assert.Equal(0, casts);
        Assert.False(sim.World.Get<Motion>(wolf)!.WantsToMove);
    }

    [Fact]
    public void WithNoHandlersNoMinionFollowsHeelsOrObeys()
    {
        var arena = MinionArena.Create();
        arena.Cast(arena.Learn("t_shades"));
        var minions = arena.Minions("t_shades");
        Assert.Equal(2, minions.Count);
        arena.Sim.AddService(new IntentHandlers());
        var start = minions.ToDictionary(m => m, m => arena.At(m));
        var seen = minions.ToDictionary(m => m, _ => new HashSet<string>());
        void Run(double seconds)
        {
            for (var i = 0; i < seconds * arena.Sim.Clock.TicksPerSecond; i++)
            {
                arena.Sim.Tick();
                foreach (var m in minions)
                {
                    seen[m].Add(arena.Sim.World.Get<Behavior>(m)!.State);
                }
            }
        }
        var home = arena.OwnerAt.Position;
        // Follow: the owner steps beyond the follow distance.
        arena.Move(arena.Owner, home + new Vector3(8, 0, 0));
        Run(1);
        // Heel: with a foe to fight, the owner goes past the leash but short of the recall distance, for less
        // than the time a stuck minion waits before the leash recall (a world rule) teleports it.
        var foe = arena.Foe("hob", home + new Vector3(0, 3, 0));
        foreach (var m in minions)
        {
            arena.Sim.World.Get<Behavior>(m)!.Target = foe;
        }
        Run(0.5);
        arena.Move(arena.Owner, home + new Vector3(20, 0, 0));
        Run(1);
        // Obey: an order to hold a mark.
        arena.Sim.Events.Publish(new MinionCommandRequested(arena.Owner, "hold", Entity.None, home + new Vector3(0, -6, 0), "t_shades"));
        Run(1);
        foreach (var m in minions)
        {
            Assert.Contains("follow", seen[m]);
            Assert.Contains("heel", seen[m]);
            Assert.Contains("obey", seen[m]);
            Assert.Equal(start[m].X, arena.At(m).X);
            Assert.Equal(start[m].Y, arena.At(m).Y);
        }
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
        Assert.True(AiIntents.Send(ctx, AiIntents.Stop).Accepted);
        Assert.Equal(new[] { "held_fast", "no_slot", IntentHandlers.PlayersOnly }, ctx.Refusals.Select(r => r.Reason));
        Assert.IsType<MoveToIntent>(ctx.Refusals[0].Intent);
        Assert.Equal(0, published);
    }

    [Fact]
    public void TheHandlersThatTakePlayersOnlyAreTheseAndNoOthers()
    {
        var handlers = IntentHandlers.CreateDefault();
        string[] Names(bool playerOnly) => handlers.IntentTypes.Where(t => handlers.IsPlayerOnly(t) == playerOnly)
            .Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[]
        {
            "AcceptQuestIntent", "AllocatePassiveIntent", "BindFountIntent", "BindGraftIntent", "BuildStructureIntent",
            "BuyBackIntent", "BuyIntent", "BuyPageIntent", "BuyTreeNodeIntent", "ChooseDialogueIntent",
            "CloseDialogueIntent", "ClosePaneIntent", "CraftStartIntent", "CraftSubmitIntent", "DropItemIntent",
            "EnactDecreeIntent", "EquipIntent", "InteractIntent", "LearnFormIntent", "MoveItemIntent", "PickUpIntent",
            "RefundPassiveIntent", "RespawnIntent", "SellIntent", "SetWeaveIntent", "SocketStoneIntent",
            "StationServiceIntent", "TurnInQuestIntent", "UnequipIntent", "UnlearnFormIntent", "UnsocketStoneIntent",
        }, Names(playerOnly: true));
        Assert.Equal(new[]
        {
            "AimIntent", "CancelCastIntent", "CommandMinionsIntent", "JumpIntent", "MoveDirIntent", "MoveToIntent",
            "PressWeaveIntent", "ReleaseWeaveIntent", "StopIntent", "SwapWeaveSlotsIntent", "UseMetaIntent", "UseWeaveIntent",
        }, Names(playerOnly: false));
    }

    [Fact]
    public void APlayersClickPlansAsAChaseDoesButAfreshEachTime()
    {
        var sim = ZoneBuilder.CreateArena(CoreContent.Registry, 1);
        var e = Player(sim, "Clicker", new Vector3(5, 5, 0));
        var player = sim.World.Get<Player>(e)!;
        var m = sim.World.Get<Motion>(e)!;
        var t = sim.World.Get<Transform>(e)!;

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
            Block(sim, 17, y);
        }
        player.Intents.Enqueue(new MoveToIntent(new Vector3(20.5f, 6.5f, 0)));
        sim.Tick();
        Assert.NotEmpty(m.Path);
        sim.RunSeconds(5);
        Assert.True(t.GroundDistanceTo(new Vector3(20.5f, 6.5f, 0)) < 0.5f, $"the click around the wall ended at {t.Position}");
    }

    [Fact]
    public void TheSearchBudgetGoesByWhoMovesNotByWhatTheIntentSays()
    {
        var sim = ZoneBuilder.CreateArena(CoreContent.Registry, 1);
        // A long wall between the walkers and their goal: the way round takes a search past the AI's budget.
        for (var y = 10; y <= 70; y++)
        {
            Block(sim, 17, y);
        }
        var goal = new Vector3(22.5f, 40.5f, 0);
        var pathfinder = sim.Service<Pathfinder>();
        var aiBudget = (long)sim.Content.Rules.Number("move.ai_path_max_nodes", 500);
        var player = sim.World.Get<Player>(Player(sim, "Budget", new Vector3(12.5f, 40.5f, 0)))!;
        // A player's walk searches on the pathfinder's own budget, even sent as the AI sends its walks.
        var before = pathfinder.Expanded;
        player.Intents.Enqueue(new MoveToIntent(goal) { KeepPlan = true });
        sim.Tick();
        Assert.True(pathfinder.Expanded - before > aiBudget, $"the player's search stopped at {pathfinder.Expanded - before} cells");
        // A wolf's walk searches on the AI's, however it is sent.
        var wolf = sim.Service<PatternFactory>().Spawn(sim, "wolf", new Vector3(12.5f, 44.5f, 0), new SpawnOptions { Faction = "red" });
        before = pathfinder.Expanded;
        Assert.True(AiIntents.Send(ContextFor(sim, wolf), new MoveToIntent(goal)).Accepted);
        Assert.InRange(pathfinder.Expanded - before, 1, aiBudget);
    }

    [Fact]
    public void AClickAnyDistanceAwayCostsBoundedWork()
    {
        (int Chunks, long Expanded) Click(float distance)
        {
            var sim = ZoneBuilder.CreateArena(CoreContent.Registry, 1);
            var player = sim.World.Get<Player>(Player(sim, "Far", new Vector3(5, 5, 0)))!;
            sim.Tick();
            var pathfinder = sim.Service<Pathfinder>();
            var (chunks, expanded) = (sim.Terrain.LoadedChunkCount, pathfinder.Expanded);
            player.Intents.Enqueue(new MoveToIntent(new Vector3(5 + distance, 5.5f, 0)));
            sim.Tick();
            return (sim.Terrain.LoadedChunkCount - chunks, pathfinder.Expanded - expanded);
        }
        var budget = (long)CoreContent.Registry.Rules.Number("move.path_max_nodes", 4000);
        var near = Click(1000);
        var mid = Click(10000);
        var far = Click(100000);
        // Past the budget's reach a click searches on the budget and no further, however far the point lies:
        // a click a hundred thousand units away loads no more ground than one at ten thousand.
        Assert.InRange(far.Expanded, 1, budget);
        Assert.Equal(mid.Chunks, far.Chunks);
        Assert.True(far.Chunks < 4 * budget / 32, $"the far click loaded {far.Chunks} chunks");
        // A click within the budget's reach finds its way and loads only the ground along it.
        Assert.True(near.Chunks <= far.Chunks, $"the near click loaded {near.Chunks} chunks, the far {far.Chunks}");
    }

    /// <summary>
    /// A hunter's walk tick by tick: where it stands, which way it faces and how many waypoints its path has left,
    /// the same walk the independent review traced on the base before the walks were intents.
    /// </summary>
    private static List<(float X, float Y, float Yaw, int Path)> Walk(string hunter, Vector3 at, Vector3 preyAt, Action<Simulation> walls, double seconds, bool preyMoves)
    {
        var sim = ZoneBuilder.CreateArena(HistoricalAiContent.Registry, 3);
        var factory = sim.Service<PatternFactory>();
        var h = factory.Spawn(sim, hunter, at, new SpawnOptions { Faction = "red" });
        var p = factory.Spawn(sim, "boar", preyAt, new SpawnOptions { Faction = "blue" });
        if (!preyMoves)
        {
            sim.World.Remove<Behavior>(p);
        }
        walls(sim);
        var t = sim.World.Get<Transform>(h)!;
        var m = sim.World.Get<Motion>(h)!;
        var walk = new List<(float X, float Y, float Yaw, int Path)>();
        for (var i = 0; i < seconds * sim.Clock.TicksPerSecond; i++)
        {
            sim.Tick();
            walk.Add((t.Position.X, t.Position.Y, t.Yaw, m.Path.Count));
        }
        return walk;
    }

    /// <summary>
    /// Holds a walk to the recorded one tick by tick: where it stands and which way it faces within
    /// <paramref name="tolerance"/> (facing as an angle, so a turn across the circle's seam is no difference),
    /// and how many waypoints its path has left exactly.
    /// </summary>
    private static void Follows(string name, JsonNode recorded, List<(float X, float Y, float Yaw, int Path)> walk, double tolerance)
    {
        var ticks = recorded.AsArray();
        Assert.True(ticks.Count == walk.Count, $"{name}: {walk.Count} ticks walked against {ticks.Count} recorded");
        for (var i = 0; i < ticks.Count; i++)
        {
            var tick = ticks[i]!.AsArray();
            var (x, y, yaw, path) = walk[i];
            double Recorded(int column) => JsonNum.TryDouble(tick[column], out var v) ? v : throw new InvalidDataException($"{name}: tick {i} has no number in column {column}");
            var off = Math.Max(Math.Abs(x - Recorded(0)), Math.Abs(y - Recorded(1)));
            Assert.True(off <= tolerance, $"{name}: at tick {i} the walker stands at ({x}, {y}), {off} from the recorded ({Recorded(0)}, {Recorded(1)})");
            var turned = Math.Abs(Math.IEEERemainder(yaw - Recorded(2), 2 * Math.PI));
            Assert.True(turned <= tolerance, $"{name}: at tick {i} the walker faces {yaw}, {turned} from the recorded {Recorded(2)}");
            Assert.True(path == Recorded(3), $"{name}: at tick {i} the path has {path} waypoints left against {Recorded(3)} recorded");
        }
    }

    [Fact]
    public void ChasesAroundObstaclesWalkTheBaseTrajectoryTickForTick()
    {
        // The reference holds the walks the base drew (main before the walks were intents) tick by tick, the numbers
        // as the review's probe printed them. Floating point differs in its last bits from one platform to another
        // (the reference names the one it was recorded on), so a walk is held to it within a ten-thousandth of a unit
        // and of a radian, far below a tick's step, rather than by a hash of the trace; the waypoints left match exactly.
        const double tolerance = 1e-4;
        var file = Path.Combine(CoreContent.FindContentDir(), "..", "..", "tests", "fixtures", "ai-chase-reference.json");
        var chases = JsonNode.Parse(File.ReadAllText(file))!["chases"]!;
        Follows("wall_detour", chases["wall_detour"]!, Walk("wolf", new Vector3(10.5f, 10.5f, 0), new Vector3(17.5f, 10.5f, 0),
            sim => { for (var y = 2; y <= 18; y++) Block(sim, 14, y); }, 8, false), tolerance);
        Follows("forest", chases["forest"]!, Walk("wolf", new Vector3(5.5f, 5.5f, 0), new Vector3(30.5f, 24.5f, 0),
            sim => { for (var x = 8; x <= 28; x += 3) for (var y = 6; y <= 24; y += 2) { Block(sim, x, y); Block(sim, x, y + 1); } }, 12, false), tolerance);
        Follows("hob_zigzag", chases["hob_zigzag"]!, Walk("hob", new Vector3(5.5f, 10.5f, 0), new Vector3(30.5f, 10.5f, 0),
            sim => { for (var y = 0; y <= 16; y++) Block(sim, 10, y); for (var y = 5; y <= 22; y++) Block(sim, 16, y); for (var y = 0; y <= 16; y++) Block(sim, 22, y); }, 15, true), tolerance);
    }
}
