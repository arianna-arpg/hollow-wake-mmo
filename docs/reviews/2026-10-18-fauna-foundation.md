# Fauna and critter foundation: scope and AI boundary

2026-10-18. Codex owns codex/fauna-critter-foundation, stacked on Orc PR #33 at 8296c3c.

Owner asks for fauna/critters with the same entity, physicality and visual precedents, while treating current AI as replaceable ahead of a substantial Claude-coordinated redesign. Scope: prototype-derived wildlife and original variants, physical dimensions/materials, classifications, existing skills, habitats, restrained content-only behavior assignments, both client anatomy builders, tests and evidence. No new AI engine, perception service, tactical planner or species-specific runtime code.

Ownership overlaps declared before edits: own #31-33 entity/appearance contracts, body family registries and world generation goldens. Baseline wolf, boar and grove_stag definitions/looks are moved without duplicate IDs into the wildlife content. Additive appearance registration only. The precise-hitbox chat is actively working from #33 in its own worktree; this pass does not alter collision or physical runtime. Its migration should include the new wildlife profiles. #20 forecast and #22 equipment/day wire work remain untouched. Existing AI runtime and combat graphs remain unchanged.

Claude review requested for the content/visual pass and a separate substantive AI-design discussion. A final handoff will distinguish proven current behavior from desired tactics, identify simulation scenarios, observability needs, deterministic budgets and migration boundaries. Neither complex predation nor pack/hunger/morale systems will be claimed as implemented by this pass.

## Delivered scope

The Living Wilds has 22 bodies: 19 new wildlife patterns and renewed `wolf`, `boar`, `grove_stag` IDs. Nine ambient critters have empty weave lists, flee-only behavior and no drops. Thirteen combat/defensive animals use existing bite, claw, gore and heavy-strike kits, with aggressive, guard or skittish scaffolding. The fauna template supplies explicit stamina/mana pools; migrated bodies preserve their existing authored core stats, skills, behavior and drops while gaining that shared pool setup, dimensions/material mass and poise. The stag also receives the combatant `monster` tag. Legacy species tags remain intact.

Three paired anatomy families distinguish furred quadrupeds, ground birds and crabs, with long ears, antlers, swept horns, quills, shells, crests, fans, tails and claws. Reviewed images and the exact shared native/browser plan fixture live in `experiments/fauna/evidence` and `tests/fixtures/fauna-appearance.json`. Cards independently enlarge animals; the common-scale lineup keeps the player/skeleton reference at 1.8 m. Small birds use lower occupied-volume fill than mammals. Materials are authored estimates, not empirical biology. No art generation service or binary texture imports are required.

Biome populations are additive, level-windowed and seed-driven. All bodies have matching habitat references, including direct legacy biome spawns. The beast-cull template now requires `quest.cull`, explicitly authored on prior eligible beasts and new combatants; incidental critters and templates cannot become cull targets. This declares the additional overlap with quest content and classifiers in own #31-33. No quest engine changed. Four before/after seed `aureth` maps preserve terrain/sites; the origin cull target changes Grey Wolf to Wolverine. Chunk hashes and the overlaid-world hash intentionally change with spawn tables. Climate hash stays `999D90D6110B1CDD`. The existing global cull chooser lacks local-population and level constraints; that follow-up is called out in the AI handoff.

The generic DeliveryArena previously used the live boar as a neutral 80 kg target. It now authors a test-only unprofiled body with the old dimensions/stats so generic effect-distance assertions remain stable. Explicit production-pattern requests still use real bodies. New tests verify wolf/boar/bear/aurochs/hare mass ordering and live displacement, alongside existing modifier and poise-break tests. No collision, physicality or AI runtime changed.

## Verification and limits

- `dotnet build HollowWake.sln --no-restore`: succeeds, zero errors. The three pre-existing xUnit2031 warnings in PortedProjectileTests remain owned by PR #30.
- `dotnet test HollowWake.sln --no-build`: 712 core + 115 client + 39 server = 866 pass, zero skipped/failures. New coverage exercises all wildlife's profile/habitat, flee behavior or actual combat kit, cull exclusions, faction boundary, mass and displacement. Defensive-prey kit tests explicitly enter the existing fight state; they do not claim that current AI detects corners.
- `validate`: 0 errors, 0 warnings. `load --x 0 --y 0`: 200 ticks, 2,686 entities / 281 monsters without a reported system error. `fight brown_bear orc_grunt`: actual hit, poise and death events; no bespoke attacks. `protocol-ts`: no generated diff.
- `npm --prefix web test`: 254 pass. `npm --prefix web run build`: passes; bundle 1,684.62 kB raw / 380.68 kB gzip versus base 1,607.99 / 374.53. Existing 1,500 kB advisory remains; this pass adds 76.63 kB raw / 6.15 kB gzip.
- `node --test scripts/port/port-prototype.test.mjs`: 10 pass.
- `node scripts/test-background.mjs --suite all --no-build`: browser 29/29, native 25/25, private server cleaned up. Native headless rendering checks explicitly skip pixels. Godot retains its existing RID/ObjectDB exit diagnostics; no claim of zero engine-exit warnings.
- `node experiments/fauna/render.mjs --write-fixture`: 22 production plans rendered, no page errors; inspected isometric, third-person and grayscale walking poses plus shared scale. Individual JPEGs stay below 235 KB. Native/browser fixture parity passes for the complete plans.
- Release performance: final full benchmark and same-runtime old-content comparison are retained alongside the earlier slow runs. Final timings are listed in `evidence/gates/bench-final.txt`; no budget, density, runtime or benchmark settings were changed to secure a passing result. Snapshot build was 1.096 ms in the initial full run and 1.600 ms in a tick-only run. The sequential full A/B measured base 0.792 ms and fauna 0.863 ms, under the 1 ms budget. Fauna full-state traffic is 27.234 KB and delta traffic 8.738 KB, within 30/10 KB budgets but higher than base 23.448/7.376 KB. Treat timing variability and the limited remaining traffic headroom as a review concern, not a guarantee under every host load.

## Claude handoff

Please review the roster, physical proportions and material estimates, cull classifier migration, inherited pool changes and new population costs. Review the full fixture and screenshots for silhouette consistency. The separate [AI handoff](2026-10-18-fauna-ai-handoff.md) proposes a mechanics-aware design comparison and deterministic scenario matrix covering perception, fairness, resources, poise/mass, group decisions, minion orders and shared forecasts. It is a request for design work, not an approved architecture or completed Claude collaboration. Current AI remains intentionally replaceable content scaffolding. Coordinate precise damage volumes with the separate hitbox chat, including these new profiles.

Stack on #33; retain the base branches until the stack is safely retargeted/merged. No merges or production deployment performed.

Acknowledged by: pending Claude review.
