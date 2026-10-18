# Independent construct review

Codex reviewed request #50 at `4505c1d3f82145a3033a66c28bfbb5dd6e9380e9`, stacked on request #49. The review covered device intent dispatch, pacing and trap disposal, inherited source versus cast origin, free preset/replay resolution, content changes, contracts and the scenario tests.

## Owner decision and interruption evidence

The owner explicitly chose normal cast timing and interruptions for totems on 2026-10-18. The measured first bolt at 1.00 seconds and nine bolts in ten seconds are accepted. `ConstructInterruptionTests` verifies that a running totem cast is interrupted by stun, freeze and a forbidden spell tag and produces no bolt while the blocker persists. These three cases passed in the focused review run.

## Finding: latest-cast storage does not preserve every use

The new contract and scenario test names promise that every owner cast is replayed. `MinionService.OnCast` stores only the latest weave/point/target and increments a counter; `EchoOwner` advances its remembered counter directly to that value and replays only the latest entry. Multiple casts between echo thinks are lost. The same storage/polling logic existed before this request; routing through `Replay` carries the limitation forward rather than introducing it.

The independent reproduction uses seed 9, the normal `cinder_spit` runtime, 40x cast speed, a durable foe and two seconds of accepted casts, then half a second to drain pending work. It measures **40 owner casts and 18 echo replays**. No cooldown or cast-state bypass is used; owner mana is replenished between attempts to isolate replay behavior.

The reproduction is preserved at `experiments/combat-grammar/review/ConstructEchoReplayProbe.cs`, with the failed assertion in `construct-review-tests.txt`. It is an explicitly failing review probe outside the normal test project, not a passing gate or an exemption in the suite. To reproduce in a disposable checkout, copy the source into `tests/HollowWake.Core.Tests/` and run `dotnet test tests/HollowWake.Core.Tests -c Release --filter FullyQualifiedName~ConstructEchoReplayProbe`, then remove that copied probe.

Claude should either preserve each eligible paid resolution in a replay queue, with burst/instant-cast and removed-or-changed-slot coverage, or explicitly document and scope the existing latest-cast policy before presenting this request as an every-use guarantee. The normal and double-speed scenarios alone do not prove that guarantee. This follow-up makes no runtime change to that policy.

## Release-policy integration

Request #20's author confirmed on #50 that replay must construct the final context first (`Source` is the owner, `From` is the echo), then call the common `ReleaseTargets.Execute(context, weave.Effects)`. It must not pay another use or add an echo-specific gate. The current free path still removes dead targets and directly executes effects; reconcile it with #20's explicit target-kind and selector policy.

Required combined cases are an echo behind a wall, an owner far away with the echo in range and the reverse, a body leaving a committed selector, and damage riders applying to the actual selected victim. They are pending integration with the published #20 correction and are not claimed as run here. Keep #50 draft until that integration and Claude review are complete. The request was safely retargeted to main before #49 merged as `552dcec`; its draft remains open.

## Verification and limits

At the reviewed head, the independent Release build had no warnings or errors and all 776 solution tests passed: core 619, client 114 and server 43. The additional focused review run passed all three interruption cases and failed the high-speed replay assertion as described above. With the three permanent interruption regressions added, the full core suite passed all 622 tests with no skips. Local logs are in the review checkout's ignored `artifacts/`; the small failed-probe log is committed beside its reproduction.

No runtime behavior is changed by this Codex follow-up. The original record's performance measurements contain no constructs and do not establish construct-heavy scaling. Its two composition-lint findings for the instant snare form remain explicitly named. No benchmark or graphical smoke was rerun for the test/documentation additions. Claude review is required for the additions as well as the unresolved findings.

Acknowledged by: pending Claude review on PR #50. The owner decision above is resolved; the burst replay and combined release-policy findings remain open.
