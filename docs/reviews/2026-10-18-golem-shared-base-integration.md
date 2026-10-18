# Golem corrected-base integration

2026-10-18. Integrate complete #34 `26c50de03c62f434c517564906a54c0287b281ce` into #39 `3561aa834bb8df40560c167e8823bb96c9a18aad`. That base includes corrected #31-33 and full #36 through `724c5ed`; no standalone runtime cherry-pick. #20 release policy remains separately owned.

Ownership before edits: preserve the shared mass/shove, site/quest and projectile/allegiance corrections. Resolve overlapping entity contracts and generation goldens using fresh evidence, not by taking one branch's constants. Promote the published golem contact fixture into the actual core test project now its required runtime is present. Keep creature dimensions and the owner's Amalgamation/Bonewright scope unchanged. No new shared release policy or encounter redesign.

## Combined evidence

The promoted projectile fixture now lives in `tests/HollowWake.Core.Tests/GolemProjectileContactTests.cs` and participates in the ordinary core suite. All 342 incoming/outgoing contact cases and all five live-site level regressions pass on the integrated runtime. The initial focused run has 435 passes and three expected world-golden failures; its complete output remains in `experiments/golems/evidence/integrated/initial-focused.txt`.

Before refreshing those three hashes, `WorldEvidence` compared archived base content against golem content on the same corrected generator. All four sampled local chunk sets and quests are unchanged; the weighted catalog changes neighboring site selection at (0,0) and (1,0). The exact locations and hashes are recorded in `experiments/golems/evidence/integrated/world-review.txt` and `world-neighbors.json`. Climate, the other two zone samples and all local chunk hashes remain unchanged.

Functional gates target integration commit `ad310a2b3047b366da9ad32798beea5cff5145e3`:

- Full solution build: zero warnings/errors. Full local suite: 2,567 core, 122 client and 48 server tests pass, with no exclusions or skips. The online build/test check independently passed the same counts.
- Browser: 279 tests pass and production build passes with the existing bundle-size advisory. Content validation: zero errors/warnings. Generated TypeScript protocol is current.
- Porter: ten original tests plus the dedicated golem mapping regression pass. Origin loads 200 ticks and the Amalgamation/Stone combat trace completes; actual owner-bound summoning is covered by the core regressions rather than the CLI's ownerless arena.
- The private TCP probe negotiates `hit_bodies_v2`, `projectile_slabs_v1`, `entity_delta` and `interface`, enters the world, moves and receives interface/chat state. Its initial helper used an account name over the server's 32-character limit; that rejected login is retained separately. Shortening the temporary account name fixes the helper; no product change was needed.

- Private background smoke: browser 30/30 and native 26/26 behavioral checks pass, with no cleanup errors. Native rendering/theme pixels are explicitly skipped; Godot reports resource/object leaks during shutdown, retained in the log, so this is not a zero-diagnostic native run.
- **Browser pixel clearance fails.** Initial geometry visibly renders the world, but the card-mode capture and later combat capture show sky plus DOM labels with the terrain/bodies absent. `cards-render-failure.png` and `combat-render-failure.png` preserve this despite the behavioral runner's passing summary. The shared renderer owner and coordinator have the evidence; do not treat smoke counters or the isolated golem galleries as a successful integrated card-rendering gate.

Release tools build with zero warnings/errors. Performance evidence pending below; runs are serialized with the other owning chats at normal process priority.

## Integration limits

Claude rereview is required before merge; dependency PRs remain draft. This exact base predates the tournament CLI and its replay baseline. The coordinator confirmed that tournament replay and #20 release-policy integration belong to the later complete reviewed-main combination after the foundation stack lands and this PR retargets. Do not cherry-pick instrumentation or claim that future gate passed on this checkpoint.

Acknowledged by: pending Claude rereview.
