# Shared cast forecasting: review corrections, 2026-10-18

This supersedes the completion claims in the historical [forecast record](2026-10-17-shared-cast-forecasts.md). PR #20 remains the owned integration request. Approved main 26b9e46 was merged in 3ca56eb; approved tournament main 6e7e241 was merged in 1072b94. Release/approach changes are 415ccb6 and 7bd30a5. The forecasting chat's rider fix 47de147 was integrated as 8ba3f27. No unmerged anatomy, AI-intent or construct branch is included.

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

The integrated Release build has zero warnings/errors; all 905 .NET tests (733 core, 45 server, 127 client) and 317 browser tests pass. Content validates with zero errors/warnings, the zone runs 200 ticks, the live probe grants all negotiated features and receives 75 forecast frames before PROBE OK, and the generated protocol matches. Background/port scripts pass 19 checks. The convergence ledger covers 948 captured skills and 457 supports across 12 families with no unclassified vocabulary, while its partial/pending families remain explicitly incomplete. Final client smoke runs and timing verification are still in progress at this source handoff. Earlier failed measurements remain evidence, never a passing gate. In particular, the pre-approach integrated full snapshot exceeded both the 1 ms and 30 KB limits; after requiring the authored approach range its bytes fell below 30 KB without changing precision, cadence or protocol defaults. Timing variance remains material. The most recent complete default run before final client checks measured behavior 2.334 ms, full snapshots 0.994 ms / 29.659 KB and combined snapshots 1.056 ms / 8.449 KB. The combined time exceeds its 1 ms budget, so this is not a passing performance gate. Approved main in the adjacent normal-priority run also exceeded full snapshot time (1.262 ms); this does not excuse the branch failure or prove its exclusive cause. Incremental forecast allocation is 8.913 KB per player over 1,852 observations. Retained evidence is under experiments/forecast-rendering/evidence/rereview.

The comparison must use the same approved base and tournament staging. Original fight traces under 26b9e46 are retained as historical evidence, separate from 6e7e241's mirrored positions/facing and alternating spawn order. Changed hashes are named and rebaselined only for intentional combat changes, then checked again.

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

## Integration handoffs and review request

- #36: consume its full corrected foundation, not correction 724c5ed alone. Preserve this request's compiled release/miss policy, add authored contact: body to the melee selector, and preserve sampled CastState/EffectContext AimOffset for projectile elevation. Its removed private stored-target gate must not return. Repeat the species/relief matrix on the combined head.
- #49: pass the current target into PreferredRange while preserving its intent-dispatch and movement-plan seam. File overlap was recorded on both requests before edits.
- #50: echo replay resolves the owner's paid use through ReleaseTargets.Execute after its final context is built, from the echo's actual origin. No new cost or echo-specific bypass. Origin/range/wall and selected-recipient regressions belong on the combined head. Handoff: https://github.com/arianna-arpg/hollow-wake-mmo/pull/50#issuecomment-6253135942.

Claude: rereview the compiled release contract, after_damage packet semantics, intentional melee/area composition expansion, approach comparisons and combined snapshot budget. No merge or final visual-production certification is claimed. Headless native evidence covers behavior, not rendered pixels or controllers.

Acknowledged by: pending Claude rereview on PR #20.
