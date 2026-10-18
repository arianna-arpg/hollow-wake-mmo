# Combat AI: the intent seam

Date: 2026-10-18. Author: the combat session (Claude). Base: main `a810b0a`. Branch `claude/combat-ai-intent-seam`. This is the first engine request of the combat AI rebuild that [the AI split record](2026-10-18-combat-ai-split.md) sequences: the seam both halves of the rebuild build on. The AI inventory behind it found that no AI body could send an intent and that the AI wrote the simulation directly at twenty-one call sites. An unanchored review (an Opus agent given the change, not the author's conclusions) returned "merge after fixes"; its findings and what became of them are below. The seam changes no gameplay: constructs keep their legacy path in this request and move in their own, stacked on this one (its record is `2026-10-18-combat-ai-constructs.md`).

## What changed

- **Any actor sends intents.** `IntentContext` no longer requires a `Player`: it carries the player component when the sender is one (`Player` is null otherwise) and a sender to tell refusals to when the sender is not a client. A handler that works a player's state is registered `playerOnly: true` and the registry refuses any other sender with `players_only` before it runs; a test pins the player-only set. The queued path for players is unchanged: same queue, same per-tick cap, same `intent_rejected` reasons. `Register<T>(handler, whileDead)` keeps its two-parameter form for compiled mods.
- **The seam.** `AiIntents.Send(AiContext, IIntent)` dispatches at once, during the think, through the simulation's `IntentHandlers` with a context built for the actor, and returns an `IntentOutcome` (accepted, or refused with the handler's reason). It never enqueues, since a queued intent would act a tick late, after input. Each refusal is recorded on the think's context (`AiContext.Refusals`, a list the behavior system lends each think and clears before the next, so it is valid only during the think). No `IntentRejected` is published for an AI's refusal; what the runtime publishes itself, such as `WeaveFailed`, stays on the simulation's bus as before.
- **One planner for a click and a chase.** The AI's straight-walk sample, node budget and plan cache moved into the `MoveTo` handler (`Movement/MovePlanner.cs`), the cache now on `Motion`. `MoveToIntent` gains `Stop` (how near counts as arrived) and `KeepPlan` (a destination sent again every think), which the AI sends and no client sends yet. The budget goes by who moves: a player's work runs on `move.path_max_nodes`, any other mover's on `move.ai_path_max_nodes`. A straight walk is tried only for a segment within the budget's samples; a longer one falls to the bounded search. A click clears steering by direction; a kept plan leaves it.
- **Walks that would change nothing are not sent.** A chase whose plan already leads to the same point and still stands sends no intent.

## Every direct write, and what it became

| Where | Was | Now |
|---|---|---|
| `Think`, stunned or frozen | `Motion.Stop` | `StopIntent` |
| `idle` | `Motion.Stop` | `StopIntent` |
| `stop` | `Motion.Stop` | `StopIntent` |
| `ChaseTo`, within reach | `Motion.Stop` | `StopIntent` |
| `AttackTarget`, after a cast began | `Motion.Stop` | `StopIntent` |
| `ChaseTo`, out of reach | `SetDestination` | `MoveToIntent { Stop, KeepPlan }` |
| `wander` | `SetDestination` | `MoveToIntent { Stop, KeepPlan }` |
| `return_home` | `SetDestination` | `MoveToIntent { Stop, KeepPlan }` |
| `flee` | `SetDestination` | `MoveToIntent { Stop, KeepPlan }` |
| `MoveTo` helper (orders, `follow_owner`, `return_to_owner`) | `SetDestination` | `MoveToIntent { Stop, KeepPlan }` |
| `AttackTarget` | `WeaveRuntime.TryBegin` | `UseWeaveIntent` |
| `use_weave` | `WeaveRuntime.TryBegin` | `UseWeaveIntent` |
| `heal_self` | `Stats.AddResource("life")` | removed (below) |
| `ChaseTo`, within reach | `Transform.Face` | unchanged: a world rule (below) |
| `face_target` | `Transform.Face` | unchanged: a world rule (below) |
| `return_to_owner`, leash recall | `MinionService.Teleport` | unchanged: a world rule |
| `spring`, a spent trap | `MinionService.Dismiss` | unchanged: a world rule |
| `ConstructCasting.Fire` | `Transform.Face`, `WeaveBook.ReadyAt`, `CompiledEffect.ExecuteAll`, publishes `WeaveCast` | unchanged in this request: the one direct effect run left in the AI, which the constructs request replaces |

