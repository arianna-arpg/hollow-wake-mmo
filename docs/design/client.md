# Client

The Godot client is presentation. It never resolves gameplay. The browser client in `web/` follows the same rules with server-sent terrain; its contract is [web-client.md](web-client.md).

## World

On `enter_world` the client builds a `WorldGenerator` from the seed, layout and overlays the server sent and streams chunks around the player, generating them with the same core code the server runs. Generation runs on worker threads, nearest chunk first and one chunk further out than the meshes, so that every mesh is built knowing its neighbours; meshes are built on the main thread a few per frame. Chunks the server reports as modified arrive as `chunk_data` and replace local ones; their biome and water level come from the local generation, since the wire carries only what the simulation changes.

Terrain meshes are one quad per cell with vertex colors from biome and surface tags, smooth normals from the height field, vertical skirts wherever a flattened or structure cell steps against a neighbour, extra quads for layered surfaces and a translucent water plane. Godot takes clockwise triangles as front faces and the north axis flips under the coordinate conversion, so quads are emitted in reversed order with their normals on the lit side. Click rays are marched against the height field of built chunks only, so a click never generates terrain on the main thread.

Entities are placeholder bodies chosen by what they are (capsules for creatures, a trunk and canopy for trees, boxes for other props and structures, spheres for projectiles, cylinders for drops, fields, markers and founts), colored by faction and rarity, with a fixed-size name label carrying level, cast progress and statuses, and a life bar on a camera-facing overlay above them. Positions interpolate between snapshots with velocity extrapolation.

## Camera

`CameraRig` has two modes over one world. Isometric: orthographic, fixed pitch, yaw in 45 degree steps, follows the player, scroll to zoom. Third person: perspective behind the character, orbit with the right mouse button, scroll for distance. `V` toggles; `Q` and `E` rotate. Everything else in the client asks the rig for the ground-plane forward and for mouse rays.

## Input

WASD moves relative to the camera as a held direction; left click moves to the ground point under the mouse (a ray marched against the local height field) or interacts with an entity under the mouse; `1` to `8` cast slots with the hovered entity as target and the ground point as the aim; `Space` jumps; `F` interacts with the nearest interactable; `G` picks up the nearest drop; `C` opens the sheet; `Enter` chats; `Escape` cancels a cast. The input map is registered from code in `InputSetup`.

## UI

The interface contract (the cluster, the core pane and its ribbon, the panes the world opens beside it) is [ui.md](ui.md); this section describes what is built today.

Resource bars, a weave bar with cooldowns and costs, a target line, notices, a chat log, a character sheet (attributes, resistances, gear, bag, currency, quests) and a quest offer panel. All placeholder; the data shapes come from `self_state` and the quest and crafting messages.

## Coordinates

Core is X east, Y north, Z up; Godot is X east, Y up, Z south. `Coords` is the only place that converts.

## Running and checking

The project needs Godot 4.7 .NET. `src/HollowWake.Client/HollowWake.Client.sln` is what the editor builds when play is pressed, and `content/core` is found beside the executable first and then by walking up from the project directory, so a checkout and an exported build both read the same content as the server.

`--host H` and `--port P` (also `--host=H`, as engine arguments or after `--`) fill the login panel's server fields; the launcher starts exported builds that way. `export_presets.cfg` holds the Windows Desktop and Linux release presets the release pipeline exports with; `scripts/package-client.sh` exports both, puts `content/core` beside each executable and zips them (see [launcher.md](launcher.md)).

A scripted play-through drives the client against a running server with no hand on the keyboard:

    godot --path src/HollowWake.Client -- --smoke --shots <dir> [--account NAME] [--host H] [--port P]

It logs in, creates or picks a character called Smoke, enters the world, waits for the terrain, then exercises held movement, jumping, click-to-move, the quartermaster's quest offer, both cameras, the sheet and weaves on the nearest hob through the same input map a player uses, saving a screenshot at each stage. Every check prints a `SMOKE PASS` or `SMOKE FAIL` line and the process exits non-zero when any check fails. The character it drives keeps its position between runs, so a run that starts next to the hedgerow fights before it walks back to the hamlet.

## Still rough

Placeholder on purpose, but worth knowing before judging a screenshot: every interactable prop carries a label, so resource nodes clutter dense areas; structure cells are flat-topped blocks with skirts, so walls read as crenellated slabs; bodies neither animate nor face anywhere meaningful; the sheet and quest panels are unstyled containers and the sheet cuts off below the bag; water and layered surfaces have not yet been seen in play; the third-person camera passes through terrain, walls and canopies; the hint line and the target line share the top-left column with the bars.
