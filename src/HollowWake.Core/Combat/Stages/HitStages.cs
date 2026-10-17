using System.Collections.Concurrent;

namespace HollowWake.Core.Combat.Stages;

/// <summary>What a stage tells the pipeline: go on to the next stage, or end the hit here.</summary>
public enum HitFlow
{
    Continue,
    /// <summary>The hit ends: nothing later runs, and only a stage that published the hit has published it.</summary>
    Stop,
}

/// <summary>
/// One rung of the defensive ladder every hit walks: immunity, guard, evasion, block, mitigation, damage
/// taken, the pools, culling. Stages are registered by name; rule <c>combat.stages</c> orders them and rule
/// <c>combat.stage_skips</c> skips some for hits carrying a tag. A stage reads and changes the hit through
/// its <see cref="HitScope"/>.
/// </summary>
public interface IHitStage
{
    string Name { get; }
    HitFlow Run(HitScope hit);
}

/// <summary>
/// The registry of hit stages by name. Built-in stages register here; packages and mods add theirs with
/// <see cref="Register"/>. Steps that are not stages but can still be skipped by tag (the attacker's
/// <c>crit</c> roll) are declared with <see cref="DeclareStep"/> so rule validation knows them.
/// </summary>
public static class HitStages
{
    private static readonly ConcurrentDictionary<string, IHitStage> Stages = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, bool> Steps = new(StringComparer.Ordinal);

    /// <summary>The order rule <c>combat.stages</c> falls back to: today's ladder.</summary>
    public static readonly IReadOnlyList<string> DefaultOrder = new[] { "immunity", "guard", "evade", "block", "mitigate", "taken", "pools", "cull" };

    static HitStages()
    {
        Register(new ImmunityStage());
        Register(new GuardStage());
        Register(new EvadeStage());
        Register(new BlockStage());
        Register(new MitigateStage());
        Register(new TakenStage());
        Register(new PoolsStage());
        Register(new CullStage());
        DeclareStep("crit");
    }

    public static void Register(IHitStage stage) => Stages[stage.Name] = stage;

    public static bool TryGet(string name, out IHitStage stage) => Stages.TryGetValue(name, out stage!);

    public static IEnumerable<string> Names => Stages.Keys.OrderBy(k => k, StringComparer.Ordinal);

    /// <summary>Declares a step inside a stage, or outside the ladder, that rule <c>combat.stage_skips</c> may name.</summary>
    public static void DeclareStep(string name) => Steps[name] = true;

    /// <summary>Whether <c>combat.stage_skips</c> may name this: a stage or a declared step.</summary>
    public static bool IsSkippable(string name) => Stages.ContainsKey(name) || Steps.ContainsKey(name);
}
