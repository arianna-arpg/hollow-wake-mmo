# Golem final foundation correction

2026-10-18. Follow-up to the published #39 checkpoint `20db6e36c6b00a7d1eaaa2aa5f7060140c2e7b69`. The owner-authorized coordinator supplied the minimal corrections `b1dd4d9` (bounded local quest search), `097c526` (explicit site-level births in both paths), and `192e073` (exact spawn-fact query and conservative precheck). The shared owner's evidence head is `3007003db865eceafac955ecf2a859431f010ae2`, tested source `ec144c3`.

Ownership before edits: consume these shared corrections with attribution, preserving the existing #34/#36 and card-renderer integrations. This branch owns the golem inhabitants' explicit scaling choice, corresponding actual birth/respawn tests and audited goldens. Update the overlapping population contracts only to explain opt-in behavior consistently. No duplicate search/runtime implementation, encounter expansion, #20 policy or unrelated species edits.

The four golem works and provisional Suture Works retain this branch's already-reviewed site-relative resident levels by explicitly opting their inhabitants into `use_site_level`. Shared landmark stamps and legacy residents retain the restored pattern-level default. Eligibility and level scaling remain separate. Before changing golden constants, retain four-zone counts, actual spawn levels and quest offers from the old and new runtimes using the shared owner's level-aware audit program. Old timing and smoke evidence remains historical because resident levels and available offers change.

## Audited world changes

`experiments/golems/evidence/foundation-final/` retains `before.json`, `after.json`, `world-diff.json` and a readable `world-diff.txt`. The audit uses the shared owner's level-aware program from `097c526`, first against the old #39 runtime/content, then against these corrections plus the explicit golem opt-ins. It compares all four golden zones, counting sites, spawn points, bodies, explicit/effective levels and quest targets. All sampled sites and population counts are unchanged.

At Ninewells, Clear Knot is omitted by the bounded local search; Gather Floss and Cull Hob remain. The subsequent seeded scout selection moves from ruin `poi.1_1.ruin.305.334` to ruin `poi.0_1.ruin.213.450`. This differs from #31's own scout target, so its golden constants were not copied. The other three sampled zones still have no offers. Origin guards/quartermasters return from mixed level 1/5 to their authored level 5; knot imps/shades/tangles return to 4/2/6. The eastern Orc/Goblin residents and the other zone's Wight likewise regain their pattern levels. Full per-site counts, including removed explicit stamps whose effective levels stay unchanged, remain in the audit.

The first focused run passes 458 cases and fails only the five expected golden cases. All five golem-site regressions verify actual birth and replacement at region levels 1 and 21; all 342 projectile cases, bounded-search, legacy-level and exact spawn-fact/overlay cases pass. The level-aware chunk hashes change in all four zones; only the origin zone-info hash changes. Climate remains `999D90D6110B1CDD`. After reviewing the audit, update origin info/chunks to `467923F597DB83C5`/`38740F7C0C66C2F6`, zone(1,0) chunks to `0E386FE1D6659BC0`, zone(0,-1) chunks to `2AFA9EE5AA75F409`, zone(3,2) chunks to `B927209C7C70E524`, and the overlay hash to `F2165541CF5F5CDF`. Other zone-info hashes stay unchanged.

## Combined functional checkpoint

Source `a21a58dbf11b974df7702405e6c933a53f3842be` contains the three corrections as `787e955`, `cf4310d` and `a73a404`, plus the explicit golem content, regression and audited-golden changes. The only cherry-pick conflict was the shared historical review record; its retained failed planning sample and subsequent optimization notes were preserved. No shared runtime behavior was changed locally.

Debug solution and Release tools builds have zero warnings/errors. Full tests pass with core 2,581, client 122 and server 48, no exclusions/skips. Browser tests are 283/283 and its production build succeeds with the existing size advisory. Content validation is zero errors/warnings, generated protocol is current, and all 11 porter tests pass. The origin loads 200 ticks with three offers, the Amalgamation/Stone duel completes, and the private TCP probe negotiates hit-body/slab/delta/interface features, enters, walks and receives interface/chat state.

The private background harness passes browser 31/31 and native 26/26 with no cleanup errors. Card and combat screenshots were visually inspected and retain terrain/bodies; the nonblank-world assertion records 18.57% dominant color and 171 bins. Guards/quartermasters visibly use their restored level 5. Native pixels and controller input remain untested, and the headless shutdown resource/object leak diagnostics remain in the log.

## Validated base ancestry

After minimal integration, the coordinator confirmed complete #34 tip `745015e5a0849a201e6857e47c9e23435618fb0a` for ancestry reconciliation. Merge it while preserving this branch's independently audited goldens and explicit golem opt-in content. Retain both shared historical review records in their complete newer form, including #34's original 228 B/tick allocation-test failure, isolated pass and unchanged-suite retry. The cause remains unestablished; this branch does not erase or explain away that evidence.

