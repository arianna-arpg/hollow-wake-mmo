# Independent construct review

Codex reviewed request #50 at `4505c1d3f82145a3033a66c28bfbb5dd6e9380e9`, stacked on request #49. The review covered device intent dispatch, pacing and trap disposal, inherited source versus cast origin, free preset/replay resolution, content changes, contracts and the scenario tests.

## Owner decision and interruption evidence

The owner explicitly chose normal cast timing and interruptions for totems on 2026-10-18. The measured first bolt at 1.00 seconds and nine bolts in ten seconds are accepted. `ConstructInterruptionTests` verifies that a running totem cast is interrupted by stun, freeze and a forbidden spell tag and produces no bolt while the blocker persists. These three cases passed in the focused review run.

## Finding: latest-cast storage does not preserve every use

The new contract and scenario test names promise that every owner cast is replayed. `MinionService.OnCast` stores only the latest weave/point/target and increments a counter; `EchoOwner` advances its remembered counter directly to that value and replays only the latest entry. Multiple casts between echo thinks are lost. The same storage/polling logic existed before this request; routing through `Replay` carries the limitation forward rather than introducing it.

The independent reproduction uses seed 9, the normal `cinder_spit` runtime, 40x cast speed, a durable foe and two seconds of accepted casts, then half a second to drain pending work. It measures **40 owner casts and 18 echo replays**. No cooldown or cast-state bypass is used; owner mana is replenished between attempts to isolate replay behavior.

The original reproduction is preserved at `experiments/combat-grammar/review/ConstructEchoReplayProbe.cs`, with its historical failed assertion in `construct-review-tests.txt`. It failed on the originally reviewed source. The permanent corrected regression is now in `ConstructEchoQueueTests`; the old log is retained as evidence, not presented as a current failure or a passing gate.

The normal and double-speed scenarios alone did not prove the guarantee. Codex claimed and implemented the focused runtime correction after the initial review, as described below. Claude review remains required.

## Release-policy integration

Request #20's author confirmed on #50 that replay must construct the final context first (`Source` is the owner, `From` is the echo), then call the common `ReleaseTargets.Execute(context, weave.Effects)`. It must not pay another use or add an echo-specific gate. The current free path still removes dead targets and directly executes effects; reconcile it with #20's explicit target-kind and selector policy.

Required combined cases are an echo behind a wall, an owner far away with the echo in range and the reverse, a body leaving a committed selector, and damage riders applying to the actual selected victim. They were pending at the initial review and are now covered by the combined integration below. Keep #50 draft until final performance evidence and Claude review are complete. The request was safely retargeted to main before #49 merged as `552dcec`; the new integration depends on #20 and must be retargeted before that dependency's branch is deleted.

## Verification and limits

At the reviewed head, the independent Release build had no warnings or errors and all 776 solution tests passed: core 619, client 114 and server 43. The additional focused review run passed all three interruption cases and failed the high-speed replay assertion as described above. With the three permanent interruption regressions added, the full core suite passed all 622 tests with no skips. Local logs are in the review checkout's ignored `artifacts/`; the small failed-probe log is committed beside its reproduction.

The initial Codex follow-up changed only tests and documentation. The original record's performance measurements contain no constructs and do not establish construct-heavy scaling. Its two composition-lint findings for the instant snare form remain explicitly named.

## Burst replay correction

Main through `e76ecbd` is integrated in `47ba9c8`. Each echo subscribes to its owner's resolutions at birth, selected from its behavior's registered action graph rather than a pattern or weave name. Only those echoes allocate a pending queue. The local `WeaveCast` event carries the actual compiled cast view internally; this is not a protocol field. Each echo drains its own pending uses in resolution order and retains the original compiled weave when its owner's slot changes. A snapshot of queue length bounds each think; uses caused by replays wait for the following think. Death and despawn clear pending uses. Inactive echo actions retain their queues until resumed or removed; there is no hidden dropping policy.

Summons, movement and deployed constructs are excluded. Explicit construct exclusion prevents replaying the echo's own deployment, which the former first-think counter initialization happened to skip. The first attempted implementation exposed this case and its failed log is retained. Single-replay random streams remain unchanged; additional uses in one think receive distinct deterministic streams derived from their owner sequence.

Seven new regression cases cover the original burst (40 paid uses, now 40 replays), three instant uses before the first think, removal/replacement of the slot, birth after earlier uses, independent queues for multiple echoes, replacement without inherited history, and echo/owner death or removal. The existing normal-speed, double-speed and totem scenario trace assertions also pass unchanged. Initial compile errors in the test's imports and the deployment-recursion failure are retained in the local evidence.

