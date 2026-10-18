using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.World;

namespace HollowWake.Core.Movement;

/// <summary>
/// How a mover plans a walk to a point, for every sender of a <see cref="Intents.MoveToIntent"/>: a player's
/// click and a monster's chase plan the same way. A new plan walks straight when the segment is clear;
/// otherwise the pathfinder searches, on a budget the server picks by who moves: a player's search runs on
/// the pathfinder's own budget (<c>move.path_max_nodes</c>), any other mover's on the smaller
/// <c>move.ai_path_max_nodes</c>, since the AI's movers are many and renew their plans as they go. When a
/// search cannot bring the mover closer (no step, or a partial path that ends no nearer the goal), the mover
/// follows what it found and then walks straight at the goal.
/// <para>
/// A destination sent again every think (<see cref="Intents.MoveToIntent.KeepPlan"/>) keeps its plan while it
/// lasts: it is planned again only when its path or move target has run out, when the destination has moved
/// more than <c>move.ai_repath_distance</c> from the planned goal, or when the plan is older than
/// <c>move.ai_repath_seconds</c>, and a search that failed is not repeated for that goal until the plan is
/// renewed, so an unreachable target costs one search per renewal instead of one per think. A click plans
/// afresh every time. The flag only lets a sender keep its own plan; it never changes the budget.
/// </para>
/// The plan is a cache on <see cref="Motion"/> (<see cref="Motion.PlanGoal"/>, <see cref="Motion.PlannedAt"/>,
/// <see cref="Motion.Planned"/>, <see cref="Motion.PlanFailed"/>); a teleport or an order drops it.
/// </summary>
public sealed class MovePlanner
{
    /// <summary>How near a point counts as arrived when the sender names no distance.</summary>
    public const float DefaultStop = 0.3f;

    /// <summary>Spacing of the straight-walk samples: about a walker's step per tick.</summary>
    private const float StraightStep = 0.25f;

    private Rules? _source;
    private double _repathSeconds = 1.5;
    private double _repathDistance = 2;
    private int _aiNodes = 500;
    private double _stepUp = 0.6;
    private double _maxDrop = 3;

    /// <summary>The simulation's planner, registered on first use where the composition root did not register one.</summary>
    public static MovePlanner Of(Simulation sim)
    {
        if (!sim.TryService<MovePlanner>(out var planner))
        {
            planner = new MovePlanner();
            sim.AddService(planner);
        }
        return planner;
    }

    /// <summary>
    /// Plans the mover's walk to <paramref name="destination"/>, arriving within <paramref name="stop"/>. With
    /// <paramref name="keep"/> a plan that still serves stands. A <paramref name="player"/> searches on the
    /// pathfinder's own budget, anyone else on the AI's. A walker's goal is put on the ground; a flier keeps the
    /// height it was given.
    /// </summary>
    public void Plan(Simulation sim, Transform transform, Motion m, Vector3 destination, float stop, bool keep, bool player)
    {
        Read(sim.Content.Rules);
        var now = sim.Clock.Time;
        m.MoveDir = Vector2.Zero;
        m.StopDistance = stop;
        if (keep && m.Planned && now - m.PlannedAt < _repathSeconds && GroundDistance(m.PlanGoal, destination) <= _repathDistance)
        {
            if (m.Path.Count > 0 || m.MoveTarget is not null)
            {
                return;
            }
            if (m.PlanFailed)
            {
                m.MoveTarget = destination;
                return;
            }
        }
        var goal = m.Flying ? destination : sim.Terrain.SnapToGround(destination, 3);
        m.Planned = true;
        m.PlanGoal = goal;
        m.PlannedAt = now;
        m.PlanFailed = false;
        var pathfinder = sim.Service<Pathfinder>();
        var from = transform.Position;
        if (CanWalkStraight(sim, pathfinder, from, goal))
        {
            m.Path.Clear();
            m.MoveTarget = goal;
            return;
        }
        switch (pathfinder.FindPath(from, goal, m.Path, player ? pathfinder.MaxNodes : _aiNodes))
        {
            case PathResult.None:
                m.MoveTarget = goal;
                m.PlanFailed = true;
                break;
            case PathResult.Partial:
                // Progress means ending at least a cell nearer the goal than the search started.
                m.PlanFailed = GroundDistance(m.Path[^1], goal) > GroundDistance(from, goal) - 1;
                break;
        }
    }

    /// <summary>
    /// Whether walking straight from one point to another works: every sample along the segment can be
    /// occupied as the movement system judges it, no step drops further than <c>move.max_drop</c>, and no
    /// cell crossed costs more than plain ground (so dear ground such as bog still goes to the pathfinder).
    /// </summary>
    public bool CanWalkStraight(Simulation sim, Pathfinder pathfinder, Vector3 from, Vector3 to)
    {
        Read(sim.Content.Rules);
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var steps = (int)MathF.Ceiling(MathF.Sqrt(dx * dx + dy * dy) / StraightStep);
        var blocked = sim.TryService<BlockedCells>(out var bc) ? bc : null;
        double feet = from.Z;
        var cell = CellCoord.Of(from.X, from.Y);
        for (var i = 1; i <= steps; i++)
        {
            var f = i / (float)steps;
            var x = from.X + dx * f;
            var y = from.Y + dy * f;
            if (!MovementSystem.CanOccupy(sim, blocked, x, y, feet, _stepUp, out var ground) || feet - ground.Height > _maxDrop)
            {
                return false;
            }
            var at = CellCoord.Of(x, y);
            if (at != cell)
            {
                if (pathfinder.CellCost(at) > 1)
                {
                    return false;
                }
                cell = at;
            }
            feet = ground.Height;
        }
        return true;
    }

    /// <summary>Reads the move rules once per ruleset.</summary>
    private void Read(Rules rules)
    {
        if (ReferenceEquals(_source, rules))
        {
            return;
        }
        _source = rules;
        _repathSeconds = rules.Number("move.ai_repath_seconds", 1.5);
        _repathDistance = rules.Number("move.ai_repath_distance", 2);
        _aiNodes = (int)rules.Number("move.ai_path_max_nodes", 500);
        _stepUp = rules.Number("move.step_up", 0.6);
        _maxDrop = rules.Number("move.max_drop", 3);
    }

    private static float GroundDistance(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
