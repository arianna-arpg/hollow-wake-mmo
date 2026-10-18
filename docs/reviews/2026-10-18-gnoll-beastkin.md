# Gnoll and Beastkin clans: scope and ownership

2026-10-18. Codex owns codex/gnoll-beastkin-clans, based on fauna PR #34 at 90cb7fc. The owner requests distinct hunched hyena/coyote Gnolls and horned satyr/minotaur/goat/ram Beastkin with hocked legs, continuing the fully classified and physically authored entity foundation.

Scope: prototype-inspired content, separate factions, existing skills and provisional behaviors, habitats/POIs, material mass, paired client anatomy, scale/grayscale galleries, integration/parity tests and evidence. No AI or collision runtime replacement. Declare overlap before edits with own #31-34 body family registries, entity/appearance contracts and world generation goldens. Use dedicated new content files; existing wildlife and undead bodies remain owned by their preceding passes.

The owner also requested three separate chats, now dispatched from #34 in their own worktrees: golems (owns stone_warden migration if needed), elemental entities, and reptilian Kobolds. Parent owns Gnolls and Beastkin only. Shared registry/contract and population-golden edits are additive overlaps to reconcile during stack integration; no chat should reuse this worktree. Precise hitboxes remain a separate active chat. Current main has moved independently, including prior equipment and warning fixes; this content stack remains on #34 pending integration.

Claude review is requested on PR #37 with evidence. The fauna AI design handoff remains the boundary for future complex AI work. This pass does not imply pack coordination, herding, morale or special movement from visual anatomy.

## Review and AI handoff

Claude: inspect the paired canine/ungulate anatomy and the silhouette gallery, the occupied-volume/material estimates, supported kit substitutions, low/high-level champion gates and existing-budget world changes. The roster contains twelve Gnolls and ten Beastkin; exact records and source revision are in the experiment provenance. All combat kit checks require positive damage, not merely a cast event. They do not prove every skill is tactically chosen.

Use the fauna AI handoff as the design boundary. Add diagnostic scenarios for a Gnoll pack circling a blocker while a longshot holds a sightline; a Matron choosing between self-preservation, ally triage and rally; an isolated Trapper choosing reachable trap placement; and a mixed satyr/minotaur group coordinating openings without smaller bodies becoming trapped behind the Bullwarden. Observe commitment windows, poise, shove resistance, resources, friendly spacing, failed reachability and decision budget. These are proposed scenarios for Claude’s design pass, not implemented behaviors or completed simulations. Current skeleton state machines remain replaceable content bindings.

The shared-scale report distinguishes authoritative body height from conservative rendered bounds, which include horns and other extremities. Body profiles do not automatically claim every horn tip as a damage volume. The separate hitbox chat must migrate these families after integration.

## Initial evidence and remaining gate (historical)

Implementation is on draft PR #37. All 893 .NET, 276 web and 10 porter tests pass; validation is 0/0, production builds pass, and private browser/native smoke checks pass 29/29 and 25/25. Native headless pixel checks are skipped and baseline exit diagnostics remain. The committed experiment contains full logs, reviewed before/after maps, rendered body evidence, shared plan fixtures and source adaptations.

The Release snapshot-time budget is not declared passed. Initial and repeated same-runtime comparisons vary around 1 ms, with both the fauna baseline and this branch exceeding it in some runs. The three-pair medians are 1.039 and 1.089 ms respectively. Every run is retained in the evidence; all report zero system errors. Keep the PR in draft until controlled performance review resolves the gate. The existing card-view screenshot issue is also documented with baseline/current images; smoke interaction counts alone do not prove rendered card output. See experiments/gnoll-beastkin/evidence/verification.md for exact limitations.

Acknowledged by: pending Claude review.

## Final shared foundation and client verification

Source/content head `ce3bcf569fb27426991ec69931c950e6951c74e1` incorporates the full corrected fauna stack through `745015e`, including #36. The final solution build has zero warnings/errors; all 2,811 .NET tests pass (core 2,642, server 48, client 121), as do 290 browser and 10 porter tests. Content validation is zero errors/warnings; load, live duel, production web build and generated protocol parity pass. The web build retains its existing large-chunk advisory. No new networking change requires repeating the earlier private TCP probe.

Private run `run-4gmvzw` passes browser 31/31 and native 26/26 with clean teardown. The fresh card and grayscale frames show terrain and bodies correctly; both are retained alongside the complete logs under `experiments/entity-review/evidence/37-final-*`. Native rendered pixels remain explicitly skipped. Godot still emits the previously documented headless exit allocation diagnostics; their full output is retained and no zero-diagnostic claim is made. The common-scale gallery and paired body-plan fixtures remain unchanged; final tests cover those fixtures, the 462 kin and 462 fauna real-flight cylinder cases, and 882 authored-profile cases. These are fallback cylinders for new kin, not authored anatomical profiles.

The audited generation change preserves site placement and every population count in four reference zones. Default resident levels return to pattern levels on both birth paths; `use_site_level` explicitly enables site scaling. The 16-chunk bounded population query omits origin Clear Knot and changes the later Scout selection to `poi.0_1.ruin.213.450`. Exact per-pattern/site/effective-level and offer differences are retained before the golden refresh. Climate remains unchanged.

Earlier timing/traffic evidence at `6a65ed1` is historical after the resident-level change, not an exact-head performance pass. The shared #31 source `ec144c3` measured planning at 36.6863 ms against the proposed 40 ms target; that is not a measurement of this #37 tree or acceptance of the proposed target. Claude rereview, coordinated final performance evidence and combined stack integration remain pending. #20 owns the separate release policy. No merge to main was performed.

## Retained pre-main Release measurement

The final coordinated first samples at `c6e117a` are recorded in `experiments/entity-review/evidence/37-final-benchmark-review.md`. A fresh Release build preceded measurement after preflight rejected an older #31 binary. Snapshot building is 1.0225075 ms against the established 1 ms budget; zone planning is 40.8383 ms against the proposed 40 ms target. Both misses are retained, with all raw rows, hashes and the authored-profile workload. Other named numeric timing/size limits pass; process-wide expression allocation remains nonzero. No reruns or source changes occurred, and all benchmark jobs stopped before the next owner. The separate main integration is not certified by this pre-main measurement. Keep draft pending review and resolution of the misses.
