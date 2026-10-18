# Golem corrected-base integration

2026-10-18. Integrate complete #34 `26c50de03c62f434c517564906a54c0287b281ce` into #39 `3561aa834bb8df40560c167e8823bb96c9a18aad`. That base includes corrected #31-33 and full #36 through `724c5ed`; no standalone runtime cherry-pick. #20 release policy remains separately owned.

Ownership before edits: preserve the shared mass/shove, site/quest and projectile/allegiance corrections. Resolve overlapping entity contracts and generation goldens using fresh evidence, not by taking one branch's constants. Promote the published golem contact fixture into the actual core test project now its required runtime is present. Keep creature dimensions and the owner's Amalgamation/Bonewright scope unchanged. No new shared release policy or encounter redesign.

## Combined evidence

The promoted projectile fixture now lives in `tests/HollowWake.Core.Tests/GolemProjectileContactTests.cs` and participates in the ordinary core suite. All 342 incoming/outgoing contact cases and all five live-site level regressions pass on the integrated runtime. The initial focused run has 435 passes and three expected world-golden failures; its complete output remains in `experiments/golems/evidence/integrated/initial-focused.txt`.

Before refreshing those three hashes, `WorldEvidence` compared archived base content against golem content on the same corrected generator. All four sampled local chunk sets and quests are unchanged; the weighted catalog changes neighboring site selection at (0,0) and (1,0). The exact locations and hashes are recorded in `experiments/golems/evidence/integrated/world-review.txt` and `world-neighbors.json`. Climate, the other two zone samples and all local chunk hashes remain unchanged.

Full combined gates and performance evidence pending below. Performance runs are serialized with the other owning chats at normal process priority.

## Integration limits

Claude rereview is required before merge; dependency PRs remain draft. This exact base predates the tournament CLI and its replay baseline. The coordinator confirmed that tournament replay and #20 release-policy integration belong to the later complete reviewed-main combination after the foundation stack lands and this PR retargets. Do not cherry-pick instrumentation or claim that future gate passed on this checkpoint.

Acknowledged by: pending Claude rereview.
