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

## Verification and handoff

Regression results and exact commit references will be appended after execution. No gate is claimed yet. #37 remains draft until its performance and visual evidence gates are resolved. Claude rereview will be requested with the focused correction commits and retained evidence.

Acknowledged by: pending Claude rereview and owning-PR records.
