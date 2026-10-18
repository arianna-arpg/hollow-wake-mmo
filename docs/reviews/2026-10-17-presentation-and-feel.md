# Presentation and feel: the plan, the claim and the split with Codex

Date: 2026-10-17. Author: the Claude presentation session, branch `claude/visuals-feel-6141d3`, draft pull request opened from it. Base: main `b910e94`. This record claims the scope below under [COLLABORATION.md](../COLLABORATION.md), so the Codex chats that own the cast cues, the forecasting visuals and the positional hits know what is being built beside them, and it lists what is left to them. It answers the owner's request that the game's look and the mechanics of feel be brought up to the bar of a well-produced indie ARPG, in tandem with Codex.

## What is adopted, read before planning

- [visual-language.md](../design/visual-language.md): the material hierarchy, the shape vocabulary, the functional and decorative separation, the phase semantics and the validation gates. The nine Threads keep their lore names as the player-facing names (Ember stays).
- [sprite-rendering-study.md](../design/sprite-rendering-study.md): an optional appearance adapter inside entity presentation, with painted low-poly geometry as the default candidate and directional cards as the isometric candidate, selected per appearance and camera through an Options entry, with the measurements the study asked for before either is preferred.
- Pull request 6 (`codex/flame-cast-cues`): confirmed cast lifecycle cues rendered by both clients from `ui_cast_visuals` content. This session consumes those cues where it needs a cast moment and never duplicates them.
- The contracts of the two clients ([client.md](../design/client.md), [web-client.md](../design/web-client.md)), the interface ([ui.md](../design/ui.md)), the options ([options.md](../design/options.md)) and the deliveries ([deliveries.md](../design/deliveries.md)), whose shapes and telegraphs already follow the authoritative geometry on the wire.

## The claim

This session owns, until its pull request merges:

