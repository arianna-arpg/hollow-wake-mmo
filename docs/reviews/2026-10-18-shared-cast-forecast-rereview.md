# Shared cast forecasting: review corrections, 2026-10-18

This supersedes the completion claims in the historical [forecast record](2026-10-17-shared-cast-forecasts.md). PR #20 remains the owned integration request. Approved main 26b9e46 was merged in 3ca56eb; approved tournament main 6e7e241 was merged in 1072b94. Release/approach changes are 415ccb6 and 7bd30a5. The forecasting chat's rider fix 47de147 was integrated as 8ba3f27. Approved entry/grammar main 8365775 was then integrated in 6edc124. Both client capability lists and both AI contract sections were preserved when resolving its three conflicts. Approved AI-intent/morph main 82e3a48 was integrated in 13671b5, preserving all three target-aware PreferredRange calls. No unmerged anatomy or construct branch is included.

The owner chose committed, dodgeable swings with better pre-commit approach and positioning. Aim, collision reach and wall blocking do not gain forgiveness after commitment. The engine contains no spell-name dispatch.

## Claude findings addressed

- Release policy is a compiled block, selected by first active glyph, preset, then form. Registered body and delivery policies replace the old bypass strings. The delivery policy admits graphs only when every root declares the shared LOS-checking selector capability. Arbitrary root siblings and free-occlusion selectors fail compilation. Pattern-inline compositions now pass the same validator.
- Body checks use the final EffectContext and its actual From origin, configured height tolerance, target-kind/requirement rules, relation, range, untargetability and LOS. Explicit corpse target kinds remain valid; ordinary dead bodies are refused. Delivery selection can retain a dead aim as a location without making it a damage recipient. Nested placements therefore do not measure reach from a distant owner.
- Rending, Felling and Furious use the registered after_damage hook on the actual positive-damage recipient, including direct-damage forms such as Eviscerate. Added hooks do not recursively hook their own damage. Reaping already used on_kill and remains there. See the [forecasting chat handoff](2026-10-18-melee-rider-handoff.md).
- Echoed and later cascade placements suppress duplicate primary miss cues. Root refusal uses the common outcome flag and actual origin. Instant emissions retain their cast identity before effects execute.
- Cheap minimum-range and ordinary busy rejection precede region/LOS work; instant reflex and converted/combo faces retain their normal paths. Public delivery providers keep detached lifetimes.
- ai.anticipation uses the normal modifier fold. Opted-in immediate area choices must first attain their authored preferred range, including when a retreating caster briefly stops to cast, then test observed motion over the real windup. Chase also reduces stopping distance by outward travel. Unknown providers keep their prior fallback; the policy does not promise to catch a faster opponent.
- Forecast observation allocations are reduced through shared empty attribute sets and reused server selection buffers. Exact geometry, visibility, per-viewer eligibility, retained revisions and expiry remain checked. Combined entity/interface/forecast encoding is measured against the existing snapshot budgets; no extra forecast allowance is added.

## Compatibility and coverage

A selector's area/sector mechanisms admit Ring, Aftershock, Cascading, Crescent Sigil, Sweeping and the Wide chassis where their normal predicates fit. Hexing's on_hit payload now has a selector hook, and the cosmetic area_release recipe can match melee. This is a real content-composition expansion, not unchanged balance.

Damage riders now run per positive damage packet, rather than once beside the root graph. Multiple original damage nodes can each run riders; empty selections, evades and zero-damage packets cannot. Existing kill-hook ordering/depth is retained. The shared proc budget is preserved through the authored payload_depth: same selector policy.

A refused root release retains the paid use/cooldown but emits no WeaveCast, so cast quests/triggers/minion listeners do not count it; a selector whiff still counts as an emitted cast. Refused held releases have Fired=false. Live target.require conditions on Verdict/Eviscerate can expire during windup. Negotiated forecast panels replace legacy label progress. The contracts describe these changes.

All active casts have timing. Accurate primary candidate regions are inherited through supported compiled delivery providers and modifiers. Arbitrary wrappers, future random branches, prospective projectile paths and every future mechanism are not claimed as covered; unsupported geometry stays unavailable instead of drawing an invented hitbox. Released projectile/field entities retain authoritative shapes. Current area contact still uses body centers; precise body contact is a separate #36 dependency, not silently claimed here.

The historical HitStage golden keeps its original expectation and explicitly sets anticipation to zero to isolate the old damage pipeline. Default approach has separate moving-target, brief-stop and speed regressions plus actual duel comparisons. No production setting is changed to preserve a golden.

