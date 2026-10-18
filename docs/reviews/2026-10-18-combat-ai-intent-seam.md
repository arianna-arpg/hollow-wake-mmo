# Combat AI: the intent seam

Date: 2026-10-18. Author: the combat session (Claude). Base: main `a810b0a`. Branch `claude/combat-ai-intent-seam`. This is the first engine request of the combat AI rebuild that [the AI split record](2026-10-18-combat-ai-split.md) sequences: the seam both halves of the rebuild build on. The AI inventory behind it found that no AI body could send an intent and that the AI wrote the simulation directly at twenty-one call sites, constructs casting through `CompiledEffect.ExecuteAll` past every check the runtime makes.

## What changed

- **Any actor sends intents.** `IntentContext` no longer requires a `Player`: it carries the player component when the sender is one (`Player` is null otherwise) and a sender to tell refusals to when the sender is not a client. A handler that works a player's state is registered `playerOnly: true` and the registry refuses any other sender with `players_only` before it runs: every item, crafting, vendor, stash, progression, quest, interaction, territory, dialogue and pane intent, binding a weave (`SetWeaveIntent`) and respawning. Movement, facing, casting, the held-input intents, metas, swapping seats and orders to minions take any actor. The queued path for players is unchanged: same queue, same per-tick cap, same `intent_rejected` reasons.
- **The seam.** `AiIntents.Send(AiContext, IIntent)` dispatches at once, during the think, through the simulation's `IntentHandlers` with a context built for the actor, and returns an `IntentOutcome` (accepted, or refused with the handler's reason). It never enqueues, since a queued intent would act a tick late, after input. Each refusal is recorded on the think's context (`AiContext.Refusals`, a list the behavior system lends each think so recording allocates nothing) for the decision event to report. AI refusals are not published, so no client hears of them.
- **One planner for a click and a chase.** The AI's straight-walk sample and node-budget policy moved into the `MoveTo` handler (`Movement/MovePlanner.cs`), with the plan cache it had, now on `Motion`. `MoveToIntent` gains `Stop` (how near counts as arrived) and `KeepPlan` (a destination sent again every think); the AI sets both, a click sets neither. A new plan walks straight when the segment is clear, else searches, a player's on the pathfinder's own budget (`move.path_max_nodes`, the budget a click always had) and any other mover's on `move.ai_path_max_nodes`; a kept plan stands until it runs out, its goal moves `move.ai_repath_distance` or it ages `move.ai_repath_seconds`; a click plans afresh.
- **Facing is an intent.** `FaceIntent(point)`, registered for any actor.
- **Constructs cast through the runtime.** The device actions send `UseWeaveIntent` through the seam, so a construct's cast begins in `TryBegin` on its own book and pays, waits its cast time and cooldown, passes gates and target checks, shows its cast, and raises `WeaveCast` when it resolves. The runtime runs a cast's effects as its origin (`WeaveRuntime.OriginOf`, read by `Run` and by the slot behaviors' preset casts): the caster; its owner when it is a construct that inherits its caster, from where the construct stands; or the body an echo's seat names. The hand-written `ReadyAt`, `Face`, `ExecuteAll` and `WeaveCast` are gone.

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
| `ChaseTo`, within reach | `Transform.Face` | `FaceIntent` |
| `face_target` | `Transform.Face` | `FaceIntent` |
| `AttackTarget` | `WeaveRuntime.TryBegin` | `UseWeaveIntent` |
| `use_weave` | `WeaveRuntime.TryBegin` | `UseWeaveIntent` |
| `heal_self` | `Stats.AddResource("life")` | removed (below) |
| `ConstructCasting.Fire` | `Transform.Face` | `UseWeaveIntent`; `TryBegin` faces the target |
| `ConstructCasting.Fire` | `WeaveBook.ReadyAt` | the weave's own cooldown in `TryBegin`, plus the device's pace (below) |
| `ConstructCasting.Fire` | `CompiledEffect.ExecuteAll` | `UseWeaveIntent`; `TryBegin` and `Run` with the owner as origin |
| `ConstructCasting.Fire` | publishes `WeaveCast` | the runtime publishes it when the cast resolves |
| `return_to_owner`, leash recall | `MinionService.Teleport` | unchanged: a service call |
| `spring`, a spent trap | `MinionService.Dismiss` | unchanged: a service call, once the trap's cast is over (at once, since its burst is instant) |

## What stays a service call, and why

A world rule a body is under is not something it does, and no intent asks for it: a minion's leash recall teleports it (`MinionService.Teleport`), a spent trap goes (`MinionService.Dismiss`), and an echo carries copies of its owner's weaves in its own book (`ConstructCasting.Mirror`, new: the echo then casts the copy through the seam). The AI's own mind (its target, memory, order, the echo's replay count, the leash's headway, the device's pace) is the AI's to write.

