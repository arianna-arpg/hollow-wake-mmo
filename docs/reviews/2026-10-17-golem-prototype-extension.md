# Prototype golem extension

2026-10-17. Follow-up authorized by the owner on PR #39, continuing in the existing `codex/golem-construct-foundation` worktree. Base for this extension: `9d3ef741efe0154d3b0ed26d43ae16ebda8b7a6e`.

## Scope and ownership before edits

Add the prototype's assembled Fire, Ice, Blood and Stone summon identities and the stitched Amalgamation creature. Add a Lightning Golem adaptation: the inspected source revision has no separate lightning golem definition. Quarry Golem and Stone Warden already translate the stone body; preserve both existing IDs. Bone Golem remains owned by the undead pass. The elemental chat owns free-form elemental creatures; the new bodies here are jointed assemblies with separate IDs and dedicated definition files.

Extend the golem geometry builders, fixtures and gallery owned by PR #39. Append entity/appearance contracts and update world goldens only after a recorded before/after review. No collision, AI, protocol or physicality runtime changes. Declare overlap with the open entity passes' shared documentation and world goldens before those edits. Use existing skills, modifiers, minion contracts and population mechanisms.

Source: `arianna-arpg/arpg-game-world` revision `118b510f8b5bfb781163c5bc13ea4908f698e75b`; inspected `src/data/golemLooks.ts`, `golemGlyphs.ts`, `monsters.ts`, `summonLooks.ts`, `summonGlyphs.ts`, `src/packages/defs/amalgamation.ts` and `docs/render/golems.md`. Both prototype golem and summon comparison images were inspected.

## Delivered extension

Five fixed named bindings supply Stone, Fire, Ice, Lightning and Blood golems. They share the Warden slot with the older Stone Warden, Bone Golem and Salvaged Golem; each reserves 30 mana, respawns after eight seconds and remains reward-free. The Stone identity is additive; Quarry Golem and Stone Warden are retained. The Blood Golem uses actual life leech, while elemental and Void resistances are explicit modifiers verified through damage. Source skills are reused or translated into existing forms; the detailed mapping and omitted source mechanics are in `experiments/golems/prototype-extension.md`.

The Amalgamation uses a grafted silhouette, existing Heavy Strike/Poison Nova, a separate faction and ordinary encounter rewards. One body occupies an eligible Suture Works site. This pass covers its creature and fixed encounter. The Bonewright's hunt chain, player-selected graft parts, Crown promotion and graft-selected rewards remain deferred. No dynamic anatomy, new AI tactics, environmental immunity, flight or phasing is implied.

Paired geometry adds kindled limbs, ice fans, conductor cages, clot masses, sinews, ribs, stitches, a spare skull and belly maw. The original fixture still passes; the six new plans match across clients. New geometry costs 332-682 triangles per body. Inspected isometric, low-angle and grayscale walk captures use actual production geometry. The 1.8 m player/skeleton and 0.9 m Goblin lineup stays at a common scale; decorative extremities may exceed nominal stature. Gallery images remain below 161 KB each.

## Verification and evidence

Evidence is under `experiments/golems/evidence/prototype/`.

- `dotnet build HollowWake.sln`: zero errors, the same three inherited xUnit2031 warnings.
- `dotnet test HollowWake.sln`: **783 core + 117 client + 39 server = 939 passed**. After strengthening the natural-site check, all **31 focused extension tests** also passed with detailed combat evidence. Each attack is isolated and must repeatedly hit through the real spawn path, with identical seeded replay.
- `npm --prefix web test -- --run`: **269 passed**. Production build passes; existing large-bundle advisory remains, now 1,764.30 KB raw / 389.86 KB gzip. Regenerated protocol has no diff. Prototype porter: **10 passed**.
- Content validation: **0 errors, 0 warnings**. Start zone load: 200 ticks, 2,686 entities / 281 monsters. A naturally eligible Suture Works at zone (-32,-42), coordinates (-8056.5,-10574.5), level 47 has one actual Amalgamation spawn point; its zone loads 2,402 entities / 451 monsters and completes 200 ticks. Pinned population tests separately verify exclusion below level 14 and repeatable inclusion at level 14.
- Amalgamation/Stone Golem headless fight completes with damage and death. Blood Golem restores **7.523 life** through leech in its focused check. An 8 m requested shove moves Fire **2.676311 m**, Ice **0.779549 m** and Stone **0.155624 m** through the existing integrator. The source-like resistances reduce a real 100-damage hit to the expected amount while leaving positive damage.
- Private background smoke `run-ooNOKE`: **browser 29/29, native 25/25**, cleanup errors empty. Native headless explicitly skips rendered pixels and retains the inherited RID/ObjectDB exit diagnostics. The normal browser smoke is start-zone coverage; the dedicated gallery supplies this roster's visual evidence.
- World neighborhood comparison against the extension base changes only the sampled zone (3,2) info hash: two nearby sawmills become Gnasher Pens and two shrine candidates become eligible. All four local chunk hashes, the other zone-info hashes, climate/region hash, overlay hash and all sampled quests remain unchanged. `world-review.txt` names the changes; the initial expected golden failure is retained in `gates/tests-initial.txt`.
- Full Release benchmark comparison meets all budgets: current tick p95 **10.108 ms**, behavior **1.793 ms / 23 KB**, snapshot build **0.852 ms**, zero system errors. Baseline snapshot build was **0.824 ms** and tick p95 **9.918 ms**. Keep the initial **1.264 ms** snapshot overrun in `gates/bench.txt`; the identical-settings baseline/current comparison is in `bench-base.txt` and `bench-comparison.txt`. Shared-host timing varies. No simulation runtime, benchmark setting, population count or budget was adjusted to obtain a passing sample; benchmark traffic byte counts match the base.

## Review request and integration limits

Claude review requested for the explicit defenses and leech, summon replacement, source translations, Amalgamation rewards and level gate, silhouette readability and effective mass estimates. The separate hitbox pass owns precise damage volumes. Merge shared validator/contract/golden changes additively and recompute generation goldens for the combined catalogs. Full progression unlock placement, bespoke animation, native rendered pixels and the Bonewright event are not claimed. No merge requested.

Acknowledged by: pending Claude review.
