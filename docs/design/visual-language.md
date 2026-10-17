# Visual language

## Status and authority

This is a proposed art-direction contract and a bounded review study. It does not change gameplay, production assets, client code, content schemas or the accepted renderer architecture. The world remains three dimensional, with the isometric and third-person cameras in [client.md](client.md) and [web-client.md](web-client.md). Renderer and sprite implementation choices belong to the separate rendering study.

Existing decisions remain authoritative: [VISION.md](../VISION.md), the [lore bible](../lore/00-bible.md), [cosmology](../lore/cosmology.md), [regions](../lore/regions.md), the [interface arrangement](ui.md), [options](options.md), [casting](weaves-and-effects.md), [deliveries](deliveries.md), [territory](territory.md) and [crafting](crafting.md). In particular, the nine Threads are fixed, Callings are starting paths, a Fount binding is a rite, and every outcome belongs to the server.

**Recommended for adoption:** the material hierarchy, shape vocabulary, functional/decorative separation, phase semantics and validation gates below. Palette values, motif drawings, ornament density and cosmetic decay times are starting proposals. The concept is an invented study location, not a canonical depiction of Ninewells or a new lore decision. Its grand horizon and fine surface detail are mood references, not a production complexity target.

## Review artifacts

- [Interactive study](../../art/visual-language/study.html): open directly in a browser. Select a Thread and phase; compare bright ground, crowd density, grayscale and reduced motion. A second scene compares unbound, Vigil, bound and contested Founts. It uses original Canvas diagrams and local files only.
- [Optional local concept sheet, excluded from Git](../../art/visual-language/hollow-wake-concept.png): an environmental vignette and a Flame sequence with anticipation, release, impact and recovery.
- [Proposed presentation recipe](../../art/visual-language/visual-recipe.example.json): a discussion example, explicitly not loadable game content.
- [Generation prompt](../../art/visual-language/concept-prompt.txt): full provenance for the concept image. Built-in imagegen; no image reference inputs or third-party art copied into the project.
- [Validation report](../../art/visual-language/validation.txt): observed local checks and remaining work. [Review script](../../art/visual-language/review-study.mjs) exercises the standalone study through the existing local browser dependency.

The study's phase playback is four equal presentation beats. Those beats do not prescribe cast, projectile, hit or recovery durations. Its crowd is a readability sketch, not a representative load test. Its glyphs are design proposals, not a newly declared in-world alphabet.

## The direction: a tended world with visible tension

Aureth should feel deliberately made and quietly left behind. An empty seat at a Firstwork can say more than an inscription. A swept path beside a ruin says someone cares. Magic briefly exposes the grain of the world. Keep that exposure rare enough to matter.

Use broad painted planes, simplified silhouettes and selective edge detail. Limestone is matte and heavy; cloth folds and catches light at its hem; copper and brass mark joints and touch points; water holds a small light beneath its surface. Firstworks have measured, slightly impossible alignment. Marcher work is practical, repaired and grown into older foundations. Avoid making every prop an ornamented artifact or every grove a glowing spectacle.

At gameplay distance, read in this order: navigable ground and silhouettes, active danger and target contact, interactive subjects, material identity, fine decoration. Quiet backgrounds carry variation through large value groups. Reserve narrow bright accents for current action. This is compatible with painted geometry or directional sprites; neither representation is chosen here.

Suggested base tokens: ink `#142025`, limestone `#d8d2ba`, worn brass `#b89a67`, moss `#71816c`, deep water `#314f52`, bone highlight `#eee9da`. These are art swatches, not a claim of verified UI contrast. Give critical marks a dark backing stroke and a pale inner stroke so they survive both chalk and deep forest. Do not rely on bloom for their edges.

Characters need a distinct head, shoulder/weapon mass, open arm gesture and visible contact with the floor. Show build identity through equipment, stance and the currently woven Thread; do not lock a Calling to one hue or silhouette. Character creation should preview the same character in neutral light and at gameplay size. A cosmetic may add material and ornament; it must retain the functional direction, readiness and danger vocabulary.

## Nine Threads, nine grains

Each identity has a shape, a movement character and a still substitute. Colour enriches identity, never determines danger, allegiance, readiness or availability. The colours are proposed bright cue samples; environment versions should be quieter.

