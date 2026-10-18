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
- Release benchmark: zone p95 11.007 ms, average 5.797 ms, 74.517 KB/tick; behavior 2.040 ms / 23 KB; snapshot build 1.074 ms, full/delta 27.234/8.738 KB; world chunk 0.909 ms /14.531 KB; zero system errors. That earlier sample exceeds the 1 ms snapshot.build budget; the previous all-budgets-passing description was incorrect. Other named budgets passed. Retain the sample and measure the integrated tree separately.

The normal suite is not projectile acceptance. Run `node experiments/kobolds/test-projectiles.mjs` for the separate combined-stack gate. It makes 84 real casts, testing both Firebolt and Piercing Arrow against every authored Kobold on flat and ±0.5 m target terrain. Baseline 74 pass and 10 fail: both presets miss the five shortest bodies when downhill. The runner exits 1, with no expected-miss assertion or skipped case. #36's owner confirmed that the fix depends on its shared contact/aim work and must not be cherry-picked alone into #38. The published combined stack has now been tested successfully, as recorded below; #36 remains a separate dependency.

At this independent checkpoint, the #31–34 owner was still preparing the corrected base, including canonical mass, bounded wall-safe shove, site-born levels, quest reachability and shared `close_melee`. The old 15.219 m shove is defect evidence, not accepted behavior. The later integration section records its corrected measurements and behavior rebinding. Claude rereview is requested for the independent changes now and the integrated evidence when available. Do not merge while these dependencies remain open.

## Published projectile correction acceptance

Temporarily merged #36 commit 724c5ed57d7a6082f9c485f66b8dd7001acbe2fb into clean Kobold commit 38dce3400e284b938281c64f94224db7721e2c0b and ran the committed acceptance runner. All 84 real casts passed: both presets against all fourteen unchanged bodies on flat and ±0.5 m terrain. This resolves all ten recorded downhill misses. Full output is experiments/kobolds/evidence/review-gates/projectiles-724c5ed.txt.

The temporary merge was aborted successfully. No #36 runtime commit is absorbed by #38. This is acceptance of those exact combined inputs, not an assertion that the standalone #34-based branch contains the correction. Repeat this gate after the corrected entity foundation changes those inputs. #36's own review and #20 release-policy integration remain separate prerequisites.

## Corrected foundation integration ownership

Consuming published #34 at 26c50de03c62f434c517564906a54c0287b281ce, including full #36 at 724c5ed. This pass resolves the shared generation-golden overlap using population evidence, switches dedicated Kobold bindings to close_melee, and strengthens Kobold mass, occupied-site birth/respawn and actual-projectile regressions. The projectile matrix becomes part of the normal core suite now that the required runtime is present. No competing foundation implementation is introduced.

## Integrated behavior and functional gates

Published base 26c50de is now consumed in this branch, including the full #36 correction. Melee roles bind the common close_melee definition; the duplicate Kobold behavior file is removed. Actual projectile tests moved into the normal core test assembly, and the focused runner independently passes all 84 cases. Damage contact remains the explicit cylinder fallback; this pass does not claim authored compound anatomy.

Canonical mass stays 19.349 kg for Scavenger and 148.850 kg for Ironback. Requested 4 m knockback now travels 11.956 m and 1.175 m, with final scale 3.000 and 0.302. The light case is bounded by 4 × the authored cap of 3; this is a multiplier ceiling, not a universal three-metre travel limit. Species tests also prove the small body stops at placed and terrain walls, emits collision and loses its impulse.

The home-region Hold has a living Tunnelguard at site level 7 both initially and after a real death/respawn. Natural Hold, Scrap, Cinder and Reed residents use levels 35, 27, 26 and 16 respectively, matching their sites rather than their pattern starting levels. Exact-group low/high eligibility, reciprocal faction combat and two-window sustained AI tests pass.

World audit compares an exact archived content snapshot of 26c50de against this branch with seed aureth. All four reference-zone chunks, populations and quests match that base. Only nearby site choices change at the reference zones: the northern Chainkeeper Camp becomes Copper-Crest Hold, and the named neighboring Scrap Fort/Knot choices become ruins or a shrine. The compact before/after ledger is experiments/kobolds/evidence/world-integrated-summary.json; prior world evidence remains labeled with its old inputs. Shared quest regression confirms locally available, level-qualified targets, retaining the origin's Hob cull and level-three ruin scout.

Audited hashes: origin info 033B8E3528AB9828 -> D2F6FA64DD42E7B7; eastern info 767BE9395B9E34DB -> 33C9B160B66B9604; overlay 6FD0F6CF80B497A4 -> AC3C2BFA30192BBC. All four baseline chunk hashes, the other two zone-info hashes and climate remain unchanged. Spawn-level hashing from the foundation is preserved.

Evidence is under experiments/kobolds/evidence/integration/:

- Solution build: zero warnings/errors. Normal tests: core 2249, server 48, native client 124, all passing. This includes the 84 projectile cases and all updated generation goldens.
- Browser: 281 tests / 35 files pass; production build passes with its existing bundle-size advisory.
- Validation: zero errors/warnings. Load: 100 chunks, 115 spawners, 2686 entities, 281 monsters, 200 ticks. Tunnelguard/Loosed duel resolves with mutual damage. Generated protocol current. Server loopback coverage is included above.
- Separate measurement run: 6/6 live mass/site tests; focused projectile runner 84/84.
- Private smoke run-WQ1ujF: browser 30/30 and native 26/26, cleanupErrors empty. Native pixels explicitly skipped. Existing native shutdown allocation warnings are retained in the log.

