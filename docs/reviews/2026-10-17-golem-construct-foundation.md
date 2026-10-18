# Golem construct foundation

2026-10-17. Codex owns `codex/golem-construct-foundation`, based on fauna PR #34 at `90cb7fcb5eec97a6ad438c10f7b564f1dfa88790` in its own managed worktree.

## Scope and overlap declaration

Own assembled golem bodies, dedicated content/material/faction/POI files, paired geometry, tests and experiment evidence. Review and migrate the legacy `stone_warden` without changing its ID, summoning contract, player allegiance or reward-free default. Leave undead `bone_golem` untouched. Fire, ice, storm elementals, Kobolds, Gnolls and Beastkin belong to concurrent chats.

Before edits, declare overlap with preceding entity PRs #31-34 and concurrent entity passes: additive registrations in both body family registries and the appearance validator; append-only entity/appearance contract sections; removal of the migrated stone warden from the shared pattern/look files; world generation goldens after seed-specific before/after review. Native shared fixture infrastructure may need one additive fixture entry. Do not alter collision, physicality, AI runtime, protocol or shared attack graphs. Dedicated files isolate all remaining changes.

Read `docs/reviews/2026-10-18-fauna-ai-handoff.md`. Current AI is replaceable existing behavior scaffolding. Desired guarding, repair, coordinated formations and mechanic-aware use of mass stay deferred authoring intent under metadata. Materials describe geometry and mass, never implied resistance or immunity.

Claude review requested for materials and shove balance, visual silhouettes, preserved stone-warden summon compatibility, sparse site placement and measured gates. No merge authorized. Final evidence and limitations will be appended here.

## Delivered content and compatibility

Eight wild bodies plus the migrated Stone Warden: moulded clay, quarry blocks, riveted iron, faceted quartz, lashed timber, fired ceramic, a walking vault and scavenged treasure. Dedicated content files own patterns, material densities, faction, loot, behavior, four sparse POIs and the salvage summon preset. Native/browser `assembled_golem` builders use the ordinary biped rig, change torso and limb assembly through features, and expose hand/chest/head sockets. Every body has direct material, assembly, role, habitat, size and locomotion classifiers.

Correction after Claude's review: the original publication incorrectly described all combat stats as preserved. Inheriting `golem_body` also changed mana capacity, mana regeneration and stamina regeneration. The 2026-10-18 cleanup restores `creature` inheritance and tests the original stats at levels 1, 3 and 20. ID, player faction, 2.2 m height / 0.7 m radius, Warden Slam, 10% reduction and binding are retained. Explicit 1,905.904 kg mass replaces the old 80 kg reference; construct classifiers and assembled geometry without scale jitter are intentional changes. This is not an additions-only behavioral migration. It remains reward-free and summon-only. Bone Golem content is untouched.

Wild bodies share the Unbound Works faction. Vault/bauble prototype faction and economy mechanics are deliberately adapted, not imported. All bodies use existing damage weaves. The salvage pool binds one of the eight wild patterns, assigns existing melee/ranged minion behavior, reserves 30 mana and respawns after eight seconds; wild drops/XP are stripped on birth and respawn. No wire, collision, physicality or AI runtime changes.

## Evidence and verification

Evidence root: `experiments/golems/evidence/`; provenance and reproduction instructions sit beside it.

