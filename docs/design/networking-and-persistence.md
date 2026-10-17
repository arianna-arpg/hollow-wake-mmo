# Networking and persistence

## Sessions

A connection becomes a session. `hello` negotiates the protocol; `login` authenticates (development: any name; token mode: a shared secret) and returns the account's characters and the available callings; `create_character` and `select_character` manage characters; after `enter_world` every in-world message becomes an intent queued on the player entity and validated by the simulation. Pings answer with server time. Chat broadcasts within the zone.

## Zones and interest

A `ZoneInstance` ticks its simulation and, at the snapshot rate, sends each session the entities within its interest radius: spawns for newly visible entities, full states for known ones, despawns for those that left. A self state (level, attributes, resources, weaves with cooldowns and costs, bag, equipment, currency, quests, reputation, holdings) goes twice a second. Simulation events become combat events, notices, quest offers, crafting challenges and results, and intent rejections for the sessions that can see the entities involved.

Walking out of a zone or using a portal transfers the player: the sheet is captured, the entity despawned, the neighbor zone created if needed, the entity materialized there, and the client told to re-enter. Zones with no players unload after a timeout (the start zone stays).

## World events

On an interval the host picks a loaded region and a weighted event definition, adds its generation overlay over the region for the event's duration, and tells everyone. Active events and their end times persist.

## Transports

`ITransport` raises connected, received and disconnected from any thread into a thread-safe inbox drained on the world thread. TCP frames messages by length; loopback runs in process for tests; RakNet (BRUTAL host) sends reliable-ordered for everything but entity updates, which go unreliable-sequenced. WebSocket (`HttpListener`, port `websocket_port`, path `/ws`) carries one whole message per binary frame with no length prefix; text frames and messages over the serializer's size limit close the connection, and a client that falls behind loses queued entity updates before anything else. The same listener serves the built web client from `web_root`, falling back to `index.html` for routes and never serving outside the root or dot files, and answers a plain GET on `/ws` with a JSON description of the service so a page it serves can find it. `CompositeTransport` puts several transports behind one, re-numbering connections so session ids stay unique; the pure host runs TCP and WebSocket that way unless `websocket_enabled` is false.

## Terrain on the wire

Entering a zone sends `chunk_data` for every chunk the simulation has modified. `request_chunk` returns any chunk within `chunk_request_radius` chunks of the player (farther requests get a `chunk_out_of_range` error, so a client cannot make the world thread generate and keep land nobody is near); the web client builds all of its terrain this way. `chunk_data` carries heights, cell flags, the interned surface tag strings, layers and the chunk's water level.

## Persistence

Store interfaces: accounts, characters, world (holdings, events, the item uid counter) and modified chunks. The JSON file implementation writes one file per record under the data directory with atomic replace. Autosave runs on a timer and on shutdown; characters save on leave and on autosave. A database implementation of the same interfaces is the planned next step.
