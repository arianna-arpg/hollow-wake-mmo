# Web client

The browser client is presentation, like the desktop client. It never resolves gameplay: it renders what the server says and sends intents the server may refuse. It lives in `web/` (Vite, TypeScript, three.js) and talks to the server over the WebSocket transport.

## Shape

`src/app.ts` is the root, the twin of the desktop `Main`: it owns the connection, the terrain and entity views, the camera rig and the UI, handles messages and turns input into intents. `net/` holds the generated message types, the codec and the socket; `world/` the chunk store, terrain colours and meshes, entity bodies and snapshot interpolation; `view/camera.ts` both camera modes; `input/input.ts` the key and mouse bindings; `ui/` the login panel, the HUD and server address handling. `world/coords.ts` is the only place that converts coordinates: the simulation is X east, Y north, Z up, three.js is X east, Y up, Z south, so a simulation point (x, y, z) is drawn at (x, z, -y).

## Connecting

The login panel takes a host, a port, a secure (wss) switch, an account and an optional token for servers in token auth. The host field also takes a whole `wss://`, `ws://`, `https://` or `http://` address, which is what a Codespace gives you. The fields are filled in this order: a `?server=` link (and `?account=`), then `server.host` from config.json, then a game server at the page's own origin, then the last server used in this browser. A page served by the game server finds it at its own origin: a plain GET on the socket path answers with a small JSON description (`service`, `protocol`, `socket`, `name`) instead of an upgrade.

Each WebSocket binary frame carries one whole message, with no length prefix, encoded exactly as `MessageSerializer` writes it: JSON with snake_case fields and the `t` discriminator, which the client always writes first because the server's reader requires it there. The server closes the socket on a text frame or an oversize message. C# `long` values (entity keys, item uids, nonces, the seed) are `Int64` in TypeScript: a number when it is a safe JavaScript integer, otherwise its exact decimal string, which the decoder keeps through JSON.parse source access and the server accepts back in either form. The session runs as on the desktop: `hello` (with the features the client understands), `login`, the character list, `create_character` or `select_character`, then `enter_world` and everything after it.

## Terrain

A browser cannot run the C# generator, so where the desktop client generates a chunk this one asks for it: `request_chunk` for every chunk within the view radius plus one ring, nearest first, a few in flight at a time, with a timeout and a later retry for requests that get no answer. The server answers with `chunk_data` and refuses chunks farther from the player than `chunk_request_radius`. Chunks the server reports as modified on entering a zone arrive the same way and replace what is there. Meshes outside the view radius plus two rings are dropped, and chunk data outside plus three.

Meshes are a port of the desktop `TerrainMesh`: one quad per cell, corner heights averaged across chunk borders so neighbours meet (a mesh is built once its eight neighbours are here or given up on), smooth normals from the height field, vertical skirts wherever a flattened or structure cell steps against a neighbour, extra quads for layered surfaces such as bridges and floors, and a translucent water plane at the chunk's `water_level`. Colours come from each cell's surface tag strings and cell flags through rules in config.json: structure cells take the first matching structure rule; other cells take the first matching base rule (snow, ash, grey, bog, sand, rock, forest floor, scrub, heath, meadow, grass) and then every matching tint in order (road, cliff, shallow and deep water, blocked ground). Mixing is done in sRGB like the desktop's `Lerp` and `Darkened`, then converted to linear for the renderer. Click rays are marched against the height field of built chunks only.

## Entities

