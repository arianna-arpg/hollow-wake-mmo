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

## Current-main icon follow-up

After the verified integration commit `bff066d11757164a11a31817c60fd6362f8f4a8e`, main advanced to `45ce87dae2102245a91c09e1d12086d4481880f0` by merging the reviewed shared-icon work. The follow-up merge retains that complete history. Its only conflict is `contentFiles.ts`: the eager content glob excludes both server-negotiated anatomy and the separately preloaded artwork catalog. The reviewed icon loader and its presentation-only protocol identities otherwise merge unchanged. Combat, projectile execution, weave runtime and entity data are unchanged by this follow-up.

After the coordinator released functional work, the final follow-up solution build passed with 0 warnings/errors. All 1731 core, 59 server, 132 native-client and 269 browser tests pass (2191 total), including the Windows replay baseline, all authored-body cast cases and the feature-negotiation matrix. Content validation is 0/0; regenerated protocol matches. Production web build passes with a separate 109.60 kB artwork asset and the retained 1664.06 kB app-chunk advisory. A fresh private server probe grants all six features, sends the anatomy catalog and interface states, enters and moves. Evidence is under [main-icons-integration](../../experiments/entity-hitboxes/evidence/main-icons-integration/). The earlier e76ecbd counts remain tied to their tested source, not relabeled as this later combination. Fresh background smoke passes browser 30/30 and native 36/36 with no cleanup errors, including return to Mu. Native pixel skips and shutdown diagnostics match the earlier run. Visual inspection of the final card screenshot again reproduces the separate #37 renderer-state defect; this does not pass card-mode visual acceptance. No timing sample was taken.
## Corrected foundation forward integration

The final forward merge takes complete, validated Orc #33 `80dcac71e6de2d561cd79d58755b34433ffd0e81` (runtime `921ba9e93beacf608985cf21dcdca47fed6c63e8`, including corrected #31 and #32) into #36 `cfeca690f6f85e44f9a86175287e2dd21d4b13ed`. It retains main `45ce87d` and complete #36 correction `724c5ed`. The agreed order is #33, this final #36, then #34, then #37. No #34 history is merged backward and no foundation correction is copied separately.

Four conflicts are resolved: CharacterSheet reads the foundation's `physicality.reference_height` while retaining hit-body attachment and cache invalidation; ProjectileSystem keeps the centered slab and reads the foundation's `Relations.FactionOf` for projectile identity; the entity contract describes both reference height and height-scaled anatomy independent of navigation radius; the literal inventory retains the corrected raise_zombie effects[0] count/duration paths and the foundation owner's review comments. The summon `raise_as` axis declaration merges alongside the existing body/aim audit. All other foundation runtime and content changes are imported as the complete base. The new mass, wall-safe displacement, population and corpse semantics belong to that reviewed foundation, not new hit-body behavior.

`HitBodies`, `Shapes`, delivery contact execution, authored hit-body profiles and entity dimensions are unchanged by this integration. The Release standard replay matches the existing #36 Windows baseline exactly; no baseline or assertion changes were needed.

Fresh evidence: [corrected-base](../../experiments/entity-hitboxes/evidence/corrected-base/). Raw output is retained under `experiments/entity-hitboxes/artifacts/base-80dcac7/`; committed text normalizes line endings and trailing whitespace.

- Solution Debug build and Tools Release build: 0 warnings/errors.
- Full .NET tests: core 1777, server 59, native client 132; browser tests 269. All 2237 pass, with no .NET skips. This includes the 882 actual-cast anatomy matrix and four entry/anatomy negotiation combinations.
- Content validation: 0 errors/0 warnings. The generated frozen-literal census exactly matches all 499 keys, disregarding comment lines only. No inventory assertion was weakened.
- Release Windows `tournament --standard --check-hashes`: identical to the committed standard-duels baseline. Zone load completes 200 ticks; ten-second loosed_kindler/boar fight completes. Loopback passes within the server suite.
- Generated TypeScript protocol matches. Production web build passes; the app is 1665.53 kB and separately loaded artwork 109.60 kB, with the existing large-chunk advisory retained.
- A new private server probe grants entity_delta, interface, cast_cues, entry_flow, hit_bodies_v2 and projectile_slabs_v1 together, receives HitBodiesMsg and interface state, enters and moves. Only that probe's owned server is stopped afterward.
- Background smoke: browser 30/30, native 36/36, cleanup errors empty. Native rendering and theme pixels remain skipped. The native log retains the same five dummy-texture, two shaped-text, one font, one CanvasItem and 26 ObjectDB shutdown diagnostics.
- Isometric screenshot inspection shows terrain and bodies. The card-mode capture still reproduces the separately owned #37 renderer defect and is retained as failure evidence. No card visual acceptance is claimed.
- Integration-authored whitespace checks pass. Generated untracked native UID files are removed only from this run's own client directory. The worktree and root checkout are checked clean after commit.
- No new benchmark was run. Prior timing results apply only to their recorded heads; #20's actual combined contact/forecast acceptance and a coordinated performance run remain pending. Budgets are unchanged.

Claude rereview is requested on the resulting full #36 merge before main integration, especially the four resolutions and inherited foundation interaction. The foundation owner receives this exact published head before advancing #34.

## Shared #20 handoff

The #20 owner's [acceptance contract](https://github.com/arianna-arpg/hollow-wake-mmo/pull/36#issuecomment-6254141942) names intended integration head `85fa5b5aa3ddb1575151558a6769747c3eb8e315`. That branch is not merged here. Its compiled `release_check` and `ReleaseTargets` remain the release owner. The eventual complete-foundation combination must consistently author body contact on strike, measured_strike and bash, share the execution/forecast eligibility predicate, preserve committed aim and `AimOffset`, preserve actual-recipient riders and cast identity, and retain snapshot delta reuse history/precision/invalidation behavior.

Repeat species/relief acceptance on that actual combined head, including body-edge overlap, vertical separation, blocked LOS, escape from the footprint and ineligible bodies. Preserve any separately reviewed original-cast Aim change independently of delivery Point. Run the combined client/functional gates and obtain a coordinated quiet window before new performance claims. This record does not certify that future combination or authorize merging #36 before Claude rereview.

Acknowledged by: pending Claude rereview on PR #36.
