using HollowWake.Core.Components;
using HollowWake.Core.Crafting;
using HollowWake.Core.Ecs;
using HollowWake.Core.Items;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Dialogue;

/// <summary>
/// Closes the world panes a hero walked away from: a conversation, a vendor or a station whose entity is gone, dead or
/// out of reach. Checked every rule <c>dialogue.check_seconds</c>.
/// </summary>
public sealed class PaneSystem : SimSystem
{
    public override string Name => "panes";
    public override SimPhase Phase => SimPhase.React;
    public override int Order => 21;

    private double _nextAt = double.NegativeInfinity;
    private readonly List<(Entity Hero, Panes Panes)> _open = new();

    public override void Update(Simulation sim)
    {
        if (sim.Clock.Time < _nextAt)
        {
            return;
        }
        _nextAt = sim.Clock.Time + Math.Max(0.05, sim.Content.Rules.Number("dialogue.check_seconds", 0.5));
        // Closing publishes events and may touch components, so the query is copied first.
        _open.Clear();
        foreach (var (hero, panes) in sim.World.Query<Panes>())
        {
            if (panes.Conversation is not null || panes.Vendor is not null || panes.Station is not null)
            {
                _open.Add((hero, panes));
            }
        }
        foreach (var (hero, panes) in _open)
        {
            if (panes.Conversation is { } talk && !DialogueService.InReach(sim, hero, talk.Source, talk.Reach))
            {
                sim.Service<DialogueService>().Close(sim, hero, "departed");
            }
            if (panes.Vendor is { } vendor && !DialogueService.InReach(sim, hero, vendor.Source, vendor.Reach))
            {
                sim.Service<VendorService>().Close(sim, hero, "departed");
            }
            if (panes.Station is { } station && !DialogueService.InReach(sim, hero, station.Source, station.Reach))
            {
                sim.Service<StationService>().Close(sim, hero, "departed");
            }
        }
        _open.Clear();
    }
}
