# Combat AI: constructs act through the runtime

Date: 2026-10-18. Original author: the combat session (Claude); subsequent corrections: Codex. Branch `claude/combat-ai-constructs`, targeting main with an outstanding #20 dependency and main through `17ca2f1` integrated. The original intent-seam base `87ad953` and measurements below are historical. Current correction and validation evidence is in [the independent review](2026-10-18-construct-independent-review.md).

## What changed

- **A device's cast is a real use on its own clock.** `construct_cast` and `spring` begin a ready weave by `UseWeaveIntent` through the seam, so its uses, charges, gates, target, cooldown and cast time apply and its cost is paid through the ordinary costs. A device begins each weave at most once per `minions.construct_interval`, a clock it keeps on its behavior (`Behavior.PacedUntil`), as the hand-written cooldown did before.
- **Devices are free of cost, not of uses, charges or gates.** The `device` pattern carries a `cost` modifier of 100% less, so every device cast pays a cost folded to nothing, as data the cost fold reads; a device pattern without the modifier pays from its own pools.
- **The stat split.** A construct that inherits its caster casts with its owner as the origin (`WeaveRuntime.OriginOf`, read by `Run` and the preset path): the effects run as the owner, so their numbers are the owner's and kills credit the owner, from where the construct stands; the reach, the cast time and the cooldown are the construct's own; and the cast is the construct's, which is paid and validated and named by `WeaveCast`.
- **An echo resolves its owner's use.** An echo's replay is not a new use but a resolution of the use its owner paid for, so it is no intent: it runs on the runtime's free path (`WeaveRuntime.Replay`, using the common release policy), free of cost and cooldown and with no cast time, as the owner at the echo's `damage_scale`, from where the echo stands, on the echo's own stream as before. Only eligible resolved uses are replayed: content excludes summons, constructs, movement and body spending. The captured effect section and cast variables are retained; incapacitation cancels pending replays. The earlier draft's copies of the owner's weaves in the echo's book (`Mirror`) are retired. The replay is a world rule, a service call, named in `docs/design/ai.md` beside the leash and the spent trap.
- **The trap stays instant, by a form of its own.** The review asked for an attribute the preset sets if the runtime reads the cast time through `cast.time`. It does not for the blast: the blast's cast time is the number 0.9 on its form, read from the form, and no attribute a preset sets changes it (only the shells read `weave.cast.time`). So the snare's burst casts through `snare_blast`, the blast with no cast time and nothing else changed (the same costs, attributes, mechanisms, tags and graph, default thread and glyph limits, no drop weight); its compiled tags differ from a blast's only in the cast scope (`cast.instant` for `cast.timed`) and the form tag, which no content scopes by. A trap that springs goes once its cast is over, at once for this one.

## Measured

The review's totem and echo probes are now tests (`ConstructScenarioTests`): the owner casting its spit whenever ready for ten seconds with an echo beside it (at normal and double cast speed, the foe nine and fourteen units off), and the ember totem and a plain totem with a foe eight units off for ten seconds. Each scenario's casts and hits are traced event by event and hashed (SHA-256). Before is the seam's head, where constructs still took the legacy path; after is this request. Both were measured again once the two were rebased onto main `26b9e46`: the same numbers and hashes.

| | Before | After |
|---|---|---|
| Echo: owner casts replayed (normal speed) | 15 of 15 | 15 of 15 |
| Echo: owner casts replayed (double speed) | 29 of 29 | 29 of 29 |
| Echo: owner casts replayed (foe 14 off) | 15 of 15 | 15 of 15 |
| Echo: first replay after the owner's cast | 0.10 s | 0.10 s |
| Echo traces | `c28c136d…` | `c28c136d…`, the same |
| Totem (inheriting and plain): first bolt after planting | 0.35 s | 1.00 s |
| Totem: bolts in ten seconds | 10 | 9 |
| Totem traces | `b911c303…` | `837978b2…` |
| Trap: hit within half a second of a foe stepping near | yes | yes |

