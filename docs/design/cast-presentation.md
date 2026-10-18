# Shared action presentation

This is the cosmetic event and recipe contract for both clients. It applies to every actor and compiled weave. Ember is an authored Thread override within that contract. The simulation never reads a visual recipe, and a cosmetic cue never decides damage, timing, reach or a hit region.

The original Hollow Wake ARPG is a required convergence source. [Visual convergence](parity/visual-convergence.md) records its reusable cast, combat and defense language and the families still lacking parity. A neutral fallback means graceful display, not complete visual coverage.

## Confirmed lifecycle and outcomes

`CastPresentationEvent` has a zone-local cast identity allocated without gameplay randomness. Timed and held casts emit `start`; executing a non-empty effect list emits `release`. Guard starts before its raise effects execute. Instant casts enter at release. Channels can release multiple payloads under one identity. `end` reports completion and whether a payload executed; `interrupt` reports interruption, death or external clearing. Rejection and fizzle do not invent a successful cast or hit. Presentation is event-driven, not emitted every animation tick.

The effect context retains identity through copies, delayed work, projectile contacts and nested cast effects. `impact` means confirmed positive damage that was neither evaded nor blocked. It does not imply life loss: defenses can route damage through other pools. Cast end or interruption does not retract a projectile already released. Its later confirmed contact retains the original identity.

`OutcomeCueAdapters` is an open registry over authoritative event types. Positive uncorrelated damage, healing, block, evade, status application/expiry and revival enter the same stream as target-bound outcomes. They use `Outcome=true`, cast id zero and semantic phase names; they never manufacture a cast correlation. Correlated damage already has an impact and is not decorated twice. Beneficial status application has a different tag from harmful application. Zero healing and unsuccessful damage do not acquire success cues. Ongoing status ticks without a cast context can still produce confirmed damage outcomes.

Coordinates are core X east, Y north, Z up. A release carries resolved aim; an impact carries the contact location and stays there as the body moves. An outcome follows the affected body. Recovery is an authored visual tail, not authority for recovery lock, readiness or cooldown.

## Observation, wire and interest

`Simulation.EmitCastPresentation` defaults to false. Zone membership enables it only while at least one session negotiated `cast_cues`. Without observers, the runtime retains identity and terminal bookkeeping for late interest reconstruction but allocates no lifecycle/contact presentation records. Mode names are cached. Tools explicitly observing these events must enable the flag.

`CastCueMsg` is reliable-ordered and opt-in. Its additive fields carry compiled `Mechanisms` and `Outcome`; it contains no color or renderer primitive. A viewer must know the source and, for impacts/outcomes, the affected target. Other phases redact unknown targets and their aim. Sourceless outcomes bind to the target. A new interest view reconstructs only ongoing preparation through `sync`; past releases and contacts are not history to replay.

Clients clear sequence watermarks and decoration on world entry/disconnect, remove entity-bound cues on despawn, deduplicate sequences and expire stale flourishes. An end removes only preparation for its cast, leaving released work eligible for later contact. Both clients have repeated-reset and interruption-after-release conformance tests.

## Composition from content

`ui_cast_visuals` is registered by the interface content family and consumed only by clients. Its budgets bound total live layers, layers per event, stale events and abandoned preparation. Profiles match all required semantic tags and compiled mechanism tags, including dot descendants. Names, weave keys and actor roles are never dispatch keys.

Distinct `layer` values compose. Each layer selects its first applicable profile by descending priority then ordinal id. Profiles marked `fallback` are considered after ordinary profiles and only when no ordinary layer was selected. A validated unfiltered fallback is required for every lifecycle phase and for generic outcomes when outcomes are declared. A known outcome whose specialized profiles do not match can still use that generic fallback. Budgets choose deterministic winners and may omit cosmetic layers.

`thread_phases` is a shared template instantiated from every `ThreadDef`, including content-added Threads. Its color comes from the Thread definition. Generated profile/layer keys use `thread.<id>`; generated profile IDs are reserved. An authored profile with a different ID can override that same layer at higher priority. Ember does this for `thread.ember`. Mixed Threads compose their layers; authors need not enumerate every combination.

Mechanisms come from the compiled weave after glyph processing. They describe capabilities, including nested/conditional paths, not proof that every branch executed on this release. Delivery accents are deliberately small and body-bound. Only authoritative entity state or outcome events can assert flight, a hit, healing or a defense. Glyphs that change geometry must ultimately affect the shared resolved plan described in [cast forecasting](cast-forecasting.md), not scale a cosmetic ring to suggest an unverified hitbox.

Outcome names are authored in `outcomes`. A newly registered authoritative adapter can use a new name and content profile without a skill-specific client branch. Unknown vocabulary receives the generic outcome fallback. Durable states require an appropriate replicated-state contract; one-shot outcomes are insufficient for persistent auras, defensive plates or status stacks.

## Geometry and accessibility

`CastVisualPrimitives` and the matching browser registry produce normalized local geometry. Built-ins are authored segments, rings/arcs, rays and corner strokes. Recipes control piece count, arc, size, body height, stroke width, duration, opacity, expansion and spin. A new primitive requires paired implementations and a shared numeric conformance fixture before content uses it. Validation rejects unregistered primitives and invalid geometry/budgets.

Both renderers use their existing coordinate adapters and crossed ribbons, preserving stroke width across the two camera projections. Geometry is depth-tested and has no shadow. A phase's `show_on_dead` policy can preserve a lethal outcome or a confirmed revival while the preceding dead snapshot is still present. Reduced motion keeps phase geometry and authoritative timing while removing decorative rotation, expansion and fading. Cosmetic budgets and reduced motion never suppress gameplay-critical cast bars or future truthful forecasts.

The original game's inward mend, defensive barrier/ghost, beneficial opening, harmful barb and expiry collapse conventions inform the shared outcome recipes. Source definitions and renderer hashes are preserved in the convergence census. These simple primitives are an initial reusable implementation, not parity with all original sprites, authored effect voices, body animation or persistent defense artwork.

## Names and extension gate

Threads retain their lore names as player-facing names, including **Ember**. Persisted `ember` keys, weave IDs, attribute paths and icon references remain compatible. Presentation selects the stable semantic tag, not the displayed name.

A new skill using supported mechanisms is content work. A new mechanism requires a family-level event/state adapter where necessary, paired rendering support, validation and conformance evidence. The [coverage manifest](../../tests/fixtures/cast-visuals/coverage.json) classifies the captured original vocabulary and current effect registry, including partial/pending work. Tests reject an unclassified current handler, an unclassified refreshed prototype mechanic or a primitive registry mismatch. Every shipped preset must retain valid shared lifecycle presentation. These gates cannot certify unimplemented future behavior or detect separate-repository changes until its census is refreshed.

Verification includes shared composition fixtures, actual delayed damage after interruption, opt-in/interest/privacy tests, both lifecycle consumers, content validation, the seeded duel, live probe, client smokes and Release allocation evidence. Measurements and remaining review obligations belong in the [dated handoff](../reviews/2026-10-17-cast-cues.md).
