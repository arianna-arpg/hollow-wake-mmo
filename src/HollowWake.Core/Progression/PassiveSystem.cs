using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Progression;

/// <summary>
/// Opens realms when what unlocks them happens: a hero who levels, turns in a quest or has a story flag set gets
/// their realms re-checked once that tick.
/// </summary>
public sealed class PassiveSystem : SimSystem
{
    public override string Name => "passives";
    public override SimPhase Phase => SimPhase.React;
    public override int Order => 20;

    private readonly List<Entity> _pending = new();
    private bool _subscribed;

    public override void Update(Simulation sim)
    {
        if (!_subscribed)
        {
            _subscribed = true;
            sim.Events.Subscribe<LevelUp>(e => Mark(e.Entity));
            sim.Events.Subscribe<QuestTurnedIn>(e => Mark(e.Entity));
            sim.Events.Subscribe<StoryFlagSet>(e => Mark(e.Entity));
        }
        if (_pending.Count == 0)
        {
            return;
        }
        var progression = sim.Service<ProgressionService>();
        foreach (var hero in _pending)
        {
            if (sim.World.IsAlive(hero))
            {
                progression.RefreshRealms(sim, hero);
            }
        }
        _pending.Clear();
    }

    private void Mark(Entity hero)
    {
        if (!_pending.Contains(hero))
        {
            _pending.Add(hero);
        }
    }
}
