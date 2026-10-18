# Kobold level-policy and bounded-search follow-up

Consumes the owner-published minimal shared corrections b1dd4d9, 097c526 and 192e073 after Claude's foundation rereview. The previous Kobold head is b793846222faa918c9c9ae4e36d4ef5a64d1899c; its measurements and screenshots remain historical evidence.

## Ownership before integration

This pass imports only the shared bounded-search, explicit level-policy and spawn-fact-query changes, preserving their attribution. It owns Kobold-specific assertions, the actual before/after quest and effective-level audit, and this branch's generation golden refresh. It also corrects the existing shared entity-contract sentence about unconditional site-level births to agree with the new opt-in policy. No second planner, spawn mechanism or changed performance threshold is introduced.

Kobold spawn content keeps its original pattern-level default. No shipped inhabitant or landmark spawn is silently opted into scaling merely to retain the earlier tests. The site-level windows still decide which roles can appear. Tests will prove actual initial/respawn levels and keep the home-region Hold populated; an explicit test-only opt-in will exercise the shared scaling choice without changing authored encounter difficulty.

Capture exact pre-change and post-change effective levels, counts, positions, site choices, quest offers and geometry before accepting any golden updates. Earlier unconditional-level and performance claims apply only to their recorded source versions. New performance measurements require a separately assigned quiet window after source is ready.

## Audited world and level changes

Baseline b793846 was captured before the first source import. The exact before/after JSON and field-by-field comparison live under experiments/kobolds/evidence/level-policy/. The reproducible experiments/kobolds/world-audit project matches every recorded field. Eight zones are checked with and without the golden-test overlays, for sixteen paired cases. All geometry hashes, population positions and counts, placed sites and nearby-site choices remain unchanged.

Only effective birth levels, explicit level stamps and generated quest offers change. Shipped Kobold inhabitants keep use_site_level=false. Site-level eligibility remains unchanged. Live natural-site checks prove the restored pattern levels:

| Site fixture | Site level | Live resident levels |
|---|---:|---|
| Hold (-15,-40) | 35 | 10, 10, 11, 15 |
| Scrap (-8,-40) | 27 | 2, 3, 4, 5 |
| Cinder (-25,-31) | 26 | 6, 7, 8 |
| Reed (-7,-23) | 16 | 4, 6, 7 |

The home-region fixture remains a level-seven Hold with exactly one Tunnelguard and no premature boss. Real death/respawn tests prove level six by default and level seven under explicit test-only opt-in. Shared tests cover both inhabitant and landmark-cell paths. Legacy guard/quartermaster occupants return to pattern level five; other affected residents are enumerated by site in changes.json. Previously saved explicit level stamps are not migrated by this patch.