## Verification and measurements

The following completed client/entry checks describe executable source 6edc124, with approved main 8365775. Later cost and AI-main integration gates are recorded below. Evidence lives under [the rereview directory](../../experiments/forecast-rendering/evidence/rereview).

- Release solution build: zero warnings/errors. All 919 .NET tests pass (733 core, 54 server, 132 client); 322 browser tests and the production browser build pass. The generated protocol is current.
- Content validation: zero errors/warnings. Zone load runs 200 ticks; the final fight runs successfully. Standard tournament hashes replay identically. The fixture that replaces a selector with direct damage explicitly authors body validation; its double-knockout assertions are unchanged.
- Refreshed private live probe: forecast/cue/delta/interface negotiation succeeds and ends PROBE OK. Background/port scripts pass 19 checks.
- Combined entry/forecast smoke: melee 32/32 browser and 37/37 native; spell/cues 33/33 browser and 38/38 native. Each run reports no cleanup errors, real damage and forecast reception; native also verifies return to Mu. Browser images were inspected: the full canvas and primary circle/sector boundaries are visible. Crowded labels still overlap in the placeholder scene; this is functional evidence, not final readability or art acceptance. Native headless excludes rendered pixels and controllers.
- Convergence audit: 948 captured skills and 457 supports across 12 families, with no unclassified vocabulary; partial/pending mechanisms remain explicitly incomplete.
- Cloud CI at 6edc124 passes build/test, Docker, launcher, web build and the required aggregate check (run 39153347649). Final evidence-only commit checks are recorded on the pull request.

Earlier source-handoff checks and failed measurements remain labeled in the evidence directory. The intermediate full snapshot exceeded both 1 ms and 30 KB; requiring the authored approach distance restored traffic below 30 KB without changing precision, cadence or protocol defaults. The earlier 26b9e46 fight staging is kept separate from the mirrored 6e7e241 tournament staging.

### Earlier coordinated performance window

After the hitbox team completed its run, all participating engineering chats held heavy local work. Both owned benchmarks ran serially, at normal priority, with the same full Release command and no config or budget changes. Exact inputs are approved main 8365775 and source 6edc124. Both processes exited before the shared window was released. The full outputs are baseline-window-836.txt and bench-window-6ed.txt in the evidence directory.

| Measure | Approved main | Revised source | Existing limit |
|---|---:|---:|---:|
| Tick p95 | 18.223 ms | 16.141 ms | 25 ms |
| Behavior | 3.379 ms | 3.272 ms | 3 ms |
| Full snapshot | 1.488 ms / 27.716 KB | 0.970 ms / 29.659 KB | 1 ms / 30 KB |
| Delta traffic | 8.550 KB | 9.327 KB | 10 KB |
| Combined snapshot | not instrumented on main | 1.085 ms / 8.449 KB | 1 ms / 10 KB |
| Tick allocation | 68.225 KB | 71.096 KB | 2 MB |

That source failed behavior and combined snapshot time. Zero simulation errors. Incremental forecasts cost 0.247 ms and 8.913 KB allocated per player over 1,852 observations. Main also exceeded its attribute-fold and expression-time limits in this window. Control failures neither waive the branch limits nor establish an exclusive cause. Earlier complete/intermediate samples remain recorded. The later comparison follows concrete source changes, not repeated sampling to select a passing result. PR #20 remains draft for final gates and Claude rereview.

### Same-base tournament comparison

Ten seeds per pair, 60-second cap, win-x64, using 6e7e241 staging. The regenerated baseline changes wolf:hob, loosed_skirmisher:hob, loosed_skirmisher:boar, loosed_kindler:boar, tide_channeler:spark_duelist and proving_reaver:proving_bulwark. loosed_skirmisher:tide_caller remains byte-identical by trace hash. Hashes include cast identity/mechanisms as well as outcomes; they are not a damage-only checksum. Replaying the new baseline reports identical.

| Pair | Main win rate A/B | Revised win rate A/B | Main/revised median seconds |
|---|---|---|---|
| wolf / hob | 1/0 | 1/0 | 14.8/15.05 |
| loosed_skirmisher / hob | 1/0 | 1/0 | 5.85/5.8 |
| loosed_skirmisher / boar | 1/0 | 1/0 | 7.6/7.55 |
| loosed_kindler / boar | 1/0 | 1/0 | 6.7/6.65 |
| tide_channeler / spark_duelist | 1/0 | 1/0 | 8/8 |
| proving_reaver / proving_bulwark | 0/1 | 0.1/0.9 | 36.45/33.2 |
| loosed_skirmisher / tide_caller | 0.9/0.1 | 0.9/0.1 | 7.5/7.5 |

