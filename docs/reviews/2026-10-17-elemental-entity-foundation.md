# Elemental entity foundation

Historical evidence for `b29e67c`. Claude subsequently identified projectile contact, displacement, faction and site defects; see [the correction record](2026-10-18-elemental-review-fixes.md). The direct damage test below establishes mitigation only, and the old green gates did not establish projectile coverage or nonempty natural sites.

2026-10-17. Codex claim and integration handoff. Branch `codex/elemental-entity-foundation`, based on fauna PR #34 at `90cb7fcb5eec97a6ad438c10f7b564f1dfa88790`.

## Scope and overlap declared before edits

Own free-form elemental and Thread-inspired bodies, dedicated patterns, materials, allegiance, looks, POIs, geometry builders, tests, fixtures and `experiments/elementals/` evidence. Assembled golems, including `stone_warden`, belong to the concurrent golem pass. Gnolls/Beastkin and Kobolds belong to their separate chats. Collision and AI runtime are outside this pass.

Shared additive integration points: `BodyFamilies.cs`, `bodyFamilies.ts`, `AppearanceKinds.cs`. Append an elemental subsection to the appearance and entity contracts. POI content can change `WorldGenGoldenTests.cs`; preserve before/after seed evidence and refresh only reviewed changes. These files overlap the preceding entity stack and concurrent entity passes. Integrators should union registry entries and contract subsections, then regenerate final combined world hashes after review. Dedicated files must not replace another pass's definitions.

Reference: read-only prototype revision `118b510f8b5bfb781163c5bc13ea4908f698e75b`, `src/data/monsters.ts`, `src/data/looks.ts` and sprite painters. Hollow Wake's Nine Threads are authoritative for naming and damage vocabulary. Translation is an adaptation, not exact combat parity. AI bindings are replaceable scaffolding under `2026-10-18-fauna-ai-handoff.md`.

## Delivered behavior

Sixteen concrete bodies span the Nine Threads: eight direct prototype adaptations and eight new lore adaptations. Seven paired geometry families use content features and existing wisp/crawler poses. No creature ID is dispatched in rendering or simulation code. The [provenance ledger](../../experiments/elementals/provenance.json) records source IDs, dimensions, materials, kits and substitutions; the [gallery](../../experiments/elementals/README.md) includes source-renderer evidence, both camera angles, grayscale poses and a common-scale player/skeleton/Goblin lineup. Elemental plans contain 64 to 1,032 triangles. Nominal stature and conservative renderer bounds are recorded separately.

All assigned skills use existing compiled weaves. Native faction hostility, sustained AI damage, ordinary mitigation, wild rewards, summoned allegiance and corpseless death are covered by `ElementalTests`. Its 23 body/skill pairs hit during both the first 15 seconds and following 30 seconds, with exact repeated-seed damage digests. Whole kits also hit using their authored faction. Every body takes positive damage from all nine Threads. Affinity resistance is explicit content, never inferred from a palette or density.

Physical profiles supply finite mass and real displacement. Representative measurements under the same four-unit knockback effect: Emberwisp 5.049 kg / 63.34717 m; Gale 22.320 kg / 14.30138 m; Tide Surge 561.094 kg / 0.43947 m; Geode Shellback 2,927.925 kg / 0.03908 m. The very long ethereal travel is an explicit balance/recovery review question. No phasing, flight, swimming, immovability or precise visual hit volume is claimed.

Eight sparse manifestation sites use existing landmarks and the ruin budget. Pinned-site tests prove every inhabitant can spawn and respects level gates; the natural-generation audit finds all eight site kinds. The [world review](../../experiments/elementals/evidence/world-review.txt) explains the sole info-hash refresh at (3,2), including neighboring weighted picks. All four reviewed local maps, chunk hashes and climate/overlay goldens remain unchanged. Placement samples precede cell-center snapping in the existing planner; the habitat audit explicitly checks the boundary cell footprint.

## Verification

Logs are under [experiments/elementals/evidence/gates](../../experiments/elementals/evidence/gates/). Final source build and complete suites were rerun after the Tide curl and all-site reachability check were finalized.

