using System.Numerics;
using HollowWake.Core.Ecs;

namespace HollowWake.Core.Intents;

public sealed record InteractIntent(Entity Target) : IIntent;

public sealed record AcceptQuestIntent(string QuestId, Entity Giver) : IIntent;

public sealed record TurnInQuestIntent(string QuestId, Entity Giver) : IIntent;

public sealed record BindFountIntent(Entity Fount) : IIntent;

public sealed record EnactDecreeIntent(string HoldingId, string DecreeId) : IIntent;

public sealed record BuildStructureIntent(string HoldingId, string StructureId, Vector3 Point) : IIntent;

public sealed record RespawnIntent : IIntent;
