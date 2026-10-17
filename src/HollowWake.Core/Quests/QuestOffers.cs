using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Interaction;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Quests;

/// <summary>What a quest giver has for a hero: the quests it offers and those ready to turn in, published as one event.</summary>
public static class QuestOffers
{
    public static void Publish(Simulation sim, Entity hero, Entity giver)
    {
        if (!sim.TryService<QuestService>(out var quests) || !sim.World.TryGet<Identity>(giver, out var id))
        {
            return;
        }
        var interactable = sim.World.Get<Interactable>(giver) ?? new Interactable();
        var offers = quests.Offers(sim, hero, interactable, id).Select(q => q.Id).ToList();
        var turnIns = new List<string>();
        if (sim.World.TryGet<QuestLog>(hero, out var log))
        {
            foreach (var (qid, p) in log.Quests)
            {
                if (p.State != QuestState.Complete)
                {
                    continue;
                }
                var def = quests.Find(sim, qid);
                if (def?.Giver is not null && (def.Giver == interactable.Ref || def.Giver == id.Pattern))
                {
                    turnIns.Add(qid);
                }
            }
        }
        sim.Events.Publish(new QuestsOffered(hero, giver, offers, turnIns) { Tick = sim.Clock.Tick });
    }
}
