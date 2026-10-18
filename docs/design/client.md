# Client

The Godot client is presentation. It never resolves gameplay. The browser client in `web/` follows the same rules with server-sent terrain; its contract is [web-client.md](web-client.md).

Identity, startup and world-entry preparation follow [loading.md](loading.md). The Crossing is a client-only activity, isolated from the simulation and ordinary world input.

## World

On `enter_world` the client builds a `WorldGenerator` from the seed, layout and overlays the server sent and streams chunks around the player, generating them with the same core code the server runs. Generation runs on worker threads, nearest chunk first and one chunk further out than the meshes, so that every mesh is built knowing its neighbours; meshes are built on the main thread a few per frame. Chunks the server reports as modified arrive as `chunk_data` and replace local ones; their biome and water level come from the local generation, since the wire carries only what the simulation changes.

Terrain meshes are one quad per cell with vertex colors from biome and surface tags, smooth normals from the height field, vertical skirts wherever a flattened or structure cell steps against a neighbour, extra quads for layered surfaces and a translucent water plane. Godot takes clockwise triangles as front faces and the north axis flips under the coordinate conversion, so quads are emitted in reversed order with their normals on the lit side. Click rays are marched against the height field of built chunks only, so a click never generates terrain on the main thread.

Terrain meshes are coloured by the surfaces content (`ui_surfaces`, see [appearance.md](appearance.md)) from each cell's surface tags and flags, with a seeded value jitter in patches, skirts in a structure's side colour and a pitched roof over roof cells; water is a lit rippling plane; foliage tufts are scattered per chunk by the world content, seeded from the cells. The sun, sky, ambient light and haze follow the world's day the server reports (`enter_world` and the self state carry it; a server that predates the fields leaves the zone clock to fold) through the content's sky (`ui_world`), driven in `ClientWorld`.

Entities are built bodies from the appearance content (`ui_appearances`): a body plan of parts on bones from a family (`World/Logic/BodyFamilies.cs`) with the gear it wears (`GearFamilies.cs`), drawn as painted geometry (`World/Appearance/BodyNode.cs`) or as a directional card baked from the same body (`CardBaker.cs`, `CardNode.cs`) as the bodies option and the camera decide (`AppearanceService.cs`), posed each frame by the shared animator (`World/Logic/Animator.cs`) from the state the server sends, standing on a contact mark whose edge says the relation (`ContactMark.cs`). The pipeline is [appearance.md](appearance.md); its engine-free logic under `World/Logic` is tested by `tests/HollowWake.Client.Tests` like the interface's. A fixed-size name label carries level, cast progress and statuses, with a life bar on a camera-facing overlay above; prop labels show under the cursor or within reach of the hero. A projectile, area, field or tether the server names a shape for is drawn as that shape (`UI/Logic/ShapeOutline.cs`, `World/ShapeMesh.cs`): a slab for a projectile, a flat mark for the rest, facing its heading (else its yaw) and turned by its spin. A telegraph is a ground mark at its radius or shape whose fill grows to the whole mark as the server time nears its `until`, fading out when its time is up or it goes. The mouse picks the bodies as drawn: creatures and interactables over plain props, the body the ray passes nearest the middle of first, shapes and marks never. Positions interpolate between snapshots with velocity extrapolation; a body faces its yaw with its forward along its own x. A hit a `combat_event` confirms flinches and flashes the struck body and brings the striker's swing through; the moments the animator raises (a footstep on its surface, a swing, a hit, a death) go to `World/AudioCues.cs`, whose buses and cue names wait for assets.

## Camera

`CameraRig` has two modes over one world. Isometric: orthographic, fixed pitch, yaw in steps, follows the player, scroll to zoom. Third person: perspective behind the character, orbit with the right mouse button, scroll for distance. The zoom and the turn ease toward their targets and a shake rides on a hit taken or a kill, timed by the motion content (the screen shake option and reduce motion still it). The starting mode, the step, the zoom range, the drag sensitivity and inversion and the field of view are Options. `V` toggles; `Q` and `E` rotate. Everything else in the client asks the rig for the ground-plane forward and for mouse rays.

## Input

Every binding is content: `InputSetup` registers the `input_actions` entries (keys and controller inputs) with the player's rebinds from the settings file layered over them, and nothing binds a key in code. The defaults: WASD moves relative to the camera as a held direction; left click moves to the ground point under the mouse (a ray marched against the local height field, unless click to move is off) or interacts with an entity under the mouse; `1` to `8` cast slots with the hovered entity as target and the ground point as the aim; `Space` jumps; `F` interacts with the nearest interactable; `G` picks up the nearest drop; `Enter` chats. Each page's action (the inventory, the weaves, the passives, the reliquary, the character popout, Options) requests it and a second press closes the core pane; the menu action fans the tray; Tab and Shift+Tab walk a book of two or more tabs. `Escape` drops a carried item, folds the tray, closes the front pane and closes the core pane last; with nothing open it cancels a running cast, and otherwise opens Options.