The first full integrated run exposed #47's new frozen-axis census: the existing `snare_blast` status chance of 0.3 was absent from the baseline. The fixture gains exactly `form snare_blast effects[0].effects[1].chance`, matching the already documented copied blast graph. No gameplay number changes. Lint reports 562 smells and 197 notes: against the accepted axes census, the two existing snare findings are added and the former EchoOwner filter key disappears because that unchanged exclusion policy moved into the queue writer. This does not resolve the remaining hardcoded tag vocabulary; the moved policy additionally excludes constructs and remains review debt.

Queue-only functional verification: clean Release build, 756 core tests, 52 server tests and 119 client tests, all 927 passing with no skips. After the census-only fixture correction, the complete core suite was rerun; server and client results are from the same runtime build. Content validation is 0/0; origin load and `fight wolf hob` complete; all seven standard tournament hashes match the committed main baseline. Raw logs use `artifacts/echo-queue-*`.

## Combined release-policy verification

#20 source `85fa5b5` is integrated in `a39a890`, with the follow-up correction `e4e620f`. The final free-resolution context has the owner as Source and the echo's position as From before calling `ReleaseTargets.Execute`. A replay keeps its captured aim instead of turning toward a body that moved while queued. The stored target remains available to the common policy, including dead-target rejection; a rejected body application raises the common miss event rather than a successful echo cast. Nested presets keep their existing live-target placement behavior.

`ConstructReleasePolicyTests` exercises the real paid runtime and queued echo action. A wall inserted after the owner's resolution stops the replay; an owner moved far away does not stop an in-range echo; moving the echo out of range does stop it; and a body leaving the committed sector receives neither damage nor a rider while a replacement inside receives both. Owner credit and unchanged mana after the replay are asserted. These four cases and the existing construct/release tests pass together (32 cases). The combined full suite passes 861 core, 58 server and 132 client tests, 1,051 total. Build and validation are clean, protocol is current, load/fight complete and all seven hashes match #20's inherited baseline. This is the declared #20 combat baseline, not a claim that #20's intentional changes disappear relative to main.

Source `268ab10` additionally integrates #20's additive allocation instrument `d3982f6`. It leaves gameplay/client code unchanged and preserves the historical whole-process expression counter. Final Release build is clean and all 1,054 solution tests pass (864 core, 58 server, 132 client); validation remains 0/0 and all seven replay hashes remain identical to the inherited baseline. One coordinated Release measurement will be reported for this exact source; no repeated pass-seeking samples are requested. #54's separate Aim property and negative-heal finding remain outside this stack.

Acknowledged by: pending Claude review on PR #50. The owner decision is resolved; the queue and combined release corrections await review and final performance acceptance.

## Final first-result performance sample

The one coordinated default Release run on runtime source `268ab10`, measured at documentation head `f475eaf`, ran at Normal priority on 2026-10-18 after the other owners confirmed stopped. The invocation was `dotnet src/HollowWake.Tools/bin/Release/net10.0/hollowwake.dll bench --json`. Every row is retained in [the raw result](../../experiments/combat-grammar/review/construct-final-bench.json), with [binary provenance](../../experiments/combat-grammar/review/construct-final-bench-preflight.json) and [run metadata](../../experiments/combat-grammar/review/construct-final-bench-run.json). No repeat was taken.

| Measure | First result | Budget / assessment |
|---|---:|---|
| Tick p95 | 13.6169 ms | 25 ms, passes |
| Tick allocation | 70.470391 KB | 2 MB, passes |
| Behavior | 2.905159 ms / 22.4 KB | 3 ms / 1 MB, passes with limited time headroom |
| Snapshot build | 1.014923 ms | 1 ms, **misses** |
| Combined snapshot build | 1.159832 ms | same 1 ms, **misses** |
| Full snapshot | 29.658721 KB | 30 KB; 0.341279 KB headroom |
| Delta snapshot | 9.326582 KB | 10 KB; 0.673418 KB headroom |
| Combined snapshot traffic | 8.448813 KB | 10 KB, passes |
| Chunk | 0.728134 ms / 14.316284 KB | 4 ms / 400 KB, passes |
| Damage fold | 368.5 ns / 0.1344 B | 500 ns / 200 B, passes |
| Expression time | 113.737 ns | 150 ns, passes |
| Expression legacy process allocation | 0.0168 B | retained nonzero diagnostic |
| Expression fixed calling-thread / companion process sample | 0 / 0 B | warmed small-call instrument; measurement-contract clarification awaits Claude |
| Zone planning | 19.7213 ms | reported; no entity-stack planning target imported |
| System errors | 0 | passes |

Forecasts observed 1,852 active casts; their incremental build was 0.2951595 ms, with 1.284521 KB traffic and 8.913438 KB allocation. The combined group includes actual entity/interface/forecast encoding; sequential group averages are not added together. The default workload includes no constructs, so it does not certify construct-heavy scaling or the retained-queue memory bound under indefinitely inactive actions.