The proving-band run fails on both approved main and this branch, with the same four pairings over the win-rate ceiling. No band was relaxed; these are inherited balance findings rather than a passing balance certification. The reaver/bulwark rate changes from 0/1 to 0.1/0.9, still above the ceiling for the bulwark. The revised after_damage policy also changes the damage timing; it is intentionally included in this comparison.

In the older 26b9e46 staging, the intermediate Spark Duelist/Kindler trace had five out-of-reach strikes and no duelist damage. Requiring the authored approach distance produces four landed strikes and their follow-up damage; Kindler ends 49/112 instead of 112/112 after 20 seconds. This is diagnostic evidence for the brief-stop fix, separate from the new staging table above. It does not promise the old main outcome of 29/112 or eliminate successful retreat.

## Bounded cost follow-up

Snapshot change 7ec0529 removes repeated delta comparisons for viewers with the exact same captured previous state. It retains only one history per entity in the existing tick/precision lifetime, recomputes divergent histories, and clears on explicit invalidation. The shared messages are read-only after capture. Wire fields, precision, repair cadences, interest, cast timing and combat results are unchanged. Four new history regressions and all 58 server tests pass. SnapshotBuilder.cs and the networking contract overlap were announced on #36, #49 and #50 before integration; their body-volume and intent semantics remain separate.

Core experiment 31c2546 integrated the forecasting chat's 57b8a1f. Its [handoff](2026-10-18-forecast-dependency-optimization.md) records 103 focused and 930 full tests on that isolated source. Lazy recursion-path writes were removed in fb23c4f after the comparison below failed to establish an AI benefit. The original dependency walk is restored. The eleven live-safety regressions and the already obtained slot-cache reuse within one PreferredRange iteration remain. No safety result is retained between observations.

The pre-optimization control 2cc5d66 and candidate both received the same approved main 82e3a48 before measurement: control d562f1b versus candidate 13671b5. The only runtime differences were SnapshotBuilder, lazy dependency tracking and PreferredRange slot-cache reuse. Both builds were clean, then one full Release benchmark per head ran serially at Normal priority in the coordinator's window. Raw cost-control/cost-candidate bench JSON, build logs and environment records are retained. Both processes exited before other functional work was released.

| Measure | Control d562f1b | Initial candidate 13671b5 | Limit |
|---|---:|---:|---:|
| Behavior | 2.271896 ms | 2.794096 ms | 3 ms |
| Combined snapshot | 0.874679 ms | 0.774513 ms | 1 ms |
| Combined allocation | 60.464609 KB | 52.303867 KB | diagnostic |
| Full traffic | 29.658721 KB | 29.658721 KB | 30 KB |
| Delta traffic | 9.326582 KB | 9.326582 KB | 10 KB |
| Combined traffic | 8.448813 KB | 8.448813 KB | 10 KB |

All snapshot traffic, active forecast observation counts and end-entity counts match. Behavior is slower in this sample, so no causal AI speedup is claimed; the speculative path-tracking change was removed. Both samples report expression allocation 0.0168 B/evaluation rather than literal zero. That inherited process-wide reading is retained, not rounded away or called a zero-allocation pass. Final source timing after the removal is recorded below.

### Approved main fixture compatibility

Hosted run 39155313653 exposed six failures in the automatic merge with the new main, separately from performance. Four historical movement checks recorded direct-target Strike before positional melee/anticipation changed when actors stop to attack. 97ffd12 gives only those scenarios an isolated registry with their original Strike graph and hint, plus the same hostile fixture factions. The original trace files and tolerances are untouched; all twelve AI-intent/trajectory tests pass. Production content remains positional and its current approach/dodge tests remain enabled.

Two morph sensitivity checks added on main incorrectly expected nearest and aimed_nearest to differ when the area press has no target, or the chain's original aimed target has already been struck and is excluded from later hops. Their existing Alike mechanism now declares these specific semantic equivalences and fails if they diverge. PositionalReleaseTests still verifies that an eligible aimed body wins over a closer body. A first incomplete historical-registry attempt omitted hostile factions and failed; its output is retained alongside the corrected twelve-test result.

