# Bounded local quest population search

Owner-authorized follow-up to Claude's #31 review: https://github.com/arianna-arpg/hollow-wake-mmo/pull/31#issuecomment-6253471494 . This correction is owned by #31 and will propagate through #32/#33/#34/#37. It changes ZonePlanner, generator parameters, the world rule, the quest/performance contracts and focused population regressions. Other species consume the validated minimal correction ref. No release-policy, hit-body or AI replacement changes are included.

The existing search caches facts across slots, but an empty search can still generate about a hundred terrain/landmark/flora/spawn chunks synchronously. `world.gen.quest_population_chunks` bounds inspected nearest-first chunks per slot, including cache hits so earlier templates cannot enlarge a later slot's search. Actual spawn existence, effective site/pattern level, radius, stable coordinate ordering and seeded candidate selection remain required. A search exhausted before finding a valid target produces no offer. This deliberately bounds completeness, never weakens eligibility.

The initial setting is 16 chunks and the proposed default-zone planning budget is 40 ms. Neither is described as passing until measured in a coordinated quiet window. Existing budgets are unchanged. Regressions cover a distant eligible resident under small/large caps, wrong-level and out-of-radius residents, and cache reuse that must not bypass the cap. Population/quest audits and timing results will be recorded after checks are released.

## Functional checkpoint

The source correction is checked together with its audited golden refresh: final solution and Release tool builds have zero warnings/errors; full tests pass with core656/server44/client115. All six new cases pass. Content validation reports zero errors/warnings; zone load and Skeleton Warrior/Zombie duel pass. The first focused run's two expected golden failures and initial test-analyzer warning are retained beside the corrected passing run.

Every site and every pattern's spawn/body count is unchanged in all four reference zones, as are terrain/chunk and climate goldens. At the origin the16-chunk limit omits Clear Knot because its eligible cinder imp lies farther away. Gather Floss and Cull Hob remain. Scout chooses the eligible Ossuary instead of Ruin because omitting the earlier offer consumes one fewer seeded selection. Other reference-zone offers are unchanged. The overlaid-world hash also refreshes because it contains generated quests. Evidence: `experiments/entity-review/evidence/31-bound-*`.

The preserved pre-correction Release runtime is from6a65ed1, whose complete `src/HollowWake.Core/WorldGen` directory is identical to71ad34f. Baseline content is an exact71ad34f git archive. The pending before/after benchmark will compare that unbounded generation code against this correction; no speed or budget pass is claimed before measurement. Other runtime differences are irrelevant to the worldgen-only comparison and will not be used to claim a combat/snapshot speedup.

## Site-level policy follow-up ownership

Claude's preceding review also blocks unconditional site-level births, which changed legacy residents while landmark-cell spawns retained pattern levels. #31 owns the correction to SpawnSpec/LegendCell, both ChunkPasses birth paths, the population tests and the audit format. `use_site_level` is an explicit opt-in on both spawn specifications and landmark legend cells. Omission preserves the pattern's level, including all shipped legacy and newly added populations in this correction. A site's eligibility window still gates placement; it does not implicitly scale residents. Authors may opt either birth path into the site difficulty, with the same initial/respawn transport.

The existing historical golden-format change is already published and will not be rewritten. This correction keeps that level-aware format and records the level-policy behavior change separately, with per-pattern/per-site/effective-level counts in the audit. Old stored chunks with null Level keep pattern levels; chunks saved by the unmerged unconditional-level branch retain their explicit stamps and are not silently migrated. No production save migration is claimed.

## Complete functional checkpoint

The level-policy correction passes build and Release build with zero warnings/errors, core660/server44/client115, validation zero errors/warnings, load and the recorded duel. Tests cover both spawn paths in default and opt-in modes through initial birth and respawn, plus live legacy guards/tangles/shades. The original constructor compile error, baseline-probe framework mismatch and expected golden failures are retained with their corrected results.

The audit now records explicit and effective level, pattern, owning site, spawn points and body counts. It compares exact71ad34f content on the preserved unbounded generator with the final corrected tree. All sites and population counts remain unchanged in all four zones. Origin guards and quartermasters are uniformly level5 (instead of mixed1/5); tangle returns3→6, cinder imp3→4 and shade3→2. Northern knot inhabitants return from site7 to their pattern levels (imp4/shade2/tangle6). The complete per-site list, including unchanged effective levels whose explicit stamps disappear, is in `31-level-level-diff.txt`. The earlier bounded-search offer changes remain the only offer changes; the new golden refresh is specifically the level-policy behavior correction, retaining the already published level-aware hash format. Climate remains unchanged.