**Performance acceptance remains open.** Both snapshot time misses are retained, with no change to the 1 ms budget. The forecasting owner is investigating the common measurement and snapshot path; no branch-specific cause or contention explanation has been established. Functional and hosted checks pass, but this request stays draft for that issue and Claude review of Codex's corrections.

## Paid-device release correction

Further independent testing at `cc0e1d4` found that `Run` built the owner effect context before checking a paid device use's release policy. This read the owner's range instead of the device's and discarded the device radius. Two timed-use cases failed in opposite directions: device range 12 / owner range 2 missed a target eight units away, while device range 2 / owner range 12 hit it. The historical measurements are retained in [construct-use-range-failure.json](../../experiments/combat-grammar/review/construct-use-range-failure.json); the original failed test log is retained locally.

The correction checks a context sourced from the physical caster first, so the registered policy sees that caster's stats, body, conditions and miss attribution. After acceptance, an inheriting device creates the owner effect context at the device's position. Both contexts retain the same cast identity, random stream, variables and owner origin; an ordinary caster still creates one context and dictionary. Free echo replay is unchanged and continues to check an owner use from the echo's location.

Five permanent cases cover both range directions, inclusion of the device radius, both target-condition directions, owner payload stats/credit and device miss attribution. The targeted construct run passes 31 cases. Final combined gates and performance evidence are refreshed separately after integrating approved #54; earlier performance rows remain evidence for their named source, not measurements of this correction.

## Final device and morph integration

Correction `e8bc863` and approved #54 through main `17ca2f1` are combined at `a7a08f1`. The merge preserves the new `EffectContext.Aim` through its existing copy paths, the final-fold lethal-resource guard, the shared probe bindings and capacity-before-birth behavior. The device gate uses the device context; its accepted effects and queued echo replay retain owner credit.

Independent final gates on this source: Release build 0 warnings/errors; **1,081 .NET tests** (883 core, 58 server, 140 client), no skips; **344 web tests**; **48 deep morph cases at 128 points**; validation 0/0; load and wolf/hob duel; current generated protocol; all seven Windows hashes identical to the inherited #20 baseline. The private background run with `--forecasts --cast-cues --calling kindler` passes **33 browser and 38 native checks**, with no cleanup errors. Browser world and forecast captures were inspected; native pixels remain untested. A private TCP probe successfully negotiated all five supported features and exercised entry, movement and interface state. The initial missing browser dependency was corrected with the pinned install before the web suite; its failed startup log is retained. Local logs are under `artifacts/construct-morph-*`.

After all other owners confirmed quiet and every local test/server exited, one default Release sample ran at Normal priority on the verified `a7a08f1` executable. [All first rows](../../experiments/combat-grammar/review/construct-device-morph-bench.json) and [source/binary/run metadata](../../experiments/combat-grammar/review/construct-device-morph-bench-metadata.json) are retained. This is the first sample of changed source, not a repeated attempt on `268ab10`; that source's earlier misses remain recorded above.

| Measure | First result on `a7a08f1` | Budget / assessment |
|---|---:|---|
| Tick p95 / allocation | 14.4723 ms / 70.752370 KB | 25 ms / 2 MB, passes |
| Behavior | **3.202574 ms** / 22.4 KB | 3 ms **misses**; 1 MB passes |
| Full snapshot build | 0.909541 ms | 1 ms, passes this sample |
| Combined snapshot build | **1.105977 ms** | 1 ms, **misses** |
| Full / delta traffic | 29.658721 / 9.326582 KB | 30 / 10 KB, limited headroom |
| Combined traffic / allocation | 8.450063 / 52.368203 KB | 10 KB traffic passes |
| Chunk | 0.674927 ms / 14.322998 KB | 4 ms / 400 KB, passes |
| Damage fold | 346.6785 ns / 0.168 B | 500 ns / 200 B, passes |
| Expression time | 120.0667 ns | 150 ns, passes |
| Expression legacy process allocation | 0.02016 B | retained nonzero diagnostic |
| Fixed calling-thread / companion process sample | 0 / 0 B | explicit warmed instrument; #20 measurement acceptance remains separate |
| Zone planning | 18.2885 ms | reported, no entity-stack target imported |
| System errors | 0 | passes |

The 1,852 forecast observations, unchanged full/delta byte counts and every raw row remain visible. No contention, causality or improvement claim is inferred from comparison with a different source's one sample. This workload contains no constructs and cannot certify construct-heavy scaling or an indefinitely inactive echo queue. **Behavior and combined snapshot time remain open budget misses.** No further unchanged-source sample, relaxed budget or performance clearance is claimed. The request remains draft for these issues and Claude's review of the paid-device and queue corrections; retarget it to main before #20's base is merged or deleted.