Bodies are chosen by what an entity is, by the same rules as the desktop `EntityView`: capsules for creatures, a trunk and canopy for trees, boxes for other props and structures, spheres for projectiles, cylinders for drops, fields, markers and founts. Colours come from config.json by player, drop rarity, kind, flora and faction, then rarity. A label in screen space carries the name, the level for creatures, a dead mark, cast progress and statuses, and a life bar. The mouse picks against the drawn bodies rather than the simulation radius (a marker's radius covers its whole place). Creatures and interactables win over plain props under the cursor, so a canopy in front of a hob does not take the hover from it, and among overlapping bodies the one the cursor is nearest the middle of wins, so a quest giver beside a marker is the one you get when you point at the quest giver.

An `entity_spawn` for a known key replaces its state; static entities (flora, props, markers, founts, drops) arrive once with `static` set and are never updated, and an `entity_update` lists only the entities that changed, so one it does not list keeps the state it had. The hello asks for the features in `server.features` (`entity_delta` by default), and with it granted, changes arrive as `entity_delta` messages applied to the known state exactly as `EntityDelta.ApplyTo` does on the desktop (`net/delta.ts`); a delta for an unknown entity is ignored until the next refresh. A `?full-states` link asks for no features, for comparison.

Other entities are drawn a little in the past at the server's time, blended between the two snapshots around it, and run on with their velocity for a short while past the newest one. The server clock is estimated from snapshot times and smoothed against jitter. The player's own body leads instead: its last known position plus velocity, smoothed, as on the desktop.

## Camera

The same two modes as the desktop `CameraRig`. Isometric: orthographic, fixed pitch, yaw in steps with `Q` and `E`, follows the player, scroll to zoom. Third person: perspective behind the character, orbit with the right mouse button, scroll for distance, `Q` and `E` in smaller steps. `V` toggles.

## Input

Input becomes the same intents the desktop client sends. Bindings are `KeyboardEvent.code` values in config.json.

| Input | Intent |
|---|---|
| `W` `A` `S` `D` held | `move_dir` relative to the camera's ground forward, `stop` on release; sent only when the direction changes |
| Left click | `interact` (or `pick_up` for a drop) on an entity under the cursor, otherwise `move_to` the ground point under it |
| `1` to `8` | `use_weave` on that slot, target the hovered entity, aimed at the ground under the cursor |
| `Space` | `jump` |
| `F` | `interact` with the nearest interactable within reach |
| `G` | `pick_up` the nearest drop within reach |
| `R` | `respawn` |
| `Escape` | `cancel_cast`, and closes the quest panel |
| `Enter` | focuses chat; `Enter` again sends `chat` to the zone |
| `C`, `V`, `Q`, `E`, wheel | the sheet and the camera, nothing sent |

Keys typed into a text field never reach the game, and when the page loses focus held movement stops.

## UI

Placeholder, as on the desktop: resource bars, a weave bar with cooldowns and costs, a target line for the hovered entity, notices, a chat and combat log, a character sheet (attributes, resistances, gear, bag, currency, quests) and a quest offer panel with accept and turn-in buttons. Every string from the server goes into the page as text, never as markup.

## Configuration

`web/public/config.json` is the one source of client settings: it is compiled in as the defaults and fetched again beside the page at startup, so an operator can retune a deployed client by editing `dist/config.json` without rebuilding. Sections: `server` (default address, socket path, whether to look for a server at the page's origin, hello features), `world` (view radius, requests in flight, timeouts, meshes built per frame, ping interval), `camera`, `motion` (interpolation delay, extrapolation cap, own-body smoothing), `input` (bindings, interaction reach), `render` (pixel ratio, antialiasing, shadows, sky, fog, sun and ambient light), `terrain` (the colour rules above), `entities` (body and label colours) and `ui`. A field left out of the runtime file keeps its default.

## The protocol module

`web/src/net/protocol.ts` is generated from `HollowWake.Protocol` by `hollowwake protocol-ts` (`TypeScriptContract`): an interface per message and per nested record, with field names exactly as the serializer writes them, and a union of every message keyed by `t`. `ProtocolContractTests` fails while the committed module is stale and checks that every key a real message puts on the wire is declared. Adding or changing a message means regenerating the module in the same change.

## Building and hosting

`npm --prefix web ci` installs the pinned dependencies; `npm --prefix web run build` type-checks and writes `web/dist`; `npm --prefix web test` runs the unit tests; `npm --prefix web run dev` serves the client with hot reload and proxies the socket to a local server. The build uses relative asset paths, so the same `dist` works served by the game server at `/` (the WebSocket transport serves `web_root` beside the socket) and under a repository path on GitHub Pages. `.github/workflows/pages.yml` builds and tests the client and publishes it from main when Pages is available; the Pages copy does not look for a server at its own origin, and the player points it at a running server. [Codespaces](../infrastructure/codespaces.md) covers playing from a codespace.

## Running and checking

`npm --prefix web run smoke -- --url http://localhost:7778/ --shots <dir>` drives the client in an installed Edge or Chrome through playwright-core against a running server: it logs in, creates or picks a character called Smoke, enters the world, waits for the terrain, then checks held movement, jumping, click-to-move, the quartermaster's quest offer, both cameras, the sheet, chat and weaves on the nearest hob through real keyboard and mouse input, saving a screenshot at each stage. Every check prints a `SMOKE PASS` or `SMOKE FAIL` line and the process exits non-zero when any check fails. The page exposes read-only state for it as `window.hollowWake`.

## Still rough

Placeholder on purpose, and worth knowing before judging a screenshot: labels of things behind the HUD show through it; trees and walls hide the player from the isometric camera; `F` takes the nearest interactable, so a villager standing between you and the quartermaster answers first (click the quartermaster instead); the browser downloads every chunk it shows, tens of kilobytes each; the server speaks plain HTTP and WebSocket, so TLS comes from whatever is in front of it (the Codespaces forwarder, a reverse proxy); on Windows the server listens on this machine only unless a URL ACL allows the wildcard prefix.