## Settings

The client keeps the player's Options in `user://settings/<profile>.json` (the profile is `default` unless `--profile NAME` names another), read at start with the content's defaults for anything missing or malformed and written on every change. Live entries apply as they change through one applier per entry id in `Main`; on-close entries apply when the pane closes. The smoke and the mockup run under their own profiles and start from the defaults each run. `--developer` shows the Developer page.

## UI

The interface contract (the cluster, the core pane and its ribbon, the panes the world opens beside it) is [ui.md](ui.md); its look is the theme content in `content/core/ui/`. The hello asks for the `interface` feature, and the client draws from `self_state` with the cluster's fields, `weave_slots`, `inventory_state`, `passive_state`, `page_state` and the world panes' states; without the feature it falls back to the plain self state's resources, weaves and bag.

- `UI/Hud.cs` orchestrates: the cluster, the target plate, notices and combat text above the hero, the chat in the lower left corner, the core pane and the books beside it, the item card, the carried item and the quick actions. Every intent it raises goes out through one event.
- `UI/Logic/` holds the layout logic as plain classes that never touch an engine type, tested by `tests/HollowWake.Client.Tests`: the cluster's geometry, the board (footprints, the cell under the pointer, drop fits), the book rule (binding, fronting, promotion, the walk), the workspace (page requests, the core pane's lifecycle, Escape, departure), pane placement (the remembered seat, docking, merging the sides into one book, the rail's compaction), the menu fold, the sheet and the reading of the interface content.
- `UI/Cluster/`, `UI/Core/`, `UI/Pages/`, `UI/WorldPanes/`, `UI/Book/`, `UI/Overlay/` and `UI/Widgets/` draw: orbs with a shader for the pool, the veil and the glass; rings, frames, wells and sweeps drawn on the canvas in the theme's frame style; glyphs rasterized from the icon set's SVG paths; pane grounds from a surface shader (stone grain and seams, parchment fibre).
- Pages and world panes dock beside the core pane on the side with room. When the other side has room too, world panes stand there; when it does not, they bind with the pages into one book where a station's pane fronts over a development page. The core pane drags by its header and remembers its seat in `user://interface.cfg`.
- Items lift by a drag or a click and drop on a bag cell, a doll slot, a rack seat or socket, a vendor (to sell), a container board or the world, one `move_item` (or `socket_stone`, `learn_form`, `sell`) each; right-click opens quick actions with the classic equip, unequip and drop intents.

## Coordinates

Core is X east, Y north, Z up; Godot is X east, Y up, Z south. `Coords` is the only place that converts.

## Running and checking

The project needs Godot 4.7 .NET. `src/HollowWake.Client/HollowWake.Client.sln` is what the editor builds when play is pressed, and `content/core` is found beside the executable first and then by walking up from the project directory, so a checkout and an exported build both read the same content as the server.

`--host H` and `--port P` (also `--host=H`, as engine arguments or after `--`) fill the login panel's server fields; the launcher starts exported builds that way. `export_presets.cfg` holds the Windows Desktop and Linux release presets the release pipeline exports with; `scripts/package-client.sh` exports both, puts `content/core` beside each executable and zips them (see [launcher.md](launcher.md)).

A scripted play-through drives the client against a running server with no hand on the keyboard:

    godot --path src/HollowWake.Client -- --smoke --shots <dir> [--account NAME] [--host H] [--port P]

It logs in, creates or picks a character called Smoke, enters the world, waits for the terrain, measures the look on the hamlet (painted geometry with and without shadows, then directional cards: frame times, draw calls, primitives, bodies, atlas and texture memory, on one `SMOKE measure` line each) and takes the readability shots (grayscale, reduced motion, night and evening through the developer day options), then exercises held movement, jumping, click-to-move, the quartermaster's quest offer, both cameras, the character popout, the core pane and every ribbon page, a villager's conversation, a vendor's trade, Options (switching the theme, rebinding a key and using it, scaling the interface, resetting a group) and weaves on the nearest hob through the same input map a player uses, saving a screenshot at each stage. Every check prints a `SMOKE PASS` or `SMOKE FAIL` line and the process exits non-zero when any check fails. The character it drives keeps its position between runs, so a run that starts next to the hedgerow fights before it walks back to the hamlet.

`--theme ID` draws the interface with another theme from `ui_themes`. A mockup renders the interface with no server, over locally generated terrain and with a staged hero that uses every field the interface draws, and saves the cluster, the core pane and the book beside it in every theme (or those named):

    godot --path src/HollowWake.Client -- --mockup <dir> [--themes etched_stone,runic_brass]

## Still rough

Worth knowing before judging a screenshot: bodies are generated low-poly proxies from content, not authored models, and cards carry no per-entity variation (see [appearance.md](appearance.md)); structure cells are flat-topped blocks with skirts, so a wall is a slab and only roof cells get a cap; the third-person camera passes through terrain, walls and canopies; world labels draw under the interface but are not hidden by it; no audio asset ships yet.
