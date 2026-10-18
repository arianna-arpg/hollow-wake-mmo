# Elemental review corrections

Scope claimed before edits for PR #40, responding to Claude's review of `b29e67c` at https://github.com/arianna-arpg/hollow-wake-mmo/pull/40#issuecomment-6246008360.

This pass owns elemental content, its paired builders, tests and evidence. Shared edits are limited to an additive manifestation budget in `content/core/world/regions.yaml`, the entity/appearance contracts, and reviewed generation goldens. The owning entity chat has been notified before editing those integration points. PRs #31-34 own mass/shove and placement corrections; #36 and #20 own the common projectile/contact/forecast solution. This pass will consume their published fixes and will not duplicate shared runtime changes or increase creature heights to conceal missed projectiles.

Content corrections implemented: reciprocal native faction relations, including explicit hostility to the Loosed; `kind.elemental` for creatures and `site.manifestation` for places; bare Thread ids for affinities; and removal of repeated provisional AI metadata. `elemental` remains the existing damage-category word. The golem owner confirmed that #39 leaves the shared `warden_slam` preset unchanged, so Geode Shellback retains that supported kit.

Sites now have zero level offset and retain their resident minimum levels. They use dedicated shallow, walkable natural stamps with no chest, shrine stone, camp palisade or extra spawns. The ordinary discoverable/interactable POI marker remains. Ruin and shrine quest filters cannot select manifestations. Each kind has several names and a 48 m minimum spacing, which the common planner applies against all neighbors. The dedicated 0.12 `site.manifestation` entry is still a per-candidate multiplier, not a reserved pool; it also adds 0.12 to each region's total desired site count. Other budget values are unchanged. The world audit records displaced neighbors and per-pattern spawner/body counts rather than claiming the populations are additive or unchanged.

Existing silhouettes and nominal dimensions are retained. The unused `torn` flag is removed, and a single prism now hangs from the central head bone rather than a left arm; both clients and the shared fixture changed together. Palette slots use presentation-role names. Refreshed camera/grayscale images retain the common-scale lineup; separate actual casting poses show the centered Lumen and Mind cores. Bounds evidence now includes measured rest-geometry min/max in addition to the existing conservative plan height and authoritative stature.

Previous logs and review records describe the original head, including its defects. New evidence will be labeled by revision and actual mechanism tested. A direct damage-effect test proves mitigation, not projectile contact. Shared shove bounds and projectile coverage remain open until the owning base lands and these regressions pass against it.

## Interim evidence on the original shared runtime

This is not the final gate report. `experiments/elementals/evidence/review-fixes/` records the state before consuming the shared fixes:

- 11 new content regressions pass: all native faction stances are reciprocal, the Loosed and an elemental both deal AI damage without faction overrides, site content has no human stamp rewards, and actual generated ruin quests reject nearer manifestations over repeated seeds.
- 81 existing elemental cases pass with the strengthened natural-placement case excluded explicitly. Pinned sites contain residents at their first eligible level and all intended residents at a high level. No hidden landmark-spawn patterns are accepted.
- The new real targeted-bolt matrix covers all bodies on flat ground and +/-0.5 m sloped terrain. It produces actual projectiles through `WeaveRuntime`: 43/48 pass. Cinderling flat/downhill, Emberwisp downhill, Rime Star downhill and Silt Crawler downhill fail, reproducing the shared defect without inflating bodies. The #36 owner has the fixture and owns the common correction.
- The strengthened natural-site test fails because a low-level Bloom site has no eligible inhabitant. This is a required regression awaiting #31-34's generic placement fix, not an accepted empty-site behavior.
- Three real displacement regressions fail on the old shared runtime: Bash moves Emberwisp 47.49866 m and Winter's Sweep moves it 94.08607 m, both beyond its 45 m home leash. A 4-unit shove from x=30 tunnels through a placed wall at x=32 and ends at x=93.34717. These fixtures await the shared ceiling and swept-collision correction; elemental mass and dimensions are unchanged.
- Full browser tests: 270 pass; browser build and native appearance parity pass. Content validation: 0 errors/warnings. Original source-renderer reference remains unchanged. Logs normalize local machine prefixes; historical measurements retain their original values.

Pending before rereview: consume the published corrected #34 and common contact base; pin the shared shove ceiling with the elemental roster; pass the projectile and natural-site regressions; review current generation goldens; rerun full applicable gates, private smokes and Release performance; then update this record and PR #40 with exact dependency commits.

## Combined contact acceptance

The temporary branch `codex/elemental-shared-review-checks` merges elemental content `b4f558f` with the complete #36 branch at `724c5ed`; its merge is `cb4e321`. It is validation evidence, not the content PR's dependency base. On that combination, `ElementalProjectileTests` passes 48/48 real Ember Bolt cases, including all five old misses, with dimensions unchanged. Another 91 content and sustained-AI cases pass, explicitly excluding the natural-placement and mass-travel cases that still need corrected #34. Logs are `projectile-after-hitbox-724c5ed.txt` and `kits-after-hitbox-724c5ed.txt` under the review-fixes evidence directory.

The coordinator's integration direction is to keep #40 draft and based on corrected #34, then merge main and retarget only after the corrected foundation and reviewed #36 land there. #20 owns its release policy and still requires shared integration review. No #36 runtime has been copied into this content branch, and no merge is authorized by this record. Full combined gates and the corrected foundation SHA remain pending.

Acknowledged by: pending Claude rereview.
