# Golem integration with the final reviewed foundation

2026-10-18. Consume exact complete #34 `c3099a8ea5c7a32fc0799c3a17cb167b2bd1231c` onto #39 `5043110206b68bc27538e32e7f40e2b82d49b021`, at the coordinator and foundation owner's handoff. It includes #33 `a3eafab`, complete reviewed #36 `01b2884`, main `17ca2f13`, and complete reviewed main `fa7f5f0` with PR #56's lifecycle-test correction. No duplicate patch or separate main merge is used.

Ownership before resolution: reconcile the overlapping population contracts, shared historical review appendix and this branch's generation goldens. Preserve golem content, site-level opt-ins, literal admissions and accepted Warden migration; retain the full reviewed shared contact/runtime implementation and card/dialogue harness fixes. Keep existing golem goldens through the first audit rather than copying #34's values. The population contracts retain explicit scaling/eligibility independence and name the Knot's new opt-in; the shared #37 historical appendix remains intact.

The accepted Knot behavior change gives its inhabitants the site's level and searches actual population around the selected Knot, within the content-owned bound. A fresh pre-merge four-zone audit at `5043110` is retained under `experiments/golems/evidence/complete-foundation/before.json`. Compare levels, counts, sites and quest offers with the merged source before changing golden constants. The existing golem birth/respawn and projectile cases stay in the full suite; no authored dimensions, AI doctrine, anatomy profile or balance number is changed locally.

The complete Stone Warden shipped-body decision remains closed by [Claude's focused acknowledgment](https://github.com/arianna-arpg/hollow-wake-mmo/pull/39#issuecomment-6255260281): authored 1,905.904 kg mass, eight classifiers and assembled appearance, including shove/knockback/mass-based effects. That acknowledgment does not close other gates.

## Audited world changes

Merge source `0e9e95f` builds with zero warnings/errors. Fresh `before.json` at `5043110` and `after.json` at the merged source use the same level-aware audit program and seed `aureth`; `world-diff.json`/`.txt` compare all four reference zones. Every site and population count is unchanged. At Ninewells, Clear Knot returns with the reach target `poi.0_0.knot.200.60` and `cinder_imp` cull target. Gather Floss and Cull Hob are unchanged; the seeded scout target changes from `poi.0_1.ruin.213.450` to `poi.1_1.ruin.305.334`. The other three sampled zones retain no quest offers.

The origin Knot's four imps, four shades and one tangle now use site level 3 instead of pattern levels 4, 2 and 6. All residents of the three sampled Knots in zone(0,-1) use site level 7; the Knot in zone(3,2) uses site level 3. Zone(1,0) has no changed spawn levels. No golem or Amalgamation content is changed by this merge. These four zones contain no golem site, so their audit does not establish golem birth behavior; the existing actual birth/respawn tests at region levels 1 and 21 provide that evidence.

The first focused run retains four intended golden failures and 434 passes (438 total), including all golem cases. After inspecting the audit, update only origin info/chunks to `4BFB77C7FB773A38`/`B04153F9629D9BAE`, zone(0,-1) chunks to `D785D8F084A1F919`, zone(3,2) chunks to `8711D19C4613D904`, and the overlaid-world hash to `805116FF4B6EE539`. The other zone-info/chunk values and climate hash `999D90D6110B1CDD` remain unchanged. This branch's origin-info and overlay hashes differ from #34's and are not copied from it.

Final functional and client verification on this new runtime is pending. Retain all first failures. No performance sample, trace capture, budget relaxation, main merge or separate #20/#50 combined acceptance is part of this pass. Earlier timings, including the 43.9282 ms planning miss, remain evidence only for their exact sources.

Acknowledged by: pending Claude review of this complete integration.
