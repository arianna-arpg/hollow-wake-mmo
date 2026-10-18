# Kobold scaleclans content claim and review

2026-10-17. Codex owns `codex/kobold-scaleclans`, based on fauna PR #34 at `90cb7fcb5eec97a6ad438c10f7b564f1dfa88790`. This is a brand-new reptilian faction, not a prototype Kobold port. Gnolls and Beastkin belong to the parent chat; golems, elementals and precise hitboxes belong to concurrent chats.

## Declared overlap before editing

Dedicated Kobold definitions, geometry, tests, fixtures and evidence are owned here. Additive registrations will touch `src/HollowWake.Core/Content/Kinds/AppearanceKinds.cs`, `src/HollowWake.Client/World/Logic/BodyFamilies.cs` and `web/src/world/appearance/bodyFamilies.ts`. The entity and appearance contracts will gain narrow anatomy/population notes. World generation golden expectations may move when dedicated Kobold POIs enter existing budgets; capture before/after seeds and explain changes before refreshing them. No collision, movement or AI runtime changes are planned. Integrators must combine concurrent registrations and recompute world goldens on the combined content, not choose one branch's expectations.

## Scope and limitations

Scaly lesser dragonkin with snouts, tails, crests/frills and clawed feet; small nimble roles plus heavier specialists. Existing supported skills and state machines are replaceable scaffolding. No flight, breath attacks, traps, invented tactics, equipment-driven armor simulation or new immunities are implied. Read `2026-10-18-fauna-ai-handoff.md` for the separate proposed AI design work.

## Implemented and inspected

Fourteen roles belong to `kobold_scaleclans`: small scavengers, knife fighters, archers, stone throwers, spear sentinels and elemental weavers; a heavier breaker and bulwark; a rallying crestcaller and scalechief. Definitions carry independent species, body, role, ground locomotion, culture, size and actual habitat classifiers. The faction is hostile to players and settled factions, neutral to Goblins/Orcs/wildlife, and allied to itself, verified in both directions. Rewards use a dedicated salvage table plus authored XP.

Every body uses a flesh/scale/iron physical profile, presentation proportions derived from the authoritative width/depth/height, and zero size jitter. Ordinary heights are 0.82–1.15 m, exceptional bodies 1.35–1.48 m. The shared-scale lineup uses the 1.8 m player/skeleton and 0.9 m Goblin. Scavenger/Bulwark mass is 19.349/148.850 kg; equal requested shoves travel 15.219/1.175 m with their authored poise. These are authored game approximations.

The new generic `scaled_biped` family is paired part-for-part in native and browser clients. It provides snouts, slit eyes, scutes, belly plates, hocks, clawed feet, long tails, frills/crests and role equipment on the existing rig. Reviewed gallery evidence includes both camera angles, a grayscale walk pose, the shared-scale lineup and exact-revision prototype sprites. The reference's grounded dragon anatomy informs this new faction; no prototype Kobold port is claimed. Meshes including gear range from 722 to 910 triangles per body. The caster silhouettes use vials, a dorsal sail, a censer and a paired coil yoke. Fine eye/claw details need a close view; equipment and major silhouette features carry distance readability.

Dedicated scrap/reed/cinder/hold POIs are constrained to appropriate existing biomes at weight 0.35 and compete for the existing ruin budget. Each has at most four single-member Kobold inhabitants, gated by site level; the hold's reused ruin stamp also contains its chest. No broad biome or budget edits. Natural populated seed examples and intentional golden changes are documented in `experiments/kobolds/evidence/world-review.txt`. Starter chunks and climate hashes remain unchanged; neighboring choices and the zone(1,0) stamp change. Integrate other faction additions before recomputing combined goldens.

## Combat and AI handoff

All melee roles use a dedicated content state machine with a 1.15 m chase stop; ranged roles use `aggressive_ranged`. This prevents a longer hint or rally range from stranding short attacks. Initial tests caught the Tunnelguard stopping outside its fallback strike and the Bulwark pushing its target beyond the test window. The corrected tests verify real AI damage twice without refilling the Kobold's resources, repositioning the bodies between windows; independent live-shove tests verify the displacement itself. All fourteen roles pass both damage windows. Backstab does not grant stealth or coordinated flanks; Buckler Strike retains its existing flanking sweeps; the fallback Strike is necessary for a directly ahead target. Hurl Debris is a supported thrown-salvage substitution. Elemental attacks are hand-cast projectiles. Rallying Shout is an existing buff, not command AI.

Future tactics, sensing, fair flanking, ally-aware rally selection and reach-aware positioning must follow the fauna AI proposal and a separately reviewed runtime design. `meta.ai_handoff` is documentation only. Flight, breath, traps, tail strikes, burrowing, mining and innate resistances are deferred. Decorative tail/frill/crest bounds do not alter current collision or mass. The precise-hitbox chat must migrate the physical profiles explicitly.

## Verification

Evidence is under `experiments/kobolds/evidence/gates/`.

- `dotnet build HollowWake.sln`: zero errors, no new warnings. Three existing xUnit2031 warnings in the base's `PortedProjectileTests.cs` remain; they were present before editing.
- `dotnet test HollowWake.sln --no-build`: 736 core, 39 server/loopback and 116 client tests passed. Dedicated Kobold integration subset: 24 passed. Shared appearance fixture includes every concrete Kobold and checks equipment attachment.
- `npm --prefix web test`: 268 tests in 34 files passed; `npm --prefix web run build` passed with the existing large-bundle advisory. Protocol generation produced no diff; prototype porter suite passed.
- `hollowwake validate`: 0 errors, 0 warnings. `load --x 0 --y 0` ran 200 ticks; `fight kobold_ironback kobold_knifehand --seconds 30` exercised attacks, poise, displacement and death.
- Release `bench`: zone p95 12.615 ms (25 ms budget), behavior 2.078 ms / 23.0 KB per tick (3 ms / 1 MB), snapshot 0.902 ms (1 ms), full/delta snapshots 27.234/8.738 KB (30/10 KB), world chunk 0.913 ms / 14.531 KB (4 ms / 400 KB); zero system errors. This is the standard mixed benchmark, not a dedicated Kobold crowd measurement.
- Final private `test-background.mjs --suite all --no-build`: browser 29/29 and native 25/25 passed, zero cleanup errors, report `smoke-result.json`. Native headless checks skip rendering and theme pixels. The isometric browser smoke image was inspected; its automated third-person capture remains obscured by panels/camera transition, so the dedicated two-angle Kobold gallery is the anatomy evidence. Browser gallery pixels and native/browser geometry parity are inspected evidence; native pixel equivalence is not claimed. The native shutdown RID/ObjectDB warnings also occur in the base fauna smoke evidence.

## Claude review request

Please review faction neutrality, small-body and specialist physical estimates, habitat/level fit, silhouette separation, existing-skill substitutions and the AI deferrals. Combine additive family registrations with concurrent creature branches. Re-evaluate goldens on the integrated content, retain the performance budgets, and arrange isolated graphical native review when available. This PR must remain stacked on fauna #34 until the stack is safely retargeted. Do not merge as part of this pass.

Acknowledged by: pending Claude review.
