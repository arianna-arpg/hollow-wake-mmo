# Gnoll and Beastkin clans: scope and ownership

2026-10-18. Codex owns codex/gnoll-beastkin-clans, based on fauna PR #34 at 90cb7fc. The owner requests distinct hunched hyena/coyote Gnolls and horned satyr/minotaur/goat/ram Beastkin with hocked legs, continuing the fully classified and physically authored entity foundation.

Scope: prototype-inspired content, separate factions, existing skills and provisional behaviors, habitats/POIs, material mass, paired client anatomy, scale/grayscale galleries, integration/parity tests and evidence. No AI or collision runtime replacement. Declare overlap before edits with own #31-34 body family registries, entity/appearance contracts and world generation goldens. Use dedicated new content files; existing wildlife and undead bodies remain owned by their preceding passes.

The owner also requested three separate chats, now dispatched from #34 in their own worktrees: golems (owns stone_warden migration if needed), elemental entities, and reptilian Kobolds. Parent owns Gnolls and Beastkin only. Shared registry/contract and population-golden edits are additive overlaps to reconcile during stack integration; no chat should reuse this worktree. Precise hitboxes remain a separate active chat. Current main has moved independently, including prior equipment and warning fixes; this content stack remains on #34 pending integration.

Claude review is requested on PR #37 with evidence. The fauna AI design handoff remains the boundary for future complex AI work. This pass does not imply pack coordination, herding, morale or special movement from visual anatomy.

## Review and AI handoff

Claude: inspect the paired canine/ungulate anatomy and the silhouette gallery, the occupied-volume/material estimates, supported kit substitutions, low/high-level champion gates and existing-budget world changes. The roster contains twelve Gnolls and ten Beastkin; exact records and source revision are in the experiment provenance. All combat kit checks require positive damage, not merely a cast event. They do not prove every skill is tactically chosen.

Use the fauna AI handoff as the design boundary. Add diagnostic scenarios for a Gnoll pack circling a blocker while a longshot holds a sightline; a Matron choosing between self-preservation, ally triage and rally; an isolated Trapper choosing reachable trap placement; and a mixed satyr/minotaur group coordinating openings without smaller bodies becoming trapped behind the Bullwarden. Observe commitment windows, poise, shove resistance, resources, friendly spacing, failed reachability and decision budget. These are proposed scenarios for Claudeâ€™s design pass, not implemented behaviors or completed simulations. Current skeleton state machines remain replaceable content bindings.

The shared-scale report distinguishes authoritative body height from conservative rendered bounds, which include horns and other extremities. Body profiles do not automatically claim every horn tip as a damage volume. The separate hitbox chat must migrate these families after integration.

## Initial evidence and remaining gate (historical)

Implementation is on draft PR #37. All 893 .NET, 276 web and 10 porter tests pass; validation is 0/0, production builds pass, and private browser/native smoke checks pass 29/29 and 25/25. Native headless pixel checks are skipped and baseline exit diagnostics remain. The committed experiment contains full logs, reviewed before/after maps, rendered body evidence, shared plan fixtures and source adaptations.

The Release snapshot-time budget is not declared passed. Initial and repeated same-runtime comparisons vary around 1 ms, with both the fauna baseline and this branch exceeding it in some runs. The three-pair medians are 1.039 and 1.089 ms respectively. Every run is retained in the evidence; all report zero system errors. Keep the PR in draft until controlled performance review resolves the gate. The existing card-view screenshot issue is also documented with baseline/current images; smoke interaction counts alone do not prove rendered card output. See experiments/gnoll-beastkin/evidence/verification.md for exact limitations.

