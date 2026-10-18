using System.Text.Json.Serialization;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Definitions;

/// <summary>Account entry policy. A later tutorial registers a stage and records its completion on the server.</summary>
public sealed record EntryFlowDef : Definition
{
    public bool Default { get; init; }
    [JsonPropertyName("character_limit")]
    public int CharacterLimit { get; init; }
    public List<EntryStageDef> Stages { get; init; } = new();
}

public sealed record EntryStageDef
{
    public string Id { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Presentation { get; init; } = "";
    /// <summary>All these exact account tags must be present to skip an already completed stage. Empty never skips.</summary>
    [JsonPropertyName("completed_by")]
    public TagSet CompletedBy { get; init; } = TagSet.Empty;
}

/// <summary>Local entry presentation, never read by simulation or account policy.</summary>
public sealed record UiEntrySceneDef : Definition;