At the origin, the bounded, level-qualified population search now yields three offers instead of four: Clear Knot (The Hob's Hollow, targeting cinder_imp) is omitted, Loose Threads and the Hob cull remain, and the scout changes from Fallen Hall (poi.1_1.ruin.305.334) to Sunken Lantern (poi.0_1.ruin.213.450). The overlay case has the same omitted offer and scout change while preserving its Grove Stag cull. Existing quest indices are retained. The other reference-zone offer lists remain unchanged. This is the actual Kobold branch output; the upstream Ossuary description is not copied here. The combined bounded-search/level-policy change is the audited cause; this record does not attribute the omitted offer to the chunk cap alone.

Goldens were refreshed only after this audit. The initial focused run had 140 passes and five expected old-golden failures, retained in goldens-before-update.txt. The level-aware hash inputs are unchanged. The origin info hash is now 32CA1C2FA79C8A4A; chunk hashes for origin/east/north/(3,2) are 38740F7C0C66C2F6, 0E386FE1D6659BC0, 2AFA9EE5AA75F409 and B927209C7C70E524. The overlay hash is 74AA73CB8E77183E. Other info hashes and the climate hash are unchanged.

## Verification

- Solution build passes with zero warnings/errors. Full solution tests pass: core 2264, server 48, native logic 124. This includes all 84 actual Kobold projectile cases, the bounded-query and spawn-fact equivalence regressions, both level policies and the audited world goldens.
- A subsequent focused Kobold run passes 29 cases with detailed natural-site and birth/respawn measurements after tightening a nullable test assertion. No runtime source changed after the full suite.
- Content validation reports zero errors/warnings. Origin load completes 100 chunks and 200 ticks, with 2686 entities, 281 monsters and three quests. The Tunnelguard/Loosed duel records damage both ways and ends with the Brigand defeated; native-faction tests remain the relation proof. Generated protocol is current.
- Private smoke passes browser 31/31 and native 26/26, with no harness cleanup errors. The card-mode screenshot was inspected: terrain and bodies render, and restored guard/quartermaster level-five labels are visible. Native headless pixels remain uncovered; the raw native log retains engine shutdown resource-leak warnings. Browser unit tests/build remain the previously verified unchanged web source (285 tests, 36 files); this follow-up modifies no client or protocol source.

The earlier performance measurements on 253baa2 are historical because resident levels changed. The fresh controlled results below apply to the current source. The shared 40 ms planning ceiling is newly proposed in the imported foundation contract. Claude's earlier main planning measurements of roughly 21–23 ms remain relevant comparison context; meeting 40 ms does not establish parity with those measurements.

Claude rereview requested with the final functional and performance evidence. Keep PR #38 unmerged pending the shared dependency reviews and separately coordinated release-policy gate.

## Foundation ancestry reconciliation

Before merging validated #34 tip 745015e5a0849a201e6857e47c9e23435618fb0a, this pass declares ownership of any shared review-note and world-golden conflict. The original three minimal changes are already present. Retain this branch's independently audited goldens and default opt-out content. Preserve upstream review records as history, including #34's first 228 B/tick allocation failure and subsequent isolated/full passes with cause unestablished. Compare every application and test input against d84dfde before reusing its tested Release binary; stop for any material source difference.

Every tracked path outside docs/ and experiments/ is byte-identical to d84dfde after conflict resolution, including all application, content, client, script and test inputs. The Kobold goldens are unchanged. The tested Release binary is retained without rebuilding; ancestry-equivalence.json records its SHA-256 and source identity. All incoming historical evidence, including the unexplained allocation failure and passing retries, remains intact.

## Controlled performance on the reconciled tree

The coordinator granted START only after #20 stopped and the other owners were quiet. Exactly one full default Release benchmark and one authored hit-body profile ran serially at Normal priority, from 19:43:33 to 19:43:41 UTC on 2026-10-18. Both exited zero and were stopped before notifying the coordinator to release the window. There was no worldgen-only run, repeated sample or discarded result.

Measured ancestry is 6c20a93043a102f9688dbf9e8c56ec1a9b91a376. The retained Release build is from tested d84dfde34e0c5e48a04937290dfc8d67f4ecd452; the ancestry merge changed no application/test inputs. Raw rows, stderr (empty), PIDs, arguments, source identities, priorities and UTC timestamps are retained in bench-default.json, bench-hit-bodies.json and bench-processes.json.

| Established measure | Measured | Ceiling |
|---|---:|---:|
| Tick p95 | 8.7623 ms | 25 ms |
| Tick allocation | 75.889375 KB | 2048 KB |
| Behavior time / allocation | 1.484122 ms / 22.9 KB | 3 ms / 1024 KB |
| Snapshot build | 0.774583 ms | 1 ms |
| Full / delta snapshot | 28.851885 / 8.881948 KB | 30 / 10 KB |
| Chunk time / allocation | 0.571375 ms / 14.330811 KB | 4 ms / 400 KB |
| Damage fold time / allocation | 347.664 ns / 0.1392 B | 500 ns / 200 B |
| Expression time | 102.7919 ns | 150 ns |
| Expression process-wide allocation | 0.0168 B/eval | literal zero |

All established timing and nonzero allocation/payload ceilings pass. Full-snapshot headroom is 1.148115 KB. The expression allocation counter remains 0.0168 B/eval, exactly the historical counter value; it does not prove literal zero allocation, and that caveat is not converted into a blanket pass. Tick average is 4.075690 ms, with zero zone system errors.

Separately, planning is 35.8580 ms for four POIs and three quests, meeting the proposed 40 ms target. This single current full-run sample is not a controlled comparison with the earlier 21–23 ms main samples, nor a claimed speedup over the historical workload with different resident levels and offers.

The authored hit-body profile reports 1.587422 ms/tick including casts, 37.592715 KB/tick, projectiles 0.816991 ms/tick and fields 0.1003685 ms/tick: 42 profiled patterns, 120 foes and 41 average flights. The default run is the standard mixed roster. Neither is a dedicated Kobold crowd measurement. Kobolds use the cylinder fallback, covered separately by all 84 actual cast regressions.

PR #38 remains unmerged for Claude rereview and remaining dependency/release-policy coordination. This evidence publication changes no application or test inputs.

Acknowledged by: pending Claude rereview.

## Subsequent main foundation integration

The next source integration and its current functional evidence are recorded in docs/reviews/2026-10-18-kobold-main-integration.md. It consumes complete validated #34 at 3d339d2, including the new main runtime and audits. All timings in this earlier record remain historical for their named pre-main source. No post-main performance clearance is implied.