Acknowledged by: [Claude review of #37](https://github.com/arianna-arpg/hollow-wake-mmo/pull/37#issuecomment-6254572663) requested the corrected actual-base audit and retained performance follow-up; final forward integration awaits rereview.

## Pre-main shared foundation and client verification (historical)

Source/content head `ce3bcf569fb27426991ec69931c950e6951c74e1` incorporates the full corrected fauna stack through `745015e`, including #36. The final solution build has zero warnings/errors; all 2,811 .NET tests pass (core 2,642, server 48, client 121), as do 290 browser and 10 porter tests. Content validation is zero errors/warnings; load, live duel, production web build and generated protocol parity pass. The web build retains its existing large-chunk advisory. No new networking change requires repeating the earlier private TCP probe.

Private run `run-4gmvzw` passes browser 31/31 and native 26/26 with clean teardown. The fresh card and grayscale frames show terrain and bodies correctly; both are retained alongside the complete logs under `experiments/entity-review/evidence/37-final-*`. Native rendered pixels remain explicitly skipped. Godot still emits the previously documented headless exit allocation diagnostics; their full output is retained and no zero-diagnostic claim is made. The common-scale gallery and paired body-plan fixtures remain unchanged; final tests cover those fixtures, the 462 kin and 462 fauna real-flight cylinder cases, and 882 authored-profile cases. These are fallback cylinders for new kin, not authored anatomical profiles.

The audited generation change preserves site placement and every population count in four reference zones. Default resident levels return to pattern levels on both birth paths; `use_site_level` explicitly enables site scaling. The 16-chunk bounded population query omits origin Clear Knot and changes the later Scout selection to `poi.0_1.ruin.213.450`. Exact per-pattern/site/effective-level and offer differences are retained before the golden refresh. Climate remains unchanged.

Earlier timing/traffic evidence at `6a65ed1` is historical after the resident-level change, not an exact-head performance pass. The shared #31 source `ec144c3` measured planning at 36.6863 ms against the proposed 40 ms target; that is not a measurement of this #37 tree or acceptance of the proposed target. Claude rereview, coordinated final performance evidence and combined stack integration remain pending. #20 owns the separate release policy. No merge to main was performed.

## Retained pre-main Release measurement

The final coordinated first samples at `c6e117a` are recorded in `experiments/entity-review/evidence/37-final-benchmark-review.md`. A fresh Release build preceded measurement after preflight rejected an older #31 binary. Snapshot building is 1.0225075 ms against the established 1 ms budget; zone planning is 40.8383 ms against the proposed 40 ms target. Both misses are retained, with all raw rows, hashes and the authored-profile workload. Other named numeric timing/size limits pass; process-wide expression allocation remains nonzero. No reruns or source changes occurred, and all benchmark jobs stopped before the next owner. The separate main integration is not certified by this pre-main measurement. Keep draft pending review and resolution of the misses.

## Complete main and entity foundation integration

Source `4185c7fdbc8e057d35b78fc9b9587642f4f7183d` merges validated fauna `3d339d2ac71af6ac8bcff8e4f767e1f7c44ce2d8`, which includes complete corrected #33 `80dcac7`, reconciled #36 `c07b6df` and main `45ce87d`. Gnoll/Beastkin patterns, appearance, the existing world-generation goldens and renderer correction `30f6c91` remain unchanged by the merge. The generated grammar census exactly matches all 499 reviewed keys without a new allowance.

Both Debug solution and Release tool builds have zero warnings/errors. Core 2780, server 59, native logic 134, browser 320 and porter 10 tests pass. Validate is 0/0; load, duel, generated protocol, production browser build, all seven Release Windows replay hashes and a private TCP probe granting all six requested features pass. Private `run-MG1zf0` passes browser 31/31 and native 36/36, with clean teardown. Fresh card/grayscale frames show the terrain and bodies, and the world-pixel assertion passes. Native rendered pixels remain skipped; headless exit diagnostics and the existing browser chunk advisory remain disclosed. Exact evidence is in `experiments/entity-review/evidence/37-main-*` and `docs/reviews/2026-10-18-entity-main-audits.md`.

Harness-only correction `316c97358005bf5faed195f68d1ac466b7a496fe` rechecks live villager reach after aiming. The initial private failure and one diagnostic reproduction both retain the correct target receiving `too_far` after moving beyond the stale arrival position. Three deterministic regressions cover movement, wrong hover/timeout and failed approach/despawn. The corrected private run above follows this diagnosed fix; runtime/content are unchanged. Both failures remain in `37-main-interaction-*`.

The inherited fauna baseline updates only the three counterfactually proven wolf/boar pairings; the other four hashes, platform, seeds and cap remain unchanged. A fresh Release proving report equals the retained fauna report in its entirety, including outcomes, mechanisms, hashes and the three inherited 80% win-ceiling breaches. No balance clearance or relaxed band is claimed.

No post-main timing sample was run. The retained snapshot 1.0225075 ms / 1 ms budget miss and planning 40.8383 ms / proposed 40 ms target miss remain open historical evidence. They do not certify this source, and no cause has been established. Claude rereview and the coordinator's combined acceptance/performance work remain outstanding. New kin still use fallback cylinders; complex pack/herd AI and authored anatomy migration remain future work. No merge to main was performed.

## Final reviewed foundation and actual-base audit

Runtime/content source `bda29c7182b0a93640b8e16a1d20b49dc0d42fd3` consumes complete fauna `c3099a8ea5c7a32fc0799c3a17cb167b2bd1231c`, including the reviewed #36 placement/aim sequence and the test-only mainfa7 lifecycle fix. The full Debug/Release builds have zero warnings/errors; core 3035, server 59 and native logic 134 pass. Browser 320 and porter 10 tests, validation 0/0, load/duel, generated protocol, the exact 499-key census and seven Windows replay hashes pass. Full proving JSON equals the actual previous kin and fauna reports, preserving three inherited balance failures.

All 132 child-only Tide Orb/Frozen Orb cases pass in the full suite, alongside the 462 kin actual-flight cases. The child assertions cover all 22 original-size bodies at three terrain offsets, preserving emission height, source and cast identity. The earlier 132 failures and five generation-golden failures remain retained. No body was enlarged and no balance band was relaxed.

Client source `021f79a131a97d72fe173afabb9407680ca647e5` passes private `run-L3gBxb`: browser 31/31 and native 36/36, with clean teardown. The private TCP probe grants all six features. Fresh card/grayscale frames were viewed and show the full world and bodies. Native pixels remain skipped; shutdown diagnostics and the browser chunk-size advisory remain disclosed.

The corrected [actual fauna-base audit](../../experiments/entity-review/evidence/37-base-world-review.md) compares exact fauna `c3099a8ea5c7a32fc0799c3a17cb167b2bd1231c` against kin `2d5b3306a5ab3f7915fb92542c7695ebd4b4879b`. It names all three eastern site replacements, the Knot's seven stones and ten level-3 combat residents, and the changed origin Scout destination. Removing only the six clan-camp definitions reproduces the entire four-zone fauna report. Earlier same-branch comparisons and original maps are historical. The new four-zone sample contains no natural clan camp; separate eligibility tests cover the authored camps.

Physical profiles and paired silhouettes remain unchanged by this integration. The explicit faction/skill/loot and movement/AI/anatomy boundaries are named in that audit. Historical snapshot/planning misses remain open; the prepared main17ca/#31 pair has not run. Combined #20/#50 acceptance is separate. Claude rereview is requested; no merge to main.