| Thread | Cue sample | Shape and grain | Still or reduced-motion identity |
|---|---|---|---|
| Flame | `#edac69` | Rising hook, tapered flare, upward grain | Hook with a short rising tip |
| Tide | `#81c3c9` | Paired long curves, a pull and return | Two unequal wave strokes |
| Storm | `#c5b7ef` | Forked angles, directional snap | One branch silhouette, no repeated flicker |
| Stone | `#c5ae82` | Stacked facets, compression, weight | Low faceted block and footing |
| Bloom | `#a5c58b` | Branching spine, paired leaves, unfurling | Leaf pair with an exposed stem |
| Umbra | `#c1a1c8` | Folded veil, soft downward grain | Closed soft fold with settling bars |
| Lumen | `#eee0a3` | Straight rays, aligned facets, steady light | Faceted diamond and axial strokes |
| Void | `#bcc5d5` | Broken brackets, missing interior, discontinuity | An open gap held by separated brackets |
| Mind | `#d4dcd8` | A grain that returns on itself | Linked returning loops with a small index stroke |

Umbra is rest and decay, not evil; Void is absence and possibility, not a darker Umbra. The bright Umbra swatch is an edge accent around its dark material. Void's interior belongs to the scene behind it; it must not erase hazard boundaries. Mind's silver is distinguishable from Lumen's gold even without hue through loops versus straight axes.

For a mixed weave, keep the delivery silhouette and use a neutral gathering seat with at most two readable motif accents. Extra components can appear on inspection. Do not rainbow-blend all Threads into a new indistinct identity. Semantic delivery changes such as fork, chain, ring, anchor and repeat alter the path or boundary only when the compiled delivery actually has that behavior. A dormant glyph contributes no active behavior cue.

## Casting: four readable phases

The lore's Reach and Draw inform anticipation; Cast informs release; Fall informs ending. Contact is a presentation phase inside a delivery, not an added cosmological movement. A long-lived field can outlast its caster's recovery, and a channel can make many contacts. These are independent timelines, not one obligatory four-frame animation.

| Phase | What the player should see | Source of truth | What must not be implied |
|---|---|---|---|
| Anticipation | Body takes weight; rear intake travels through shoulders and arms; hands gather the delivery's shape | Authoritative cast state, resolved aim, actual finite progress if known | A decorative circle is not an area-of-effect boundary; a repeating channel beat is not completion |
| Release | The shape leaves its seat with an unambiguous direction; a summon's cradle opens at its resolved placement | Confirmed release with payload, or authoritative spawned delivery | A disappearing cast bar is not success; release is not contact |
| Impact | Small high-value contact core, then a Thread-shaped fan or crease at the confirmed point | Confirmed hit/outcome correlated with the delivery | Never invent a hit for a projectile that expired, missed, or was evaded |
| Recovery | Body relaxes; decoration separates into open fragments and fades | Cosmetic tail after a confirmed event; gameplay lock separately from server recovery state | A fade is neither cooldown nor proof that another cast is allowed |

Use the same grammar for players, enemies and summons. Attack preparations can remain bodily and weapon-led; adding a Thread does not require a wizard halo. A bolt gathers toward an exit point, a beam aligns parallel strands, a strike prepares an arc, a field opens a placement frame, a summon assembles a cradle, a guard presents a frontal face, and travel establishes an exit direction. These are composable presentation families rather than one animation branch per weave ID.

**Other endings and held modes:** interruption breaks strands inward and removes readiness; cancellation unthreads without a launch; a dry or underfilled release that fires no payload settles without impact. If the end reason is unknown, dissolve neutrally. A guard's raised shape, remaining pool, parry window and bash readiness are distinct signals. An endless channel has no completion promise. Overcharge stages use distinct filled segments; brim uses a fixed notch. Keep functional stages visible with all decoration disabled. Instant casts can go directly to release. Cosmetic fades may overlap the next action but must recede beneath it.

Suggested cosmetic starting point: a contact core held briefly, followed by a roughly 120–240 ms tail; no full-screen flash or compulsory camera impulse. This is a tuning range only. Cast length, timing windows, active fields, channel beats, travel speed and actual recovery are never retimed for beauty. Preserve the body outline and the target contact point throughout.