Visual inspection is not fully passing: ordinary isometric world drawing works, but switching to cards leaves sky and UI with no world. The failing screenshot is retained as integration/card-world-missing.png. The foundation owner independently identified the shared card-bake viewport bug and is fixing it in #37; this pass does not duplicate that renderer edit. Behavioral smoke passing does not close this visual defect.

Performance is pending the coordinator's assigned quiet window. No benchmark was started while other owners' comparison runs were reserved. Claude rereview, shared dependency approval and the shared visual fix remain open; do not merge this checkpoint.

## Shared card-renderer correction consumed

Cherry-picked the owner-published standalone 30f6c91 as 4d482d5, preserving attribution. It has no Gnoll/Beastkin content dependency. This consumes the shared renderer fix instead of adding a second implementation. The imported review record documents viewport/scissor isolation, physical-pixel atlas extents and renderer restoration on success/failure.

On this Kobold tree, all 285 browser tests / 36 files and the production build pass. Fresh private browser run-lWMcHU passes 31/31, including the new nonblank-world assertion, with cleanupErrors empty. The card screenshot's dominant color fraction is 18.55% across 172 color bins; direct inspection confirms terrain and bodies. Retain both integration/card-world-missing.png and integration/card-world-restored.png. This closes the observed browser blank-world defect; it is not a general art-quality or native-pixel certification.

The change only touches browser rendering/tests and evidence. The previous 2249 core / 48 server / 124 native-client tests and native26/26 smoke still apply to the unchanged simulation/native tree; no redundant native run is claimed. A fresh Release Tools build is clean. Performance still awaits the coordinator's explicit window. Shared reviews and #20 release-policy integration remain open.

## Controlled final performance evidence

Measured exact source 253baa24f2f135d03497b429631aa0c59734f9cb in the coordinator's explicit quiet window, 2026-10-18 18:39:51–18:40:00 UTC. The Release build was prepared beforehand. Ran the default full benchmark once, then the authored hit-bodies profile once; no retries or discarded samples. Both processes used Normal priority, exited zero and were disposed before the next owner's window. Raw JSON, source/priority/process metadata and budget comparisons are integration/bench-*.json.

All timing ceilings and nonzero memory/payload budgets pass:

| Measure | Result | Budget |
| --- | ---: | ---: |
| Zone p95 | 9.8209 ms/tick | 25 ms |
| Zone allocation | 75.7974 KB/tick | 2048 KB |
| Behavior | 1.684334 ms/tick; 22.8 KB/tick | 3 ms; 1024 KB |
| Snapshot build | 0.890812 ms/player | 1 ms |
| Full snapshot | 29.295835 KB/player | 30 KB |
| Delta snapshot | 8.889854 KB/player | 10 KB |
| World chunk | 0.525319 ms; 14.3352 KB | 4 ms; 400 KB |
| Damage fold | 442.6085 ns; 0.1392 B | 500 ns; 200 B |
| Expression evaluation | 119.1071 ns | 150 ns |

Full snapshots retain only 0.704165 KB/player of headroom. The process-wide expression allocation counter reports 0.0168 B/eval, exactly matching both the corrected foundation and original #36 in the [independent baseline](https://github.com/arianna-arpg/hollow-wake-mmo/pull/34#issuecomment-6253489076). This does not satisfy a literal zero reading; the value and zero budget are retained without rounding away the discrepancy or changing the contract.

Authored-profile deliveries measure 1.7543845 ms/tick and 37.5926 KB/tick, including 0.87639575 ms/tick in projectiles, across 42 profiled patterns, 120 foes and 41 average flights. This is the common authored-profile workload, not a dedicated Kobold crowd or a separate body-area performance bound. The default fixed mixed roster likewise remains a shared-runtime check; the species AI and 84 cylinder-fallback contact cases provide Kobold functional coverage. Zero zone system errors.

Requested code corrections and local evidence are published. Claude rereview and shared dependency/#20 release-policy approval remain open; #20's separate performance failures are not cleared by these measurements. Keep #38 unmerged until those gates close. The older snapshot over-budget result, failed card screenshot and baseline projectile misses remain historical evidence.

Acknowledged by: pending Claude rereview.

## Subsequent explicit level-policy correction

The current follow-up is docs/reviews/2026-10-18-kobold-level-policy-followup.md. It restores original pattern-level defaults and bounds quest population searches. Earlier unconditional site-level birth/respawn claims, world goldens and performance values in this record describe their named historical heads; they are superseded for the current branch. The original logs are retained.

## Subsequent main foundation integration

The next source integration and its current functional evidence are recorded in docs/reviews/2026-10-18-kobold-main-integration.md. It consumes complete validated #34 at 3d339d2, including the new main runtime and audits. All timings in this earlier record remain historical for their named pre-main source. No post-main performance clearance is implied.

## Subsequent unresolved-spacing correction

The 32 m spacing was not accepted by Claude's rereview. The correction and its first failing/passing uniqueness evidence are now recorded in docs/reviews/2026-10-18-kobold-spacing.md: all four sites use the engine's 48 m default floor and the natural habitat fixtures again assert Single. Prior successful gates did not close that authored-content defect. Original 32 m reports remain historical evidence.