No shipped spawn opts into scaling in this correction. New content may choose it explicitly in either path, and both are exercised by tests. The planning benchmark remains queued pending a coordinator-granted quiet window. No downstream owner should consume an unvalidated timing claim.

## First controlled planning pair: target missed

The granted quiet window ran exactly one serial worldgen-only pair at Normal priority, baseline then candidate. Baseline: preserved6a65ed1 Release runtime whose entire WorldGen source is identical to71ad34f, with exact71ad34f archived content. Candidate: clean f12ea18 Release/current content. Arguments, hashes, source identities and raw results are retained under `31-bound-bench-*`; both processes exited zero. No reruns or discarded samples.

| Measure | Baseline | Candidate |
| --- | ---: | ---: |
| worldgen.zone.plan (ms) | 63.5384 | 56.7857 |
| worldgen.chunk (ms) | 0.590186 | 0.495100 |
| worldgen.chunk.alloc (KB) | 14.126465 | 14.126465 |
| generated offers | 4 | 3 |

The40ms planning target **fails**. Bounding the scan reduces worst-case work but is insufficient on the measured default seed. The correction remains draft and will not propagate as performance-cleared. Further work must remove unnecessary population-query generation through shared passes and prove actual spawn equivalence; no budget is relaxed. This is a worldgen-only comparison, not a new tick/snapshot claim for the changed resident levels.

## Shared spawn-fact query ownership

Following the retained missed target, #31 adds `WorldGenerator.GenerateSpawnFacts` through the existing terrain, landmark and spawn passes. It omits flora placement, whose separate random stream and results are never read by spawning. Normal chunk generation still runs every pass unchanged. The planner continues to cache exact spawn facts and apply the same bounded eligibility query. Reference-chunk equivalence checks cover active overlays and their removal; existing world goldens must remain unchanged. This is an optimization, not another quest/population policy change.

The query also rejects impossible candidate chunks before materializing terrain. Its conservative union includes every biome/table reference admitted by regional level, site inhabitants at the chunk center, and overlapping landmark spawn references. It consumes no RNG and cannot offer anything itself. Positive candidates still use exact shared spawn passes; rejected chunks are not cached across species sets. Existing per-slot inspected-chunk limits still count these rejections. Full-chunk equivalence cases also assert no generated pattern is falsely rejected across overlays and their removal.

## Spawn-query functional checkpoint

The shared spawn-fact query and conservative precheck pass solution/Release builds with zero warnings/errors; full tests core664/server44/client115; validation zero errors/warnings; load and duel. All existing generation goldens are unchanged by this optimization. The four new parameterized cases compare512 exact chunk-population arrays across four zones with overlays active and then removed; every actually generated pattern must pass the conservative precheck, while an absent fixture is rejected. Full chunks still produce flora normally. Intermediate flora-only results and final precheck results are separately retained under `31-facts-*` and `31-query-*`.

A new controlled planning comparison is required because the source changed. The prior56.7857ms failure remains retained; it is not replaced or hidden by a new passing sample. No performance verdict is claimed yet for this implementation.

## Optimized controlled pair: planning target met

The next granted quiet window measured the changed implementation at exact `ec144c3df03252fa88d97e3931f1f2366d28b586`. It used the same original preserved runtime/content baseline, serial baseline then candidate, Normal priority and unchanged worldgen-only arguments. Both first samples and metadata are retained under `31-query-bench-*`; both processes exited zero. There were no reruns. The earlier bounded-only failure remains separately published.

| Measure | Original baseline | Optimized candidate |
| --- | ---: | ---: |
| worldgen.zone.plan (ms) | 57.6400 | 36.6863 |
| worldgen.chunk (ms) | 0.527831 | 0.579517 |
| worldgen.chunk.alloc (KB) | 14.126465 | 14.111084 |
| generated offers | 4 | 3 |

The candidate meets the40ms planning target,4ms chunk target and400KB chunk allocation target in this controlled pair. The cap/offer tradeoff and restored legacy levels remain explicit above. Spawn-query optimization itself changes no generation goldens. These results are not substituted for a new tick/snapshot measurement after resident levels changed; older species measurements remain historical checkpoints. Claude rereview is still required before main integration.

Validated minimal source commits are `b1dd4d9` (bound), `097c526` (explicit level policy) and `192e073` (shared spawn facts and conservative precheck). Branch-specific goldens/evidence are separate. Full #31 will propagate bottom-up through #32/#33/#34/#37; independent species owners can consume the minimal sequence and audit their own output.

Acknowledged by: pending Claude rereview.
