# Knot inhabitants follow their site's level

The owner decision relayed by Claude in [#31 comment6255074492](https://github.com/arianna-arpg/hollow-wake-mmo/pull/31#issuecomment-6255074492) supersedes the earlier authored-level preservation in the starter restoration record. All three Knot inhabitants now explicitly opt into their site's level; other sites retain their existing policies. The foe slot retains `population_at: $knot` and restores its `level_delta.max: 2`. Search radius160 and the content-owned16chunk cap are unchanged. The quest observes the destination's population; it does not mutate residents for a giver.

Real initial-birth and respawn regressions cover Cinder Imp, Remnant Shade and Tangle at Hob's Hollow, all level3. Unscaled hamlet guards and ruin shades remain covered separately. Global pattern levels are unchanged. The first build exposed two missing test namespace imports; the first focused test then exposed an obsolete unscaled-shade example that lived only at Knots. Imports were corrected and the example moved to an actual non-Knot ruin. All initial diagnostics are retained. Generation goldens change only after exact world/map audit.

## #31 propagation from `0107faca0ce709b712fb877824bf649571fbc3d1`

The before maps and population audit use that exact committed source. After maps/audits use the reviewed working-tree content captured by this commit; subsequent gate evidence identifies its source commit. Sites, per-pattern counts and terrain maps remain identical; only Knot spawn levels differ. The JSON carries every site and effective/explicit level, not only changed examples. Quest offers are unchanged in all four zones relative to the already restored anchored offers.

| Zone | Site | Pattern | Bodies | Old level | New site level |
|---|---|---|---:|---:|---:|
| 0_0 | poi.0_0.knot.200.60 | cinder_imp | 4 | 4 | 3 |
| 0_0 | poi.0_0.knot.200.60 | remnant_shade | 4 | 2 | 3 |
| 0_0 | poi.0_0.knot.200.60 | tangle | 1 | 6 | 3 |
| 1_0 | poi.1_0.knot.431.198 | cinder_imp | 6 | 4 | 3 |
| 1_0 | poi.1_0.knot.431.198 | remnant_shade | 3 | 2 | 3 |
| 1_0 | poi.1_0.knot.431.198 | tangle | 1 | 6 | 3 |
| 0_-1 | poi.0_-1.knot.176.-74 | cinder_imp | 5 | 4 | 7 |
| 0_-1 | poi.0_-1.knot.185.-214 | cinder_imp | 6 | 4 | 7 |
| 0_-1 | poi.0_-1.knot.41.-37 | cinder_imp | 4 | 4 | 7 |
| 0_-1 | poi.0_-1.knot.176.-74 | remnant_shade | 4 | 2 | 7 |
| 0_-1 | poi.0_-1.knot.185.-214 | remnant_shade | 2 | 2 | 7 |
| 0_-1 | poi.0_-1.knot.41.-37 | remnant_shade | 4 | 2 | 7 |
| 0_-1 | poi.0_-1.knot.176.-74 | tangle | 1 | 6 | 7 |
| 0_-1 | poi.0_-1.knot.185.-214 | tangle | 1 | 6 | 7 |
| 0_-1 | poi.0_-1.knot.41.-37 | tangle | 1 | 6 | 7 |
| 3_2 | poi.3_2.knot.965.744 | cinder_imp | 5 | 4 | 3 |
| 3_2 | poi.3_2.knot.965.744 | remnant_shade | 3 | 2 | 3 |
| 3_2 | poi.3_2.knot.965.744 | tangle | 1 | 6 | 3 |

All changed rows previously had null explicit level; they now store the site level. Exact before/after maps, quests and population JSON are under `31-sitelevel-*`. Golden replacements: `{"60C8EBC63D00ECC7": "3ABA13943256C927", "FF2F6E711BC363CE": "99C1C0903D1A255E", "20BCBB252116A13A": "18A63543AC11297A", "7631657EC3C1D2E1": "6D9AEE576A168431", "0C972378F3EEFF45": "657770B629C24C11"}`. Climate and zone-info hashes are unchanged in this narrow policy delta. Ninewells continues to offer Clear the Knot targeting its actual Cinder Imps; the earlier restoration audit names the contrast with the omitted-offer branch and actual main.

### #31 final functional verification

Source `3a44b85aea3e00d0bf00a310fb82cf8d759d48f1` over previous `0107faca0ce709b712fb877824bf649571fbc3d1`, consuming complete parent `17ca2f13a4c193d4b65d99ca4545aae267581092`. Solution and Release builds: zero warnings/errors; core 830, server 53, client 128 pass. Validation0/0, load/duel and seven Release Windows replay hashes pass without changing the combat fixture. The exact 497-key grammar census is unchanged. Full proving JSON equals the preceding same-branch post54 report, including its four inherited win-ceiling failures; the balance gate remains failed. Only the Knot site-level policy and foe-window content differ, exactly matching `3a44b85`; pattern combat stats, other population policies and geometry are unchanged. No timing run or main merge. Claude rereview requested.

Acknowledged by: [Claude rereview of #31 at 749e99b](https://github.com/arianna-arpg/hollow-wake-mmo/pull/31#issuecomment-6255391549) accepts the Knot implementation; the contract follow-up and paired timing remain pending final review.

## #32 propagation from `e65a7b6e61e8b13bb8705f3442c3e95903b7299a`

The before maps and population audit use that exact committed source. After maps/audits use the reviewed working-tree content captured by this commit; subsequent gate evidence identifies its source commit. Sites, per-pattern counts and terrain maps remain identical; only Knot spawn levels differ. The JSON carries every site and effective/explicit level, not only changed examples. Quest offers are unchanged in all four zones relative to the already restored anchored offers.

| Zone | Site | Pattern | Bodies | Old level | New site level |
|---|---|---|---:|---:|---:|
| 0_0 | poi.0_0.knot.200.60 | cinder_imp | 4 | 4 | 3 |
| 0_0 | poi.0_0.knot.200.60 | remnant_shade | 4 | 2 | 3 |
| 0_0 | poi.0_0.knot.200.60 | tangle | 1 | 6 | 3 |
| 1_0 | poi.1_0.knot.431.198 | cinder_imp | 6 | 4 | 3 |
| 1_0 | poi.1_0.knot.431.198 | remnant_shade | 3 | 2 | 3 |
| 1_0 | poi.1_0.knot.431.198 | tangle | 1 | 6 | 3 |
| 0_-1 | poi.0_-1.knot.176.-74 | cinder_imp | 5 | 4 | 7 |
| 0_-1 | poi.0_-1.knot.185.-214 | cinder_imp | 6 | 4 | 7 |
| 0_-1 | poi.0_-1.knot.41.-37 | cinder_imp | 4 | 4 | 7 |
| 0_-1 | poi.0_-1.knot.176.-74 | remnant_shade | 4 | 2 | 7 |
| 0_-1 | poi.0_-1.knot.185.-214 | remnant_shade | 2 | 2 | 7 |
| 0_-1 | poi.0_-1.knot.41.-37 | remnant_shade | 4 | 2 | 7 |
| 0_-1 | poi.0_-1.knot.176.-74 | tangle | 1 | 6 | 7 |
| 0_-1 | poi.0_-1.knot.185.-214 | tangle | 1 | 6 | 7 |
| 0_-1 | poi.0_-1.knot.41.-37 | tangle | 1 | 6 | 7 |
| 3_2 | poi.3_2.knot.965.744 | cinder_imp | 5 | 4 | 3 |
| 3_2 | poi.3_2.knot.965.744 | remnant_shade | 3 | 2 | 3 |
| 3_2 | poi.3_2.knot.965.744 | tangle | 1 | 6 | 3 |

All changed rows previously had null explicit level; they now store the site level. Exact before/after maps, quests and population JSON are under `32-sitelevel-*`. Golden replacements: `{"A93A852778DA4EDC": "B7AF6323F6B6FF7C", "427D59987E52B280": "09ED05825A4C6B50", "C54EA309DEC80391": "04FB8C3BEEEC74A9", "6401AD38AFEB12E9": "FE239499AF4B1519", "E0B1CC2C04A28F7C": "72299E8C3D124A24"}`. Climate and zone-info hashes are unchanged in this narrow policy delta. Ninewells continues to offer Clear the Knot targeting its actual Cinder Imps; the earlier restoration audit names the contrast with the omitted-offer branch and actual main.

### #32 final functional verification

Source `eb506a717169600dd635488f7381535a34d80aa1` over previous `e65a7b6e61e8b13bb8705f3442c3e95903b7299a`, consuming complete parent `749e99b83d07521050184ebac64132728ab7acb8`. Solution and Release builds: zero warnings/errors; core 878, server 53, client 129 pass. Validation0/0, load/duel and seven Release Windows replay hashes pass without changing the combat fixture. The exact 499-key grammar census is unchanged. Full proving JSON equals the preceding same-branch post54 report, including its four inherited win-ceiling failures; the balance gate remains failed. Only the Knot site-level policy and foe-window content differ, exactly matching `3a44b85`; pattern combat stats, other population policies and geometry are unchanged. No timing run or main merge. Claude rereview requested.
