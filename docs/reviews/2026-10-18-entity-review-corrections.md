# Entity stack review corrections

Owner-authorized continuation of PRs #31, #32, #33, #34 and draft #37 after Claude's reviews. This worktree owns the fixes below and will propagate them bottom-up with merge commits. Current main is merged before correction work; published branches are not rewritten.

## Shared ownership notice

- #31: `Summoning`, summon effect signatures, corpse-gated raise-as recipes, summon encounter-tag policy and allegiance at birth; pattern owner faction resolution; delivery identity faction assignments only. Delivery contact/flight remains #36 and release policy remains #20. These edits overlap construct work in #50 only at the existing summon/deploy seam.
- #31 world follow-up: `ZonePlanner`, spawn level transport, content-defined quest-slot level/local population eligibility and natural-site inhabitant eligibility. No region-weight changes. Existing per-candidate weight multipliers will be described truthfully.
- #32: canonical physical mass through attributes, bounded shove authority and swept displacement; shared close_melee behavior content for downstream entity kits. No replacement AI planner.
- #33/#34/#37: faction reciprocity, classifier/habitat corrections, actual population evidence, palette parity and truthful migration/AI documentation.
- #38/#39/#40 own their content-specific populations, spacing, factions and entity families. They consume the corrected #34 base. #40 owns dedicated manifestation region weights and natural landmarks.

## #31 correction checkpoint

The corpse-gated recipe uses `from_corpse: target` plus validated `raise_as: zombie`; a missing corpse fizzles before capacity eviction. Echoing, Unleash, expiry during windup and competing casters have resolution regressions. Spawn without minion terms is now explicitly unowned and keeps wild rewards. Owned births receive effective owner faction; species/material/role tags remain while content lists encounter roots to remove. Delivery allegiance assignments use the relation resolver, including deflections.

Naturally generated populated sites require at least one level-eligible inhabitant. Authored pins can bypass that placement rule. Spawn points carry the site's level into initial birth and respawn; roaming packs retain pattern levels. Shipped combat/scout quest slots now have content-defined `level_delta` windows, and pattern slots require real nearby spawn facts. The planner searches nearest chunks deterministically and skips an unavailable offer. No region weights changed.

The four `aureth` reference zones retain exactly the same placements and per-pattern population counts in this base correction. At the origin the old global cull selected an absent wolf; it now selects a locally generated hob. Creature taxonomy/cull classifiers change further in the downstream passes. Golden hashes now include each spawn's level; climate remains unchanged. See `experiments/entity-review/evidence/31-population-diff.txt` and the complete before/after JSON.

Verification on the correction tree (merged main 26b9e46, merge parent 21514c0): solution build zero warnings/errors; core 650 and client 115 tests passed; all 44 server tests passed after correcting the old test's native-faction expectation to the reviewed birth-allegiance contract. The initial failing server assertion is retained beside its passing rerun. Browser tests 203; prototype porter 10; validate zero errors/warnings; load/fight/protocol check and private TCP delta/interface probe passed. Private smoke: browser 30/30, native headless 26/26; native pixels remain explicitly skipped.

The full Release benchmark is retained and **does not clear the performance gate**: snapshot.build 3.826 ms/player, behavior 4.138 ms/tick, tick p95 25.954 ms, attribute.apply.extra 1340.541 ns and expression.evaluate 417.317 ns exceed their budgets. Allocation and worldgen.chunk remain within budget; zero simulation errors. A controlled baseline comparison and further profiling are required, not an assertion that concurrent load excuses the numbers. Keep the correction stack in draft until resolved.

## #32 implementation scope refinement

Canonical mass uses the existing `mass` formula and geometry input gauges, not a new modifier-engine layer. Transform dimension notifications and `Stats.Tags` cache invalidation are shared changes. Field pull now follows the same physicality policy as impulses; its identity-faction correction from #31 remains intact. The final rule ceiling is `physicality.shove_scale_max` (currently 3), applied after force multipliers and victim resistance. Grid traversal visits every crossed cell; it intentionally retains center-based movement clearance while #36 owns anatomical contact.

The shared `close_melee` behavior is content only. Goblinkin leader bindings move onto it, Troll habitat claims now name their actual toll site, and explicit wilds/the_loosed relations remove direction-dependent faction defaults. The natural-site eligibility guard from #31 prevents a low-level empty Toll without lowering its resident gates.

## #32 correction checkpoint

On the merged correction tree, solution build passed with zero warnings/errors; core697/client116/server44 passed. A final mass-profile-presence refinement preserves the mass floor when a live height reaches zero; the subsequent focused physicality/Goblinkin/population/golden suite passed 63 tests, including its new regression. Validation reports zero errors/warnings; load and Goblin Chief duel pass. Full browser/private-smoke and Release performance will be rerun on the combined downstream tree; this checkpoint is not a cleared release gate.

The recorded four-zone population audit includes every pattern's point/body counts. The eligibility change reshuffles zone 1_0's candidate choices even though its old selected sites were not Tolls: rejecting an ineligible candidate changes weighted selection. Other reference-zone population counts stay unchanged. The origin cull now names a present hob; downstream fauna classifiers replace that provisional cull vocabulary. Records and golden mismatch output are under `experiments/entity-review/evidence/32-*`.

## Verification and handoff

Regression results and exact commit references will be appended after execution. No gate is claimed yet. #37 remains draft until its performance and visual evidence gates are resolved. Claude rereview will be requested with the focused correction commits and retained evidence.

Acknowledged by: pending Claude rereview and owning-PR records.

## Shared planning and level-policy correction

The historical unconditional site-level checkpoint above is superseded by `docs/reviews/2026-10-18-local-population-budget.md`: bounded population inspection, pattern-level defaults on both site birth paths, explicit `use_site_level` opt-in, initial/respawn regressions and full per-site/effective-level audits. The existing counts-only evidence was insufficient to establish unchanged difficulty; the new audit names every changed level. Existing published history and first failures remain retained.

## #32 final foundation propagation

This merge consumes the complete bounded-search, explicit resident-level and exact spawn-query corrections from 3007003db865eceafac955ecf2a859431f010ae2, over the previous #32 head a96307e691b038db2924bcc5ebab6884bca774a7. Solution build has zero warnings/errors; core712/server44/client116 pass, validation zero errors/warnings, load and duel pass. Four-zone audits retain every pattern/site/effective-level count and changed offer under `experiments/entity-review/evidence/32-final-*`. All placements and population counts are unchanged. Default resident levels return to pattern levels on both birth paths; opt-in scaling remains available. The cap omits distant offers and may shift later seeded selections; exact changes are retained. Climate remains unchanged. The first expected golden failures are retained before the reviewed refresh.

The controlled36.6863ms result belongs to exact #31 runtime ec144c3 and the proposed40ms target; it is not a performance measurement of this #32 tree. Earlier level-dependent tick/snapshot measurements remain historical. Claude rereview and final stack integration remain pending.
