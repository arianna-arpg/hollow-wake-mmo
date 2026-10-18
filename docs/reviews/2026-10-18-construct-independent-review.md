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

Request #20's author confirmed on #50 that replay must construct the final context first (`Source` is the owner, `From` is the echo), then call the common `ReleaseTargets.Execute(context, weave.Effects)`. It must not pay another use or add an echo-specific gate. At that historical head the free path still removed dead targets and directly executed effects; the combined corrections below now enforce the shared policy.

Required combined cases are an echo behind a wall, an owner far away with the echo in range and the reverse, a body leaving a committed selector, and damage riders applying to the actual selected victim. They were pending at the initial review and are now covered by the combined integration below. Keep #50 draft until final performance evidence and Claude review are complete. The request was safely retargeted to main before #49 merged as `552dcec`; the new integration depends on #20 and must be retargeted before that dependency's branch is deleted.

## Verification and limits

At the reviewed head, the independent Release build had no warnings or errors and all 776 solution tests passed: core 619, client 114 and server 43. The additional focused review run passed all three interruption cases and failed the high-speed replay assertion as described above. With the three permanent interruption regressions added, the full core suite passed all 622 tests with no skips. Local logs are in the review checkout's ignored `artifacts/`; the small failed-probe log is committed beside its reproduction.

The initial Codex follow-up changed only tests and documentation. The original record's performance measurements contain no constructs and do not establish construct-heavy scaling. Its two composition-lint findings for the instant snare form remain explicitly named.

## Burst replay correction

Main through `e76ecbd` is integrated in `47ba9c8`. Each echo subscribes to its owner's resolutions at birth, selected from its behavior's registered action graph rather than a pattern or weave name. Only those echoes allocate a pending queue. The local `WeaveCast` event carries the actual compiled cast view internally; this is not a protocol field. Each echo drains its own pending uses in resolution order and retains the original compiled weave when its owner's slot changes. A snapshot of queue length bounds each think; uses caused by replays wait for the following think. Death and despawn clear pending uses. At this historical queue-only head inactive actions retained their queues until resumed or removed. The later owner-approved stun/freeze cancellation is documented below.

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

## Echo re-review corrections

Claude's review of `cc0e1d4` (PR comment 6254942459) found further replay defects. The owner answered the stun/freeze choice with **Cancel pending replays** on 2026-10-18. Codex integrated Claude's decision record `e713cf6` before making these corrections.

- Body spending: content now owns `minions.echo_excluded_tags` and `minions.echo_excluded_mechanisms`. The registered `spends_bodies` graph probe covers corpse/minion consumption and corpse summons, including held sections. The shipped `marrowhooks` and a `consume_minion` fixture each spend exactly one body through the owner and produce no echo replay. This closes the moved hardcoded exclusion policy; no weave names are tested by runtime code.
- Incapacity: applying stun/freeze clears queued uses immediately, even if it expires before the next scheduled think; casts during incapacity are discarded. Recovery replays only newly accepted uses. Both flags are tested at 0.1 and 4 seconds. This implements the human answer to the stun/freeze question. A custom behavior that merely stops scheduling its echo action is a separate retained-queue limit; it is not claimed fixed or measured here.
- Payload fidelity: the local cast event retains the actual resolved section and an immutable copy of initial cast variables. Channel pulses/releases, charged and overcharged releases, and guard raise/bash are tested through the paid runtime, including nonzero held/fill/stage/absorbed values and cast-local modifiers. Replay rolls remain independent. Ordinary casts with no cast variables allocate no variable snapshot.
- Identity: every replay allocates a nonzero cast identity. `CastSource` retains the physical echo/device through copied and nested contexts, independently of owner `Source`/`Origin`. Root and selector misses name the echo; two queued selector misses have distinct identities matching their presentation cues. Successful damage and riders still credit the owner.
- Queue safety: permanent cases now assert a replay-triggered owner use waits for the next think, three damageable burst replays use distinct reproducible rolls, and pending payloads are released on replacement or death. Deliberate mutations prove these tests fail if the drain runs to empty, the per-use reseed is removed, or a removed echo retains its queue. Each mutation was reverted before the full gates.
- Documentation: the original record now separates its historical measurements from the current #20 stack and resolved shared-file integration. Eligibility is stated precisely. The historical failed-probe log is sanitized to a worktree placeholder; the original remains in ignored local evidence.

The first valid regression run against the prior runtime had **11 failures and 6 passes**, preserved in local `artifacts/echo-review-before/`. After correction, **48 focused construct cases pass**. The full Release build has **0 warnings/errors**, and **1,098 solution tests pass** (900 core, 58 server, 140 client), with no skips. Content validation is 0/0; load and wolf/hob duel complete; all seven Windows replay hashes match the inherited #20 baseline. Composition lint reports 566 smells and 197 notes; this is a report, not a claim that inherited lint debt is zero. The new eligibility vocabulary lives in content rather than escaping lint by moving directories.