## Danger, crowding and two cameras

Relation and Thread are separate channels. Proposed ground-edge grammar: inward teeth for a hostile extent, spaced square ticks for a supportive extent, neutral dashed brackets when relation is unavailable, and a separate owner emblem for territory. A buff is not safe merely because it is Bloom, and an Umbra effect is not hostile merely because it is dark. A target or self-cast with no area must not acquire an ornamental area boundary.

Ground marks use the server's registered shape, dimensions, inner gap and heading. An inner hole is safe only when the delivery geometry says it is. Radius is not inferred from particle reach. Preserve exact occupied surfaces on bridges, floors and slopes; never stamp a bridge hazard onto the ground below. World position, height and depth determine contact and occlusion in both cameras. Orthographic review is insufficient for a mark that becomes edge-on in third person: allow a short vertical boundary accent or a compact on-screen indicator, without changing the footprint or exposing concealed opponents.

Proposed priority order: imminent danger affecting the local player; the player's own body/readiness; selected target and confirmed contacts; nearby supportive extents; other functional actions; other players' ornament; ambient motion. Critical cues remain even at the lowest quality tier. Reduce distant ally filaments first, then secondary motes and ambient motion. Do not drop hostile outlines to meet a particle limit, or merge areas into a convex envelope that invents danger in a safe gap. If simplification still cannot make a crowded fight readable, the presentation has failed the gate; opacity alone is not a solution.

Keep active edges crisp and interiors mostly open. Bound the flash around contact. Permit density controls per relation and distance but impose a functional floor. Decoration can be culled or batched independently; performance targets must be measured in the renderer study, not guessed from this Canvas sketch. The existing [performance contract](performance.md) budgets server work, not GPU frame time.

## Accessibility is a mode of the same language

Reduced motion removes camera impulses, orbiting motes, decorative travelling waves, repeated flashing and large expansion. Replace motion with static segments, a single directional release stroke, a contact mark and a non-pulsing end state. Keep the true timing bar, threat footprint and stage count. Do not lengthen or move a gameplay window. The study starts still and honours the operating system's reduced-motion preference.

Test monochrome first, then colour-vision variants with users. Shape, light/dark contrast, placement and optional concise text duplicate the critical state. An interruption must not be only a sound or a red tint. Provide numeric readiness and status options from the existing options contract. Audio may reinforce grain, weight and outcome; every required action remains possible muted. Show-don't-tell is about communicating through play, not removing labels and accessibility aids.

HUD material follows [ui.md](ui.md), whose arrangement is already settled. Use dark inset surfaces and restrained worn brass joins. Keep the life and mana fills, translucent shield, etched ward, poise/insight/stamina rings and reserved caps visually distinct by texture and edge treatment. A cracked poise ring must remain cracked in monochrome. Use the same Thread symbols in weave slots, crafting material cards and world contact. Put fine texture at the frame edges, not beneath numbers. Tab selection, dormant reasons and keyboard focus need explicit geometric states. Material recommendations do not create a second inventory hub or rearrange the cluster.

## Environmental communication

| Situation | World cue | Data condition and guardrail |
|---|---|---|
| Discoverable Landmark | Deliberate silhouette, aligned path, human-scale empty seat or repaired approach | Authored landmark vocabulary applies equally to generated and pinned placement; visibility and discovery remain authoritative |
| Active Knot | Tension, repeated irregularity, roots that turn against the terrain's grain | Distinct from ordinary decay; cleanup follows authoritative state, not a local timer |
| Resource available / depleted | Exposed vein, ripe head or taut floss; after harvest a visible cut, empty stem or open spool | Decoration does not invent harvestability; focus/interaction adds an explicit icon and optional label |
| Quest destination | Landmark sightline and physical evidence appropriate to the objective | Reuse authored location cues; offer a configured tracker and inspect aid rather than hiding necessary information |
| Vigil in progress | Partial tie, binder posture and discrete progress segments | Driven by rite progress; cancellation opens the tie; incomplete binding never displays an owner seal |
| Bound Holding | Closed Fount tie, separate owner emblem, tended beds, woven-up threshold, maintenance details | Only actual structures, ownership and active overlays change the world; the scene is a semantic example |
| Contested Holding | Existing owner retained; second separated tie and split-knot badge | Requires an actual contest signal; do not fake capture by recolouring the entire landscape |
| Decree / upkeep change | Regional growth, water, lantern or maintenance changes appropriate to the decree | Apply only confirmed generation/state changes; remove decoration cleanly when overlays end |
| Crafting: timing | Shuttle reaches a fixed notch in a held frame | Use the server's seeded challenge windows; cosmetic easing never moves the valid interval |
| Crafting: sequence / alignment | Distinct Thread-shaped tokens / clearly indexed interlocking dials | Symbols plus labels; server scores raw input; success is shown only when confirmed |

