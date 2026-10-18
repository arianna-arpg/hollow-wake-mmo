# Sprite and 2.5D rendering study

Status: bounded experiment, not an accepted architecture change. Evidence captured 2026-10-17. Owner scope: this document and `experiments/sprite-rendering/`. No production client, content, protocol, camera, roadmap or art-direction files are changed.

## Recommendation

Keep the accepted Godot desktop client, three.js browser client, authoritative C# simulation, generated 3D terrain and dual cameras. Add an optional **appearance adapter** inside entity presentation if the gates below pass. Directional sprites are a credible isometric presentation candidate. Painted low-poly characters are the stronger default candidate for close third-person views and freely rotating equipment. A hybrid can select the representation per appearance and camera without changing gameplay.

**Do not choose sprites on a claim of lower hardware requirements yet.** This experiment reduces submitted triangles substantially but shows overlapping CPU timings on a powerful GPU. It does not measure GPU time, minimum hardware, production animation, streamed asset churn, or Godot performance. High camera pitch visibly exposes the card approximation. These findings support a reversible trial, not a production migration.

| Candidate | Strength | Cost / failure to evaluate |
|---|---|---|
| Directional, depth-tested character cards in the current 3D world | Authored silhouettes, predictable small character meshes, painterly detail without live skinning | Direction/pitch changes, texture residency, alpha coverage, equipment combinatorics, flat depth and baked lighting |
| Painted low-poly world and characters | Continuous rotation, shared equipment meshes, stable depth and live lighting | Model/rig authoring, material and skinning cost; simplifying triangles alone may not solve the actual bottleneck |
| Shared source model, sprite at distance/isometric, mesh near third person | Reuses proportions, animation and costume design; preserves both cameras | Two export paths, visual transition matching, both resources briefly resident, more QA |

## Review the deliverable

From the repository root:

```powershell
node experiments/sprite-rendering/serve.mjs
```

Open `http://127.0.0.1:8791/`. Stop with Ctrl+C. The listener is loopback-only, serves an explicit file allowlist, and uses already installed dependencies in `web/node_modules`. It installs nothing, contacts no game server, and publishes nothing. Opening `index.html` directly does not work because the browser needs the local module server. An occupied port can be changed with `SPRITE_STUDY_PORT`.

The controls compare sprite / geometry / side-by-side, rotate the existing camera, switch to third person, test its 5–80 degree pitch range, change population, exchange a staff/hood for crown/greatblade, hide foreground canopy, hide effects, freeze time, select quality, and export a measurement. Use side-by-side to judge appearance and **single modes** to compare cost; split view draws the world twice.

Run the repeatable browser check and short benchmark:

```powershell
node experiments/sprite-rendering/validate.mjs --benchmark
node experiments/sprite-rendering/check-assets.mjs
```

The browser harness uses installed Edge and playwright-core, starts and closes its own loopback server, and overwrites only `experiments/sprite-rendering/evidence/`. Omit `--benchmark` for the shorter control and rendering check. The asset checker verifies JSON syntax, local schema references, grid/frame/pivot arithmetic and the exported PNG dimensions; it is not a complete JSON Schema validator. Evidence includes:

- [Isometric comparison](../../experiments/sprite-rendering/evidence/comparison-isometric.png), [equipment comparison](../../experiments/sprite-rendering/evidence/comparison-equipment.png), [third person](../../experiments/sprite-rendering/evidence/comparison-third-person.png), [80 degree pitch](../../experiments/sprite-rendering/evidence/comparison-high-pitch.png), and [Low tier](../../experiments/sprite-rendering/evidence/comparison-low.png).
- [Raw measurements and checks](../../experiments/sprite-rendering/evidence/results.json), including browser/GPU identity, dimensions, source hashes, per-run statistics and unmeasured items; [asset checks](../../experiments/sprite-rendering/evidence/asset-checks.json).
- [Staff atlas](../../experiments/sprite-rendering/evidence/atlas-staff.png), [crown atlas](../../experiments/sprite-rendering/evidence/atlas-crown.png), [configurable study settings](../../experiments/sprite-rendering/study-config.json), [example appearance manifest](../../experiments/sprite-rendering/appearance.example.json) and [JSON Schema](../../experiments/sprite-rendering/appearance.schema.json).

