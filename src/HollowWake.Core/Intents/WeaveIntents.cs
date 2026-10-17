using System.Numerics;
using HollowWake.Core.Ecs;

namespace HollowWake.Core.Intents;

public sealed record UseWeaveIntent(int Slot, Entity Target, Vector3 Point, Vector2 Direction) : IIntent;

public sealed record CancelCastIntent : IIntent;

public sealed record SetWeaveIntent(int Slot, Definitions.WeaveSpec Spec) : IIntent;

/// <summary>Lets go of a held weave: ends a channel's hold or releases a charge-up.</summary>
public sealed record ReleaseWeaveIntent(int Slot) : IIntent;

/// <summary>A second press of a weave's key while it is casting: strike timing, salvo beats.</summary>
public sealed record PressWeaveIntent(int Slot) : IIntent;

/// <summary>The live aim while a held or tracking cast runs: a point on the ground and a facing.</summary>
public sealed record AimIntent(Vector3 Point, Vector2 Direction) : IIntent;

/// <summary>The second ability on a slot: the form's meta first, then each glyph's in socket order, one beat apart.</summary>
public sealed record UseMetaIntent(int Slot) : IIntent;

/// <summary>Swaps two rack seats; the rack order is the weave bar order.</summary>
public sealed record SwapWeaveSlotsIntent(int A, int B) : IIntent;