A Holding should feel like a miniature kingdom through connected roads, working places, defensive choices and maintained land. Reserve boundaries for inspect, editing and contested moments. Constant neon territory walls would obscure the world. The Wellmother at Ninewells is a special canonical common binding and cannot be contested; the generic contested study does not apply to it.

## Composition and integration handoff

Treat visual recipes as a client presentation registry. Select by authored visual profile, then semantic Thread/delivery composition, then a neutral fallback. Define family, material grain, anchors, phase roles, contrast treatments, relation sign, reduced-motion replacement and density tier as data. Keep numeric look tuning in presentation content. Neither gameplay effects nor protocol messages should carry shader colours, font names or rendering instructions. The example JSON illustrates responsibilities, not a schema ready to merge.

Separate resolved semantics from renderer primitives. A renderer adapter places the same recipe at the hand, body, target surface or delivery entity using the client's coordinate converter. Author anchor compatibility and bounds for each body representation. The same message playback should produce equivalent phase meaning in Godot and the web client. Unknown profiles resolve to a neutral readable cue; missing timing remains unknown. Stable visual variation may derive from an event identifier without consuming gameplay RNG.

### What is available versus what is proposed

At inspection, `EntityState` carries `Casting`, `CastProgress`, world transform, `Shape`, `ShapeSize`, `Heading`, `Spin` and `Until`. The owner's `SelfStateMsg.Cast` is a richer `CastInfo` with mode, progress, stages, brim, held time, duration and guard pool. `WeaveSeatInfo` supplies Threads and cast mode for the owner's slots. Those facts permit bounded progress and delivery geometry; remote actors do not automatically have the owner's detail.

`CombatEventMsg` and the inspected `ZoneInstance` broadcast path expose damage/block/evade, heal, status and death. The core's `WeaveReleased` and `WeaveInterrupted` events are not evidence of a reliable client lifecycle stream. No complete wire contract for cast identity, release payload result, interruption reason and recovery end was found in the inspected files. Do not synthesize them from a name string, a depleted resource or a missing bar.

Proposed follow-up for the owning runtime team: a small semantic descriptor/event path with cast-instance identity, authoritative timestamp/sequence, resolved delivery profile or semantic tags, release-with-payload outcome, cancellation/interruption result and recovery end. Correlate contact with cast/delivery identity; provide a surface anchor for layered ground where needed. Deduplicate replayed events, expire stale decoration, clear on death/despawn, and rebuild ongoing functional cues from snapshots after reconnect. A late projectile spawn may justify drawing that projectile; it does not justify replaying a guessed anticipation or fabricating an impact. Under an unknown end, neutral dissolution is the safe presentation.

Claude integration sequence:

1. Review this visual vocabulary and the rendering study together. Agree on one body representation and anchor contract without changing the 3D world or camera promises.
2. Inventory actual server descriptors after the active casting/interface merge. Close lifecycle gaps before wiring outcome-specific flourishes. Record any protocol decision in its owning contract.
3. Implement one family across both clients: neutral fallback plus Flame bolt, then one area, one held channel and one guard. Include interruption, underfilled release, death and reconnect before adding decorative variants.
4. Add the remaining Thread recipes and semantic glyph overlays. Keep HUD theme and settings on their established content paths.
5. Validate both camera modes and crowded combat, then permit asset production. This study authorizes no shared runtime changes by itself.

## Acceptance gates