- Solution build succeeds with zero errors. The same three inherited xUnit2031 warnings in `PortedProjectileTests` remain; they were present before this pass and are already fixed on main via PR #30.
- Full solution tests: **752 core + 116 client + 39 server = 907 passed**, no skips/failures. Forty focused golem cases cover every physical profile, actual repeated AI damage from every assigned skill, mixed kits, deterministic hit replay, natural site selection, actual POI inhabitants/level windows, mass/shove ordering, all eight summon-pool bodies, owner allegiance, reward removal, respawn and Warden/Bone Golem exclusivity.
- Native/browser fixture: nine full plans match, including bones, parts and sockets. Browser suite: **263 passed**. Production web build passes; the existing large-bundle advisory remains (1,729.47 kB raw / 385.77 kB gzip).
- `validate`: **0 errors, 0 warnings**. Origin load: 200 ticks, 2,686 entities / 281 monsters. Natural foundry zone (-8,-4): 200 ticks, 3,389 entities / 370 monsters. Clay-versus-Iron fight records repeated hits, poise, stun and death. Protocol regenerated with no diff. Prototype porter: **10 passed**.
- Private `test-background.mjs --suite all --no-build`: **browser 29/29, native 25/25**, cleanup errors empty. Native headless mode explicitly skips rendered pixels and retains the existing RID/ObjectDB exit diagnostics. The normal browser smoke covers the start zone; golem rendering evidence is the dedicated gallery.
- Inspected actual-geometry isometric, low-angle, grayscale walk and common-scale images. Player/skeleton reference is 1.8 m, ordinary Goblin 0.9 m. Mesh bounds are separately reported because branch crowns, vents and crests exceed nominal stature. Bodies use 320-624 triangles each; committed gallery JPEGs are below 180 KB.
- Reviewed before/after seed `aureth` maps and full neighboring-POI/quest diffs before updating goldens. All four local chunk hashes and climate/region hash remain stable. New candidates alter weighted nearby-site selection and one Ninewells scouting target; the complete changes are in `world-neighbors.json` and `world-review.txt`.
- Final full Release sample, after smoke processes exited: **tick p95 12.575 ms**, **behavior 1.876 ms / 23.0 KB**, **snapshot build 0.875 ms**, **full/delta traffic 27.234/8.738 KB**, **tick allocation 74.480 KB**, zero system errors. Attribute-extra 336.786 ns and expression 112.179 ns meet their budgets. Keep the earlier runs: snapshot build measured 1.009 ms initially and 1.829 ms in the sequential comparison, while base measured 0.827 ms. Snapshot byte counts and benchmark populations are identical to base; timing variability is a limitation, not evidence of a guaranteed margin under shared host load. No density, budget, runtime or benchmark setting was changed to obtain the final sample.

The same 8 m requested knockback produced timber 1.275963 m, clay 0.812630 m, quarry 0.096182 m and iron 0.018816 m through the existing integrator. Authored masses are game estimates, not measured anatomy. This is intentionally a heavy roster and needs encounter balance review.

## Claude review and deferred work

Please review the material estimates/poise interaction, sparse-site levels, global POI selection impact, silhouettes at game scale, retained Warden compatibility and reward-free summon reuse. The appearance registrations and goldens overlap concurrent entity work and must be integrated additively; remeasure goldens after combining POI catalogs rather than copying constants from competing branches.

The fauna AI handoff remains the design starting point. Proposed future golem scenarios: a guard protecting an entrance against a lighter shoving player; a vault guardian choosing between movement and an ordinary attack; ranged crystal cover/line-of-sight choices; owner commands overriding guarding; and repair/reassembly interrupted by damage. These are deferred requirements, not implemented tactics. Current state machines do not reason about mass, anticipate hits, repair, form ranks or sense through a new perception service. No material implies bleed, poison or elemental immunity.

Precise damage volumes belong to the separate hitbox chat, including these profiles. Native pixel evidence requires an isolated graphical session. Full progression/encounter balance, unique golem animations and dedicated skill unlock placement are not claimed. The salvage binding is valid content available through existing composition paths, not a newly added quest reward.

Stack on PR #34; retain/retarget its base before deleting merged stack branches. No merge or deployment performed. Root checkout was left untouched, including its pre-existing untracked `.claude/`.

## Publication status

Implementation committed as `b946d5d` and published in [PR #39](https://github.com/arianna-arpg/hollow-wake-mmo/pull/39), stacked on fauna PR #34 (`codex/fauna-critter-foundation`). The owner explicitly authorized switching back to the connected `arianna-arpg` GitHub account and publishing the branch and PR, correcting an accidental selection to leave the work local. The account switch succeeded, repository push permission was verified, and the branch was pushed without rewriting history.

This resolves the earlier publication blocker: `JohnO312` lacked push permission, and automatic approval review had rejected using the other account without specific authorization. No token was extracted by that rejected action. PR #39 includes the validation evidence, limitations and explicit Claude review request. All source and durable evidence are committed. Claude review is pending; no merge or deployment was performed.

The owner's subsequent prototype roster extension is recorded in [the follow-up review](2026-10-17-golem-prototype-extension.md), with its own validation and visual evidence. The counts above describe the original pass.

Acknowledged by: pending Claude review.