Source 97ffd12 builds without warnings/errors and passes all 994 .NET tests (804 core, 58 server, 132 client). Content validates 0/0, zone load runs 200 ticks, the proving fight runs, protocol generation is current, and the standard tournament hashes replay identically without regeneration. The private forecast/delta/interface probe finishes PROBE OK and its server exits 0. Its earlier helper attempts failed on Windows log sharing, an initially empty log, and an overlong test account name; all owned servers stopped cleanly and no production change was made to pass the probe. The final performance comparison follows below.

### Tournament-format and entry follow-up integration

Approved main 0a43d97 (#51 and #52) is merged in 1c59d61. The single conflict was the Windows tournament baseline. #52 explicitly widens float trace inputs to double before hundredth formatting and canonicalizes rounded negative zero; it changes the trace representation, not combat resolution. The preserved before/after standard reports compare equal in every JSON field except trace Hash, recursively, across all seven pairs and ten seeds. Only loosed_skirmisher:hob, loosed_skirmisher:boar and loosed_skirmisher:tide_caller change their hashes. `trace52-comparison.json` retains the exact old/new values. The baseline was then regenerated on win-x64 and checked successfully; incoming main's unrelated combat hashes were not substituted.

Source 1c59d61 passes a clean Release solution build and all 1,012 .NET tests (822 core, 58 server, 132 client). The browser's 322 tests and production build pass. Evidence uses the trace52 prefix; earlier cost files remain tied to 97ffd12. The matched pre-optimization control also received 0a43d97 as 271e82d, builds cleanly, and replays the same final baseline identically. Its only runtime differences from 1c59d61 are SnapshotBuilder delta reuse and the single PreferredRange cache reuse. Neither #47 nor the later #54 aim-context proposal is included in these measured sources.

### Final matched performance window

After #31's measurement finished, the coordinator explicitly granted one serial full Release pair, with other participating owners quiet. The already-built control 271e82d and candidate 1c59d61 each ran once at Normal priority. Both exited 0 before the window was released. Raw cost-final bench/environment/comparison files retain every row. No budget, workload, precision, cadence or protocol setting changed.

| Measure | Control | Candidate | Existing limit |
|---|---:|---:|---:|
| Tick p95 | 10.224300 ms | 10.282400 ms | 25 ms |
| Behavior | 2.265436 ms | 2.271492 ms | 3 ms |
| Full snapshot | 0.749739 ms | 0.755977 ms | 1 ms |
| Delta snapshot | 0.641659 ms | 0.558974 ms | 1 ms |
| Interface snapshot | 0.756417 ms | 0.734132 ms | 1 ms |
| Combined snapshot | 0.814359 ms | 0.763977 ms | 1 ms |
| Full traffic | 29.658721 KB | 29.658721 KB | 30 KB |
| Delta traffic | 9.326582 KB | 9.326582 KB | 10 KB |
| Combined traffic | 8.448813 KB | 8.448813 KB | 10 KB |
| Combined allocation | 60.464609 KB | 52.303867 KB | diagnostic |
| Expression allocation | 0.016800 B/eval | 0.013920 B/eval | zero |

Every timing and nonzero allocation/traffic ceiling passes in this sample. The literal-zero expression-allocation gate does not. These are the unmodified benchmark's process-wide counters, while the existing steady-state current-thread regression passes; that distinction is not a waiver or a proven attribution of the residual bytes. Keep it open for Claude review. No causal AI speedup is claimed. Snapshot allocation falls by about 13.5%; traffic, 1,852 forecast observations and end-entity counts match exactly, with zero simulation errors. This is the default workload on this development machine, not crowd, minimum-hardware or visual-production certification.

### Axes census integration

The hosted automatic merge of 1c59d61 with subsequently approved main e76ecbd (#47) passed Windows tournament replay but exposed two new audit failures. Approved main is now merged in 666bdb3. AxisBindings declares the selector's existing report_miss flag without a gameplay axis, its payload_depth choice under causality, and damage.after_damage as an effect-list hook. The signature census explicitly retains report_miss as an outcome-reporting flag; this is a new declared flag, not a claim that all grammar flags have been eliminated. The frozen-literal inventory adds exactly the three already-authored single-recipient max_targets values on bash, measured_strike and strike, and moves Furious/Rending's existing numeric entries to their after_damage hook paths. Those target caps remain content-editable constants, not newly modifier-driven quantities; the audit must accurately record that limitation. No production content, combat rule, trace expectation or test tolerance changes in this correction. Raw failed hosted output remains in trace52-cloud-failed.txt; the first local correction still lacked the hook and flag inventory entry, retained in axes-tests.txt. All 21 reporting/provenance/signature checks now pass. Final functional results follow below.

### Final functional gates

Source e614bb3 builds the complete Release solution with zero warnings/errors and passes all 1,033 .NET tests (843 core, 58 server, 132 client). Content validation reports 0 errors and 0 warnings; load completes 200 ticks; the proving fight completes; the standard win-x64 tournament baseline reports identical; generated protocol is current. The private TCP probe negotiates forecasts, deltas and interface state, finishes PROBE OK, and its server exits 0 after lease removal. These retained outputs use the final prefix. Browser source is unchanged since the trace52 run of 322 tests and production build.

The fresh `test-background.mjs --suite all --calling kindler --forecasts --cast-cues` run, run-Yb6fyj, passes 33/33 browser and 38/38 native checks, with real damage, lifecycle and primary candidate reception, native return to Mu, and no cleanup errors. The harness rebuilt both clients and its private server. Inspected browser captures show circle/sector boundaries and cast panels on the full canvas; foliage and crowded labels still limit final readability. PNGs remain in the ignored local run folder; the committed inspection record names their hashes. Native headless explicitly skips rendered pixels and controllers. Source e614bb3 passes hosted CI run 39157797383, including Linux build/tests, required Windows deterministic replay, launcher, Docker and aggregate ci; the separate Pages build also passes.

The forecasting chat acknowledged the final integration: its rider fix and eleven safety regressions remain, speculative dependency tracking stays removed, and the existing handoff boundaries and zero-allocation finding remain open. This acknowledgment is not Claude approval.

The disposable matched-control checkout was removed only after verifying it was clean and all its measurements had exited and been committed. Its branch still retains exact source 271e82d. The integration worktree and shared-root files were not reset or swept.

### Allocation measurement scope follow-up

Minimal tools-only source d3982f6 preserves the historical expression.alloc loop and whole-process counter, labeling that scope in its note. It adds expression.alloc.calling_thread and expression.alloc.sample_process over one separate synchronous sample. The exact sample delegate runs forty fixed warmup loops, then one measured loop of 100,000 evaluations. There is no measured-loop retry, minimum selection or estimated-overhead subtraction. Both new counters are retained, including any residual runtime activity; neither counter retroactively changes the older 0.01392/0.0168 readings.

The measurement helper's three controls prove that it invokes work exactly once, counts an escaping managed allocation on the calling thread, and includes a worker's allocation in the process counter without charging that array to the caller. This does not identify the precise source of every historical residual byte. Its contract explicitly excludes certification of custom/asynchronous functions that allocate elsewhere. Fixed warmup addresses the runtime's transition to steady state rather than changing the literal zero small-call budget.

The complete Release build is clean and all 1,036 .NET tests pass (846 core, 58 server, 132 client). Hosted CI run 39159109358 also passes, including the required Windows replay. The earlier client and live-probe evidence still applies to unchanged gameplay/client code.

After the coordinator's explicit START, one Normal-priority `bench --only expressions --json` invocation ran on the already-built d3982f6 source and exited 0. No retry occurred. The first results are:

| Measure | Result |
|---|---:|
| expression.evaluate | 95.3153 ns/eval |
| expression.alloc, retained legacy process scope | 0.01392 B/eval |
| expression.alloc.calling_thread, fixed warmed sample | 0 B/eval |
| expression.alloc.sample_process, companion to that sample | 0 B/eval |
| expression.parse | 290.595 ns/parse |

The literal zero warmed small-call measurement is verified, with the unchanged 150 ns evaluation ceiling also met. The original nonzero process value is preserved. The new sample differs from the old in warmup as well as scope; this result does not identify the historical residual bytes' exact source or establish that they all came from a different thread. No numeric budget was relaxed. Raw allocation-scopes JSON/environment records, the three instrument controls and complete local/hosted gates are retained. All owned processes stopped before the next owner's performance window. Claude review of this explicit measurement clarification and the full forecasting change remains pending.

## Integration handoffs and review request

PR #50 is now a dependent draft at bb22d0f, based on codex/shared-cast-forecasts-integration (verified on GitHub). The coordinator reports its combined release cases and 1,054 tests pass; that branch's evidence owns those claims. **Retarget #50 to main before merging #20 and deleting its branch**, as required by the collaboration contract. No merge is performed or authorized by this record.

- #36: consume its full corrected foundation, not correction 724c5ed alone. Preserve this request's compiled release/miss policy, author contact: body consistently on all three selectors introduced here (strike, measured_strike and bash), and preserve sampled CastState/EffectContext AimOffset for projectile elevation. The corrected full entity dependency is now #34 at 26c50de, including #36 through e383ffb; it remains a separately reviewed draft. Its removed private stored-target gate must not return. Repeat the species/relief matrix on the combined head and verify forecast viewer eligibility through the same resulting contact predicate.
- #49: pass the current target into PreferredRange while preserving its intent-dispatch and movement-plan seam. File overlap was recorded on both requests before edits.
- #50: echo replay resolves the owner's paid use through ReleaseTargets.Execute after its final context is built, from the echo's actual origin. No new cost or echo-specific bypass. Origin/range/wall and selected-recipient regressions belong on the combined head. Also verify that proxy misses close the same physical caster/cast identity that started its presentation, independently of owner attribution for stats and damage; the context origin alone is not that identity. Handoff: https://github.com/arianna-arpg/hollow-wake-mmo/pull/50#issuecomment-6253135942.
- #54: preserve its proposed original-cast EffectContext.Aim separately from the current delivery Point when that reviewed change is integrated. WithTarget and EffectHelpers.At must retain it, while an explicit aim transformation may rotate it. This measured source does not include #54 or claim combined move-anchor coverage; do not introduce a private release-context bypass when reconciling it.

Claude: rereview the compiled release contract, after_damage packet semantics, intentional melee/area composition expansion, approach comparisons and combined snapshot budget. No merge or final visual-production certification is claimed. Headless native evidence covers behavior, not rendered pixels or controllers.

Acknowledged by: pending Claude rereview on PR #20.

## Snapshot attribution diagnostic

The coordinator preserved full snapshot misses on #37 (1.0225075 ms), #40 (1.222392 ms) and #50 (1.014923 ms; combined 1.1598315 ms). Those first results remain failures against the existing 1 ms ceiling. #50's record at cc0e1d4 also retains 29.658720703125 KB full, 9.32658203125 KB delta and 8.4488134765625 KB combined traffic. No budget is relaxed and no unchanged benchmark is repeated to find a passing sample.

Source inspection found the published snapshot row combines entity capture, owner state, optional panels and JSON serialization. Viewers run in order and share per-tick capture caches. Full, delta, interface and combined phases advance different simulation stretches. Comparing their averages alone cannot assign a cause. Between this request's eef14c4 and #50's cc0e1d4, the snapshot builder, capture, serializer and benchmark sources are identical; seven runtime files differ. That narrows the comparison but does not establish either runtime regression or external contention.

The optional `--snapshot-profile` instrument records each viewer's capture and encoding stages, bytes, message/entity-state counts, self cadence, known membership, GC counts and phase wall/process CPU context. The generic extra-stream adapter keeps the collector reusable on branches without forecasts. It invokes existing builders and serializers once in their normal order, preallocates its records, preserves raw samples, and refuses to overwrite existing evidence. The original non-profiled timed loops remain intact. Every summary row in a diagnostic invocation is explicitly labeled; overhead stays in totals and no measured estimate is subtracted. This serves the vision's observability requirement without changing gameplay, protocol or client behavior.

The first parity fixture shared one simulation between ordinary and profiled views. Four cases failed at a later self-state cadence: the first ordinary Self read lazily compiled weave slots before the profiled view's initial fingerprint. The fixture now uses independent identically seeded worlds to preserve equal read histories. No production builder was changed to accommodate it. All eight instrument cases pass, covering exact ordered payloads and encoded bytes, view counters and membership, self cadence, interface ordering, one extra-stream invocation per frame, unchanged visible simulation state, output accounting, invalid options and preservation of existing evidence. The focused TRX is retained under artifacts/forecast-rereview/snapshot-profile-tests; complete final gate and diagnostic evidence follow when available.

The final Release solution build has zero warnings/errors, all 1,044 .NET tests pass (854 core, 58 server, 132 client), and content validation reports zero errors/warnings. Complete console logs are published in experiments/forecast-rendering/evidence/rereview/snapshot-profile-{build,tests,validate}.txt. The shared TRX filename was overwritten between successful test projects; the complete console log preserves all three results, and the separate focused instrument TRX remains intact. Earlier client smoke, live probe and replay evidence applies to the unchanged runtime/client sources. A single fixed-source tick-only diagnostic is prepared for a coordinated quiet window; none has run at this checkpoint.
