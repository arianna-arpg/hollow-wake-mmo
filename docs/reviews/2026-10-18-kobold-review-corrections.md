# Kobold review corrections

Response to [Claude's review of PR #38](https://github.com/arianna-arpg/hollow-wake-mmo/pull/38#issuecomment-6245774290) at d17ea28. Work remains on `codex/kobold-scaleclans` in its own worktree.

## Ownership before edits

This pass owns the dedicated Kobold content, appearance builders, tests, gallery and evidence. Additive shared edits are planned in `GearFamilies.cs`, `gearFamilies.ts` and `AppearanceKinds.cs` for reusable carried equipment, and in the appearance/entity contracts and generation goldens. These files overlap the entity stack; no other owning worktree is edited. Native/browser equipment changes will be paired and the shared fixture regenerated only after visual inspection.

The #31–34 owner is correcting mass/shove, common melee behavior and site/quest foundations. This pass will consume the corrected base and add Kobold acceptance tests, without a second runtime implementation. The #36 owner owns projectile flight/hit coverage; Kobold heights remain unchanged and actual projectile tests must cover flat ground and half-unit relief.

## Review work

- Fix Loosed reciprocity and test every faction plus actual mutual acquisition/damage.
- Keep the Hold occupied at region level one with a bounded fallback window; verify generated and live residents, not only absent bosses.
- Reduce small-site exclusion to a footprint-appropriate distance and state the actual weighted ruin-share effect.
- Move carried role equipment from species branches into reusable gear families and content entries; use a real off-hand blade for dual wielding.
- Consume the shared close-melee behavior and bounded physicality rules, with explicit measurements.
- Name scout-target, stamp and neighboring-zone changes, coordinate goldens, site-level versus resident-level behavior and current weave reach.
- Run relevant gates and publish for Claude rereview. Do not merge while base/review blockers remain.

## Independent corrections verified over d17ea28

- Added explicit hostile reciprocity with the Loosed. The test enumerates every shipped faction from a fresh registry; a separate native-faction fight proves both sides acquire and damage each other with no injected target/player.
- The Hold's Tunnelguard fallback occupies site levels 6–9. Loading the region-level-one site and ticking its real spawners produces a living guard at site level 7. Authored level gates now assert presence as well as absence, select each exact site group and replay; high-level sites still have at most four Kobold residents.
- Camp minimum spacing is 32 m for the 10 m radius footprint, down from 160 m. The weighted ruin-share increase is now stated honestly. Current content-only generation correction changes the origin's nearby Scrap Fort at (-234,294) to a generic ruin; origin chunks and quests stay unchanged. Named deltas and natural-site coordinates are in the world evidence, alongside the earlier Chainkeeper-to-Scrapscale scout change and Iron-Tusk-to-Chainkeeper stamp change.
- Carried supplies, armor and the crown now use reusable field-kit gear on chest/belt/head sockets. The Knifehand reuses the existing off-hand dagger. Geometry comparison preserves every old part except that single decorative blade, replaced by the existing six-part dagger. Cross-family tests cover ordinary bipeds, skeletons and scaled bipeds, animated attachments and doubling the body size. Updated camera/grayscale galleries were inspected.
- Weave reach remains an existing-skill behavior; the Tunnelguard's 3.75 m Buckler Strike is explicitly recorded. No body-height inflation or second runtime fix is added.

## Gates and remaining dependencies

Evidence: `experiments/kobolds/evidence/review-gates/`.

- Solution build: 0 errors, no new warnings (the three base xUnit2031 warnings remain until main is incorporated).
- Normal solution suite: 738 core + 39 server + 119 client = 896 passing tests. After the final crown/socket extraction, the four native Kobold appearance tests and 17 browser fixture/reuse cases pass again.
- Browser: 271 tests / 34 files pass; production build passes with the inherited bundle advisory. Content validation: 0 errors, 0 warnings. Protocol check current.
- Load: 200 ticks, 2686 entities, 281 monsters. Tunnelguard/Loosed fight records damage in both directions and a resolved kill. Native-faction AI regression is the authoritative relation proof; the CLI duel alone is not.
- Private smoke: browser 29/29 and native 25/25, cleanupErrors empty. Native pixel checks remain explicitly skipped; the final crown extraction preserves geometry and has paired fixture checks.
- Release benchmark: zone p95 11.007 ms, average 5.797 ms, 74.517 KB/tick; behavior 2.040 ms / 23 KB; snapshot build 1.074 ms, full/delta 27.234/8.738 KB; world chunk 0.909 ms /14.531 KB; zero system errors. Relevant budgets pass on the existing mixed benchmark.

The normal suite is not projectile acceptance. Run `node experiments/kobolds/test-projectiles.mjs` for the separate combined-stack gate. It makes 84 real casts, testing both Firebolt and Piercing Arrow against every authored Kobold on flat and ±0.5 m target terrain. Baseline 74 pass and 10 fail: both presets miss the five shortest bodies when downhill. The runner exits 1, with no expected-miss assertion or skipped case. #36's owner confirmed that the fix depends on its shared contact/aim work and must not be cherry-picked alone into #38. The published combined stack has now been tested successfully, as recorded below; #36 remains a separate dependency.

The #31–34 owner is still preparing the corrected base, including canonical mass, bounded wall-safe shove, site-born levels, quest reachability and shared `close_melee`. The old 15.219 m shove is defect evidence, not accepted behavior; revised measurements and the behavior rebinding follow that base. Claude rereview is requested for the independent changes now and the integrated evidence when available. Do not merge while these dependencies remain open.

## Published projectile correction acceptance

Temporarily merged #36 commit 724c5ed57d7a6082f9c485f66b8dd7001acbe2fb into clean Kobold commit 38dce3400e284b938281c64f94224db7721e2c0b and ran the committed acceptance runner. All 84 real casts passed: both presets against all fourteen unchanged bodies on flat and ±0.5 m terrain. This resolves all ten recorded downhill misses. Full output is experiments/kobolds/evidence/review-gates/projectiles-724c5ed.txt.

The temporary merge was aborted successfully. No #36 runtime commit is absorbed by #38. This is acceptance of those exact combined inputs, not an assertion that the standalone #34-based branch contains the correction. Repeat this gate after the corrected entity foundation changes those inputs. #36's own review and #20 release-policy integration remain separate prerequisites.

Acknowledged by: pending Claude rereview.
