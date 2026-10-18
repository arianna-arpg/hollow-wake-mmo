# Appearance

How the world and the things in it look on both clients: bodies and props, the ground, the water, the light and the day, and the motion of bodies and feedback. Authored presentation is content the clients read; the protocol supplies the worn item classes and world day, while the simulation never reads the presentation content; a mod recolours a hob, adds a creature's look or retimes a swing's anticipation by editing data. The art direction this serves is [visual-language.md](visual-language.md); the representation choice and its measurements come from [sprite-rendering-study.md](sprite-rendering-study.md). Cast telegraphs, releases and impacts are the cast cues of [cast-presentation.md](cast-presentation.md), consumed here and never duplicated.

Three rules hold everywhere:

- **A look is content.** `content/core/appearance/` holds four kinds, registered beside the interface kinds (`Content/Kinds/AppearanceKinds.cs`, definitions in `Definitions/Appearance.cs`): `ui_appearances` (bodies and gear), `ui_surfaces` (the drawn ground and water), `ui_world` (the day, the sky, the haze, the foliage) and `ui_motion` (animation and feedback timings). Validation names a family no client builds, a shape number no family reads, a malformed colour, a keyframe out of order.
- **Composition over bespoke.** A body is a plan of parts on bones built by a family (biped, quadruped, tree, rock ...) from an entry's proportions, palette, features and gear. The engine knows families, never a hob. Gear is its own family on a body's sockets, so the worn weapon and armour class read on any body with hands and a chest.
- **The server's facts, the client's pose.** Every pose follows state on the wire: the walk from the drawn motion, the swing's anticipation from the cast's progress, its release from the cast's end, the flinch from a confirmed hit, the fall from `dead`. Nothing is retimed for beauty, and seeded variation comes from the entity key, so two clients draw one body.

## Looks

