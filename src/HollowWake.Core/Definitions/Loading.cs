namespace HollowWake.Core.Definitions;

/// <summary>Local loading presentation. All activity parameters and art live in meta; no simulation reads it.</summary>
public sealed record UiLoadingScreenDef : Definition
{
    public bool Default { get; init; }
}
