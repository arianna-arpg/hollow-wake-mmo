# Golem final foundation correction

2026-10-18. Follow-up to the published #39 checkpoint `20db6e36c6b00a7d1eaaa2aa5f7060140c2e7b69`. The owner-authorized coordinator supplied the minimal corrections `b1dd4d9` (bounded local quest search), `097c526` (explicit site-level births in both paths), and `192e073` (exact spawn-fact query and conservative precheck). The shared owner's evidence head is `3007003db865eceafac955ecf2a859431f010ae2`, tested source `ec144c3`.

Ownership before edits: consume these shared corrections with attribution, preserving the existing #34/#36 and card-renderer integrations. This branch owns the golem inhabitants' explicit scaling choice, corresponding actual birth/respawn tests and audited goldens. Update the overlapping population contracts only to explain opt-in behavior consistently. No duplicate search/runtime implementation, encounter expansion, #20 policy or unrelated species edits.

The four golem works and provisional Suture Works retain this branch's already-reviewed site-relative resident levels by explicitly opting their inhabitants into `use_site_level`. Shared landmark stamps and legacy residents retain the restored pattern-level default. Eligibility and level scaling remain separate. Before changing golden constants, retain four-zone counts, actual spawn levels and quest offers from the old and new runtimes using the shared owner's level-aware audit program. Old timing and smoke evidence remains historical because resident levels and available offers change.

## Audited world changes

`experiments/golems/evidence/foundation-final/` retains `before.json`, `after.json`, `world-diff.json` and a readable `world-diff.txt`. The audit uses the shared owner's level-aware program from `097c526`, first against the old #39 runtime/content, then against these corrections plus the explicit golem opt-ins. It compares all four golden zones, counting sites, spawn points, bodies, explicit/effective levels and quest targets. All sampled sites and population counts are unchanged.

At Ninewells, Clear Knot is omitted by the bounded local search; Gather Floss and Cull Hob remain. The subsequent seeded scout selection moves from ruin `poi.1_1.ruin.305.334` to ruin `poi.0_1.ruin.213.450`. This differs from #31's own scout target, so its golden constants were not copied. The other three sampled zones still have no offers. Origin guards/quartermasters return from mixed level 1/5 to their authored level 5; knot imps/shades/tangles return to 4/2/6. The eastern Orc/Goblin residents and the other zone's Wight likewise regain their pattern levels. Full per-site counts, including removed explicit stamps whose effective levels stay unchanged, remain in the audit.

The first focused run passes 458 cases and fails only the five expected golden cases. All five golem-site regressions verify actual birth and replacement at region levels 1 and 21; all 342 projectile cases, bounded-search, legacy-level and exact spawn-fact/overlay cases pass. The level-aware chunk hashes change in all four zones; only the origin zone-info hash changes. Climate remains `999D90D6110B1CDD`. After reviewing the audit, update origin info/chunks to `467923F597DB83C5`/`38740F7C0C66C2F6`, eastern chunks to `0E386FE1D6659BC0`, northern chunks to `2AFA9EE5AA75F409`, distant chunks to `B927209C7C70E524`, and the overlay hash to `F2165541CF5F5CDF`. Other zone-info hashes stay unchanged.

## Combined functional checkpoint

Source `a21a58dbf11b974df7702405e6c933a53f3842be` contains the three corrections as `787e955`, `cf4310d` and `a73a404`, plus the explicit golem content, regression and audited-golden changes. The only cherry-pick conflict was the shared historical review record; its retained failed planning sample and subsequent optimization notes were preserved. No shared runtime behavior was changed locally.

Debug solution and Release tools builds have zero warnings/errors. Full tests pass with core 2,581, client 122 and server 48, no exclusions/skips. Browser tests are 283/283 and its production build succeeds with the existing size advisory. Content validation is zero errors/warnings, generated protocol is current, and all 11 porter tests pass. The origin loads 200 ticks with three offers, the Amalgamation/Stone duel completes, and the private TCP probe negotiates hit-body/slab/delta/interface features, enters, walks and receives interface/chat state.

The private background harness passes browser 31/31 and native 26/26 with no cleanup errors. Card and combat screenshots were visually inspected and retain terrain/bodies; the nonblank-world assertion records 18.57% dominant color and 171 bins. Guards/quartermasters visibly use their restored level 5. Native pixels and controller input remain untested, and the headless shutdown resource/object leak diagnostics remain in the log.

New coordinated performance measurements pending. Earlier #39 tick/snapshot/plan figures are historical because restored legacy levels change the workload. Keep #39 draft for Claude rereview and later complete reviewed-main integration; the tournament/#20 checks still belong to that later combination.

Acknowledged by: pending Claude rereview.
