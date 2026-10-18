using HollowWake.Core.Definitions;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Characters;

/// <summary>Open stage capabilities. New kinds register here and implement their server/client lifecycle.</summary>
public static class EntryStageKinds
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> Selection = new(StringComparer.Ordinal) { ["vessels"] = true };
    public static void Register(string kind, bool allowsSelection)
    {
        if (!Selection.TryAdd(kind, allowsSelection)) throw new ArgumentException($"Entry stage kind already registered: {kind}");
    }
    public static bool Has(string kind) => Selection.ContainsKey(kind);
    public static bool AllowsSelection(string kind) => Selection.GetValueOrDefault(kind);
}

public static class EntryPolicy
{
    public static EntryStageDef? Current(EntryFlowDef flow, TagSet facts) => flow.Stages.FirstOrDefault(s =>
        s.CompletedBy.IsEmpty || !s.CompletedBy.All(facts.HasExact));
    public static bool Available(CallingDef calling, TagSet facts) => facts.HasAll(calling.AccountRequirements);
}
