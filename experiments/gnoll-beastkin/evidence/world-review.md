# Historical world output review

This original capture predates the reviewed integration. Use [the actual fauna-base comparison](../../entity-review/evidence/37-base-world-review.md) and its source-matched maps for current PR review. The original comparison below is retained only as history.

# World output review

Compared the same four seed-aureth maps against base #34 before refreshing goldens. Zone (1,0) selects different existing ruin-budget sites and their terrain stamps: ossuary/hold/ossuary/grove becomes muster/knot/ruin. The new weighted candidates and spacing change that deterministic selection; underlying climate is unchanged. Zone (0,0) changes its nearby scout quest destination. The other sampled terrain maps match. Zone information hashes also cover wider scout destinations, so unchanged visible maps can have different info hashes. The overlay hash changes with its site/quest selection.

Only expected info, chunk and overlay hashes changed; climate golden remains unchanged. Forced low/high site tests exercise new champions and deterministic inhabitants even when these particular sampled zones do not select a new camp. No changes to terrain algorithms, POI budgets or spawn/runtime logic.
