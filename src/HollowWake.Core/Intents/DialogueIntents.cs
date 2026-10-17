using HollowWake.Core.Ecs;

namespace HollowWake.Core.Intents;

/// <summary>Picks an open choice of the conversation's current line; a choice read from another line is refused.</summary>
public sealed record ChooseDialogueIntent(string Conversation, string Choice, string? Node) : IIntent;

/// <summary>Ends the hero's conversation.</summary>
public sealed record CloseDialogueIntent(string Conversation) : IIntent;

/// <summary>Closes a world pane (a vendor, a station, a stash) the entity opened.</summary>
public sealed record ClosePaneIntent(string Pane, Entity Source) : IIntent;
