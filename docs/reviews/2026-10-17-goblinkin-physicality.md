# Goblinkin and physical bodies

Claimed by Codex on 2026-10-17, branch `codex/goblinkin-physicality`, based on undead PR #31 (`5d4b69b`) and kept as a separate stacked change while that review is open.

Scope: prototype-derived Goblinkin and allied warbeasts, pronounced three-dimensional silhouettes, authored physical dimensions/materials, and a shared mass/poise/shove-authority foundation. Small scavengers, warband specialists and hulking bodies must differ in behavior as well as appearance. Prototype impact damage, trample/bowling, mounts and elaborate squad policy will be assessed and explicitly recorded where deferred.

Ownership overlap declared before editing: #31 owns entity/appearance/world contracts and additive family registries; this branch builds on those changes. #22 owns `BodyFamilies.cs`, `BodyPlan.cs`, appearance infrastructure and `rules.yaml`; this pass adds separate families and separate physicality rules instead of changing its wire work. #20 owns delivery attributes; physical attributes will be separate. No unrelated branches are modified.

## Delivered

PR [#32](https://github.com/arianna-arpg/hollow-wake-mmo/pull/32), stacked on [#31](https://github.com/arianna-arpg/hollow-wake-mmo/pull/31). Retarget to main before the base branch is deleted.

- Sixteen Goblinkin and allied warbeast patterns with native combat faction, hierarchical material/family/role/habitat/size tags, current skills, regeneration, loot and active population references. Tiny scavengers and gnashers contrast with brutes, trolls and the colossus.
- Shared procedural anatomy on existing animation rigs. Full native/browser fixtures include worn equipment. Common-scale lineup plus individual isometric, third-person and grayscale walk captures show the actual production geometry.
- Authored XYZ envelopes, occupied volume, effective material density and mixtures feed geometric mass. Current Transform scale is included, so rarity scaling changes mass cubically. The common mass modifier fold is reused, not replaced.
- Knockback, pull and buffet use mass, remaining poise and bounded pusher authority. Plain effects and woven effects both respect authority modifiers. Existing immovable, pull-anchor, collision and voluntary travel semantics remain covered by the full delivery tests.
- Wounded raiders withdraw; ranged actors kite; defenders hold short leashes. Cinder Keg uses the existing lob and area handlers. Three site types plus roaming tables provide level-gated encounters. No species-based switches or client authority were introduced.

## Evidence and gates

Committed evidence: `experiments/goblinkin/evidence/gates/`. Full transient smoke data and screenshots remain under ignored `artifacts/testing/`.

| Gate | Result |
| --- | --- |
| Solution build | Pass, no errors; the same three xUnit2031 warnings in PortedProjectileTests owned by #30 |
| .NET tests | 644 core + 113 client + 39 server, all pass |
| Content validation | 0 errors, 0 warnings |
| Focused new content/physicality tests | 32 pass, including every AI kit, Cinder Keg relations, retreat, level gating, material volume, modifiers, rarity, poise and displacement |
| Web tests/build | 214 tests pass; production build succeeds |
| Protocol | Regenerated, no diff |
| Prototype porter | 10 tests pass |
| Headless load/fight | Pass; Goblin Skirmisher and Skeleton Warrior both deal damage |
| Browser smoke | 29/29 pass, no page errors |
| Native smoke | 25/25 pass; headless GPU checks explicitly skipped |
| Shared visual fixtures | Native full-plan comparison plus browser per-body fixture checks pass |
| Release benchmark | Within all recorded budgets; zero simulation errors |

Release measurements: tick p95 9.222 ms (25 ms budget), behavior 1.690 ms (3 ms), snapshot build 0.711 ms (1 ms), full snapshots 23.448 KB (30 KB), delta snapshots 7.376 KB (10 KB), chunk generation 0.756 ms / 14.378 KB. The first density pass exceeded the full-snapshot budget at 31.077 KB; roaming density was reduced before the final gate. Both measurements are retained. Weighted ruin choices and roaming spawns intentionally change world output; before/after captures and the seeded level-gating test precede the updated goldens. Climate and region hashes remain unchanged.

The browser app chunk grew from 1487.60 KB / 362.77 KB gzip on the base to 1548.07 KB / 369.29 KB gzip. This introduces Vite's 1500 KB advisory; the build succeeds and the measured browser/server budgets pass. It is recorded for review, not presented as a pre-existing warning. Splitting the bundled content is a separate packaging improvement. Native shutdown resource-leak diagnostics are retained in the smoke log; a graphical native session has not been exercised by this headless run.

## Limits and review request

Material densities, fill and mixtures are authored gameplay estimates, not mesh-integrated biological measurements. Current collision is the conservative cylinder. Unprofiled older bodies retain their nominal reference mass; poise anchoring intentionally changes their shove response too. `Physicality.Mass` is the geometric external-base query; plain mass-channel lookups and links from mass do not infer geometry. This distinction is documented, and the nominal channel is hidden from the character sheet.

Mounted riders, reinforcements, shared morale, detachable boss parts, trample, body bowling and momentum damage remain deferred. Current substitute skills are recorded in provenance; source numerical parity is not claimed. No wire shape, progression unlock or Calling loadout changed.

Claude review requested for units and modifier integration, poise/source-force balance, encounter tiers, the bundle advisory and visual distinction. This is a review request, not a claim of an independent review or permission to merge. Root checkout changes were not made; its pre-existing `.claude/` remains untouched.

Acknowledged by: pending Claude review.
