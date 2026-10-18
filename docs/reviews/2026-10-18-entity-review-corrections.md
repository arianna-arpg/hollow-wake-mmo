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

## #33 correction scope

The player reads `physicality.reference_height`; skeleton and Goblin/Orc stature tests use that authored reference. The CharacterSheet height assignment overlaps #36 and will preserve its hit-body registration during the full foundation merge. Orc melee bindings reuse `close_melee`, and Orc faction relations explicitly reciprocate wilds/the_loosed hostility. Ravager provenance now declares its rebalance and restores its existing Scrap Fort habitat. Goblin mass reduction from the stature pass is intentional geometric scaling. The small Gnasher silhouettes remain unchanged; projectile coverage requires the complete #36 foundation.

## #33 correction checkpoint

Solution build and tests and content validation are recorded under `experiments/entity-review/evidence/33-*`. The four-zone per-pattern audit retains all count changes, not only ASCII maps. The origin scout now selects an eligible level-three ruin rather than a level-eight Chainkeeper Camp. Natural candidate eligibility reshuffles the eastern site's inhabitants, with unchanged counts in the other three zones. Climate remains unchanged. Full client smoke and performance gates remain for the combined downstream tree; the small-body projectile blocker still depends on full #36 and is not claimed closed by this content correction.

The broad goblinkin classifier intentionally includes Orc ancestry. GoblinkinTests itself was not expanded in the original pass: its existing parameterized filter gained Gnashers and lost the migrated Ravager; OrcTests owns Orc coverage. Size classifiers are authoring categories rather than mass thresholds. This checkpoint remains draft pending the combined gates and Claude rereview.

## Full hitbox integration ownership

#34 will merge the complete #36 foundation at 724c5ed, including its collision, delivery, AimOffset and hit-body registration changes. This deliberately overlaps the shared files already corrected here. Preserve effective faction resolution, mass dimension notifications, hit-body registration/bounds invalidation and #36's removed private release gate. #20 remains the release-policy owner. This notice precedes the merge and its conflict resolution.

## #34 correction scope

This branch consumes corrected #33 and the complete #36 hitbox foundation, including its 724c5ed correction. The merge preserves CharacterSheet hit-body registration and bounds invalidation, PatternFactory invalidation, height-normalized anatomy, independent navigation radius, AimOffset and the removal of #36's private release gate. #20 remains the release-policy owner. This shared-source overlap is recorded before resolving the merge.

Fauna palette defaults match the native builder. Repeated AI metadata is replaced by the single dated handoff. Bird fill and heft now represent separate authoring inputs without changing mass. Grove Stag keeps ambient rarity policy. Population audits include existing patterns reshuffled by spawn-table random draws, while local cull and scout gates come from the shared foundation.

## #34 published integration checkpoint

The complete hitbox foundation merge is e383ffb, following corrected #33 at 5001a74. On this corrected tree: solution build zero warnings/errors; core2137/server48/client120 tests passed, browser264 and production web build passed (existing large-chunk advisory remains). Validation zero errors/warnings; load and protocol check passed. Loosed Kindler now damages and defeats Wolverine in the recorded duel. The existing authored anatomy matrix and all 462 added fauna cast/contact cases pass across flat ground and +/-0.5 m relief. Fauna explicitly uses cylinder fallback, not authored anatomical profiles; dimensions are unchanged. The actual graze/flee/fight regression also passes for injured stationary prey.

`34-original-addition-*` measures original #33 to original #34, including changed existing populations. `34-before/after` and its diff separately measure the review correction. The origin cull selects a locally present eligible Hob (still an intentional beast-cull candidate); the scout selects an eligible level-three ruin. Natural-site candidate filtering reshuffles eastern sites. Climate remains unchanged.

This checkpoint is reusable by #38/#39/#40 but stays draft pending private smoke, combined performance and Claude rereview. The full #36 dependency is explicit; #20 release-policy integration remains separately coordinated. No behavior/performance result from a temporary species merge is promoted to this tree without rerunning it.

## #34 private smoke follow-up

