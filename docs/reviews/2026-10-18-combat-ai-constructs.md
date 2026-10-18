# Combat AI: constructs act through the runtime

Date: 2026-10-18. Author: the combat session (Claude). Base: `claude/combat-ai-intent-seam` at `0dd5b10`, stacked on it. Branch `claude/combat-ai-constructs`. The intent seam ([its record](2026-10-18-combat-ai-intent-seam.md)) left one direct effect run in the AI: the device actions fired their weaves through `ConstructCasting.Fire`, past costs, cast times, gates, target kinds and the runtime's events. This request replaces it. The independent review of the seam measured what an earlier draft of this work did to constructs (a totem's first bolt later, an echo replaying nine of fifteen owner casts); the decisions below answer it.

## What changed

- **A device's cast is a real use on its own clock.** `construct_cast` and `spring` begin a ready weave by `UseWeaveIntent` through the seam, so its uses, charges, gates, target, cooldown and cast time apply and its cost is paid through the ordinary costs. A device begins each weave at most once per `minions.construct_interval`, a clock it keeps on its behavior (`Behavior.PacedUntil`), as the hand-written cooldown did before.
- **Devices are free of cost, not of uses, charges or gates.** The `device` pattern carries a `cost` modifier of 100% less, so every device cast pays a cost folded to nothing, as data the cost fold reads; a device pattern without the modifier pays from its own pools.
- **The stat split.** A construct that inherits its caster casts with its owner as the origin (`WeaveRuntime.OriginOf`, read by `Run` and the preset path): the effects run as the owner, so their numbers are the owner's and kills credit the owner, from where the construct stands; the reach, the cast time and the cooldown are the construct's own; and the cast is the construct's, which is paid and validated and named by `WeaveCast`.
- **An echo resolves its owner's use.** An echo's replay is not a new use but a resolution of the use its owner paid for, so it is no intent: it runs on the runtime's free path (`WeaveRuntime.Replay`, sharing the preset path's resolution with `CastPreset`), free of cost and cooldown and with no cast time, as the owner at the echo's `damage_scale`, from where the echo stands, on the echo's own stream as before. Every owner cast is replayed. The earlier draft's copies of the owner's weaves in the echo's book (`Mirror`) are retired. The replay is a world rule, a service call, named in `docs/design/ai.md` beside the leash and the spent trap.
- **The trap stays instant, by a form of its own.** The review asked for an attribute the preset sets if the runtime reads the cast time through `cast.time`. It does not for the blast: the blast's cast time is the number 0.9 on its form, read from the form, and no attribute a preset sets changes it (only the shells read `weave.cast.time`). So the snare's burst casts through `snare_blast`, the blast with no cast time and nothing else changed (the same costs, attributes, mechanisms, tags and graph, default thread and glyph limits, no drop weight); its compiled tags differ from a blast's only in the cast scope (`cast.instant` for `cast.timed`) and the form tag, which no content scopes by. A trap that springs goes once its cast is over, at once for this one.

## Measured

The review's totem and echo probes are now tests (`ConstructScenarioTests`): the owner casting its spit whenever ready for ten seconds with an echo beside it (at normal and double cast speed, the foe nine and fourteen units off), and the ember totem and a plain totem with a foe eight units off for ten seconds. Each scenario's casts and hits are traced event by event and hashed (SHA-256). Before is the seam's head, where constructs still took the legacy path; after is this request.

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

The echo is unchanged event for event. The totem's bolt is now a use with its 0.6 second cast bar before it flies, so the first bolt waits for the totem's first think, its bar and its resolution, and ten seconds hold nine bolts instead of ten; a stun, freeze or silence can now interrupt a totem's cast like anyone's. **That cadence change is the owner's decision, open until the owner answers.** If the old cadence should stand, the bolt wants an instant form of its own as the trap has (the spit itself is shared with monsters and players), or a device rule.

## Evidence

- **Duel traces, byte-identical** to the seam's and the base's: `1632a833…`, `13103d53…`, `d86145f5…`, `35f7aba1…`, `498105ff…`, `f97e63c0…`, `d84e14f3…` (the seven standard duels; no duel holds a construct).
- **Tests:** `dotnet test HollowWake.sln` passes: core 595 (four new: every owner cast replayed, the scenario numbers and hashes, a totem paying its cost and waiting its cast time with the waiver taken off, and no totem cast with an empty handler registry), server 43, client 114. The construct tests the seam left unchanged pass unchanged: the echo at half strength, the trap within half a second, the inheriting totem hitting with the owner's numbers and crediting the owner, caps, domes and the forwarded glyphs.
- **Content:** `hollowwake validate` reports 0 errors and 0 warnings; `hollowwake weave snare_burst` compiles it instant on `snare_blast`. `hollowwake load --x 0 --y 0` ran 200 ticks to 2672 entities and 272 monsters, unchanged.
- **Bench** (`bench --only tick`, Release, three runs of the seam's head and three of this request back to back): `system.behavior` 1.97, 1.78 and 1.87 ms per tick on the seam's head and 2.34, 2.08 and 1.78 here, within the machine's noise (the bench zone holds no construct) and the 3 ms budget; behavior allocation 23.4 KB a tick on both; `zone.tick.alloc` 68.8 KB on both.

## Files other open requests touch

Request #20 (`codex/shared-cast-forecasts-integration`, open) edits files this request edits:

- `Weaves/WeaveRuntime.cs`: `OriginOf`, and two lines in `Run`'s effect context (`Source` is the origin; `From` is the caster's position when the origin is another body). A trial merge with #20's head (`da8e549`) merges it cleanly; the merged `Run` keeps #20's release-time target check ahead of the origin, so a construct's reach at release is measured from the construct.
- `Weaves/SlotBehaviors.cs`: `CastPreset`'s resolution moves into `RunFree`, which `Replay` shares. This conflicts with #20's edit of `CastPreset` (it drops the dead-target check and runs the effects through its `ReleaseTargets.Execute`). The resolution is to take #20's two changes into `RunFree`, so presets and an echo's replay both get #20's release-time checks.
- `docs/design/weaves-and-effects.md` and `docs/design/networking-and-persistence.md`: a sentence each, which merge cleanly.

If #20 merges first, this request (with the seam beneath it) is rebased onto it and its gates run again.

## Open

- The totem's cadence: the owner's decision (above).

## Asks of Codex

- Review the request as the independent engineer when it opens, the stat split and the echo's replay as a resolution of a paid use in particular.
- Say whether #20's release-time checks should apply to an echo's replay as they apply to a preset, which is how the rebase would resolve.

Acknowledged by: pending Codex's review.
