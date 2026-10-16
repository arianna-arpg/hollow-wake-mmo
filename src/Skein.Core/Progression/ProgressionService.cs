using Skein.Core.Components;
using Skein.Core.Definitions;
using Skein.Core.Ecs;
using Skein.Core.Expressions;
using Skein.Core.Sim;

namespace Skein.Core.Progression;

/// <summary>Experience, levels, and the passive constellation. Curves come from progression rules.</summary>
public sealed class ProgressionService
{
    public double XpToNext(Simulation sim, int level) =>
        Math.Max(1, sim.Content.Rules.Eval("progression.xp_to_next", "round(80 * level ^ 1.6)", new DictScope().Set("level", level)));

    public int MaxLevel(Simulation sim) => (int)sim.Content.Rules.Number("progression.max_level", 60);

    public int PassivePointsFor(Simulation sim, int level) =>
        (int)Math.Max(0, sim.Content.Rules.Eval("progression.passive_points", "level - 1", new DictScope().Set("level", level)));

    public void GrantXp(Simulation sim, Entity e, double amount)
    {
        if (amount <= 0 || !sim.World.TryGet<Stats>(e, out var stats) || !sim.World.Has<Player>(e))
        {
            return;
        }
        var bonus = stats.Get("xp.gain");
        amount *= Math.Max(0, 1 + bonus);
        stats.Xp += amount;
        sim.Events.Publish(new ExperienceGained(e, amount, stats.Xp) { Tick = sim.Clock.Tick });
        var max = MaxLevel(sim);
        var guard = 0;
        while (stats.Level < max && stats.Xp >= XpToNext(sim, stats.Level) && guard++ < 200)
        {
            stats.Xp -= XpToNext(sim, stats.Level);
            stats.Level++;
            if (sim.Content.Rules.Flag("progression.heal_on_level", true))
            {
                stats.FillResources();
            }
            sim.Events.Publish(new LevelUp(e, stats.Level) { Tick = sim.Clock.Tick });
        }
    }

    public int AvailablePoints(Simulation sim, Stats stats) => PassivePointsFor(sim, stats.Level) - stats.PassivePointsSpent;

    public bool CanAllocate(Simulation sim, Stats stats, string nodeId, out string reason)
    {
        reason = "";
        if (!sim.Content.TryGet<PassiveNodeDef>(nodeId, out var node) || node is null)
        {
            reason = "unknown_node";
            return false;
        }
        if (stats.Passives.Contains(nodeId))
        {
            reason = "already";
            return false;
        }
        if (AvailablePoints(sim, stats) < node.Cost)
        {
            reason = "no_points";
            return false;
        }
        if (node.Kind == PassiveKind.Start)
        {
            reason = "start_node";
            return false;
        }
        var allocated = new HashSet<string>(stats.Passives, StringComparer.Ordinal);
        var connected = node.Links.Any(allocated.Contains) || sim.Content.Each<PassiveNodeDef>().Any(n => allocated.Contains(n.Id) && n.Links.Contains(nodeId));
        if (!connected)
        {
            reason = "not_connected";
            return false;
        }
        return true;
    }

    public bool Allocate(Simulation sim, Entity e, string nodeId, out string reason)
    {
        if (!sim.World.TryGet<Stats>(e, out var stats))
        {
            reason = "no_stats";
            return false;
        }
        if (!CanAllocate(sim, stats, nodeId, out reason))
        {
            return false;
        }
        var node = sim.Content.Require<PassiveNodeDef>(nodeId);
        stats.Passives.Add(nodeId);
        stats.PassivePointsSpent += node.Cost;
        stats.Attributes.AddRange(node.Modifiers, "passive:" + nodeId);
        sim.Events.Publish(new PassiveAllocated(e, nodeId) { Tick = sim.Clock.Tick });
        return true;
    }

    /// <summary>Re-applies every allocated node's modifiers; used when a character is materialized.</summary>
    public void ApplyAllocated(Simulation sim, Stats stats)
    {
        foreach (var id in stats.Passives)
        {
            if (sim.Content.TryGet<PassiveNodeDef>(id, out var node) && node is not null)
            {
                stats.Attributes.RemoveSource("passive:" + id);
                stats.Attributes.AddRange(node.Modifiers, "passive:" + id);
            }
        }
    }

    public void Reset(Simulation sim, Entity e)
    {
        if (!sim.World.TryGet<Stats>(e, out var stats))
        {
            return;
        }
        foreach (var id in stats.Passives.ToList())
        {
            if (sim.Content.Get<PassiveNodeDef>(id)?.Kind == PassiveKind.Start)
            {
                continue;
            }
            stats.Attributes.RemoveSource("passive:" + id);
            stats.Passives.Remove(id);
        }
        stats.PassivePointsSpent = 0;
    }
}