The measured normal-speed and double-speed echo scenarios are unchanged event for event. The totem's bolt is now a use with its 0.6 second cast bar before it flies, so the first bolt waits for the totem's first think, its bar and its resolution, and ten seconds hold nine bolts instead of ten; a stun, freeze or silence can now interrupt a totem's cast like anyone's. **The owner explicitly selected normal cast timing and interruptions on 2026-10-18**, accepting the measured cadence change. The independent review's high-speed echo limitation and release-policy integration requirements are recorded in [the follow-up](2026-10-18-construct-independent-review.md).

## Historical evidence at the original authored head

- **Duel traces, byte-identical** to the seam's and to main's at `26b9e46`: `1632a833…`, `13103d53…`, `d86145f5…`, `35f7aba1…`, `498105ff…`, `f97e63c0…`, `d84e14f3…` (the seven standard duels; no duel holds a construct).
- **Tests:** `dotnet test HollowWake.sln` passed on the original authored tree: core 619 (four new: every owner cast replayed, the scenario numbers and hashes, a totem paying its cost and waiting its cast time with the waiver taken off, and no totem cast with an empty handler registry), server 43, client 114. The construct tests the seam left unchanged pass unchanged: the echo at half strength, the trap within half a second, the inheriting totem hitting with the owner's numbers and crediting the owner, caps, domes and the forwarded glyphs.
- **Content:** `hollowwake validate` reports 0 errors and 0 warnings; `hollowwake weave snare_burst` compiles it instant on `snare_blast`. `hollowwake load --x 0 --y 0` ran 200 ticks to 2672 entities and 272 monsters, unchanged.
- **Composition lint** (`hollowwake lint`, which main gained after this request was written): two findings more than on the seam, both the new form. `form.one_use` names `snare_blast` a shell (no drop weight) that one preset composes on, and `expression.copied` counts its damage, `weave.base_damage * spell.power`, as that formula's sixteenth copy. They are the price of an instant trap by content: a weave's cast mode and cast time are its form's, which no preset or glyph sets, and no shell that reads `cast.time` aims a spell's area at the ground; a shell for that delivery (`effects: []`, the graph on the preset) would be one preset's shell all the same. The rest of the report moves only by line numbers.
- **Bench** (`bench --only tick`, Release, three runs of the seam's head and three of this request back to back, taken before the rebase onto main `26b9e46`): `system.behavior` 1.97, 1.78 and 1.87 ms per tick on the seam's head and 2.34, 2.08 and 1.78 here, within the machine's noise (the bench zone holds no construct) and the 3 ms budget; behavior allocation 23.4 KB a tick on both; `zone.tick.alloc` 68.8 KB on both.

## Shared-file integration

#20 is integrated in this branch through the source named in the independent review. `WeaveRuntime.Run` checks a paid device as the physical caster before switching payload credit to its owner. Echo replay preserves the captured section and variables, checks the shared release policy from the echo position, and runs without a second payment. `EffectContext.CastSource` carries physical miss attribution independently of inherited `Source` and `Origin`, including nested contexts. These additive context fields must be retained with #36's separate launch-height work. There is no unresolved intent-seam merge. #50 is retargeted to main to allow hosted checks, but its older included #20 subset must not land before the complete corrected dependency is integrated and reviewed.
## Open

- The owner decided the echo's pending replays, answering finding 2 of the combat session's re-review of `cc0e1d4` on the request: "Pending echo replays expire while the echo cannot act, so a stun counters the echo." So an inactive echo retains no pending uses: a use the owner resolves while the echo is stunned, frozen or otherwise unable to act expires rather than banking, which keeps invariant 2 (every bank caps and decays) and keeps a stun a counter to an echo. The correction now covers stun/freeze application, attempts during incapacity and recovery, including statuses shorter than a think interval. The exact human answer received by Codex was "Cancel pending replays (Recommended)" to the stun/freeze question.

- The owner's totem decision is resolved above. The independent follow-up corrects the burst replay loss and completes the four #20 release-policy regressions. Final performance evidence and Claude review remain pending; the original measurements above describe the authored head, not the later integrated source.

## Asks of Codex

- Review the request as the independent engineer when it opens, the stat split and the echo's replay as a resolution of a paid use in particular.
- Review the new resolved-payload, cast-variable, spending, cancellation, identity and queue regressions described in the independent follow-up.

Acknowledged by: Codex's [independent review and correction](2026-10-18-construct-independent-review.md), published on PR #50. Claude review of the subsequent correction is pending.