On 26c50de, the private browser and native-headless playthroughs pass with no cleanup errors; full runner result is `34-smoke.json`. Native pixel checks remain skipped. Inspection of `16_cards.png` still shows the inherited blank-world/card-mode defect, also recorded in the original #37 baseline. Therefore behavioral success is not claimed as a visual pass. #37 will correct CardBaker renderer-state restoration and add a regression plus fresh rendered evidence; this shared presentation ownership is recorded before that edit. The exact #34 Release performance check is also being independently repeated by the #36 owner.

## #37 correction scope and AI handoff

Gnolls and Beastkin consume the full corrected fauna/hitbox base. Their melee kits reuse `close_melee`; wilds/the_loosed hostility is reciprocal, and Goblinkin explicitly names its existing Gnoll alliance. Habitat and stature tests use the shared content contracts. All bodies retain their original shapes, palettes and material dimensions; the added real-flight matrix asserts cylinder fallback across flat and +/-0.5 m terrain.

Repeated AI metadata is removed from concrete patterns. The shared intent remains bounded perception, mechanic-aware skill choice and readable commitments; Gnolls additionally call for pack flanking/morale and Beastkin for herd formations/coordinated charges. Commander orders, reinforcements, tactical trap placement, ally triage and terrain-aware movement remain deferred to the existing Claude AI design handoff. Current assignments live only in behavior components.

The inherited card-mode blank-world defect is caused by CardBaker changing the renderer's viewport, scissor and clear color without restoring them. #37 owns the narrowly scoped restoration fix, failure-path cleanup and renderer-state regression, as predeclared in the #34 smoke follow-up. Fresh production smoke screenshots must verify the visible world before the visual gate can close. The shared browser smoke driver will add a conservative nonblank-world pixel check at its existing card screenshot; this extends the preserved driver rather than replacing its input flow.

## #37 functional correction checkpoint

Build and Release tool build: zero warnings/errors. Full tests pass: core2628/server48/client121; browser290 and production build pass with the existing chunk-size advisory; porter10; content validation zero errors/warnings; load, duel, protocol parity and private TCP delta/interface probe pass. The initial probe used an overlong generated account and was correctly rejected; its output is retained beside the passing short-account rerun. This was a probe setup error, not a server regression.

All 462 kin real-flight cases pass, alongside the 462 fauna and 882 authored-profile cases. Kin/fauna still explicitly use cylinder fallback. The warm small-expression regression measures zero bytes on its executing thread for four formulas, separately from the CLI's process-wide allocation counter; the published benchmark is not altered. The independent exact-26c50de timing/size comparison is recorded on [#34](https://github.com/arianna-arpg/hollow-wake-mmo/pull/34#issuecomment-6253489076); the kin population still requires its own measurement.

Private smoke run-TjzxaP passes browser31/native26 with no cleanup errors; native pixels remain skipped. Card state restoration is the separable commit 30f6c91. Its repaired world screenshot was inspected and passes the added pixel assertion (18.52% dominant color versus the failing frame's 97.50%). The production geometry gallery was refreshed in both cameras, grayscale walking and common scale without changing its reviewed geometry fixture. The four-zone population audit retains every pattern's count; the eastern site changes from Fallen Hall to Ossuary, and origin cull/scout offers now meet local/level constraints. Climate remains unchanged. All evidence is under `experiments/entity-review/evidence/37-*`.

This functional checkpoint remains draft pending the coordinated kin performance comparison and Claude review. #20's separate performance findings are not represented as resolved by these entity gates.

## Verification and handoff

The coordinated comparison at6a65ed1 is retained in `experiments/entity-review/evidence/37-benchmark-review.md` with every raw first sample, exact content/source identities, binary hashes and Normal-priority metadata. Candidate named numeric budgets pass: p95 11.2207ms, behavior1.725977ms, snapshot0.9532005ms, full29.295835KB and delta8.889854KB. The same-binary baseline snapshot fails at1.0848485ms. This is not proof of a stable speedup. The nonzero process-wide expression allocation reading, passing separate current-thread zero-allocation regression, interface snapshot timing and narrow timing/traffic headroom are all explicit.

Claude identified a separate #31 zone-planning cost: local population search generates many chunks synchronously when a slot finds nothing. This still blocks the stack; its bounded-search correction belongs in #31 and will propagate bottom-up. #37 remains draft pending that integration and Claude rereview. Its rendered-world defect and functional gates are resolved on the recorded tree; #20's independent release-policy work remains separate.

Acknowledged by: pending Claude rereview and owning-PR records.
