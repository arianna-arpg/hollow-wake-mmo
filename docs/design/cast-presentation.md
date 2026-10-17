# Confirmed cast presentation

Players, enemies and summons use the same lifecycle facts. Cosmetic presentation never resolves damage, moves a hit region, promises that an attack lands, or determines when another action is legal. Forecast regions are a separate contract in [cast-forecasting.md](cast-forecasting.md).

## Lifecycle

The weave runtime publishes `CastPresentationEvent` through the simulation event bus. A zone-local cast identity is assigned once, without consuming gameplay randomness. A timed or held cast emits `start`; a non-empty effect list beginning execution emits `release`. A channel can release many payloads under one identity. A guard starts before its raise effects execute. Instant casts enter at release. `end` records a completed lifecycle and whether any payload executed; `interrupt` records interruption, death or an externally cleared cast. A rejected or fizzled attempt has no preparation or successful release. No events are emitted just because a tick passed.

An effect context retains that identity through copies, delayed deliveries, projectile contacts and nested `cast` effects. The damage pipeline emits `impact` only for confirmed positive damage that was neither evaded nor blocked. This first vocabulary deliberately has no heal, block or evade flourish. It is a damage contact signal, not proof of life loss: defenses can route damage through other pools. Independently scheduled meta presets and ongoing status ticks without an originating cast context do not manufacture a correlation.

Coordinates are core X east, Y north, Z up. Release carries the current resolved target/point or direction. A contact carries the target position at resolution, so an impact remains there as the body moves. Cast end can precede projectile contact. Recovery is a cosmetic tail after an end with a payload; its duration says nothing about the server's recovery lock, cooldown or next action.

## Wire and interest

`cast_cues` is an optional hello feature. Clients without it receive none of these messages. `CastCueMsg` travels reliable-ordered, with a zone-local sequence, cast identity, source, target, semantic tags, timestamps and resolved positions. No color, renderer primitive or presentation duration is on the wire. A viewer must know the source, and also the target for impacts. Unknown targets and their aim positions are redacted from non-impact cues.

An entity spawn reconstructs an active cast as `sync`. It describes ongoing preparation only; it never replays old release or impact decoration. Clients clear sequence and decoration on world entry and disconnect, remove entity-bound cues on despawn, deduplicate sequences and reject stale flourishes. An explicit end removes preparation even if the following projectile is still in flight. Reliable messages do not make transient effects durable history.

## Shared recipes

`ui_cast_visuals`, registered by the interface content family, is read only by clients. The shipped `default` entry in `content/core/ui/cast-visuals.yaml` defines the bounded effect count and stale/preparation expiry limits. Profiles select by all required semantic tags, ordered by descending priority then ordinal id. Matching profiles lacking a requested phase fall through to a neutral profile. There are no weave-name switches.

Each phase authors pairs of line vertices in local core coordinates, color, body-height anchor, size, world-space stroke width, opacity, rotation rate, expansion and lifetime. Godot and the browser consume the same content and mirror the lifecycle consumer. Conversion uses each client's existing coordinate adapter. Each stroke uses two crossed ribbons so its thickness survives both camera angles. Strokes are depth-tested and cast no shadow; they never draw an area boundary. The Flame preparation gathers short hooks at the body, release directs an open stroke, confirmed contact opens a small ray fan, and recovery breaks into spent fragments. Cancel uses the neutral inward break. Existing authoritative projectile entities continue to describe flight.

Reduced motion keeps static phase geometry and exact event timing while removing decorative rotation, expansion and fading. The cosmetic budget can drop decoration; it never controls cast bars, delivery geometry or future gameplay-critical forecasts. The first slice is an extensible presentation foundation, not a final character animation, particle asset or hardware-performance claim.

## Names and compatibility

The owner selected **Flame**, associated with Fire, as the player-facing Thread name. Content names, descriptions and modifier scopes display that name. The persisted `ember` Thread key, weave IDs, tags, attribute paths and icon mappings remain compatible with existing saves and content. Proper place names and ordinary words for embers are not ID migrations. New profiles join the same Thread through its stable tag.

## Verification

Core regressions exercise timed, instant, guard and repeating channel casts, interruption, external clearing, death, target loss, fizzle and delayed projectile contact. Loopback regressions exercise opt-in negotiation, event order, interest reconstruction and hidden source/target redaction. Both client consumers pin stale-event rejection, deduplication, neutral fallback, cancellation and world resets against the shared recipe. The optional `--flame --calling kindler` client smoke captures preparation, release and confirmed contact through ordinary player input. Release-build timing and allocation evidence belongs in the dated review record, alongside engine captures and any remaining limitations.
