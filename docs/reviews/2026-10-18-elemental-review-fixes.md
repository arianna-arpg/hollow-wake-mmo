# Elemental review corrections

Historical checkpoint through `9f04932`. The later shared bounded-search and explicit-level integration is recorded in [the follow-up review](2026-10-18-elemental-bounded-search-integration.md). Its restored resident levels and changed quest offers supersede the level-dependent claims and performance clearance below. Original measurements remain intact.

Scope claimed before edits for PR #40, responding to Claude's review of `b29e67c` at https://github.com/arianna-arpg/hollow-wake-mmo/pull/40#issuecomment-6246008360.

Current integration checkpoint: #40 consumes the complete corrected #34 `26c50de03c62f434c517564906a54c0287b281ce` through merge `2c7c611`. That base includes corrected #31-33 and full #36 `724c5ed`. Elemental-only acceptance adjustments are `f3b491a`; reviewed generation snapshots are `2b485e1`. Full Release CI passes Core 2297, client 121 and server 48, including all 160 elemental cases. Shared card-renderer fix `30f6c91` from #37 is consumed as `1528f2c`; browser 284 tests, its build and a fresh pixel-aware browser smoke 31/31 pass. Native state checks pass 26/26. Controlled first-sample performance meets the published time/size caps; the unchanged nonzero expression-allocation counter is recorded below. This PR remains draft pending shared-stack integration and Claude rereview.

This pass owns elemental content, its paired builders, tests and evidence. Shared edits are limited to an additive manifestation budget in `content/core/world/regions.yaml`, the entity/appearance contracts, and reviewed generation goldens. The owning entity chat has been notified before editing those integration points. PRs #31-34 own mass/shove and placement corrections; #36 and #20 own the common projectile/contact/forecast solution. This pass will consume their published fixes and will not duplicate shared runtime changes or increase creature heights to conceal missed projectiles.

Content corrections implemented: reciprocal native faction relations, including explicit hostility to the Loosed; `kind.elemental` for creatures and `site.manifestation` for places; bare Thread ids for affinities; and removal of repeated provisional AI metadata. `elemental` remains the existing damage-category word. The golem owner confirmed that #39 leaves the shared `warden_slam` preset unchanged, so Geode Shellback retains that supported kit.

Sites now have zero level offset and retain their resident minimum levels. They use dedicated shallow, walkable natural stamps with no chest, shrine stone, camp palisade or extra spawns. The ordinary discoverable/interactable POI marker remains. Ruin and shrine quest filters cannot select manifestations. Each kind has several names and a 48 m minimum spacing, which the common planner applies against all neighbors. The dedicated 0.12 `site.manifestation` entry is still a per-candidate multiplier, not a reserved pool; it also adds 0.12 to each region's total desired site count. Other budget values are unchanged. The world audit records displaced neighbors and per-pattern spawner/body counts rather than claiming the populations are additive or unchanged.

Existing silhouettes and nominal dimensions are retained. The unused `torn` flag is removed, and a single prism now hangs from the central head bone rather than a left arm; both clients and the shared fixture changed together. Palette slots use presentation-role names. Refreshed camera/grayscale images retain the common-scale lineup; separate actual casting poses show the centered Lumen and Mind cores. Bounds evidence now includes measured rest-geometry min/max in addition to the existing conservative plan height and authoritative stature.

Previous logs and review records describe the original head, including its defects. Evidence below is labeled by revision and actual mechanism tested. A direct damage-effect test proves mitigation, not projectile contact. The later shared-base sections record the passing shove and projectile regressions.

## Interim evidence on the original shared runtime

This is not the final gate report. `experiments/elementals/evidence/review-fixes/` records the state before consuming the shared fixes:

