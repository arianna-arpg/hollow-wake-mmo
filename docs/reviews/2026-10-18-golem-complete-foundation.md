# Golem integration with the final reviewed foundation

2026-10-18. Consume exact complete #34 `c3099a8ea5c7a32fc0799c3a17cb167b2bd1231c` onto #39 `5043110206b68bc27538e32e7f40e2b82d49b021`, at the coordinator and foundation owner's handoff. It includes #33 `a3eafab`, complete reviewed #36 `01b2884`, main `17ca2f13`, and complete reviewed main `fa7f5f0` with PR #56's lifecycle-test correction. No duplicate patch or separate main merge is used.

Ownership before resolution: reconcile the overlapping population contracts, shared historical review appendix and this branch's generation goldens. Preserve golem content, site-level opt-ins, literal admissions and accepted Warden migration; retain the full reviewed shared contact/runtime implementation and card/dialogue harness fixes. Keep existing golem goldens through the first audit rather than copying #34's values. The population contracts retain explicit scaling/eligibility independence and name the Knot's new opt-in; the shared #37 historical appendix remains intact.

The accepted Knot behavior change gives its inhabitants the site's level and searches actual population around the selected Knot, within the content-owned bound. A fresh pre-merge four-zone audit at `5043110` is retained under `experiments/golems/evidence/complete-foundation/before.json`. Compare levels, counts, sites and quest offers with the merged source before changing golden constants. The existing golem birth/respawn and projectile cases stay in the full suite; no authored dimensions, AI doctrine, anatomy profile or balance number is changed locally.

The complete Stone Warden shipped-body decision remains closed by [Claude's focused acknowledgment](https://github.com/arianna-arpg/hollow-wake-mmo/pull/39#issuecomment-6255260281): authored 1,905.904 kg mass, eight classifiers and assembled appearance, including shove/knockback/mass-based effects. That acknowledgment does not close other gates.

## Audited world changes

Merge source `0e9e95f` builds with zero warnings/errors. Fresh `before.json` at `5043110` and `after.json` at the merged source use the same level-aware audit program and seed `aureth`; `world-diff.json`/`.txt` compare all four reference zones. Every site and population count is unchanged. At Ninewells, Clear Knot returns with the reach target `poi.0_0.knot.200.60` and `cinder_imp` cull target. Gather Floss and Cull Hob are unchanged; the seeded scout target changes from `poi.0_1.ruin.213.450` to `poi.1_1.ruin.305.334`. The other three sampled zones retain no quest offers.

The origin Knot's four imps, four shades and one tangle now use site level 3 instead of pattern levels 4, 2 and 6. All residents of the three sampled Knots in zone(0,-1) use site level 7; the Knot in zone(3,2) uses site level 3. Zone(1,0) has no changed spawn levels. No golem or Amalgamation content is changed by this merge. These four zones contain no golem site, so their audit does not establish golem birth behavior; the existing actual birth/respawn tests at region levels 1 and 21 provide that evidence.

The first focused run retains four intended golden failures and 434 passes (438 total), including all golem cases. After inspecting the audit, update only origin info/chunks to `4BFB77C7FB773A38`/`B04153F9629D9BAE`, zone(0,-1) chunks to `D785D8F084A1F919`, zone(3,2) chunks to `8711D19C4613D904`, and the overlaid-world hash to `805116FF4B6EE539`. The other zone-info/chunk values and climate hash `999D90D6110B1CDD` remain unchanged. This branch's origin-info and overlay hashes differ from #34's and are not copied from it.

## Complete-source verification

Tested source `a273622adae54ba2eb22733f8802310d8d54b5bb` changes only the independently audited goldens and review evidence after merge `0e9e95f`. Debug solution and Release tools builds report zero warnings/errors. The full suite passes core 2,842, server 59 and client 135, with no exclusions/skips; all existing golem cases and the shared launch/placement/contact regressions are included. Browser tests pass 313/313, porter 10/10 and lifecycle tests 10/10. The production browser build retains its existing size advisory.

Validation reports zero errors/warnings. The generated protocol is unchanged and the exact frozen-literal census equals all 523 reviewed keys, with no additions/removals. The origin loads for 200 ticks with four offers, and the Amalgamation/Stone duel completes. All seven standard Windows Release hashes match the reviewed baseline with the same seeds, cap and platform. No replay expectation was changed locally.

Exact archived golem content from `5043110` and current content run the proving pairs under the same new Release runtime. Their entire JSON reports are identical, including mechanisms, outcomes and breaches, and equal the retained prior report. Both runs exit one for the inherited bulwark, plaguer and pitfighter 10/10 wins against the 80% ceiling. These remain balance failures; no band or numeric content was changed to clear them. Raw and repository-normalized report hashes are recorded separately in `proving-comparison.json`.

The fresh six-feature TCP probe passes. Private complete-source run `run-6zDkjL` passes browser 31/31 and native 36/36 with no cleanup errors. The browser receives and answers the villager dialogue. Card and grayscale screenshots were visually inspected and show terrain and bodies; the nonblank-world assertion records 18.43% dominant color and 169 bins. Native rendered pixels and physical controller input remain untested, and the retained headless output includes RID/ObjectDB shutdown leak diagnostics. No repeat of a completed broad gate was needed.

Against incoming `c3099a8`, the only Core source delta is registration of the assembled appearance family; the shared simulation, protocol, server and tools implementations are retained intact. Golem/Amalgamation patterns and sites are unchanged from `5043110`. All owned private test/server sessions have completed; generated untracked Godot UID sidecars were removed from this worktree only.

## Remaining acceptance

The first four golden failures remain with the audit; earlier integration and performance failures retain their original records. No performance sample, trace capture, budget relaxation, standalone main merge or separate #20/#50 combined acceptance is part of this pass. Earlier timings, including the 43.9282 ms planning miss, remain evidence only for their exact sources. Keep the PR draft for Claude review and coordinated performance acceptance. Hosted checks for the final published head are not yet claimed passed.

Acknowledged by: pending Claude review of this complete integration.
