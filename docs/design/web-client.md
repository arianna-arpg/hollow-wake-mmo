# Web client

Identity and the playable startup/entry cover follow [loading.md](loading.md). The small boot module paints the Crossing before importing the world client; its content and reference traces are shared with the native client.

The browser client is presentation, like the desktop client. It never resolves gameplay: it renders what the server says and sends intents the server may refuse. It lives in `web/` (Vite, TypeScript, three.js) and talks to the server over the WebSocket transport.

The login insignia and browser favicon are synchronized from `assets/branding` by `scripts/sync-branding.mjs`, shared with the desktop client and launcher. Their relative URLs also work under the repository's Pages path.

## Shape

`src/app.ts` is the root, the twin of the desktop `Main`: it owns the connection, the terrain and entity views, the camera rig, the interface and the player's settings, handles messages and turns input into intents. `net/` holds the generated message types, the codec and the socket; `world/` the chunk store, terrain colours and meshes, entity bodies, delivery shapes and snapshot interpolation; `view/camera.ts` both camera modes; `input/input.ts` the keys, mouse and controller; `ui/` the interface of [ui.md](ui.md) and the login panel. In `ui/`, `hud.ts` orchestrates as the desktop's `Hud` does; `cluster.ts`, `overlay.ts`, `core.ts`, `bookView.ts`, `board.ts` and `items.ts` draw the cluster, the plate, notices and chat, the core pane, books, boards and item cards; `pages/` holds the development pages and `panes/` the world panes and Options; `ui/logic/` holds the interface's logic as plain modules with tests, the same as the desktop's `UI/Logic` (content reading, the book rule, the workspace, placement, the menu fold, the hero's states, options and settings). `world/coords.ts` is the only place that converts coordinates: the simulation is X east, Y north, Z up, three.js is X east, Y up, Z south, so a simulation point (x, y, z) is drawn at (x, z, -y).

## Connecting

`?play=1` opts into automatic connection after the fields are filled. It resumes the last selected character for that exact server/account, or the only character when there is no remembered choice. Empty and ambiguous character lists stay in the normal creation/selection panel. No character or Calling is invented, tokens are not persisted, and the server still authenticates and selects the character through the normal messages. The checkout's play helper opens this URL.

On Pages, `play-catalog.json` enables a branch picker on the login screen. The Pages workflow writes repository/branch/commit metadata and branch suggestions from git; no credential enters the bundle. A branch link opens or resumes that branch's Codespace, whose startup hook prepares and runs its matching server and client. It does not switch the already loaded Pages client's code. The field accepts new branch names even before the next Pages build refreshes the suggestions. Normal server-hosted builds ship with this catalogue disabled. Missing catalogue data leaves ordinary connection available.

The login panel takes a host, a port, a secure (wss) switch, an account and an optional token for servers in token auth. The host field also takes a whole `wss://`, `ws://`, `https://` or `http://` address, which is what a Codespace gives you. The fields are filled in this order: a `?server=` link (and `?account=`), then `server.host` from config.json, then a game server at the page's own origin, then the last server used in this browser. A page served by the game server finds it at its own origin: a plain GET on the socket path answers with a small JSON description (`service`, `protocol`, `socket`, `name`) instead of an upgrade.

Each WebSocket binary frame carries one whole message, with no length prefix, encoded exactly as `MessageSerializer` writes it: JSON with snake_case fields and the `t` discriminator, which the client always writes first because the server's reader requires it there. The server closes the socket on a text frame or an oversize message. C# `long` values (entity keys, item uids, nonces, the seed) are `Int64` in TypeScript: a number when it is a safe JavaScript integer, otherwise its exact decimal string, which the decoder keeps through JSON.parse source access and the server accepts back in either form. The session runs as on the desktop: `hello` (with the features the client understands), `login`, the character list, `create_character` or `select_character`, then `enter_world` and everything after it.

## Terrain

A browser cannot run the C# generator, so where the desktop client generates a chunk this one asks for it: `request_chunk` for every chunk within the view radius plus one ring, nearest first, a few in flight at a time, with a timeout and a later retry for requests that get no answer. The server answers with `chunk_data` and refuses chunks farther from the player than `chunk_request_radius`. Chunks the server reports as modified on entering a zone arrive the same way and replace what is there. Meshes outside the view radius plus two rings are dropped, and chunk data outside plus three.

Meshes are a port of the desktop `TerrainMesh`: one quad per cell, corner heights averaged across chunk borders so neighbours meet (a mesh is built once its eight neighbours are here or given up on), smooth normals from the height field, vertical skirts wherever a flattened or structure cell steps against a neighbour, extra quads for layered surfaces such as bridges and floors, and a translucent water plane at the chunk's `water_level`. Colours come from each cell's surface tag strings and cell flags through rules in config.json: structure cells take the first matching structure rule; other cells take the first matching base rule (snow, ash, grey, bog, sand, rock, forest floor, scrub, heath, meadow, grass) and then every matching tint in order (road, cliff, shallow and deep water, blocked ground). Mixing is done in sRGB like the desktop's `Lerp` and `Darkened`, then converted to linear for the renderer. Click rays are marched against the height field of built chunks only.

## Entities

Bodies are chosen by what an entity is, by the same rules as the desktop `EntityView`: capsules for creatures, a trunk and canopy for trees, boxes for other props and structures, spheres for projectiles, cylinders for drops, fields, markers and founts. A projectile, area, field or tether the server names a shape for (`shape`, with `shape_size` in world units and radians, see [deliveries.md](deliveries.md)) is drawn as that shape (`world/shapes.ts`): a slab for a projectile, a flat mark for the rest, facing its `heading` (else its yaw) and turned by its `spin`. A telegraph (look `telegraph`) is a ground mark at its radius or shape whose fill grows from the anchor to the whole mark as the server time nears its `until`; it fades in, breathes (still under reduce motion), and fades out when its time is up or it goes. Colours come from config.json by player, drop rarity, kind, flora and faction, then rarity. A label in screen space carries the name, the level for creatures, a dead mark, cast progress and statuses, and a life bar. The mouse picks against the drawn bodies rather than the simulation radius (a marker's radius covers its whole place). Creatures and interactables win over plain props under the cursor, so a canopy in front of a hob does not take the hover from it, and among overlapping bodies the one the cursor is nearest the middle of wins, so a quest giver beside a marker is the one you get when you point at the quest giver.

An `entity_spawn` for a known key replaces its state; static entities (flora, props, markers, founts, drops) arrive once with `static` set and are never updated, and an `entity_update` lists only the entities that changed, so one it does not list keeps the state it had. The hello asks for the features in `server.features` (`entity_delta` by default), and with it granted, changes arrive as `entity_delta` messages applied to the known state exactly as `EntityDelta.ApplyTo` does on the desktop (`net/delta.ts`); a delta for an unknown entity is ignored until the next refresh. A `?full-states` link asks for no features, for comparison.

Other entities are drawn a little in the past at the server's time, blended between the two snapshots around it, and run on with their velocity for a short while past the newest one. The server clock is estimated from snapshot times and smoothed against jitter. The player's own body leads instead: its last known position plus velocity, smoothed, as on the desktop.

## Camera

The same two modes as the desktop `CameraRig`. Isometric: orthographic, fixed pitch, yaw in steps with `Q` and `E`, follows the player, scroll to zoom. Third person: perspective behind the character, orbit with the right mouse button, scroll for distance, `Q` and `E` in smaller steps. `V` toggles. The starting mode, the isometric step and zoom range, and the third-person sensitivity, inversion and field of view are Options.

## Input

Input becomes the same intents the desktop client sends. Every binding is an `input_actions` entry from the bundled content, with the player's rebinds from Options laid over it, matched to `KeyboardEvent.code` with its modifiers; held actions (movement) ignore modifiers. A connected controller is read through the Gamepad API (standard layout) by the same action names. The defaults:

| Input | Intent |
|---|---|
| `W` `A` `S` `D` (or the arrows) held | `move_dir` relative to the camera's ground forward, `stop` on release; sent only when the direction changes |
| Left click | `interact` (or `pick_up` for a drop) on an entity under the cursor, otherwise `move_to` the ground point under it (unless click to move is off) |
| `1` to `8` | `use_weave` on that slot, target the hovered entity, aimed at the ground under the cursor |
| `Space` | `jump` |
| `F` | `interact` with the nearest interactable within reach |
| `G` | `pick_up` the nearest drop within reach |
| `R` | `respawn` |
| `I`, `K`, `P`, `O`, `H`, `C`, `F10` | the inventory (the core pane), weaves, passives, reliquary, stash, character and Options pages; a second press closes the core pane |
| `Tab`, `Shift+Tab` | the menu tray, or the walk through a book of two or more tabs while one stands |
| `Escape` | drops a carried item, folds the tray, lets the front pane use it, closes the front pane and the core pane last; with nothing open it sends `cancel_cast` while a cast runs and opens Options otherwise |
| `Enter` | focuses chat; `Enter` again sends `chat` to the zone |
| `V`, `Q`, `E`, wheel | the camera, nothing sent |

Keys typed into a text field never reach the game, a key capture in Options takes the next press before anything else sees it, and when the page loses focus held movement stops.

## UI

The interface is [ui.md](ui.md)'s, drawn from the same theme content and the same messages as the desktop client, so a player sees one interface on either: the cluster with its orbs drawn on canvases, the weave bar, the cast bar, statuses and the menu tray; the target plate, notices, combat text and the chat; the core pane with its doll, bag, rail and wallet; the pages and the world panes bound into books by the book rule; item cards, carrying and quick actions; and Options. The hello asks for the `interface` feature along with the snapshot features; without it the interface falls back to the plain self state. Every string from the server goes into the page as text, never as markup. The login panel wears the theme too and opens Options.

The player's settings are the Options content (`ui_options`) kept per profile in this browser's local storage (`hollowwake.settings.<profile>`), written on every change and read at start with the content's defaults for anything missing or malformed. Link parameters: `?profile=NAME` picks a profile, `?fresh-settings` starts it from the defaults, `?theme=ID` picks a theme, `?developer` shows the Developer page. The core pane's remembered seat is kept the same way.

## Configuration

`web/public/config.json` is the one source of operator settings: it is compiled in as the defaults and fetched again beside the page at startup, so an operator can retune a deployed client by editing `dist/config.json` without rebuilding. Sections: `server` (default address, socket path, whether to look for a server at the page's origin, hello features), `world` (view radius, requests in flight, timeouts, meshes built per frame, ping interval), `camera`, `motion` (interpolation delay, extrapolation cap, own-body smoothing), `input` (interaction reach), `render` (pixel ratio, antialiasing, shadows, sky, fog, sun and ambient light), `terrain` (the colour rules above) and `entities` (body, telegraph and label colours). A field left out of the runtime file keeps its default. The player's own preferences (draw distance, shadows, haze, the pixel ratio cap, the cameras, keys) are Options over these defaults; key bindings and the look are content, not configuration.

## The protocol module

`web/src/net/protocol.ts` is generated from `HollowWake.Protocol` by `hollowwake protocol-ts` (`TypeScriptContract`): an interface per message and per nested record, with field names exactly as the serializer writes them, and a union of every message keyed by `t`. `ProtocolContractTests` fails while the committed module is stale and checks that every key a real message puts on the wire is declared. Adding or changing a message means regenerating the module in the same change.

## Building and hosting

`npm --prefix web ci` installs the pinned dependencies; `npm --prefix web run build` type-checks and writes `web/dist`; `npm --prefix web test` runs the unit tests; `npm --prefix web run dev` serves the client with hot reload and proxies the socket to a local server. The build uses relative asset paths, so the same `dist` works served by the game server at `/` (the WebSocket transport serves `web_root` beside the socket) and under a repository path on GitHub Pages. `.github/workflows/pages.yml` builds and tests the client and publishes it from main when Pages is available; the Pages copy does not look for a server at its own origin, and the player points it at a running server. [Codespaces](../infrastructure/codespaces.md) covers playing from a codespace.

## Running and checking

`npm --prefix web run smoke -- --url http://localhost:7778/ --shots <dir>` drives the client in an installed Edge or Chrome through playwright-core against a running server: it logs in, creates or picks a character called Smoke, enters the world, waits for the terrain, then checks held movement, jumping, click-to-move, the quartermaster's quest offer, both cameras, the character page, chat, the core pane and every ribbon page, a villager's conversation, the quartermaster's trade, Options (switching the theme, rebinding a key and using it, scaling the interface, resetting a group) and weaves on the nearest hob through real keyboard and mouse input, saving a screenshot at each stage. It runs under its own settings profile, started from the defaults each run. Every check prints a `SMOKE PASS` or `SMOKE FAIL` line and the process exits non-zero when any check fails. The page exposes read-only state for it as `window.hollowWake`.

## Still rough

Placeholder on purpose, and worth knowing before judging a screenshot: entity labels draw over every body, so a crowded hamlet reads as a pile of names; trees and walls hide the player from the isometric camera; `F` takes the nearest interactable, so a villager standing between you and the quartermaster answers first (click the quartermaster instead); the browser downloads every chunk it shows, tens of kilobytes each; the server speaks plain HTTP and WebSocket, so TLS comes from whatever is in front of it (the Codespaces forwarder, a reverse proxy); on Windows the server listens on this machine only unless a URL ACL allows the wildcard prefix.