The regression/mutation summary is [construct-echo-review.json](../../experiments/combat-grammar/review/construct-echo-review.json). No performance acceptance is inferred from functional tests. The previous behavior and combined-snapshot misses remain open; a new-source first sample must be coordinated after other owners stop. Claude review of these corrections remains required.

## Paid-device forecast source correction

Forecast review comment 6255062880 found that `DescribeCast` sampled the device's effect stats while `Run` evaluated accepted payloads with owner stats from the device position. `ConstructForecastSourceTests` reproduces both directions on `70ccab3`: owner radius 6/device 2 forecast radius 2; owner radius 2/device 6 forecast radius 6. A non-inheriting control also detected the absent physical `CastSource` on observations. All three initial failures are retained locally in `artifacts/echo-forecast-before/`.

The pure `PayloadOrigin(sim, caster)` helper now supplies the same source and optional device origin to paid execution and active observation. The forecast retains physical caster identity and the device's own cast deadline. Three permanent cases test both inheritance directions and the non-inheriting control, radius, anchor, physical identity, observation event purity, and candidate-versus-actual hit membership. They deliberately give the owner and device different cast speeds as well as different radii.

The combined construct/forecast selection passes 72 cases. Release build is 0/0 and all 1,101 solution tests pass (903 core, 58 server, 140 client); validation is 0/0 and all seven replay hashes remain identical. This is an additive merge with #20's separate body/height/AimOffset work, not a replacement of its current file. Timing is deferred to the final combined candidate; no quiet window remains held. GitHub cannot run PR checks while #50 conflicts with the moving #20 base, so no hosted pass is claimed for these new heads.

Retarget follow-up: #50 now targets main, still draft, so its own hosted checks can run and deletion of the former base cannot close it unexpectedly. The complete corrected #20 dependency remains mandatory before landing; the retarget grants no review or performance acceptance.

## Captured projectile aim correction

Independent queued-projectile tests reproduce a defect on both published #50 `738bad67` and complete #20 `85ee0397`: the owner fires east, the target moves northeast before the echo thinks, and both a bare projectile and an identity aim wrapper turn the replay from 0 to 0.785398 radians. The two moving-target cases fail and two stationary controls pass. The earlier statement that a replay kept its captured aim was therefore incomplete for projectile launch; a retained live target overrode the saved point.

The local cast event now captures the target center, body-relative aim offset and committed direction before payload execution. The queue carries those value fields to `Replay`. Target remains available for live release eligibility and direct-body effects; frozen launch geometry does not replace it. Directional casts preserve their chosen planar direction without reacquiring a target. Other replay shapes retain their existing facing from the echo to the saved point. Applying the recorded direction to every shape initially broke the existing targeted-sector victim/rider test; that failure is retained and the test is unchanged after correcting the distinction.

This increment depends on #20's independently reviewed `4057f79d`, applied here as `20f296b3` after the complete `85ee0397` merge `0a5c8813`. That shared change makes explicit frozen samples authoritative for projectile launch and reads salvo/guide input from physical `CastSource`, falling back to `Source` only when physical identity is absent. No authored homing policy changes. The echo correction edits the event, queue and replay paths; the shared geometry change has its own tests and review.

`ConstructEchoAimSnapshotTests` has 12 cases covering plain/wrapped stationary controls, moved-target heading, actual replacement-body contact and owner credit, movement during the owner's payload, committed direction with sampled elevation, and every later salvo shot after the owner's cursor changes. With capture alone and the previous shared geometry, the corrected fixtures produce eight failures and four passes. With both corrections all 12 pass. Earlier fixture compilation and premature elevation setup failures remain in local evidence and are not counted as runtime regressions.

Final source build is clean (Release, 0 warnings/errors). The full solution run has **2,855 passes and four inherited world-generation golden failures**: core 2,605 passes/four failures, server 66 passes, native-client logic 184 passes. All 63 core construct cases, including the unchanged sector/rider test, pass. The four failures are the existing zone `(3,2)`, `(0,-1)`, `(0,0)` and overlaid-world hashes on this intermediate combined source; their constants are untouched and the final #34 forward integration remains required. Validation is 0/0; load and wolf/hob duel complete; all seven inherited Windows tournament hashes are identical. The first sandbox build could not access the installed Godot SDK; the same source built cleanly with normal SDK/cache access. No new browser smoke or performance sample is claimed for this core-only increment.

The compact [aim review evidence](../../experiments/combat-grammar/review/construct-echo-aim-review.json) names retained runs. Raw logs are under `artifacts/echo-aim-*`. This is an isolated source handoff for combined integration and Claude review, not approval to merge the intermediate branch or clear the historical budget misses.

Acknowledged by: [independent Codex capture-path review of 81b9197](https://github.com/arianna-arpg/hollow-wake-mmo/pull/50#issuecomment-6256013579), with 63 construct cases and 14 additional paid-runtime anatomy, live-target validity and offset-echo direction/sector cases passing. Claude reciprocal review remains pending.