- 11 new content regressions pass: all native faction stances are reciprocal, the Loosed and an elemental both deal AI damage without faction overrides, site content has no human stamp rewards, and actual generated ruin quests reject nearer manifestations over repeated seeds.
- 81 existing elemental cases pass with the strengthened natural-placement case excluded explicitly. Pinned sites contain residents at their first eligible level and all intended residents at a high level. No hidden landmark-spawn patterns are accepted.
- The new real targeted-bolt matrix covers all bodies on flat ground and +/-0.5 m sloped terrain. It produces actual projectiles through `WeaveRuntime`: 43/48 pass. Cinderling flat/downhill, Emberwisp downhill, Rime Star downhill and Silt Crawler downhill fail, reproducing the shared defect without inflating bodies. The #36 owner has the fixture and owns the common correction.
- The strengthened natural-site test fails because a low-level Bloom site has no eligible inhabitant. This is a required regression awaiting #31-34's generic placement fix, not an accepted empty-site behavior.
- Three real displacement regressions fail on the old shared runtime: Bash moves Emberwisp 47.49866 m and Winter's Sweep moves it 94.08607 m, both beyond its 45 m home leash. A 4-unit shove from x=30 tunnels through a placed wall at x=32 and ends at x=93.34717. These fixtures await the shared ceiling and swept-collision correction; elemental mass and dimensions are unchanged.
- Full browser tests: 270 pass; browser build and native appearance parity pass. Content validation: 0 errors/warnings. Original source-renderer reference remains unchanged. Logs normalize local machine prefixes; historical measurements retain their original values.

At this interim checkpoint the shared base, shove ceiling, natural-site regression, generation review and final gates were still pending. The later sections report their verification against the published corrections.

## Combined contact acceptance

The temporary branch `codex/elemental-shared-review-checks` merges elemental content `b4f558f` with the complete #36 branch at `724c5ed`; its merge is `cb4e321`. It is validation evidence, not the content PR's dependency base. On that combination, `ElementalProjectileTests` passes 48/48 real Ember Bolt cases, including all five old misses, with dimensions unchanged. Another 91 content and sustained-AI cases pass, explicitly excluding the natural-placement and mass-travel cases that still need corrected #34. Logs are `projectile-after-hitbox-724c5ed.txt` and `kits-after-hitbox-724c5ed.txt` under the review-fixes evidence directory.

The coordinator's integration direction is to keep #40 draft and based on corrected #34, then merge main and retarget only after the corrected foundation and reviewed #36 land there. #20 owns its release policy and still requires shared integration review. No competing #36 runtime was added to the elemental diff, and no merge is authorized by this record. Full combined gates and the corrected foundation SHA were still pending at this intermediate checkpoint.

## Combined physics and placement acceptance

Temporary merge `973a536` adds published #32 `a96307e` to the combined contact branch while corrected #34 propagates. The merge retains both the content-driven projectile height and inherited effective faction, plus the fauna quest classifier and the shared level/local-population filters. The content PR does not carry these temporary runtime resolutions.

All 19 displacement cases pass: every body stays at or below the 12 m bound for a 4-unit shove; Emberwisp travels 8.96672 m under Bash and 17.77583 m under Winter's Sweep, and stops at x=31.99990 before the blocked x=32 cell. Canonical mass agrees with the attribute lookup. The first combined run correctly exposed two stale fixture assumptions: capped light bodies can tie in travel, and center/cell collision stops within a millimeter of a boundary. Tests now preserve mass ordering and heavy-body resistance while accepting cap ties, and require the center to remain strictly before the blocked cell. The original failed run remains in `elementals-combined-physics.txt`; the passing 21-case shove/natural subset is `shove-and-natural-combined.txt`.

The generic placement fix rejects empty low-level sites. A separate content issue remained: the unchanged default-seed [-48,48] search found no Ember Seam under the new dedicated budget. Raising only its site weight from 0.22 to 0.8 yields three seams in its original cinderwaste/scrub habitats; the other seven manifestation counts remain 6/87/91/71/13/2/7 in content order. The common budget and other site weights are unchanged. Both counts are retained. This sample preceded consumption of final corrected #34.

## Final functional evidence

The updated snapshots pass full Release CI at `2b485e1` (zero build errors/warnings, validate 0/0, Core 2297/client 121/server 48, headless load and Docker checks). A local focused run passes all eight golden/population cases; an expanded eight-zone population audit also passes. See `ci-solution-final.txt`, `world-verification-final.txt`, `world-population-final.txt` and `world-review-final.txt`.

