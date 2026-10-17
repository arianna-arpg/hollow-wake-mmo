using System.Numerics;

namespace HollowWake.Core.Intents;

public sealed record MoveToIntent(Vector3 Point) : IIntent;

public sealed record MoveDirIntent(Vector2 Direction) : IIntent;

public sealed record StopIntent : IIntent;

public sealed record JumpIntent : IIntent;