## Decisions

- **`heal_self` is removed, not converted.** No behavior names it, and a free heal is not something any intent can ask for; running a `heal` effect from an action would have been the same bypass through another door. A body that heals casts a heal weave through `use_weave` or `attack`.
- **Player-only is a registration flag**, beside `whileDead`, so a handler states once that it needs a player and the registry refuses others before the handler runs. A mod's handler is open to any actor unless it says otherwise; the AI only sends what its actions send.
- **The search budget goes by who moves, not by the flag.** Were `KeepPlan` on the wire and the budget chosen by it, a client could pick the AI's budget for its own walks. The handler picks the budget by the sender's kind instead: a player's search runs on `move.path_max_nodes`, any other mover's on `move.ai_path_max_nodes`, whatever the intent says. `KeepPlan` only lets a sender keep its own standing plan, which touches nobody else's walk, so the flag is safe to put on the wire. Every sender today searches on the budget it had (the AI always flagged, players never), so nothing moves.
- **Facing is free.** The handler turns the body at once and refuses nothing while a cast runs, as nothing refused the AI's facing before. A raised guard's arc therefore turns with the body, as it did. A turn rate for held casts would be a rule checked in the handler; it could change the traces of duels with a guard in them, so it waits for the chooser work.
- **`MoveToIntent.Stop` and `KeepPlan`, and `FaceIntent`, are not on the wire.** A client's click carries the point only and keeps the planner's defaults; adding them to the protocol is a message change for the request that needs it.
- **A walker's goal is put on the ground; a flier's keeps its height.** The click always snapped; the AI never did. A walker's movement reads only the goal's ground position, so snapping changes nothing for it, and the shade echo, the one flier, keeps the height it was given.
- **Constructs pay through the costs, and the devices waive them in content.** The `device` pattern carries a `cost` modifier of 100% less, so the shipped totems, snares and echoes still cast free, now as data the cost fold reads rather than code that skipped it. A device pattern without the modifier pays from its own pools; the test below shows one paying. Without the waiver, a level-one totem (35 mana, 1 a second) would fire four or five bolts of its twelve, and an echo could not replay a weave dearer than its pool.
- **Traps stay instant, by content.** The snare's burst was a blast, whose 0.9 second cast time the old construct path skipped; through the runtime it would have given every trap a fuse. No shell fits: the one instant ground area shell, `slam_quick`, is an attack, and moving the burst onto it would trade its spell tags for attack tags and so change which modifiers reach it. So `snare_burst` casts through `snare_blast`, a new form beside the snare's (`forms-summon.yaml`): the blast with no cast time, the same costs, attributes, mechanisms, tags and graph, default thread and glyph limits as the blast's (so the glyphs that board it fit as before), and no drop weight, so it never enters the loot. Its compiled tags differ from the blast's only in the cast scope (`cast.instant` for `cast.timed`) and the form tag, and no content scopes anything by either. The trap test waits what it always waited.
- **Cast times hold for the other constructs.** A totem's bolt shows its 0.6 second bar before it flies; the totem still fires about once a second, because a device begins each weave at most once per `minions.construct_interval` (a pace it keeps in `Behavior.PacedUntil`).
- **A construct's reach is its own.** The device's range check and the runtime's both read the construct's stats; the old check read the owner's, so an owner's range bonus no longer reaches its totems.
- **The echo casts from its own book.** Its copy of an owner's weave shares the compiled weave and casts as the owner at the echo's `damage_scale`, so the owner's uses, combo cursor and toggles are never touched by the replay. Triggered weaves and toggles are no longer replayed: pressing them arms or switches rather than casts. A replayed self weave lands on the echo, the caster, where the old replay landed it on the owner a second time. The echo tries each owner cast once; one that arrives while its last replay is still casting is skipped.

## Gameplay this request still changes, as its brief asked

The brief asked for constructs to cast through `TryBegin`, paying and respecting cast times. Costs are waived by content and traps kept instant by content, so what remains visible is:

