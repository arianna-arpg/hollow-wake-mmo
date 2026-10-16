# Client

The Godot client is presentation. It never resolves gameplay.

## World

On `enter_world` the client builds a `WorldGenerator` from the seed, layout and overlays the server sent and streams chunks around the player, generating them with the same core code the server runs; chunks the server reports as modified arrive as `chunk_data` and replace local ones. Terrain meshes are one quad per cell with vertex colors from biome and surface tags, skirts on terrace steps, extra quads for layered surfaces and a translucent water plane.

Entities are placeholder bodies chosen by what they are (capsules for creatures, boxes for props and structures, spheres for projectiles, cylinders for drops, fields, markers and founts), colored by faction and rarity, with a name, level, cast bar text, statuses and a life bar above them. Positions interpolate between snapshots with velocity extrapolation.

## Camera

`CameraRig` has two modes over one world. Isometric: orthographic, fixed pitch, yaw in 45 degree steps, follows the player, scroll to zoom. Third person: perspective behind the character, orbit with the right mouse button, scroll for distance. `V` toggles; `Q` and `E` rotate. Everything else in the client asks the rig for the ground-plane forward and for mouse rays.

## Input

WASD moves relative to the camera as a held direction; left click moves to the ground point under the mouse (a ray marched against the local height field) or interacts with an entity under the mouse; `1` to `8` cast slots with the hovered entity as target and the ground point as the aim; `Space` jumps; `F` interacts with the nearest interactable; `G` picks up the nearest drop; `C` opens the sheet; `Enter` chats; `Escape` cancels a cast.

## UI

Resource bars, a weave bar with cooldowns and costs, a target line, notices, a chat log, a character sheet (attributes, resistances, gear, bag, currency, quests) and a quest offer panel. All placeholder; the data shapes come from `self_state` and the quest and crafting messages.

## Coordinates

Core is X east, Y north, Z up; Godot is X east, Y up, Z south. `Coords` is the only place that converts.
