# Precise entity hit bodies: current-main integration

Date: 2026-10-18. Author: Codex. PR: #36.

## Inputs and scope

The merge combines published #36 `724c5ed57d7a6082f9c485f66b8dd7001acbe2fb` with main `e76ecbd050bf75bd84a00d11ce97da2a1fa5cf62`, including the reviewed axes, entry-flow and Windows replay-format work. It keeps the complete #36 ancestry. It does not merge #34 backward or copy its pending foundation corrections. The foundation owner will propagate this refreshed head into the corrected stack.

The contact implementation and approved anatomy remain unchanged from #36: no edits to hit volumes, authored entity dimensions, `HitBodies`, `Shapes`, delivery execution, or `ProjectileSystem`. Main's compiled channel/charge/timing reads merge with the sampled `AimOffset`. Cast admission, clearance reach, contact eligibility and the absence of a private whole-graph melee release gate remain as recorded in [the correction review](2026-10-18-precise-entity-hitboxes-correction.md).

## Integration changes

Three conflicts were additive feature lists in native `Main`, server `SessionManager`, and browser `app`. They now retain both `entry_flow` and #36's `hit_bodies_v2`/`projectile_slabs_v1`. The server negotiation regression covers all four entry/anatomy opt-in combinations while always requesting slab rendering. A private TCP probe confirms all six requested features are granted together, receives the anatomy catalog, enters and moves.

Main's new axis census now declares area `contact`, conditional `height`/`elevation`, and registered projectile `aim`. `ProjectileAims.Names` exposes the existing registry vocabulary without changing resolution. The report supports a choice default from a content rule, preserving its word and provenance instead of reporting numeric zero. Unwritten area height remains a live-body value rather than a fabricated weave constant. A focused regression pins these meanings. The existing distinction between projectile elevation and nested-cast orientation is explicitly added as `aim` in the split-name baseline; neither runtime vocabulary was renamed.

The frozen-literal inventory gains 24 keys from content already present on #36: bone arrow, goblin cinder keg and undead raising/summoning presets. No content values change. The projectile morph fixture no longer claims bar and wave are equivalent: both strike the same seven hits in this scenario, but their last contacts are ticks 8 and 9 under the precise geometry. The normal morph sensitivity assertion now requires their observed difference. The cone/sector alias remains equivalent.

## Replay evidence

The original main baseline was checked and its failing output retained before regeneration. On `win-x64`, with the same ten seeds and sixty-second cap, four projectile-bearing pairings change with #36's documented launch elevation, slab and anatomy contact semantics:

| Pair | Main baseline | Integrated #36 |
|---|---|---|
| loosed_kindler:boar | 358e00d82eb1c502 | ae3a86f086814949 |
| loosed_skirmisher:boar | 79899296f1748715 | 7dd259e21c8ac0e8 |
| loosed_skirmisher:hob | 8375b6721ef21310 | 0d9d4a3110181c7a |
| loosed_skirmisher:tide_caller | d5b2b56a7491fbd1 | c78c43f0835e549e |

`wolf:hob`, `tide_channeler:spark_duelist`, and `proving_reaver:proving_bulwark` are unchanged. These are deliberate combat differences, not trace-format or cross-platform excuses. No gameplay was adjusted to match a hash. The regenerated baseline passes the full core regression and an independent Release `tournament --standard --check-hashes experiments/combat-grammar/baselines/standard-duels.json` comparison. Claude should review the four changed pairs alongside the contact contract; the fixture is not evidence that the combat is unchanged from main.

## Gates and limits

Evidence (committed text normalizes line endings and trailing whitespace; original raw logs remain under the experiment artifacts): [main-integration](../../experiments/entity-hitboxes/evidence/main-integration/).

- `dotnet build HollowWake.sln -m:1`: 0 warnings/errors. `dotnet build src/HollowWake.Tools -c Release -m:1`: 0 warnings/errors.
- Final `dotnet test HollowWake.sln --no-build -m:1`: core 1731, server 59, native client 124, all passed with no skips. Core includes all 882 authored-profile actual cast/contact cases. The affected grammar/morph/replay subset also passes 104 tests. Initial four failures and the subsequent newly exposed split-name failure are retained; no assertion was disabled.
- `npm --prefix web test`: 247 passed. Production build passes with the retained large-chunk advisory (app 1641.44 kB).
- Release `validate`: 0 errors/0 warnings. `load --x 0 --y 0`: 200 ticks. `fight loosed_kindler boar --seconds 10 --lines 100000`: complete trace, boar defeated. Loopback is included in the server suite.
- `protocol-ts`: generated file matches the merged source. Private `probe` with `entity_delta,interface,cast_cues,entry_flow,hit_bodies_v2,projectile_slabs_v1`: all granted, catalog received, interface states present, movement passed. The first probe used unknown `entry_flow_v1`; that attempt is retained separately and does not support the combined-entry claim.
- `node scripts/test-background.mjs --suite all --no-build`: browser 30/30, native 36/36; cleanup errors empty. It used this integration's newly built Debug binaries and web output. No OS input or desktop focus changes. Native pixels and controllers are not covered. Native shutdown reports five dummy textures, two shaped-text records, one font, one CanvasItem and 26 ObjectDB instances; the full log is retained.
- Browser isometric capture was visually inspected and shows terrain and bodies. Card-mode capture still shows the known renderer-state defect owned by #37; the retained image is failure evidence, not visual acceptance. That separate renderer correction has not been imported by this merge.
- `git diff --check` on integration-authored paths: clean. Incoming main trace files retain their existing trailing whitespace. Generated untracked Godot UID files were removed only from this run's own client directory.
- No new performance sample was taken: the coordinator requested functional integration and no quiet window is active. Prior #34/#36 numbers apply only to their recorded heads, not this merge.

## Shared #20 handoff

The #20 owner's [acceptance contract](https://github.com/arianna-arpg/hollow-wake-mmo/pull/36#issuecomment-6254141942) names intended integration head `85fa5b5aa3ddb1575151558a6769747c3eb8e315`. That branch is not merged here. Its compiled `release_check` and `ReleaseTargets` remain the release owner. The eventual complete-foundation combination must consistently author body contact on strike, measured_strike and bash, share the execution/forecast eligibility predicate, preserve committed aim and `AimOffset`, preserve actual-recipient riders and cast identity, and retain snapshot delta reuse history/precision/invalidation behavior.

Repeat species/relief acceptance on that actual combined head, including body-edge overlap, vertical separation, blocked LOS, escape from the footprint and ineligible bodies. Preserve any separately reviewed original-cast Aim change independently of delivery Point. Run the combined client/functional gates and obtain a coordinated quiet window before new performance claims. This record does not certify that future combination or authorize merging #36 before Claude rereview.

Acknowledged by: pending Claude rereview on PR #36.
