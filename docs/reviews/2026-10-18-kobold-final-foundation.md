# Kobold final foundation integration

## Scope and prior approval

Consume exact complete #34 c3099a8ea5c7a32fc0799c3a17cb167b2bd1231c additively onto approved Kobold head 6c0eaa187bcfd6779a56b6823d441b84cde3296a. Its runtime 26431c48 contains complete reviewed #36 01b2884, #33 a3eafab and main17ca; the final head also carries complete mainfa7f5f0/PR56 as a test-only forward merge. Do not duplicate a cherry-pick or merge main independently.

[Claude explicitly resolved the spacing finding and approved #38's own content at 6c0eaa1](https://github.com/arianna-arpg/hollow-wake-mmo/pull/38#issuecomment-6255320256). Preserve all four 48 m values and the restored Single assertions. That acknowledgment is scoped to the Kobold content; this new shared runtime still requires its own integration evidence and remaining coordinated acceptance.

## Ownership before merge

This pass owns conflict resolution in the shared entity contract, browser card renderer/tests/smoke and branch-specific world goldens. Preserve complete incoming aim/placement semantics, reviewed renderer/harness changes, explicit Knot opt-in and anchored quest search, alongside the approved Kobold content and generic field gear. Retain the current Kobold golden values until actual before/after output is audited. The before reports are the verified 6c0eaa1 spacing outputs, not a different branch's constants.

Audit all sixteen cases and exact final-base content, including actual sites/populations/effective levels/quests and target regions. Record first failures, then refresh only supported golden differences. Run required combined builds/tests, validation, census, load/duel, Release Windows replay, proving comparison, client/build/porter/lifecycle checks, six-feature private probe and private 31/36 smoke with inspected card/grayscale evidence.

No timing run, budget relaxation, entity-size change or AI replacement is authorized here. Kobolds retain cylinder fallback, existing skills and the documented reach/AI limitations. Inherited proving-band failures remain open. Separate #20/#50 combined acceptance stays with the coordinator.

## Audited world changes

Merge source fd4a088 preserves the complete incoming renderer/smoke source exactly and differs from the incoming Core only in the two existing appearance registrations. Both merge conflicts are resolved: the entity contract preserves default pattern levels plus the explicit Knot exception; Kobold goldens were retained for a first 32-pass/4-fail focused run before inspection.

Across all sixteen audited cases, geometry, positions/counts, sites and nearby choices stay identical to approved 6c0eaa1. Only Knot residents acquire explicit site levels: origin 3, north 7, (3,2) 3, Scrap fixture 27, Cinder fixture 23, with and without overlays. Kobold birth levels are unchanged. The origin gains Clear Knot targeting the local Hob's Hollow and cinder_imp; its seeded scout changes from Sunken Lantern to Fallen Hall, poi.1_1.ruin.305.334. All sixteen quest lists exactly equal final-base content on the same runtime. Standard four-zone geometry/populations also equal final base; nearby weighted species choices retain the existing branch differences. Full sites, target regions and population rows are in before/after/base reports and changes.json.

Only the five observed golden constants were refreshed: origin information/chunks, north chunks, (3,2) chunks and overlaid world. Climate and east-zone goldens stay unchanged. Own origin and overlay hashes differ from foundation constants because the branch retains Kobold neighboring choices.

## Combined functional checks

Tested source: 992a3e3a7a517c7b646136a0442f0afc7f076f3d. Solution and Release tools build with zero errors/warnings. Full solution tests pass core 2525, server 59 and native logic 137. Content validation is 0 errors/0 warnings; generated protocol is current. Origin loads 100 chunks/200 ticks with 2686 entities, 281 monsters and four quests. The Tunnelguard/Loosed Brigand duel shows mutual damage. The frozen literal census exactly equals all 499 reviewed keys. Seven Release Windows standard replay hashes remain identical; no replay or grammar fixture changed.

Web tests pass 315 cases across 39 files, including the three interaction-click regressions under Vitest. Production build passes with the inherited large-bundle advisory. Porter and background lifecycle tests pass 10 each. An additional accidental node:test invocation of the Vitest interaction file failed at runner initialization; its output is retained as interaction-wrong-runner.txt. The supported full web suite already ran those tests successfully; no test or implementation was weakened.

The complete proving JSON deep-equals the prior actual base3d/Kobold report. Exit 1 is retained: bulwark, plaguer and pitfighter still win 10/10 above the 80% ceiling. Neither bands nor fixtures were relaxed. This is not balance or performance acceptance.

## Private client checks and handoff

The fresh private TCP probe grants all six requested capabilities (entity_delta, interface, entry_flow, cast_cues, hit_bodies_v2, projectile_slabs_v1), reports PROBE OK and has no cleanup errors. The private background suite passes browser 31/31 and native 36/36, including import, with cleanupErrors empty. Both inspected browser captures show terrain and bodies in card and grayscale modes; they are retained as 16_cards.png and 17_grayscale.png. Native pixels are explicitly skipped in headless mode. The raw native log retains RID, CanvasItem and ObjectDB shutdown diagnostics; passing behavioral checks do not clear those diagnostics.

No application source changed after 992a3e3a7a517c7b646136a0442f0afc7f076f3d. This evidence commit is documentation/logs/images only. The four default-spacing values, Single habitat assertions, default Kobold pattern levels and all 84 real projectile cases remain intact. Prior spacing approval is acknowledged, while Claude rereview is requested for the complete final integration. No benchmark ran; no final performance or #20/#50 acceptance is implied.

Commands: dotnet build HollowWake.sln; dotnet test HollowWake.sln --no-build; hollowwake validate/load/fight/protocol-ts --check; hollowwake weave --axes --all with a separate generated literal file; Release tournament --standard --check-hashes experiments/combat-grammar/baselines/standard-duels.json; tournament --bands with full-report comparison; npm --prefix web test/build; node --test scripts/port/port-prototype.test.mjs and scripts/test-background.test.mjs; private six-feature probe; node scripts/test-background.mjs --suite all --no-build. Exact outputs and test/source provenance are adjacent in experiments/kobolds/evidence/final-foundation/.

Acknowledged by: prior own-content approval linked above; final integration rereview pending.