An entity finds its body entry in `ui_appearances` by its look id first (a pattern's `look`, which is the pattern's id when it names none; a Calling's `look`; `drop:<item base>`; `poi:<place>`; `fount`; `projectile`), then by its pattern's tags, including those inherited through `extends`, against each entry's `match` list, highest `priority` first, ties by id; the lowest-priority entries are the fallbacks by what a thing is (`creature`, `beast`, `humanoid`, `prop`, `flora`, `device`). A place marker resolves its point of interest's tags the same way (a knot's marker is knotted). An unresolved look draws a plain block, never nothing.

An entry names its `body` family and gives it `shape` numbers (proportions and switches over the family's defaults), a `palette` of named colours, optional `features` the family knows (long ears, a snout, horns, a tail, antlers, tusks, a hood, a leaf crown, runes), default `gear` per slot as item classes, a `variation` (seeded hue, value and size jitter so a pack is not identical twins), a `card` policy (`never`, `allow`, `prefer`) and a `scale` over the entity's height. The families and the shape names each reads are the vocabulary in `AppearanceKinds.BodyFamilies`; both clients implement the same list (`World/Logic/BodyFamilies.cs`, `web/src/world/appearance/bodyFamilies.ts`), part for part, so the same entry builds the same body.

Gear entries have a `slot` and are found by the worn item's class (hierarchical, as the core's tags are: `weapon.sword` satisfies `weapon`) and tags. What a body wears comes, in order, from the owner's `self_state` equipment (the player's own body), the `appearance` its state carries for another player (one entry per worn slot, the worn item's class and tags, as [networking-and-persistence.md](networking-and-persistence.md) lays it out), a monster pattern's equipment grants (the brigand's sword and jerkin), the entry's `gear` map, and for a player look the Calling's starting items; the catalog's `WornBy` is that order on both clients. The wire's descriptor is read as the owner's equipment is, so the class, its trailing word and the tags find the gear entry, and a change of it rebuilds the body. An available self equipment map takes precedence even when empty. Otherwise a present public descriptor is authoritative, including an empty list for an unadorned body. Only an absent descriptor, as from an older server, restores the Calling's kit. A dropped item lies on the ground as its gear piece on a ring, a parcel for materials and a gem for stones and jewels.

## Bodies

A plan (`World/Logic/BodyPlan.cs`, `bodyPlan.ts`) is bones with pivots in a hierarchy, parts (boxes, ellipsoids, cones, cylinders and wedges with a size, a centre, a turn and a colour) on those bones, and sockets gear attaches at, in body space (x forward, y up, z to the body's right). Parts are flat shaded facets in vertex colour, converted to linear for the renderer, so bodies read as painted. The desktop merges each bone's parts into one mesh under a node per bone; the browser does the same with a group per bone. Glowing parts (eyes, fount lights, a wand's tip) draw unlit.

Each body stands on a **contact mark**: a soft disc that gives it its ground contact, with an edge grammar per relation to the players from the factions content (inward teeth for a hostile, spaced ticks for a friend, a plain rim otherwise) so danger reads without colour. The mark follows the body's root, not its pose.

### Anatomical families

`AnatomyFamilies.cs` and `anatomyFamilies.ts` add exposed skeletal bipeds, stitched flesh bipeds and skeletal quadrupeds to the family registry. They use the ordinary biped and quadruped rigs, with shared sockets for equipment. Rib spacing, limb thickness, shoulders, asymmetry and proportions are content shape choices; cowls, crystals, conductor yokes, vials, bindings, grafts and missing legs are presentation features. Mage schools must remain distinguishable by silhouette in grayscale, as well as by their accent colors. These features never grant gameplay behavior.

Prototype-derived looks preserve their source look ids in metadata. The shared `tests/fixtures/undead-appearance.json` pins the reviewed plans, including parts, bones, sockets and worn gear, for both clients. `experiments/undead-entities` contains the translation ledger and the actual-geometry gallery used to inspect isometric, third-person and grayscale poses.

### Two representations

The **bodies** option (Graphics) chooses painted geometry, directional cards, or by camera: cards in the isometric camera once it is zoomed out past the card zoom (with hysteresis), geometry in third person and up close. A card is the entry's body baked once per look and gear into an atlas of eight azimuths from the isometric pitch (`World/Appearance/CardBaker.cs`, `cards.ts`), drawn as one foot-anchored quad the body's size that turns to face the camera about the vertical and samples the cell nearest the camera's bearing from the body's yaw. Cards are depth-tested and depth-written, so they occlude and are occluded like geometry; they cast no shadow, carry no per-entity colour variation, and the animator moves only their root (bob, lean, squash, the fall of death). Bodies taller than four units and entries marked `card: never` stay geometry. A body draws as geometry until its atlas is ready; the baker bakes a cell per frame on the desktop and an atlas per frame in the browser, so a crowd arriving at once costs frames, never a stall. Switching the option back disposes the cards; the atlases are forgotten with the views that held them.

Browser atlas baking restores the caller's render target, viewport, scissor, clear color/alpha and automatic clearing before the main scene draws, including on failure. Baking must never leave the world rendering into an atlas-sized viewport.

The smokes measure both on the hamlet on entry and print the line the pull request and the rendering study record: frames per second, frame time percentiles, draw calls, primitives, the bodies drawn and how many are cards, atlas bytes and texture memory.

## The animator

`World/Logic/Animator.cs` and `animator.ts` turn a visual state into bone turns, offsets and scales over the plan's rest pose, from the `ui_motion` timings:

- **Idle**: breath on the torso, a slow look of the head, a sway, phased by the entity key.
- **Walk**: the legs and arms swing in opposition with a phase advanced by the distance the drawn body moved over its stride (scaled by the body's height), a bob and a lean into the step; a quadruped's diagonal pairs; a lean into acceleration read from the drawn motion.
- **Swing and cast**: with a swinging weapon in hand (a blade, an axe, a spear, a maul) the arm draws back with the cast's progress and comes through over the release time when the cast ends on the wire or the body lands a blow, then recovers; without one the hands gather and raise with the progress and thrust on release; a channel sways, a guard presents the off hand; a quadruped rears and lunges, a crawler opens and snaps its claws.
- **Hit**: a flinch away from where the blow came (the striker's drawn position), a squash and a flash that decay over the hit timings, stronger for a heavier blow against the target's life.
- **Death**: the body falls over the fall time and settles, bipeds on their back and beasts on their side, wisps sink; a corpse sinks and fades long after.
- **Flora**: canopies sway, knots turn.

Reduced motion (Accessibility) keeps every functional pose (the draw back, the release, the guard, the fall) and drops the idle sway, the flinch and the camera shake, as the visual language asks.

## The world

`ui_surfaces` colours the ground: a cell takes the first ground rule whose surface tags and cell flags it has, then every matching tint in order (roads, cliffs, shallow and deep water, blocked ground); a structure cell takes the first structure rule, whose `side` colours the skirts below its top (walls read as stone or wood) and whose `roof` raises a pitched cap over the cell (houses, tents); layers (bridges, floors) have their own rules. A seeded value jitter in patches a few cells wide, from a hash of the cell's world position, keeps a field from being one flat colour; structures and roads stay even. Both clients read the same rules, so the browser no longer carries a terrain palette in its configuration. The water is a lit plane with ripples and a small light beneath its crests (`water` in the surfaces entry; the water detail option stills it). Terrain generation is untouched: only the drawing changed.

`ui_world` drives the light: the day is the world's as the server reports it (`enter_world` carries how long it lasts and the fraction it stands at, the self state the fraction on its cadence; see [networking-and-persistence.md](networking-and-persistence.md)), advanced between messages by the client's estimate of the server clock, so every zone shares one sky and a rule that reads the time of day agrees with what a player sees; a server that predates the fields leaves the client to fold the zone clock by `day.length_seconds` from `day.start`, which agrees between zones only while their clocks do. The sky blends between its keyframes at that fraction (sky, horizon, sun colour and energy, ambient, fog), the sun climbs over `sun.daylight` of the day around noon to `sun.noon_pitch` over `sun.noon_yaw`, and the haze thickens at night. The desktop drives its sky material, sun and environment from it; the browser its background, hemisphere light, sun and fog, and the bodies' own light. Decorative `foliage` tufts are scattered by surface tag and density (the foliage density option scales them), seeded from the cells so both clients and every visit agree; they are one instanced mesh per chunk and exist nowhere in the simulation. The developer options hold the time of day for previews.

## Feel

The camera eases its zoom and its turn and shakes on a hit taken and a kill (`camera` timings; the screen shake option and reduce motion still it). Combat text sizes by the blow's weight against its target's life and pops in, heavier numbers rising slower. Prop labels show under the cursor or within `labels.prop_reach` of the hero and fade otherwise, so a dense field of nodes is not a pile of names. The grayscale check (Accessibility) draws everything without colour for the visual language's readability gate.

## Checking

`tests/HollowWake.Client.Tests/AppearanceTests.cs` and `web/src/world/appearance/appearance.test.ts` read the shipped content the way the clients do and pin the same things: every pattern the world spawns, every Calling, every place and every item base has a look of its own and not a fallback; every entry builds a finite body; gear fits a biped's sockets and lies on the ground; the animator poses every rig in every state with numbers, draws a swing back and through, flashes a hit and lays a corpse down; the surfaces colour cells by tags and flags with a stable jitter; the day wraps and blends without a jump. The smokes add the look's stages: both representations measured, the grayscale and reduced-motion shots, and the day at night and evening.

## Still rough

Bodies are generated low-poly proxies, not authored models: a sculpted character, a rigged animation set and painted textures remain art work, and cards carry no per-entity variation. Houses straddling a chunk border get two roofs. No audio exists yet; the animator's moments (a footstep, a swing, a hit, a death) are where footstep and surface sounds attach when assets arrive, on the client's audio buses. The isometric camera still passes through canopies. The reviewer's human readability check of the visual language has not been run.

## Physical silhouettes

The paired `goblinkin`, `gnasher` and `warbeast` builders use breadth and depth ratios to the authoritative body height. Content with a physical profile keeps those ratios aligned and disables random scale variation. Ears, noses, jaws, tusks, clothing and carried equipment remain presentation. Small pack creatures, upright raiders, long-armed brutes and low quadrupeds share existing animation rigs. A preview must include a common world-scale lineup as well as enlarged individual cards so silhouettes are not judged only after independent camera fitting.

### Adult stature and heavy bipeds

The `orc` family composes a forward-set skull, high trapezius, broad chest and heavy segmented limbs on the ordinary biped rig. `hunch` moves the skull forward without silently shrinking the body's rest height. Breadth, depth, arm length, muscle and jaw proportions remain authored shape values. Equipment and features distinguish roles in grayscale. Both clients build the same full plans and gear under the shared fixture gate.

Gnasher features include tusks, crest, spots, flank plates and an empty saddle. The saddle is presentation, sized for a small rider; it neither creates a rider nor transfers control. Reference lineups compare the player, skeleton, ordinary Goblin, Orcs and Gnashers at one world scale. Ordinary player and skeletal looks preserve authoritative scale without random size jitter; decorative horns, banners and headwear can extend beyond body stature. Precise collision fitting is a separate simulation contract, never inferred from these appearance parts.

### Wildlife anatomy

The fauna quadruped, ground-bird and crab families register in both clients. Breadth and depth follow each physical profile; leg, head, tail, ear, beak and claw proportions plus optional anatomical features distinguish silhouettes. Quadrupeds use the existing four-legged rig, birds the biped rig with folded wing parts, and crabs the crawler rig. This supplies shared procedural poses, not species-specific animation or new locomotion. Body height is nominal stature; ears, antlers and crests may exceed it as decoration. Rendered dimensions are not damage volumes. Paired plan fixtures, grayscale/camera previews and a single-scale lineup check translation and continuity.

## Canine and ungulate bipeds

The `gnoll` family draws a hunched torso, low forward canine skull, grinning tooth row, ear variants, tail and paw toes. The separate `beastkin` family draws caprine or bovine faces, curved/swept/spread horns, bent hocks and cloven hooves. Both use the existing biped animation rig; the segmented legs are authored rest anatomy, not inverse kinematics or new foot contact behavior. Family shape vocabularies are registered additively. `grip` sizes worn equipment to the hand height, keeping heavy weapon tips above the ground at rest. Horn tips, tails, carried props and gear are visual extremities outside the conservative body envelope. Geometry fixtures and the common-scale gallery check native/browser parity; they do not establish precise collision volumes.
