using HollowWake.Core.Definitions;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Characters;

/// <summary>Open stage capabilities. New kinds register here and implement their server/client lifecycle.</summary>
public static class EntryStageKinds
{
    private static readonly Dictionary<string, bool> Selection = new(StringComparer.Ordinal) { ["vessels"] = true };
    public static void Register(string kind, bool allowsSelection) => Selection.Add(kind, allowsSelection);
    public static bool Has(string kind) => Selection.ContainsKey(kind);
    public static bool AllowsSelection(string kind) => Selection.GetValueOrDefault(kind);
}

public static class EntryPolicy
{
    public static EntryStageDef? Current(EntryFlowDef flow, TagSet facts) => flow.Stages.FirstOrDefault(s =>
        s.CompletedBy.IsEmpty || !facts.HasAll(s.CompletedBy));
    public static bool Available(CallingDef calling, TagSet facts) => facts.HasAll(calling.AccountRequirements);
}