The scene is an original procedural technical proxy, not finished art or a claim of Ragnarok/Tree of Savior quality. The exact requested [Lumoria page](https://lumoriagame.com/#play) remained inaccessible through the research tool. No Lumoria visual analysis is claimed and no similarly named game was substituted. The requested reference qualities are interpreted as expressive characters, readable costume shapes, rich painted surfaces, mystical light and coherent high fantasy. Final palette, motif and material decisions belong to the separate visual-language study.

## Constraints and inspected implementation

The reviewed starting revision was `9e0f84ac6b2e8a528232e59cc31052f5e8afaa72`; active work may advance it. The evidence stores hashes of reused files because this is a shared checkout.

- [VISION](../VISION.md), [architecture](../ARCHITECTURE.md), and accepted [ADR 0001](../adr/0001-stack.md), [0002](../adr/0002-world-hierarchy.md), [0003](../adr/0003-brutal-on-the-server.md), [0004](../adr/0004-wire-format.md), [0005](../adr/0005-crafting-verified-by-the-server.md), [0006](../adr/0006-browser-client.md) retain the engine, transport, data and server boundaries. This study offers no replacement stack.
- [Client](client.md), [web client](web-client.md), [movement and z-levels](movement-and-z-levels.md), and [performance](performance.md) are constraints. The existing budgets are server budgets, including a 25 ms p95 zone tick at 20 Hz, 1 ms snapshot construction per player, and 4 ms chunk generation. They are not GPU or client frame-time budgets.
- `src/HollowWake.Client/World/EntityView.cs` builds placeholder body/canopy meshes and independent labels/health overlays. Its own comment anticipates swapping appearance without changing gameplay. `Camera/CameraRig.cs` owns orthographic stepping and continuous perspective orbit.
- `web/src/world/entities.ts` already separates shape selection, cached geometry/materials, interpolation, labels and picking. Pose includes yaw; the visual root rotates by `PI/2 - coreYaw`. `world/interpolation.ts` owns snapshot interpolation and bounded extrapolation. Do not replace it with sprite animation motion.
- `web/src/world/terrainMesh.ts` builds ground, skirts and stacked layers. `chunks.ts` decodes their server representation. `view/camera.ts` owns both cameras. The experiment transpiles these and `palette.ts`/`coords.ts` **in memory, read-only**, using the installed TypeScript package. The fixture is synthetic chunk data through the current mesher, not a captured live world or a second terrain implementation.
- `web/src/net/protocol.ts` / `src/HollowWake.Protocol/Messages.cs` provide `look`, `pattern`, position, yaw, dimensions, velocity, grounded/dead state and casting progress. Public `EntityState` does not contain a complete per-slot equipment appearance descriptor. Private `self_state` equipment is not a substitute for other players' public cosmetics. No field was added here.
- The Godot project currently selects Forward Plus. Trying Compatibility as a separate low-end renderer configuration is a benchmark candidate, not an adopted setting or a requirement to change engine.

Read-only prior-project review: `D:/Games/Claude/ARPG Game - World/AGENTS.md`, its `CLAUDE.md`, and `src/render/vis/sprites.ts`, `bakeScale.ts`, `combatReadabilityLayer.ts`, plus `src/render/camera.ts`. Applicable ideas are explicit bake cache keys, bounded LRU lifetime, memory accounting, configurable presentation, and distinct gameplay cue shapes. Its canvas bake/camera implementation is not transplanted: its camera and story-layer assumptions do not establish correct 3D occlusion or MMO performance. Its capture-only supersampling is also not evidence of affordable live close-ups.

## What the prototype actually implements

Both representations use the same original character generator and four static walking poses. The sprite path bakes eight azimuths at three elevations, then selects a view in a single instanced card draw for the selected outfit. The mesh path uses four instanced pose groups, one per animation frame. This is a fairer submission comparison than one sprite batch against hundreds of independent mesh objects, but **neither path is a production skeletal-animation benchmark**.

The world, camera, pose fixture, optional contact marks, warnings and decoration are shared. There is no gameplay, real network interpolation, picking, navigation, hit testing or animation-driven damage in this experiment. The fixture's walk is prescribed presentation motion. It does not prove a legal path onto the bridge.

The character card is camera-facing, foot-anchored, world-sized, alpha-tested, depth-tested and depth-writing. It is not a screen overlay. Both upper-deck and ground characters have different world Z, and the bridge can hide a character beneath it. The current coordinate converter maps core `(x,y,z)` to `(x,z,-y)` once at the presentation boundary.

For atlas selection, model-forward is +Z and the direction index is the nearest bucket of `(viewerBearing - (PI/2 - coreYaw)) / TAU`. Orthographic bearing comes from camera direction; perspective bearing is computed per actor. The atlas's closest elevation is selected from 15, 35.264 and 65 degrees. Production should add tested angular hysteresis to avoid flicker at bucket boundaries; the proxy deliberately uses nearest selection so stepping is inspectable.

**Known visual failures:** the card gives every opaque pixel a planar depth, so a weapon, hat or torso can intersect a bridge/railing differently from the solid character. The close third-person screenshot shows softer image detail and different apparent body volume; the 80 degree screenshot shows a 65 degree baked view standing in for a steeper angle. Perspective elevation is approximated using nominal camera pitch, not a separate elevation for every actor. Baked highlights rotate with the character and cannot follow changed world lights. Cards can overlap one another incorrectly at their extremities. A high-pitch shot is an explicit stress case, not an approved appearance.

Keep actual walls, bridge decks, floors, slopes, cliffs and major canopy occluders as geometry. Decorative grass, distant foliage, motes and distant creatures are possible card candidates. Do not globally disable depth tests or sort all characters by screen Y to make cards look cleaner. If foreground fade/outline assistance is added, it must respect visibility rules and never expose server-hidden entities. Preserve existing picking proxies; test upper-deck clicks explicitly because this experiment has no picking implementation.

## Art, customization and lighting

An artist-friendly route is a simple master model/rig, consistent scale and foot origin, approved color/material families, then orthographic multi-view export with optional paint-over. A source model makes view consistency and a near-camera mesh fallback practical. Hand-drawing all directions remains possible but magnifies revision work. A costume must read in grayscale at the intended on-screen height before adding particles or small ornamental detail.

Eight yaw directions suit the default 45 degree camera steps but do not eliminate stepping when actors turn continuously. Four directions have maximum nearest-view error of 45 degrees; eight have 22.5; sixteen have 11.25. Perspective orbit and tall/near actors also need elevation coverage. More views improve coverage while increasing production, download and resident texture cost. Never mirror asymmetric weapons or readable symbols merely to halve work.

| Equipment method | Appropriate use | Cost to expose |
|---|---|---|
| Combined outfit atlas | Small NPC roster; this proxy's two complete silhouettes | Every unique combination can need its own export/cache entry |
| Separate body/head/weapon layers | Broad customization with authored socket positions | Per-frame, per-direction attachment and front/behind ordering; more materials/draws and overlapping alpha |
| Neutral costume plus dye masks | Color variety without changed silhouette | Extra texture channels; dye must preserve material values and faction/readability cues |
| 3D equipment / near-camera mesh | Weapons, large wings, capes and close inspection | Mesh/material cost; matching a flat body's apparent anatomy and lighting requires review |

Start with costume families and a small set of silhouette-changing slots. Do not bake the Cartesian product of every item and dye. Stream or cache composed appearances by source revision, outfit, dye, view-set and resolution, with explicit byte and lifetime limits. Stable public appearance IDs/revisions should be sent on change if the existing `look` contract proves insufficient. Any such wire change is a separate review with generated TypeScript, full-state/delta compatibility and server tests.

The [manifest example](../../experiments/sprite-rendering/appearance.example.json) describes provenance, atlas dimensions, cells, world scale, normalized foot pivot, direction/pitch conventions, clips, alpha/color space, mirror policy, sockets, equipment policy, mesh fallback and separate visual bounds. Its [schema](../../experiments/sprite-rendering/appearance.schema.json) is a proposal, not a registered production content kind. Production import must additionally check atlas bounds, grid arithmetic, every clip frame, complete socket coverage, referenced files, cache revision, gutters/mip bleed, and the fallback. This proxy has a combined outfit and therefore no separate socket samples. No unsupported automatic equipment feature is implied.

Keep low-frequency ambient tint plus restrained painted shading for sprites. Optional normal maps can improve response but do not give a flat card volumetric depth. Use cutout body materials, not translucent body blending, when the art permits. Keep glows in separate, bounded effects. Blob/contact marks in this study are opaque ground discs, not dynamic shadow maps. Real projected shadows need a receiving surface at the character's actual floor and must not fall through a bridge onto the lower floor. For sprites, consider a simple shadow-only proxy; camera-facing shadow geometry can change with the camera. Godot documents this billboard-shadow caveat and distinguishes cutout from alpha blending. [SpriteBase3D reference](https://docs.godotengine.org/en/stable/classes/class_spritebase3d.html)

Ground warnings must keep server position, surface, radius/shape, heading and timing. The study's two rings are illustrative fixed fixtures, not server abilities. Reserve strong contrast and stable boundaries for hostile warnings, then player actions, then ambient decoration. Low quality may reduce decorative motes, trails and nonessential glow; it must retain attack boundaries, cast timing, targets and dangerous projectiles. Use shape/animation as well as color, and test crowded scenes in grayscale. Coordinate this with Claude's active casting/interface work after its contracts settle.

## Memory, batching and fill cost

For uncompressed RGBA8, base texture bytes are `width * height * 4`; a full mip chain is approximately 4/3 of that. The proxy's 1024×1536 atlas is 6 MiB base, about 8 MiB with mips **per outfit**. Two visited outfits can use approximately 16 MiB of atlas storage on the GPU, plus 12 MiB of source canvas pixels, model buffers, render buffers and driver overhead. These are arithmetic estimates, not measured residency. The PNG files being much smaller does not reduce decoded GPU pixels.

A production planning example with 32 total animation frames, eight directions, three elevations and 128×128 RGBA cells has 768 cells: 48 MiB base, about 64 MiB with mipmaps before packing overhead. At 256×256 it is four times larger. Separate layers/normal maps can multiply that again. Actual compression format support, alpha quality and residency must be checked on each target. Godot's image import documentation separates file compression from VRAM-oriented import and describes mipmap behavior. [Image import](https://docs.godotengine.org/en/stable/tutorials/assets_pipeline/importing_images.html)

An atlas enables shared materials but does not automatically merge draws. This proxy explicitly uses `InstancedMesh` and per-instance UV cells. Its four mesh pose groups also batch. Future per-entity material clones, alpha layers or one texture per costume can destroy those savings. three.js documents instanced geometry/material sharing and the need to update instance buffers/bounds. [InstancedMesh](https://threejs.org/docs/pages/InstancedMesh.html)

For production, bucket by compatible material/atlas and spatial region; frustum-cull regions and keep bounds accurate. The bounded fixture disables actor instance culling, which must not be copied into an infinite world. Godot's MultiMesh guidance describes group visibility and suggests partitioning across areas. That page is marked as not yet updated for the current stable engine, so verify behavior against the installed build before implementation. [MultiMesh guidance](https://docs.godotengine.org/en/stable/tutorials/performance/using_multimesh.html)

Cards reduce vertices but can increase covered fragments, texture fetches and alpha discard. Blended layers add sorting and overdraw costs, especially in crowds and large effects. Cutouts avoid some blending problems, not the cost of rasterizing transparent margins. Use tight bounds, padded/extruded atlas gutters, suitable mip filtering, effect caps and target-device profiling. Godot explicitly warns that more complex opaque geometry can outperform overlapping transparency. [GPU optimization](https://docs.godotengine.org/en/stable/tutorials/performance/gpu_optimization.html) Tree of Savior's own historical optimization post also describes performance difficulties despite its 2D-like visual style; this is context, not a benchmark transferable to Hollow Wake. [Developer optimization note](https://treeofsavior.com/page/news/view.php?n=438)

## Observed results and limits

The completed browser run passed its 25 rendering/control checks with no browser or shader errors; the separate asset checker passed 14 checks. JavaScript syntax checks passed. Screenshots and the staff atlas were visually inspected. Run environment: headless Edge 155, three.js revision 186, Windows, ANGLE/D3D11 reporting an NVIDIA GeForce RTX 3090. App viewport was 1600×1000; single-view drawing buffer was 1354×858, pixel ratio 1, Balanced, staff outfit, canopy/effects enabled. Each case had one second of warm-up and two seconds of capture, three repetitions with alternating A/B order. These are short development observations under shared-machine activity.

| Actors | Sprite / mesh draw calls, whole scene | Sprite / mesh submitted triangles, whole scene | Sprite CPU p95 range (ms) | Mesh CPU p95 range (ms) |
|---:|---:|---:|---:|---:|
| 48 | 70 / 73 | 6,156 / 33,516 | 0.6–0.7 | 0.7–0.8 |
| 256 | 70 / 73 | 9,068 / 154,988 | 0.7–0.9 | 0.7–0.8 |
| 512 | 70 / 73 | 12,652 / 304,492 | 0.8–1.1 | 0.7–0.8 |

CPU timing spans fixture/camera updates, instance uploads and renderer submission. GPU work is asynchronous. `requestAnimationFrame` interval p95 ranged from about 4.3 to 12.5 ms across these runs; that variation is browser pacing/scheduling evidence under shared-machine activity, not a portable GPU render time or a guaranteed game FPS. No consistent sprite CPU advantage appears. Draw/triangle counts use `renderer.info` with explicit reset across view passes; its resource counts are counts, not bytes. [WebGLRenderer statistics](https://threejs.org/docs/pages/WebGLRenderer.html)

GPU timer support was detected but **GPU elapsed time was not collected**. There is no measured low-end GPU, integrated GPU, mobile device, energy use, memory residency, asset upload latency, network/full gameplay cost, skeletal animation cost, Godot run or production artwork comparison. The data does not establish either option as faster on the intended minimum machine.

The general repository build was attempted with outputs redirected into the experiment's ignored `.validation-dotnet/` directory. It failed to restore `Godot.NET.Sdk`, `YamlDotNet`, target packs and other NuGet dependencies, including unavailable repository-signature information. The solution tests and content-validation execution therefore remain unverified in this study; this is an environment failure, not a passing gate. No Godot executable was available on PATH. Production client builds/smokes are required at integration time.

## Measured in the game

The appearance adapter this study proposed is in both clients ([appearance.md](appearance.md)): bodies built from `ui_appearances` content as painted geometry, or as directional cards baked from the same body into eight-azimuth atlases, chosen per appearance (`card: never | allow | prefer`) and per camera through the **bodies** option (by camera, geometry, cards). Each client's scripted smoke measures both on the same scene, the hamlet of Ninewells on entry (about 230 to 290 entities, 49 chunks), on the development machine (Windows 10, RTX 3090), the desktop at 1600 by 900 with 2x MSAA and the browser at 1280 by 800 in headless Edge. Two seconds of frames per line; the desktop's frame timer has millisecond resolution. Lines come from one run each, so treat differences under about ten percent as noise.

| Client, representation | fps | frame ms p50 / p95 | draw calls | primitives | bodies (cards) | atlases |
|---|---:|---:|---:|---:|---:|---:|
| Desktop, geometry | 130 | 8 / 10 | 504 | 126,742 | 235 (0) | 0 |
| Desktop, geometry, no shadows | 142 | 7 / 9 | 485 | 65,266 | 235 (0) | 0 |
| Desktop, cards | 111 | 9 / 11 | 418 | 91,692 | 236 (219) | 15, 9.56 MiB |
| Browser, geometry | 210 | 4.7 / 5.8 | 256 | 105,914 | 231 (0) | 0 |
| Browser, geometry, no shadows | 236 | 4.2 / 5.5 | 268 | 107,024 | 230 (0) | 0 |
| Browser, cards | 239 | 4.2 / 5.0 | 185 | 90,982 | 229 (212) | 15, 9.56 MiB |

Body triangles fall from about 45,000 to 2,500 with cards, and draw calls by about a fifth, but on this machine the desktop's cards cost frames rather than saving them (alpha-tested quads over a shadowed, MSAA scene) and the browser's gain is within noise. The browser's own work per frame (animation, interpolation, labels) is about 2.9 ms with geometry and 2.3 ms with cards. The rendering study's finding stands: nothing here shows cards as the cheaper representation on a strong GPU, and no integrated or low-end GPU has been measured. Geometry stays the default (`auto` draws cards only when the isometric camera is zoomed out past the card zoom), the option exists for the measurements to continue on the intended minimum machine, and the per-look atlas cost (about 0.6 MiB per look with mipmaps at up to 192 pixel cells) is the number to budget against when that machine is named. Atlas bytes are counted from their dimensions, not measured residency.

Before this pipeline the same desktop smoke ran at 140 fps at its end and the browser at 239 fps with 1.35 ms of work per frame, drawing capsules and boxes; the before and after captures are in `experiments/appearance/evidence/`.

## Quality settings and repeatable decision gates

The study's [JSON config](../../experiments/sprite-rendering/study-config.json) applies these reversible presets:

| Tier | Drawing resolution | Sprite views selected | Contact marks / decorative motes |
|---|---|---|---|
| Low | 0.75 scale, DPR capped at 1 | Four of the eight baked directions | Off / 12 |
| Balanced | 1.0 scale, DPR capped at 1 | Eight | On / 36 |
| High | 1.0 scale, DPR capped at 1.5 | Eight | On / 72 |

All tiers retain bridge geometry, entity origins, ground warning shapes and both cameras. **Low still loads the full atlas**, so it is not an atlas-memory reduction. A production low tier would need separately exported lower-resolution/fewer-view pages and a residency budget. Dynamic shadows, normal maps and postprocessing are not implemented in any study tier.

Proposed client targets for discussion, not existing approved budgets: low tier 1280×720 at 30 FPS (33.3 ms frame budget), balanced 1920×1080 at 60 FPS (16.7 ms), leaving headroom for UI, networking, streaming and spikes. Do not adopt hardware requirements until a named target machine passes.

1. **Visual gate:** artist-approved matched assets, all supported yaw steps, continuous actor turns, both cameras, extreme pitches, minimum/maximum zoom, bright/dark biomes and both bridge levels. If flat depth, cropped weapons or pitch changes remain conspicuous, choose the mesh fallback for that view/appearance. Near-camera fallback is a proposal; the prototype keeps manual A/B to expose the failure.
2. **Truth gate:** same snapshot stream and interpolation in both adapters; no visual root motion in simulation. Verify pick/hover, bridge clicks, airborne actors, dead bodies, despawn and reconnect. Cast markers must match authoritative shape/time at every quality tier. Run existing client and server contract tests.
3. **Asset gate:** one real character with idle/run/cast/hit/death, two asymmetric weapon families, one silhouette-changing helmet and dyes. Log artist/export hours, total shipped bytes, peak resident bytes, cache hit rate and equip-change stalls. Validate footprint/pivot, every direction, clip bounds, socket coverage and import filtering.
4. **Performance gate:** use a named integrated-GPU target and a modest discrete-GPU target alongside the development machine. Record CPU, GPU/driver, RAM, OS, engine/browser build, renderer, resolution, power mode and scene revision. Warm 30 seconds, sample 60 seconds, run five alternating repetitions. Capture p50/p95/p99/max CPU and GPU frames, draw calls, triangles, texture residency, upload stalls and process memory. GPU timer queries must reject disjoint results; use engine/GPU profiling where queries are unavailable.
5. **Stress gate:** identical replay at 1/48/256/512 actors; open ground, overlapping crowd, bridge/interior occlusion, canopy, many costume variants, cold/warm streaming and ability-heavy combat. Separate no-effects from essential-effects and full-decoration runs. Compare both **equally batched**, at the same rendered dimensions; log split view separately. Include actual skeletons/animation when comparing production models.
6. **Acceptance gate:** choose sprites only if they meet the visual gate and demonstrate a worthwhile measured frame-time or memory benefit on the chosen targets, or a documented art-production benefit within performance budgets. A proposed performance threshold is a reproducible 15% improvement in the actual bottleneck metric without new p99 stalls or memory violations. If differences remain in noise, favor the simpler coherent asset pipeline; neither reduced triangle count nor this short CPU sample is a win by itself.

## Integration handoff for Claude

Wait for the active casting/interface merge before branching from the resulting state. This deliverable changes no shared files and requests no merge of the experiment into the shipped runtime.

1. Define a presentation-only `AppearanceCatalog` mapping `look`/`pattern` to an approved appearance ID and an `EntityAppearance` implementation with `applyPose`, `applyVisualState`, `setQuality` and `dispose`. Keep the current placeholder implementation as the default and fallback. Avoid concrete weapon/monster switches in the adapter; content supplies the manifest.
2. In Godot, attach the appearance beneath `EntityView` while keeping authoritative/interpolated root position, independent labels, picking and lifecycle. Prototype `Sprite3D` cutouts first; batch compatible cohorts using MultiMesh only after profiling. In three.js, adapt the existing shape/body seam, reuse caches, and use chunk/material buckets for instanced cards or meshes. The experiment's disabled culling and static pose buckets are not a drop-in production design.
3. Put mode and tier settings behind opt-in client configuration, defaulting to current placeholders until a complete vertical slice passes. A candidate policy is sprite at approved isometric ranges, mesh at close perspective/extreme pitch, with hysteresis and a transition that does not shift feet or double-draw long enough to cause excess overdraw. Keep root pose, target ID and UI identical across switches.
4. Preserve the generated protocol and server simulation. If public cosmetic data is required, review the smallest appearance-ID/revision extension separately, regenerate TypeScript, and test full-state/delta behavior. Never let the renderer grant equipment, resolve damage, infer hidden entities, or replace collision dimensions.
5. Add the import/cache validation and replay cases above, then run the repository build, tests, content validation, both client smokes and performance gates on provisioned machines. This study's evidence is not a substitute.
6. Rollback is a config switch to the existing placeholder adapter followed by disposing adapter-owned textures, meshes and caches. No saved character data, world generation, server tick, transport or authoritative outcome should depend on the selected representation.

The first deliverable ends at this reviewable comparison, manifest proposal and integration plan. No commits, pushes, production assets or deployments were made.