The four reference zones retain identical local maps, chunk hashes, initial populations and quest text. Their wider neighbor plans change. Explicitly audited neighboring populations also change: (-1,0) 154/398 to 153/399, (-1,1) 165/409 to 169/421, (2,1) 135/372 to 139/378, and (3,1) 133/345 to 134/346 (spawners/initial bodies). Removed camps/courts lose their named combatants; ruins, knots and a shrine alter ordinary residents and props. The report lists chest/shrine and per-pattern deltas. This is not a claim of unchanged or purely additive world populations.

Local validate reports 0/0; a 200-tick load, the elemental duel, current protocol check and ten prototype-port tests pass. The private TCP probe grants hit_bodies_v2, projectile_slabs_v1, entity_delta and interface, receives the anatomy catalog and completes movement/interface checks. The browser build retains its existing chunk-size advisory. Production body-plan parity and the inspected elemental gallery remain unchanged by the shared runtime integration.

Private run `run-6SAVKY` passes browser 30/30 and native 26/26 with no cleanup errors. Native rendering and theme pixels are explicitly skipped; existing native shutdown allocation warnings are retained. The initial browser isometric capture renders terrain and bodies, but `16_cards.png` and the subsequent grayscale frame lose the world while retaining labels/UI. `card-mode-inherited-blank.png` preserves that failure. State-check success does not clear this visual defect. It is reported to the shared rendering owner and coordinator; this content PR does not add a competing renderer patch.

## Shared card correction verified

The owner supplied the isolated shared renderer commit `30f6c91a12815548be2169f666f6507facb8c56d` from #37, consumed with provenance as `1528f2c`. It has no Gnoll/Beastkin content dependency. This PR carries that shared presentation commit until integration puts it on the common base; it does not implement a separate fix. The owner's [card renderer record](2026-10-18-card-renderer-state.md) explains viewport/scissor, DPI and exception-safe state restoration.

Browser tests now pass 284/284 and the build passes. Fresh private browser run `run-X9yJfV` passes 31/31, including the added nonblank-world screenshot assertion (largest color region 18.59%, 172 bins, 55,296 samples). Its card and grayscale frames were visually inspected: terrain and bodies render correctly. `smoke-web-card-fix.txt` and `smoke-result-card-fix.json` retain the exact run. The earlier failure capture remains as before evidence. Native code did not change; the completed 26/26 native state run remains applicable, with native pixels still untested.

## Controlled performance and rereview

Published source `4b7d0aaf035b5ef35de05a4dc44ddd82e5c1b7bb` was measured in the coordinator's quiet window at normal process priority. The default full benchmark and the authored hit-body profile ran serially; both first samples and their process/source metadata are retained. Both exited 0 and all owned processes stopped before the next owner's window.

The default run meets the named time/size limits: p95 tick 9.8238 ms, behavior 1.617983 ms, snapshot build 0.918138 ms, full/delta snapshots 29.295835/8.889854 KB, chunk generation 0.549073 ms and extra attribute fold 353.918 ns. Zone allocation is 75.78699 KB/tick and system errors are zero. The full snapshot has only 0.704165 KB headroom. The expression counter remains 0.0168 bytes/eval, equal to the independently measured shared base/reference; no literal-zero allocation claim is made. The shared 42-profile/120-foe run measures projectiles at 0.990746 ms/tick and total deliveries at 2.006589 ms/tick. These profiles are the shared authored-body workload; elemental-specific acceptance uses the actual-flight matrix against the shipped fallback bodies.

See `performance-review.txt` and raw `bench-*-final.json` / `bench-*-metadata.json`. Claude rereview is requested for the fixes, shared dependencies, population tradeoffs and preserved dimensions. #20 release-policy integration remains separate; this report does not clear its performance or authorize merging the shared stack. Native pixels remain outside headless coverage.

Acknowledged by: pending Claude rereview.
