# Networking and persistence

## Sessions

A connection becomes a session. `hello` negotiates the protocol; `login` authenticates (development: any name; token mode: a shared secret) and returns the account's characters and the available callings; `create_character` and `select_character` manage characters; after `enter_world` every in-world message becomes an intent queued on the player entity and validated by the simulation. Pings answer with server time. Chat broadcasts within the zone.

## Zones and interest

A `ZoneInstance` ticks its simulation and, at the snapshot rate, asks its `SnapshotBuilder` what each session needs to hear about the entities within its interest radius (a column: the radius on the ground plane and `server.interest_height` up and down). Each session keeps a `SnapshotView` of what it has been told: the entities it knows, whether each is static, the fingerprint of the state last sent, and when each is next due a refresh. The rule is to send what changed and nothing else. Simulation events become combat events, notices, quest offers, crafting challenges and results, and intent rejections for the sessions that can see the entities involved.

**Spawns and despawns.** An entity entering interest is spawned with its full state; one leaving it, or destroyed, is despawned. A spawn for a key the client already knows replaces that entity's state.

**Static entities.** An entity with none of the components that move it or change what a client sees (stats, motion, projectiles, fields; more can be registered with `Snapshots.MarkDynamic`) is static: flora, props, markers, founts and drops. It is spawned once with `static: true` (the field is absent when false) and never appears in an update. Static entities are re-verified on the refresh cadence: one that died is despawned, and one that changed or became dynamic is spawned again.

**Updates.** `entity_update` carries only the dynamic entities whose state changed since the state last sent to that session. Change is judged by a fingerprint of everything the state carries: position, size and velocity to the centimetre, yaw to a hundredth of a radian, resources in steps of `server.snapshot_resource_step` of their maximum (at most a point at the default step), cast progress in `server.snapshot_cast_steps` steps per cast, and every other field exactly. Values on the wire use the same precision (resources to a tenth, cast progress to a hundredth). An entity that does not change is not sent, so a standing player hears nothing about itself, and an update says nothing about entities it does not list. Each state is as of the `server_time` of the message that carried it.

**Refreshes.** Unreliable transports may drop updates, so an entity that changed since its last refresh is sent in full on its refresh cadence (`server.snapshot_refresh_seconds`), including once after it goes quiet, and every known dynamic entity is sent in full on the sweep cadence (`server.snapshot_sweep_seconds`). Cadences are staggered by entity key, so refreshes spread across snapshots.

**Deltas.** A session that understands `entity_delta` gets changes as deltas instead: each carries the key and only the fields that changed (absent means unchanged). Strings that can become empty (pattern, look, faction, rarity, interactable, casting) arrive as "" when they do; flags and statuses arrive whole when they change; resources and maxima carry only the entries that changed. Full states still arrive in `entity_update` on refreshes and when a delta cannot express a change (the set of resources changed). A delta for a key the client does not know is ignored; the next refresh repairs it. Deltas are opted into per session (`SnapshotView.SendDeltas`); the hello does not negotiate them yet, so clients get full-state updates unless the server enables deltas for them.

**Self state.** The owner's view of their character (level, attributes, resources, weaves with cooldowns and costs, bag, equipment, currency, quests, reputation, holdings) is checked every `server.self_state_seconds` and sent when its fingerprint changes, and at least every `server.self_state_max_seconds`. The fingerprint covers what the message reads, with cooldowns counted down in steps of the check interval so a cooldown still visibly ticks; attribute values are reused while the attribute set is unchanged.

**Cost.** Building snapshots never changes the simulation, so each entity is fingerprinted and captured at most once per tick however many sessions see it. Sessions count the payload bytes they are sent, the part that is snapshot traffic, and the snapshots built for them; leaving a zone logs the snapshot bytes per snapshot. The benchmark and tools can drive a `SnapshotBuilder` with their own views the same way a zone does.

Walking out of a zone or using a portal transfers the player: the sheet is captured, the entity despawned, the neighbor zone created if needed, the entity materialized there, and the client told to re-enter. Zones with no players unload after a timeout (the start zone stays).

## World events

On an interval the host picks a loaded region and a weighted event definition, adds its generation overlay over the region for the event's duration, and tells everyone. Active events and their end times persist.

## Transports

`ITransport` raises connected, received and disconnected from any thread into a thread-safe inbox drained on the world thread. TCP frames messages by length; loopback runs in process for tests; RakNet (BRUTAL host) sends reliable-ordered for everything but entity updates and deltas, which go unreliable-sequenced (the refresh cadence repairs what is dropped).

## Persistence

Store interfaces: accounts, characters, world (holdings, events, the item uid counter) and modified chunks. The JSON file implementation writes one file per record under the data directory with atomic replace. Autosave runs on a timer and on shutdown; characters save on leave and on autosave. A database implementation of the same interfaces is the planned next step.
