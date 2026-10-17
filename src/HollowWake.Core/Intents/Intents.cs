namespace HollowWake.Core.Intents;

/// <summary>
/// What a player wants to do. Intents arrive from the network, are queued on the player entity, and
/// are consumed by the intent system during the input phase. They are requests, never results: the
/// simulation validates every one. Each area declares its intents in its own file (movement, weaves,
/// items, progression, the world) and its handlers through the intent handler registry.
/// </summary>
public interface IIntent
{
}