| Area | Files | What lands |
|---|---|---|
| Appearance content | `content/core/appearance/*.yaml`, `Content/Kinds/AppearanceKinds.cs`, `Definitions/Appearance.cs`, a line in `InterfaceKinds.Register` | New kinds read only by clients: `ui_appearances` (one entry per look, selected by the pattern's look id and then by its tags; a body family and its parameters, palette, gear, variation, card policy), `ui_surfaces` (ground, structure, layer and water materials by cell tags, the Firstwork and Marcher materials, water and sky), `ui_world` (day length, sun and sky by time of day, haze, foliage), `ui_motion` (animation timings, hit reaction, camera feel). Validators registered beside the interface kinds. Nothing in the simulation reads any of it. |
| Godot bodies | `src/HollowWake.Client/World/Appearance/*`, `EntityView.cs` | An appearance adapter beneath `EntityView`: a plan of parts on named bones built from the appearance entry, drawn as painted geometry or as a directional card baked from the same geometry; ground contact mark; equipment, Calling and Thread read through gear and stance; seeded variation from the entity key. |
| Godot world | `World/TerrainMesh.cs`, `ClientWorld.cs`, `World/Sky.cs`, `World/Foliage.cs` | Surface materials from content, cell-level value variation, roofs over roof cells, water with a lit surface, sun, sky and haze driven by the server clock through the content day, decorative foliage per chunk by density. Terrain generation untouched. |
| Godot feel | `World/Appearance/Animator.cs`, `Camera/CameraRig.cs`, `UI/Overlay/*` | Procedural idle, walk, swing, cast anticipation and recovery, hit reaction, death; hit flash and lean against displacement; combat text weight; camera smoothing, zoom easing and screen shake; the audio bus hooks with no assets. |
| Web mirror | `web/src/world/appearance/*`, `entities.ts`, `terrainMesh.ts`, `terrain.ts`, `palette.ts`, `view/camera.ts`, `app.ts`, `ui/overlay.ts` | The same content, the same plans and the same animator logic in TypeScript, so a player sees one game. |
| Options | `content/core/ui/options.yaml` | Graphics: bodies (auto, geometry, cards), foliage density, water detail, screen shake; Accessibility: a grayscale check; Developer: the body measurement overlay. |
| Smokes | `Dev/Smoke.cs`, `web/scripts/smoke.mjs` | Stages for the look: the hamlet at two times of day, a crowd of bodies in both representations with the measurement line, grayscale and reduced motion. |
| Docs | `docs/design/client.md`, `web-client.md`, `sprite-rendering-study.md` (measurements), `appearance.md` (new contract), this record | The appearance pipeline, the measurements, and what remains for art assets and audio. |

Order of delivery, each step landing in the one draft pull request as its own commits: bodies and props on the Godot client; the world; feel; the web mirror of each; measurements and docs. The pull request is kept current and carries before and after screenshots of the same scenes from both smokes.

## What is left to Codex

- Cast telegraphs, releases, impacts, recovery decoration and the `cast_cues` stream (pull request 6): consumed, not duplicated. The animator reads the cast block (`self_state.cast`, `EntityState.Casting` and `CastProgress`) for the body's anticipation and recovery poses; it draws no ribbons, fans or ground marks of its own for a cast.
- Forecast regions and the shared delivery plan (pull request 3 and its runtime follow-up).
- Positional hits, melee sectors, miss reasons and AI avoidance ([positional hits record](2026-10-17-positional-hits.md)). A whiff, when it exists on the wire, will get a body pose; until then a cast that ends without a payload recovers neutrally.
- Any gameplay rule, number or timing. This branch changes no rule, no simulation file and no message.

## Asks of the coordinator, for fields the messages lack

Each is a protocol change this session will not make. Until it lands, the fallback named beside it is what the clients draw.

1. **A public appearance descriptor for other players.** `EntityState` carries `look` (the Calling's look) but not what a player wears; `self_state.equipment` is the owner's only. Ask: `EntityState.appearance`, a short list of appearance tags the server derives from the worn items' classes (`weapon.sword`, `shield.buckler`, `armor.chest.heavy`), sent on change, behind no feature since it is additive. Fallback: the owner's body shows its own equipment from `self_state`; other players show their Calling's default gear from the appearance entry.
2. **The world's time of day.** `WorldState.TimeOfDay` and `DayLengthSeconds` exist in the core but reach no message, and no rule names the day length. Ask: `enter_world.day_length` (seconds) and a `time_of_day` fraction on `enter_world` and on the self state's cadence, so every client in every zone shares one sky, and the value is the simulation's should a rule ever read it. Fallback: the clients turn the zone's server time into a day with `ui_world.day.length_seconds` from content, which makes two zones' skies agree only while their clocks do.
3. **A hit stamp per entity.** `combat_event` reaches every viewer of the source and target, which is enough for hit reactions on every body in view; no ask. A `stamps` map on `EntityState` would let a reconnecting client replay nothing, which is the right behaviour anyway.

## How this serves the vision

Data over code: every colour, proportion, timing and palette is content; a modder recolours a hob or adds a creature's look by adding an entry, and a pattern chooses its look through its tags and its look id. Composition over bespoke: bodies are plans of parts on bones from a handful of families, never one model per monster. Server authority: the clients read messages only; the animator derives every pose from state the server sent, and the seeded variation comes from the entity key so two clients agree without a roll. Observability: the smoke reports draw calls, primitives, frame times and texture memory for each representation on the busiest scene, in the pull request and in the rendering study.

## Gates this pull request runs before it leaves draft

`dotnet build HollowWake.sln` (with the client assembly and the client tests), `dotnet test HollowWake.sln`, `hollowwake validate` with zero errors and no new warnings, `npm --prefix web test` and `run build`, both smokes with their screenshots looked at and listed, and the measurement line from each smoke recorded in the rendering study. `load` and `fight` run too, to show the simulation unchanged (no simulation file is touched, so their traces are the baseline's).

## What landed, the same day

Pull request 9 (`claude/visuals-feel-6141d3`) holds the whole claim above, on both clients, as one draft kept current. The contract is [appearance.md](../design/appearance.md); the measurements are in the [rendering study](../design/sprite-rendering-study.md#measured-in-the-game); the before and after captures are under `experiments/appearance/evidence/`.

- Content: `content/core/appearance/` with `ui_appearances` (every pattern the world spawns, every Calling, every place and every item base has a look of its own; a test on each client fails when one falls to a fallback), `ui_surfaces`, `ui_world` and `ui_motion`, validated by `AppearanceKinds` beside the interface kinds. The browser no longer carries a terrain palette in `config.json`.
- Bodies: plans of parts on bones from twenty-four body families and nineteen gear families, built part for part on both clients (`World/Logic/BodyFamilies.cs`, `web/src/world/appearance/bodyFamilies.ts`), with gear from the owner's equipment, a monster's grants, the entry or the Calling; painted geometry or baked directional cards behind the bodies option; a contact mark per body whose edge says the relation without colour.
- World: surfaces from content with a seeded jitter, structure sides and roofs, lit rippling water, the day from the server clock through the content's keyframes, foliage tufts per chunk. Terrain generation untouched; its golden tests unchanged.
- Feel: the shared animator (idle, walk, swing and cast from the cast block, hit flinch and flash, death), camera easing and shake, combat text by weight, prop labels by reach, the grayscale check, reduced motion honoured; the audio cue hooks (`World/AudioCues.cs`, `web/src/world/audioCues.ts`) with buses and cue names and no assets.
- Gates: build with the client assembly and client tests, 90 client logic tests and 141 browser tests, `validate` 0/0, `load` and `fight` unchanged (no simulation file touched), both smokes green with their new look stages (desktop 25 checks, browser 28), and the measurement lines recorded.

Main moved while this was built (the Crossing loading screen and the brand, the shared delivery plans, the living-world contracts, the GitHub setup); the branch carries the merge. The appearance pipeline composes with the Crossing as [its record](2026-10-17-brand-and-loading.md) asked: bodies and chunks build beneath the cover and world entry still ends on the readiness the loading contract owns. The two Codex records that merged meanwhile still carry "pending Claude review" lines; those reviews are the coordinator's, not this session's.

What remains, for other hands: authored character models, rigs and textures to replace the generated proxies (the plans, sockets and animator are the seam they attach to); audio assets on the cue names; the visual language's human readability check with unfamiliar reviewers; a named low-end machine for the card measurements; the two protocol asks above. Roofs over houses that straddle a chunk border get one cap per side.

Acknowledged by: this record, for the Claude side. Codex: add your record or pull request link here when you have read it.
