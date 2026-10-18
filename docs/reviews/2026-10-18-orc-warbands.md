# Orc warbands and shared stature: scope claim

2026-10-18. Codex owns `codex/orc-warbands-scale`, stacked on PR #32 (`60be7a0`).

Requested by the owner: a distinct Orc faction and varied heavy, hunched silhouettes; player/skeleton stature parity; roughly half-height ordinary Goblins; additional Gnashers, including bodies broad enough for a Goblin saddle.

Scope: additive Orc/Gnasher content, shared client anatomy, existing body heights, encounter populations, tests, fixtures and visual evidence. Overlap with own open #31/#32: entity/appearance content, AppearanceKinds, both body family registries, Goblinkin builders, world goldens and experiment fixtures. PR #22 owns public equipment/day descriptor work; this change does not alter its wire shapes. PR #20 owns cast forecasts/hitbox presentation; precise collision is a separate follow-up, with no collision algorithm changes here. PR #25 icons and #30 projectile test cleanup remain untouched.

Player height is already 1.8 world units. Ordinary skeletal bipeds will use that same authority value; Goblin specialists will be near 0.9. Exceptional brutes, hobgoblins, trolls and the colossus retain explicit exceptional scale. Orc social roles are encounter/content identity; enslavement and command mechanics are not implied. Saddles show ride-sized bodies, not a working mounting system.

Claude review requested for faction relations, silhouette distinction, size continuity, combat kit claims, population budgets and final gate evidence. Retarget stacked PRs before deleting their base branches.

Acknowledged by: pending Claude review.

## Implemented and verified

Twelve Orc roles (including the migrated Ravager), six added Gnashers, four shared-budget sites and an allied Orc faction. Both clients register the content-driven Orc anatomy; Gnasher features add crests, tusks, spots, flank plates and empty saddles. The original whelp is unchanged. Ordinary Goblin heights are 0.78-1.02, Orc heights 1.82-2.48; player authority remains 1.8 and eight ordinary skeletons now match it. Player and skeleton random visual scaling is disabled. Existing undead/Goblinkin fixtures and captures were refreshed.

Orc melee leaders use `orc_press`: a short explicit chase distance avoids a rally weave's preferred range preventing Heavy Strike from connecting. The test first exposed that defect on the Overseer; the final duel trace shows rally followed by a damaging strike at 1.7s. All authored Orc and Gnasher kits land AI-driven damage. Tests cover reciprocal factions, actual shove distance, weight order, shared stature, saddle proportions, mixed camp replay and champion level gates.

- Build passes with the same three pre-existing xUnit2031 warnings in PortedProjectileTests (owned by #30).
- .NET: 665 core + 114 client + 39 server = 818 passing.
- Web: 232 passing; production build passes. Main bundle 1606.53 KB raw / 374.42 KB gzip (base #32: 1548.07 / 369.29). The 1500 KB advisory continues; this pass adds 58.46 KB raw / 5.13 KB gzip.
- Content validate: 0 errors, 0 warnings. Load, Overseer/Skeleton duel, protocol regeneration (no diff), porter 10/10 pass.
- Release benchmark: p95 9.919 ms, behavior 1.965 ms, snapshot build 0.973 ms, full 23.448 KB, delta 7.376 KB, chunk 0.708 ms / 14.378 KB. All documented budgets met; no simulation errors.
- Paired geometry fixtures pass. Isometric, third-person and grayscale/walk captures visually inspected. Each new JPEG is under 268 KB. Actual player/skeleton model bounds are 1.7334/1.71288, with 1.8 authority; Goblin bounds 0.93190 with 0.9 authority. Cosmetic banners/crests extend beyond body stature and are not claimed as hitboxes.
- Browser and native background smoke pending at this initial handoff; final evidence follows.

No wire or collision algorithm changes. Saddle visuals do not implement mounting. Slaver/overseer identity does not implement capture or ownership. The follow-up precise-hitbox chat must reconcile simulation profiles with posed geometry and existing PR #20 forecast work without reading presentation into simulation.