These are proposed gates for integration; the standalone study does not claim they pass in game.

| Gate | Concrete review | Pass condition |
|---|---|---|
| Phase truth | Record finite cast, instant cast, endless channel, overcharge, guard, dry release, interrupt, death and reconnect | Release/contact only on confirmed events; no false completion; recovery lock never inferred from a fade |
| Shape truth | Compare telegraphs and delivery shapes to server debug geometry on flat ground, a slope and a bridge in both cameras | Extent and inner safe region agree within two screen pixels at test resolution; no bleed to the wrong floor |
| Still readability | At 1280×720, effects off except essential marks, grayscale, audio muted, motion reduced | Direction, hostile/support relation and ready/held/spent state remain distinct; capture evidence |
| Crowd | Fixed replay with local player, selected target, at least 24 visible actors and eight overlapping extents, bright and dark terrain | All imminent hostile edges and genuine safe gaps remain readable; selected body/contact are locatable; no functional cue culled |
| Small-size proof | Inspect normal gameplay zoom and a deliberately small roughly 48-pixel-high actor | Foot point, attack direction and functional state read without decorative detail; unresolved symbols use a compact explicit fallback |
| Human check | Give five unfamiliar reviewers a short cue legend, then ten mixed still/short-clip cases per reviewer | At least nine correct phase/relation identifications per reviewer; revise recurring confusion. This is a usability target, not a statistical accessibility certification |
| Territory | Replay bind/cancel/complete/contest/decree removal/overlay refresh | Owner, progress and changed land agree with state; no premature owner change or persistent removed decoration |
| Interface | Inspect all orb layers, dormant slot, selected tab, reserved pool and keyboard focus at supported scales | Distinct in monochrome, readable text and no decoration covering interactive content |
| Composition | Apply an unrecognized profile and a new combination of existing Thread/delivery tags | Neutral fallback or composed recipe works without a skill-ID branch; dormant glyphs add no functional cue |
| Client parity | Run identical event fixtures through Godot and browser adapters | Same geometry, phase meaning, end result and accessibility alternatives; decorative material may differ |

The renderer study owns frame-time, overdraw, memory, direction counts and batching measurements. Set GPU budgets from that evidence and the intended hardware. Passing a Canvas control check is not passing a game-performance gate.

## Provenance and limitations

The previous Hollow Wake visual integration worktree was inspected read-only at `C:/Users/John/.codex/worktrees/skill-visual-integration/ARPG Game - World`, with HEAD verified as `e31d146c` during this study. Sources: `src/data/skillCastVisuals.ts`, `src/render/vis/skillCastPreparation.ts`, `src/engine/castingCues.ts`. Its main checkout at `D:/Games/Claude/ARPG Game - World` supplied project guidance. Useful ideas: families separate from hue, body-seated preparation split behind/in front, neutral fallback, true completion rather than cyclic channel progress, and state-confirmed endings. Its old school lists, named-skill overrides, 2D geometry and gameplay rules are not copied into this MMO.

External references checked on 2026-10-17:

- [Ragnarok Online's official beginner guide](https://renewal.playragnarok.com/gameguide/howtoplay_beginnerguide01.aspx) describes illustrated characters against 3D backgrounds. This supports exploring that separation of visual roles; it does not establish performance or suitability for Hollow Wake's cameras.
- [Tree of Savior's official development interview](https://treeofsavior.com/page/news/view.php?n=331), published 2015-12-31, describes its emphasis on hand drawing, shape and a fairy-tale impression. Borrow the focus on authored shapes; Hollow Wake keeps its own earnest, numinous lore tone.
- [The exact user-supplied Lumoria page](https://lumoriagame.com/#play) was inaccessible through the web tool. No claims about its rendering, no borrowed art and no substitution of the differently named Lumora game are made.

No third-party screenshots, fonts or textures are bundled. The generated image establishes mood and a reviewable sequence, not a production sprite sheet, physically exact telegraph, verified camera solution or approved canonical location. The diagram demonstrates shape, placement and state signs; it does not implement the nine full animation grammars, real occlusion, network timing or player customization. Human readability testing, colour-vision testing and engine captures remain open. No game assets were installed, and no commit, push or publication was performed.
