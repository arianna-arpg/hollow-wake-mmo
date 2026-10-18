# Kobold spacing correction

## Ownership and unresolved review

This pass addresses Claude's still-open spacing request at https://github.com/arianna-arpg/hollow-wake-mmo/pull/38#issuecomment-6253420104 . Current published source is 3afa3598539035706b5dacf0563f31222b630083, with complete #34 base 3d339d2ac71af6ac8bcff8e4f767e1f7c44ce2d8. All four sites still explicitly use 32 m, below PoiDef's 48 m default. The earlier documentation of 32 m was not reviewer acceptance. There is no cited owner decision authorizing the duplicate-site behavior.

PR #38 owns the four site definitions, Kobold uniqueness assertions, its audit helper, the Kobold entity-contract paragraph and any audited world-golden refresh. This notice precedes those edits. Restore Single at the natural habitat fixtures, assert the authored spacing floor for every site, and retain the failing checks on the old content before raising spacing to the engine default. Do not weaken uniqueness to make the new output pass.

Retain complete before/after world reports and named site/quest-target details, plus exact base-content output on the same generator. The only simulation-core difference from #34 is two appearance-vocabulary registrations; the complete generation/runtime source is identical. Exact base content will be archived from 3d339d2 rather than copied from a different species. Compare current 32 m -> corrected spacing and exact base -> corrected head separately, including populations, stamps, nearby choices and scout target region.

Prior functional passes do not close this defect. No timing measurement will run; prior failures and source distinctions remain intact.

## Correction and failing baseline

All four authored min_spacing values change from 32 to 48, exactly the current PoiDef default. The natural-site regression restores Assert.Single for each of the four original habitat fixtures. The authored-role regression additionally requires every definition to retain at least the engine default separation. No coordinates, role windows, rarity, body dimensions, skills, aggro ranges or simulation implementation change.

Before editing content, the restored tests fail 5/8 cases: all four spacing floors and the Reedspine Single assertion, with the other three natural fixtures passing. The complete first failure is retained in uniqueness-before.txt. After the four content edits, the first focused run passes 36/36 cases, including all existing world goldens. No golden value or hash format is changed.

## Actual 32 m to 48 m world changes

The optional detailed audit produces the same original-format baseline as the previously verified 3afa359 world report, field for field. Sixteen paired cases cover eight zones with and without overlays. Fourteen cases remain wholly unchanged. Only zone (-7,-23), with and without overlays, changes its sites, stamped geometry and population; every quest list across all sixteen cases is unchanged.

Before: Reedspine Camps at (-1751.5,-5754.5) and (-1728.5,-5729.5) are 33.9705755 m apart. After: the first remains, and the second camp is absent. The planner instead places the Quiet Stone shrine at (-1598.5,-5756.5). The removed camp_ring stamp belongs to poi.-7_-23.kobold_reed_camp.-1728.-5729; the added shrine_ring belongs to poi.-7_-23.shrine.-1598.-5756. This is the retained first generated outcome, not a seed or fixture replacement.

That zone's spearguard, venomweaver and rimeweaver counts each change from two to one, leaving the intended three residents in one Reedspine Camp. Shrine-stone bodies change from one to two. Remaining population groups and effective resident levels are unchanged. The live Single fixture proves one populated camp at its original coordinate. Uniqueness assertions apply to these deterministic habitat fixtures; authored name pools remain unchanged.

## Exact base versus corrected branch

base.json and base-sites.json use content archived from exact #34 3d339d2 on the identical generation/simulation implementation. The two presentation-vocabulary additions are the only Core source difference. changes.json separates that base comparison from the spacing-only comparison, so earlier Kobold population effects are not attributed to the 48 m fix.

All four standard reference zones retain base geometry and local population. Their nearby choices include Scrap Fort at (-234.5,294.5) -> the Old Seam ruin, and Chainkeeper Camp at (125.5,310.5) -> Copper-Crest Hold. Eastern nearby forts/knots become the named Fallen Hall, Barrowgate, Last Stitch and Stitch Shrine; the complete named list is retained. The four natural Kobold habitat zones retain the expected base-to-species changes, including displaced existing sites, all per-pattern/site counts and landmark identities.

The ordinary origin scout remains poi.0_1.ruin.213.450, the Sunken Lantern at (213.5,450.5), zone (0,1), region (0,0), the Hallows, site level 2. In the overlay comparison only, exact base targets poi.1_0.ossuary.421.38, the Hollow Reliquary at (421.5,38.5), zone (1,0), the same Hallows region, site level 3; the Kobold branch targets the Sunken Lantern. This base-to-Kobold difference already exists before the spacing fix. Both comparisons preserve Gather Floss and the corresponding Hob/Grove Stag cull. Detailed target IDs and regions are in each *-sites.json report.

## Verification and handoff

Fresh solution build: zero warnings/errors. Full solution suite: core 2402, server 59, native client logic 137 pass, including all 84 projectile cases and restored uniqueness checks. Validation: zero errors/warnings. Origin load: 100 chunks, 200 ticks, 2686 entities, 281 monsters, three quests. Tunnelguard/Loosed duel records damage both ways and a defeated Brigand. Release Windows replay reports all seven hashes identical using the unchanged tested Release runtime with corrected content. No golden or replay fixture changed.

This is a site-content/test/audit correction. Client and protocol source are unchanged; their prior 3afa359 browser 312, smoke 31/36 and six-feature probe evidence is retained without an unchanged-source repeat. The prior proving-band failures and post-main timing restrictions remain open. No timing run or budget change occurred.

Claude rereview is requested specifically on the corrected spacing, restored Single assertions and retained failing baseline. Green checks on the former 32 m head were not evidence that this defect was closed. Downstream integration should consume the published corrected branch; the next complete validated #34 propagation remains separate.

Acknowledged by: pending Claude rereview.
