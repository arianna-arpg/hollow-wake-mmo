# Shared cast forecasting: review corrections, 2026-10-18

This supersedes the completion claims in the historical [forecast record](2026-10-17-shared-cast-forecasts.md). PR #20 remains the owned integration request. Approved main 26b9e46 was merged in 3ca56eb; approved tournament main 6e7e241 was merged in 1072b94. Release/approach changes are 415ccb6 and 7bd30a5. The forecasting chat's rider fix 47de147 was integrated as 8ba3f27. Approved entry/grammar main 8365775 was then integrated in 6edc124. Both client capability lists and both AI contract sections were preserved when resolving its three conflicts. No unmerged anatomy, AI-intent or construct branch is included.

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

Final executable source: 6edc124, with approved main 8365775. Evidence lives under [the rereview directory](../../experiments/forecast-rendering/evidence/rereview).

- Release solution build: zero warnings/errors. All 919 .NET tests pass (733 core, 54 server, 132 client); 322 browser tests and the production browser build pass. The generated protocol is current.
- Content validation: zero errors/warnings. Zone load runs 200 ticks; the final fight runs successfully. Standard tournament hashes replay identically. The fixture that replaces a selector with direct damage explicitly authors body validation; its double-knockout assertions are unchanged.
- Refreshed private live probe: forecast/cue/delta/interface negotiation succeeds and ends PROBE OK. Background/port scripts pass 19 checks.
- Combined entry/forecast smoke: melee 32/32 browser and 37/37 native; spell/cues 33/33 browser and 38/38 native. Each run reports no cleanup errors, real damage and forecast reception; native also verifies return to Mu. Browser images were inspected: the full canvas and primary circle/sector boundaries are visible. Crowded labels still overlap in the placeholder scene; this is functional evidence, not final readability or art acceptance. Native headless excludes rendered pixels and controllers.
- Convergence audit: 948 captured skills and 457 supports across 12 families, with no unclassified vocabulary; partial/pending mechanisms remain explicitly incomplete.
- Cloud CI at 6edc124 passes build/test, Docker, launcher, web build and the required aggregate check (run 39153347649). Final evidence-only commit checks are recorded on the pull request.

Earlier source-handoff checks and failed measurements remain labeled in the evidence directory. The intermediate full snapshot exceeded both 1 ms and 30 KB; requiring the authored approach distance restored traffic below 30 KB without changing precision, cadence or protocol defaults. The final timing gate still fails as detailed below. The earlier 26b9e46 fight staging is kept separate from the mirrored 6e7e241 tournament staging.

### Final coordinated performance window

After the hitbox team completed its run, all participating engineering chats held heavy local work. Both owned benchmarks ran serially, at normal priority, with the same full Release command and no config or budget changes. Exact inputs are approved main 8365775 and source 6edc124. Both processes exited before the shared window was released. The full outputs are baseline-window-836.txt and bench-window-6ed.txt in the evidence directory.

| Measure | Approved main | Revised source | Existing limit |
|---|---:|---:|---:|
| Tick p95 | 18.223 ms | 16.141 ms | 25 ms |
| Behavior | 3.379 ms | 3.272 ms | 3 ms |
| Full snapshot | 1.488 ms / 27.716 KB | 0.970 ms / 29.659 KB | 1 ms / 30 KB |
| Delta traffic | 8.550 KB | 9.327 KB | 10 KB |
| Combined snapshot | not instrumented on main | 1.085 ms / 8.449 KB | 1 ms / 10 KB |
| Tick allocation | 68.225 KB | 71.096 KB | 2 MB |

The branch still fails behavior and combined snapshot time. Zero simulation errors. Incremental forecasts cost 0.247 ms and 8.913 KB allocated per player over 1,852 observations. The branch passes the remaining named default budgets; main also exceeds its attribute-fold and expression-time limits in this window. Control failures neither waive the branch limits nor establish an exclusive cause. Earlier complete/intermediate samples remain recorded. No further repeated sampling was used to select a passing result. PR #20 remains draft for performance work and Claude rereview, despite its functional/CI passes.

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

Core change 31c2546 integrates the forecasting chat's 57b8a1f: constant leaf attributes no longer insert/remove recursion-path entries, while live purity, flags, dependency depth, modifiers, gauges, links and cycles remain checked per observation. The already obtained slot cache is reused inside one PreferredRange iteration. Its [handoff](2026-10-18-forecast-dependency-optimization.md) records 103 focused and 930 full tests on that isolated source. No safety result is retained between observations.

The pre-optimization control is published 2cc5d66 (executable source 6edc124), built in an owned disposable worktree. The combined candidate is 31c2546. New timing claims await the coordinated comparison. Earlier failed measurements above remain acceptance blockers until a concrete revised source passes the existing budgets. A prospective merge with approved main 82e3a48 is clean and retains all three target-aware PreferredRange calls from this request; that inspection does not claim runtime verification of the combined tree.

## Integration handoffs and review request

- #36: consume its full corrected foundation, not correction 724c5ed alone. Preserve this request's compiled release/miss policy, author contact: body consistently on all three selectors introduced here (strike, measured_strike and bash), and preserve sampled CastState/EffectContext AimOffset for projectile elevation. The corrected full entity dependency is now #34 at 26c50de, including #36 through e383ffb; it remains a separately reviewed draft. Its removed private stored-target gate must not return. Repeat the species/relief matrix on the combined head and verify forecast viewer eligibility through the same resulting contact predicate.
- #49: pass the current target into PreferredRange while preserving its intent-dispatch and movement-plan seam. File overlap was recorded on both requests before edits.
- #50: echo replay resolves the owner's paid use through ReleaseTargets.Execute after its final context is built, from the echo's actual origin. No new cost or echo-specific bypass. Origin/range/wall and selected-recipient regressions belong on the combined head. Also verify that proxy misses close the same physical caster/cast identity that started its presentation, independently of owner attribution for stats and damage; the context origin alone is not that identity. Handoff: https://github.com/arianna-arpg/hollow-wake-mmo/pull/50#issuecomment-6253135942.

Claude: rereview the compiled release contract, after_damage packet semantics, intentional melee/area composition expansion, approach comparisons and combined snapshot budget. No merge or final visual-production certification is claimed. Headless native evidence covers behavior, not rendered pixels or controllers.

Acknowledged by: pending Claude rereview on PR #20.