## World rules, and why they stay direct

A world rule a body is under is not something it does, and no intent asks for it. The behavior system applies three directly, named where they happen and in `docs/design/ai.md`: facing (a chasing body within reach, and `face_target`, turn toward the target at once), a minion's leash recall (`MinionService.Teleport`) and a spent trap's going (`MinionService.Dismiss`). Facing was an intent in the first draft of this request; the review held that facing is a rule of the simulation, as the design grammar page says, not something an actor asks for, so the code now matches the grammar. A turn rate, and the movement locks of a held cast holding the body's facing, are deferred: either would move duels with roots and guards in them. The AI's own mind (its target, memory, order, an echo's replay count, the leash's headway) is the AI's to write.

## Decisions

- **`heal_self` is removed, not converted.** No behavior names it, and a free heal is not something any intent can ask for; running a `heal` effect from an action would have been the same bypass through another door. A body that heals casts a heal weave through `use_weave` or `attack`.
- **Player-only is a registration flag**, beside `whileDead`, so a handler states once that it needs a player and the registry refuses others before the handler runs. A mod's handler is open to any actor unless it says otherwise; the AI only sends what its actions send.
- **The search budget goes by who moves, not by the flag.** Were `KeepPlan` on the wire and the budget chosen by it, a client could pick the AI's budget. The handler picks the budget by the sender's kind; `KeepPlan` only lets a sender keep its own standing plan, so the flag is safe to put on the wire. Every sender searches on the budget it had (the AI always flagged, players never).
- **`MoveToIntent.Stop` and `KeepPlan` are not on the wire.** A client's click carries the point only and takes the planner's defaults.
- **A walker's goal is put on the ground; a flier's keeps its height.** The click always snapped; the AI never did. A walker's movement reads only the goal's ground position, so snapping changes nothing for it, and the shade echo, the one flier, keeps the height it was given.

## Review findings and what became of them

1. **The seam changed gameplay through constructs** (the reviewer's probe: a totem's first bolt 0.35 to 1.00 seconds, ten bolts to nine in ten seconds, an echo replaying nine of fifteen owner casts). The construct work left this request for its own, and constructs keep `ConstructCasting.Fire` here.
2. **A click had unbounded cost**: the straight-walk check sampled the whole segment, so a click 100,000 units away loaded 3,126 chunks in a 155 ms tick (the base, searching on its budget, had 127 chunks in total: one already loaded plus 126 added). The check now takes no more samples than the mover's budget and leaves a longer segment to the bounded search. Measured on the arena: a click 1,000 units away loads 31 chunks (its search expands 1,001 cells and finds the point), one at 10,000 loads 126 and one at 100,000 loads 126 (each search stops at the budget's 4,000 cells), exactly the base's numbers. A test pins it. The review asked that the far click load no more than the one at a thousand; that cannot hold under a bounded search, since a thousand-unit click finds its goal and loads only the ground along it while any click past the budget's reach loads the budget's worth (the base does the same), so the test pins the far click to the ten-thousand one and to the budget.
3. **Zeroing `MoveDir` on a kept plan moved a chase**: the movement system keeps steering by its last direction on the tick a waypoint is reached, and the handler cleared it; the review's wall-detour trace diverged from the base at tick nineteen. The handler clears it only for a click. The original Windows traces of three chases matched the base, including full-precision facing. That assertion was platform-dependent. The follow-up records full-precision position and path-count fixtures from the same base, holds aggro to avoid wandering, and scripts the moving prey on an axis; see `2026-10-18-ai-intent-portability-review.md`.
4. Smaller findings, all taken: the player-only set is pinned by a test; with an empty handler registry no minion follows, heels or obeys (and no beast walks or casts); `Refusals` is documented as valid only during the think; the docs say no `IntentRejected` is published for an AI's refusal while `WeaveFailed` stays on the bus, and name `Stop` and `KeepPlan` as fields no client sends yet; `Register<T>(handler, whileDead)` keeps its two-parameter form; `modding.md` notes that `IntentContext.Player` can be null; and a walk whose plan already stands is not sent again.