After resolving the four documentation/golden conflicts, `git diff --exit-code a21a58d -- src content tests web scripts Directory.Build.props Directory.Build.targets Directory.Packages.props HollowWake.sln` reports no differences. The application and test inputs are unchanged from the completed functional run. Retain the already-tested Release binary without rebuilding, as requested by the coordinator; merged additions are review/evidence history.

## First controlled measurements on restored levels

The coordinator granted the #39 slot after #38 reported stopped. Exactly one full default Release benchmark and one authored-hit-body profile ran serially at Normal priority on `f25a511596814e762b6972b77712b421e0d6c0de`, with the retained binaries from the equivalent tested `a21a58d` inputs. `bench-run.json` records commands, timestamps, process priorities, source identities and SHA-256 binary hashes. Both processes exited zero and stopped before the #40 handoff. There was no extra worldgen-only process, repeat, discarded sample, workload override or threshold adjustment. A successful benchmark process exit is not a budget verdict.

| Measure | First result | Limit or target |
| --- | ---: | ---: |
| Zone planning | **43.9282 ms** | **40 ms proposed target: missed** |
| Zone tick p95 | 9.1342 ms | 25 ms |
| Zone allocation | 75.889375 KB/tick | 2 MB/tick |
| Behavior | 1.605140 ms, 22.9 KB/tick | 3 ms, 1 MB/tick |
| Snapshot build | 0.879286 ms/player | 1 ms/player |
| Full snapshot size | 28.851885 KB/player | 30 KB/player |
| Delta snapshot size | 8.881948 KB/player | 10 KB/player |
| Chunk generation | 0.542323 ms, 14.335205 KB | 4 ms, 400 KB |
| Attribute fold with extras | 334.492 ns, 0.1392 B/op | 500 ns, 200 B/op |
| Expression evaluation | 102.0304 ns | 150 ns |

Planning misses the proposed target by 3.9282 ms. It is not cleared by the passing established numeric timing/size limits, and this result is not represented as returning to the earlier 21–23 ms main reference. The cause of this miss is not established here. Expression allocation remains the original **0.0168 B/evaluation** reading, not literal zero. Full-snapshot headroom is about **1.148115 KB**. The separately reported interface-inclusive snapshot takes **0.809614 ms/player**. System errors: zero.

The authored profile includes 42 patterns among 120 foes: total delivery processing 1.604306 ms/tick, projectiles 0.848563 ms/tick, fields 0.105729 ms/tick and total allocation 37.592715 KB/tick. It does not change the fifteen golem targets' cylinder-fallback coverage into per-limb anatomy. Raw first samples and stderr files are retained as `bench.json`, `bench-hit-bodies.json` and their companion files under `experiments/golems/evidence/foundation-final/`.

**Performance clearance remains open.** Earlier #39 tick/snapshot/plan figures stay historical because restored legacy levels changed the workload. Keep #39 draft for the planning miss, Claude rereview and later complete reviewed-main integration; the tournament/#20 checks still belong to that later combination. No additional measurement, build or runtime change is started during the other owners' reserved windows.

## Read-only planning inspection

Compared with validated #34 `745015e`, this branch changes no world-generation runtime. Its only Core source difference registers the assembled appearance family for validation. The world content adds four golem works and Suture Works; none adds a quest giver or a roaming spawn table. That does add site candidates to shared planning, but the first timing alone does not attribute the 3.9282 ms miss to those candidates.

`Bench.WorldGen` times the first `generator.Zone` call on a new generator, without warming that path. The timing includes site and quest planning. `GenerateQuests` gathers sites in the zone and its eight neighbors, and `PlacePois` rebuilds and sorts the complete site-definition list for each planned zone. Each candidate position checks every definition; `Fits` repeats region-invariant inhabitant-level eligibility through a capturing `Any` predicate before reaching region eligibility. The extra site definitions therefore add repeated work even in regions where they cannot be placed. Reusing sorted definitions and precomputing only region-invariant eligibility are concrete shared optimization candidates, provided stable candidate order, all weighted choices and generated outcomes are preserved. Their contribution has not been measured.

The shared local-population precheck is deliberately conservative: its roaming union includes every biome/table admitted at a population level. A positive result can therefore lead to exact terrain, landmark and spawn generation even when the relevant biome is absent from a chunk. This is another profiling lead, not an identified defect or proof of the miss. Do not replace it with a heuristic that can reject real population. A matched baseline and stage/call-count profiling after the reserved windows can distinguish site screening, exact population work and first-use compilation. No target, population, level, RNG or quest outcome is changed on this evidence.

Acknowledged by: pending Claude rereview.
