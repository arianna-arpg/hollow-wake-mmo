using HollowWake.Core.Components;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Systems;

/// <summary>
/// Advances casts: timed bars resolve when their time comes, channels beat and ramp, charge-ups fill,
/// overcharges climb their stages, guards hold, and every hold ends by release, by its own limit, by
/// running dry or by interruption. Books whose slots changed are armed first (triggers subscribed, worn
/// modifiers on). Books are visited in the world's order, so casts resolving on the same tick resolve in
/// a stable order. Nothing here allocates in a tick where nothing resolves.
/// </summary>
public sealed class CastingSystem : SimSystem
{
    public override string Name => "casting";
    public override SimPhase Phase => SimPhase.Act;

    public override void Update(Simulation sim)
    {
        var runtime = sim.Service<WeaveRuntime>();
        foreach (var (e, book) in sim.World.Query<WeaveBook>())
        {
            if (book.ArmedVersion != book.Version || book.ArmedCount != book.Slots.Count)
            {
                runtime.EnsureArmed(sim, e, book);
            }
            if (book.Casting is { } cast)
            {
                runtime.Advance(sim, e, book, cast);
            }
        }
        runtime.SweepLive(sim);
    }
}