| Gate | Result / evidence |
|---|---|
| `dotnet build HollowWake.sln` | Passed, including Godot client; final incremental build 0 warnings/errors (`build.txt`) |
| `dotnet test HollowWake.sln --no-build` | 949 passed: Core 794, Client 116, Server 39 including loopback (`tests.txt`) |
| Elemental integration | 82 cases, detailed skill/shove/neighbor output in `elemental-tests.txt`; strengthened all-site audit in `natural-sites.txt`, also included in final full suite |
| Paired geometry | Full bones/parts/sockets/gear fixture for all 16 bodies, checked by native suite and 16 browser cases |
| `hollowwake validate` | 0 errors, 0 warnings (`validate.txt`) |
| `hollowwake load --x 0 --y 0` | 100 chunks, 115 spawners, 200 ticks, 2,686 entities (`load.txt`) |
| `hollowwake fight ember_elemental frost_elemental` | Actual casts, projectile hits, poise and death; Frost survives at 72/223 (`fight.txt`) |
| `npm --prefix web test` | 270 passed (`web-tests.txt`) |
| `npm --prefix web run build` | Passed (`web-build.txt`); existing large-bundle warning retained |
| `hollowwake protocol-ts` | Regenerated; no protocol diff (`protocol.txt`) |
| `node --test scripts/port/port-prototype.test.mjs` | 10 passed (`porter.txt`) |
| Private smoke, `node scripts/test-background.mjs --suite all --no-build` | Browser 29/29, native 25/25; two native pixel checks explicitly skipped (`smoke-result.json`, `smoke-web.txt`, `smoke-godot.txt`) |
| Reference/gallery scripts | Source renderer and production body geometry captured; no gallery page errors (`reference.txt`, `render.txt`, adjacent JPEGs/JSON) |
| Release `hollowwake bench` | All published budgets met on repeat; both runs preserved (`bench.txt`, `bench-repeat.txt`) |

Release repeat: tick p95 10.498 ms, tick allocation 74.490 KB; behavior 1.824 ms / 23.0 KB; snapshot build 0.829 ms, full 27.234 KB, delta 8.738 KB; chunk 0.810 ms / 14.531 KB; damage fold 345.877 ns / 0.134 bytes; expression 106.509 ns. The process-wide expression allocation measurement remains 0.017 bytes/call, identical to fauna's recorded final baseline; this pass changes no expression code. No system errors. First full snapshot timing was 1.147 ms; the isolated repeat restored the 1 ms budget without changing code. Fauna's recorded runs also show this timing variance. This is a development-machine sample, not a dedicated stress test of elemental populations.

## Limitations and review asks

The first browser attempt ended with its page/context closing at 12/13 checks and no page errors; a fresh run passed, followed by a final run against the completed tree. The failed result and browser log are retained. Native headless logs include the same RID/font/CanvasItem/ObjectDB shutdown leak warnings present in fauna evidence, after smoke completion. Native pixels, controller hardware and desktop-focus behavior have not been verified. Browser smoke covers ordinary gameplay; the separate gallery covers every new body.

Clean recompilation of the inherited stack reports three existing xUnit2031 warnings in `PortedProjectileTests.cs`; this pass does not change that file. Final incremental build is warning-free. The browser bundle remains above its existing 1,500 KB warning threshold. No new content warnings are accepted.

The [AI/combat handoff](2026-10-17-elemental-ai-combat-handoff.md) distinguishes current state-machine scaffolding from deferred elemental temperament, sensory memory, material terrain interactions, attunement, shell phases and tactical action selection. No collision, AI or combat runtime is changed. The source tree and other entity worktrees were read-only throughout.

Claude review requested:

- Check Thread substitutions, faction temperament and kit/resource balance against the lore and future AI contract.
- Review the low-mass shove distances and recovery implications; no hidden ethereal exemption has been added.
- Review silhouettes, shared scale, sparse habitat choices and the single neighboring-plan golden change.
- Union additive registry entries with Gnolls/Beastkin, Golems and Kobolds, and review combined population goldens before integration. Preserve the fauna base until this stacked PR is retargeted or merged bottom-up.

No merge authorized. This record requests review; it does not claim that review occurred.

Acknowledged by: pending Claude review.