- A totem's bolt shows its 0.6 second bar before it flies, so its first bolt comes that much later (the cadence stays about one a second), and a stun, freeze or silence can now interrupt it like any cast.
- An echo replays with the weave's cast time, skips a replay that arrives while it is still casting, no longer replays triggered weaves or toggles, lands a replayed self weave on itself, and rolls on its own cast stream.
- A construct's reach is its own, without its owner's range bonuses.
- Construct casts show cast cues on the wire like any cast (presentation only).

If these should stay as they were too, the bolt and the echo's replays would need forms or a rule of their own: no modifier can bring a cast time to nothing, since speed divides it, and `cinder_spit` is shared with monsters and players.

## Files request #20 also touches

Request #20 (`codex/shared-cast-forecasts-integration`, open) edits `Weaves/WeaveRuntime.cs`, `Weaves/SlotBehaviors.cs`, `Components/Weaves.cs`, `docs/design/networking-and-persistence.md` and `docs/design/weaves-and-effects.md`, all of which this request touches too:

- `Weaves/WeaveRuntime.cs`: a new `OriginOf` beside `Run`, and three lines in `Run`'s effect context (`Source` is the origin, `DamageScale` carries the origin's share, `From` is the caster's position when the origin is another body).
- `Weaves/SlotBehaviors.cs`: the same three lines in `CastPreset`'s effect context.
- `Components/Weaves.cs`: `WeaveSlot.Origin` and the `CastOrigin` record beside it.
- The two contracts: a paragraph on intents from any actor in the sessions section, and a sentence on a cast's origin in the runtime section.

A trial merge of this request with #20's head (`da8e549`) merges all five without conflict, and the merged `Run` keeps #20's release-time target check ahead of the origin, so a construct's reach at release is measured from the construct. If #20 merges first, this request is rebased onto it and the gates are run again.

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

  No duel holds a construct, so the construct changes cannot reach these traces; the movement and casting changes do, and leave them unchanged.
- **Tests:** `dotnet test HollowWake.sln` passes: core 588 (six new), server 43 (the loopback and WebSocket movement included), client 114. The new tests (`AiIntentTests`): an AI body's stops, walks, turns and casts leave as intents the handlers run, every think of a chase sends one, and its bites land; with an empty handler registry neither a wolf nor a totem walks, turns or casts; a refused walk reports the handler's reason, is recorded on the think and published nowhere, and the wolf plans nothing in its place; a construct's cast pays its cost (with the devices' waiver taken off) and resolves no sooner than its cast time; a player's click walks straight over clear ground, plans afresh beside a previous click, searches around a wall and arrives, and turns by `FaceIntent`; a player's walk searches past the AI's budget even when flagged as the AI flags its walks, while a wolf's stays within it.
- **Tests adjusted:** the navigation tests read the plan cache on `Motion`, where it moved; the echo test turns its owner's crits off, since the replay now rolls on the echo's own cast stream and with this seed it crit, which the test's filter dropped.
- **Content:** `hollowwake validate` reports 0 errors and 0 warnings. `hollowwake weave snare_burst` compiles it on `snare_blast`, instant. `hollowwake load --x 0 --y 0` ran 200 ticks to 2672 entities and 272 monsters, the same as the base.
- **Bench** (`bench --only tick`, Release, six runs each, alternating the base and this branch three and three): `system.behavior` 1.86 to 2.54 ms per tick on the base (mean 2.23) and 1.78 to 2.63 on this branch (mean 2.28), within the machine's noise and the 3 ms budget; its allocation 22.9 KB a tick on the base and 24.6 on this branch, the intent records and two more fields on the think's context, far inside the 1 MB budget. `zone.tick.alloc` 68.2 and 70.2 KB. Three more runs on the final tree: 1.95 to 2.06 ms and 24.6 KB.
- **A finding the seam makes countable:** in the bench zone the AI sends about fifteen cast intents a tick and the runtime refuses about twelve of them as `busy`: the chooser presses again while its own cast is running. Nothing changes for it here; the chooser's vetoes are the place to stop it.

## Not done

- No live server and `probe` run: no message, transport or session changed, and the loopback and WebSocket tests pass.
- The unanchored review of this slice, and Codex's.

## Asks of Codex

- Review the request as the independent engineer when it opens.
- Say whether the movement leaving the AI as `MoveToIntent` (with `Stop` and `KeepPlan`), `StopIntent` and `FaceIntent` is what the position advisor needs to steer, or what it wants of `MoveDirIntent` and `JumpIntent` besides.

Acknowledged by: pending Codex's review.