## A click, before and after

Over clear ground a click now walks straight at the point, where it followed cell centres. On a seeded arena (seed 1, a player at (5.5, 5.5)):

| Route | Base | This request |
|---|---|---|
| Clear ground to (15.3, 11.7), 11.60 units as the crow flies | 10 waypoints, walked 12.25, stopped after 2.50 s, ended 0.065 off the point | no waypoints, walked 11.50, stopped after 2.35 s, ended 0.097 off |
| Around a wall to (15.5, 7.5) | 12 waypoints, walked 15.25, stopped after 3.10 s | the same |
| Walled in on every side, to (8.5, 5.5) | no step found: walks straight at the point, stops within 0.3 (blocked after 0.25) | the same |

A click with no path now always sets the stop distance to 0.3, where the old handler left whatever the body had; for a player that was 0.3 already, so nothing a player sees moves there.

## Evidence

- **Duel traces, byte-identical** (`fight`, md5 of the whole output, before and after, the after taken on the final tree):

  | Duel | md5 |
  |---|---|
  | `loosed_kindler boar --seconds 30 --lines 400` | `1632a833e515d2073de25d9fbea94835` |
  | `wolf hob --seconds 30 --lines 400` | `13103d539515f06d12cdd400c4fc94da` |
  | `proving_reaver proving_bulwark --seconds 30 --lines 400` | `d86145f5dc4c97df0653fdf1253ec7a4` |
  | `tide_channeler spark_duelist --seconds 30 --lines 400` | `35f7aba1b3c7159fbdf44c72de62b061` |
  | `loosed_skirmisher tide_caller --seconds 30 --lines 400` | `498105ff51f7e9c9aaa8a02f166dfe44` |
  | `proving_pitfighter proving_mower --seed strikes --seconds 60` | `f97e63c057dcc237494dd5ee71dfd0ee` |
  | `proving_fletcher proving_evoker --seed projectiles --seconds 60` | `d84e14f325a036a17b90c45220e3da0d` |

- **Original Windows chase evidence (superseded as the cross-platform gate):** the wall detour `2ec6e14a…`, the forest `3334d55b…` and the zigzag `2d722ec2…` (SHA-256 of position, facing and path length every tick) are the base's.
- **Tests:** `dotnet test HollowWake.sln` passes: core 591 (nine new), server 43, client 114.
- **Content:** `hollowwake validate` reports 0 errors and 0 warnings. `hollowwake load --x 0 --y 0` ran 200 ticks to 2672 entities and 272 monsters, the same as the base.
- **Live:** a server started from this worktree on a scratch configuration, and `hollowwake probe` against it: `PROBE OK (walked south)`, 356 entities seen, a clean shutdown. The browser smoke through the background harness (`node scripts/test-background.mjs --suite web`, its browser suite): 30 of 30 checks, `click to move` among them.
- **Bench** (`bench --only tick`, Release, three runs each of the base and this request back to back): `system.behavior` 2.06, 1.90 and 1.92 ms per tick on the base, 2.26, 1.82 and 1.78 here (means 1.96 and 1.95), inside the 3 ms budget; its allocation 22.9 KB a tick on the base and 23.4 here; `zone.tick.alloc` 68.2 and 68.8 KB.
- **A finding the seam makes countable:** in the bench zone the AI sends about fifteen cast intents a tick and the runtime refuses about twelve of them as `busy`: the chooser presses again while its own cast is running. Nothing changes for it here; the chooser's vetoes are the place to stop it.

## Files other open requests touch

No code: the construct edits that touched request #20's `Weaves/WeaveRuntime.cs`, `Weaves/SlotBehaviors.cs` and `Components/Weaves.cs` left with them. One contract is shared: this request adds a paragraph to the sessions section of `docs/design/networking-and-persistence.md`, which #20 also edits; a trial merge with #20's head merges it cleanly.

## Asks of Codex

- Review the request as the independent engineer when it opens.
- Say whether the movement leaving the AI as `MoveToIntent` (with `Stop` and `KeepPlan`) and `StopIntent` is what the position advisor needs to steer, or what it wants of `MoveDirIntent` and `JumpIntent` besides.

Acknowledged by: pending Codex's review.
