# The Unbound Elements

This experiment documents a free-form elemental content pass stacked on fauna PR #34. Sixteen bodies span the Nine Threads with explicit combat kits, wild rewards, dimensions, material mass, faction and sparse manifestation sites. Elemental-specific changes introduce no collision, AI or combat runtime. Shared foundation and hitbox corrections are tracked separately in the [review-fix record](../../docs/reviews/2026-10-18-elemental-review-fixes.md). Assembled golems and `stone_warden` are outside this pass.

## Review the bodies

Open `gallery.html` through the local renderer below. The preview uses the production body builders, mesh adapter, content catalog and animator. Individual cards are enlarged to inspect anatomy; both common-scale lineup rows use the same orthographic projection. The 1.8 m player and skeleton plus the 0.9 m Goblin are references. `scale-report.json` distinguishes authored profile height, conservative plan height (`modelHeight`), and actual rest-geometry min/max bounds in renderer coordinates. Suspended parts need not occupy the full nominal envelope. Turns and decoration can extend the geometry bounds.

`evidence/elemental-isometric.jpg`, `elemental-third-person.jpg` and `elemental-grayscale-walk.jpg` were inspected. Flame tongues, the hollow water curl, ice fan, open wind loops, low mineral body, hanging veils and faceted prisms remain distinguishable without color. `lumen-cast.jpg` and `mind-cast.jpg` show the central cores on the central head bone during an actual gather pose. These are low-poly procedural proxies with existing animation rigs, not final sculpted models or custom movement animation.

`reference.mjs` reads the prototype in place and calls its actual `bodySprite` and live-part painters on its monster definitions. It writes no source files. `HOLLOWWAKE_PROTOTYPE` overrides the source path. The reference is revision `118b510f8b5bfb781163c5bc13ea4908f698e75b`; the ledger distinguishes eight direct prototype bodies from eight new lore-derived adaptations. The capture is a fixed visual sample, not exact combat parity.

## Reproduce

From this worktree after installing its pinned web dependencies:

```sh
node experiments/elementals/reference.mjs
node experiments/elementals/render.mjs
# Only when intentionally approving a new paired geometry baseline:
node experiments/elementals/render.mjs --write-fixture
dotnet test tests/HollowWake.Core.Tests --filter FullyQualifiedName~ElementalTests
dotnet test tests/HollowWake.Core.Tests --filter FullyQualifiedName~ElementalReviewTests
dotnet test tests/HollowWake.Core.Tests --filter FullyQualifiedName~ElementalProjectileTests
dotnet test tests/HollowWake.Core.Tests --filter FullyQualifiedName~ElementalDisplacementTests
dotnet test tests/HollowWake.Client.Tests --filter FullyQualifiedName~ElementalAppearanceTests
npm --prefix web test
node scripts/test-background.mjs --suite all
dotnet run --project src/HollowWake.Tools -c Release -- bench
```

The renderer writes compact JPEGs, the full body-plan fixture, triangle counts and scale reports. Both clients test the same reviewed fixture. Temporary server data, logs and builds stay under ignored `artifacts/` or normal ignored build directories.

## Implemented and deferred

All bodies move on the ground and retain ordinary collision. Ethereal material is finite mass, not phasing. Open shapes do not imply precise hit volumes. Affinity resistances and opposing Ember/Tide vulnerability are explicit stats, not palette effects. Direct damage-effect tests establish mitigation for all nine Threads; they do not prove projectile contact. Separate tests launch the real Ember Bolt against every body on flat ground and +/-0.5 m relief. Generic summons inherit allegiance and lose wild loot/experience. Corpseless bodies still give their authored wild rewards.

The [AI/combat handoff](../../docs/reviews/2026-10-17-elemental-ai-combat-handoff.md) lists every substitution and deferred interaction. Current state machines are replaceable scaffolding. Flight, swimming, material terrain immunity, reactive attunement, crystal shell phases, coordinated elemental tactics and complex social behavior are not implemented.

The tests exercise every skill independently through AI for 45 simulated seconds, verify both early and late hits, then repeat the same seed and compare damage digests. Whole kits fight under their native faction. Physical tests measure actual shove travel. Site tests pin all inhabitants and level gates, and a natural-generation search finds every manifestation kind. The planner tests a candidate before snapping its stored position to a cell center; near a biome boundary the site's placement cell can contain both biomes. This existing behavior is recorded in the reachability audit rather than changed here.

## World evidence and constraints

The original `world-before-*`, `world-after-*`, `world-review.txt` and `gates/` evidence describes `b29e67c`, including the reviewed defects. It is historical, not a claim that the corrected head has unchanged maps. New evidence lives under `evidence/review-fixes/` and includes the per-pattern spawner/body counts, neighboring POIs, levels and generated quest targets against the base with elemental sites and their budget removed.

Corrected sites use `site.manifestation`, a dedicated 0.12 entry per region. It is a per-candidate weight multiplier and contributes to the total desired site count, not a reserved encounter pool. Other budget entries stay unchanged. Natural stamps add shallow walkable relief but no chest, shrine stone, camp palisade or extra creature. The ordinary discoverable/interactable POI marker remains. Sites have zero level offset, multiple names, 48 m minimum spacing against all sites, and one body per eligible inhabitant row with a 420 second respawn. They are excluded from ruin and shrine quest target tags.

The Ember Seam's site weight is 0.8; the other manifestation weights are 0.22. Its cinderwaste/scrub habitats remain unchanged. On the corrected shared runtime, the default-seed audit over zones [-48,48] found no Ember Seam at 0.22 and three at 0.8; the other manifestation counts were unchanged. This compensates for a scarce habitat without increasing the common manifestation budget. The audit records counts as well as first occurrences and verifies eligible residents at each discovered site.

The private browser smoke supplies rendered screenshots in ignored run artifacts. Native headless smoke verifies behavior and explicitly skips native pixels. No native graphical rendering, real controller or desktop focus test is claimed. The [current review record](../../docs/reviews/2026-10-18-elemental-review-fixes.md) carries gate results and limitations; the [original record](../../docs/reviews/2026-10-17-elemental-entity-foundation.md) is historical. Committed logs normalize local machine prefixes and trailing whitespace without changing measured results.
