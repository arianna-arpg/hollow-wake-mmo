using System.Collections.Concurrent;
using HollowWake.Core.Effects;

namespace HollowWake.Core.Weaves;

/// <summary>A selector that validates each selected body's position and LOS before applying its payload.
/// This is an execution capability, independent of whether a forecast provider is available.</summary>
public interface IReleaseTargetSelector
{
    bool ChecksLineOfSight { get; }
}

public delegate bool ReleaseCheck(EffectContext context, out string reason);

public sealed record ReleasePolicy(string Name, ReleaseCheck Check,
    Func<IReadOnlyList<CompiledEffect>, bool> AcceptsGraph);

/// <summary>Release policies are resolved by the compiler, never by skill identity at application.</summary>
public static class ReleasePolicies
{
    private static readonly ConcurrentDictionary<string, ReleasePolicy> Policies = new(StringComparer.Ordinal);
    static ReleasePolicies()
    {
        Register(new("body", ReleaseTargets.CheckBody, static _ => true));
        Register(new("delivery", static (EffectContext _, out string reason) => { reason = ""; return true; },
            static graph => graph.All(effect => effect is IReleaseTargetSelector { ChecksLineOfSight: true })));
    }
    public static void Register(ReleasePolicy policy) => Policies[policy.Name] = policy;
    public static bool TryGet(string name, out ReleasePolicy policy) => Policies.TryGetValue(name, out policy!);
    public static ReleasePolicy Body => Policies["body"];
}
