# AI inventory, pass zero

Base: worktree `combat-port-17f658` at `14f7f3b`. Evidence: the code, the three requested `fight` traces plus `fight tide_channeler spark_duelist`, and a read-only F# probe (`pass0/harness/duel.fsx`, run against a scratch copy of the built core) that replays the same seeded arena duels and also records `WeaveFailed`, releases, toggles, banks, states and positions. To exercise each mechanic it swaps kits onto proving bodies. Outputs are in `pass0/traces/`. The prototype was read only as a list of behaviors.

Headlines:

- The AI makes exactly one weave decision: a lottery, `weight x U(0.75, 1.25)`, over ready, affordable, in-range weaves (`WeaveRuntime.ChooseForAi`). No code reads `role`. `min_range`, `near_weight` and `range_attribute` are implemented, but no content uses them.
- No AI body can send an intent. `IntentSystem` dequeues only `Player` components, and `IntentContext` requires a `Player`. The AI writes the simulation directly at **21 call sites**, and none of them goes through `IntentHandlers`.
- Measured failures:
  - The mower chose `verdict` 22 times in 13.6 s, about two thirds of the thinks in which it was free to cast, and the runtime refused all 22 (`no_target`).
  - A melee body carrying a dash froze at 4 units and died without moving or casting.
  - A summon never fired beside a beam.
  - An arquebus fired once in 30 s.
  - An aura toggle was pressed 9 times, switching itself off with every second press.
  - A channel ran 2.7 s into a corpse.

## 1. Every decision the AI makes today

Bold marks a direct runtime write where a client would send an intent, or where a client has no way to do it at all.

| Decision | Where | Reads | Writes | Same decision by intent? |
|---|---|---|---|---|
| When to think | `BehaviorSystem.Think` | `tick_interval` | `NextThinkAt` | n/a |
| Freeze when stunned or frozen; `idle`, `stop` | `Think`, actions | flags | **`Motion.Stop`** (3 sites) | `StopIntent` |
| Which state | `Think` | transition expressions over `PerceptionScope` | state, enter/exit lists | n/a (the player's mind) |
| Whom to fight | `acquire_target` | grid query within aggro range, relation, `hidden`, `ignore` tags, `attacker:*` memory (read, but never written by any code) | `Behavior.Target` | only per cast (`UseWeaveIntent.Target`) |
| Drop, assist or order a target | `drop_target`, `assist_owner`, order kinds | owner's target or attacker (`MinionService`), quarry | `Behavior.Target` | owner side: `CommandMinionsIntent` |
| How close to stand | `chase`, `Engage` | `PreferredRange` = longest hinted range of *any* weave x 0.9 | **`SetDestination`**, or **`Motion.Stop` + `Transform.Face`** | `MoveToIntent`/`StopIntent`; no facing intent |
| Which weave | `attack` -> `ChooseForAi` | readiness, toggle-on, triggered, empty bank, gates, affordability, range + both radii, hint `when`/`weight`/`min_range`, seeded roll | nothing (arms triggers) | the key a player presses |
| Where to aim | `AttackTarget` | target centre; Self and Ally targeting become self | **`TryBegin`**, then **`Motion.Stop`** | `UseWeaveIntent` calls the same `TryBegin` |
| Cast a named weave | `use_weave` (unused) | slot key | **`TryBegin`** with zero direction | `UseWeaveIntent` |
| Kite, flee, wander, go home | `flee`, `wander`, `return_home` | target position, home, radius, roll | **`SetDestination`** (3 sites): straight away from the target, no terrain or threat | `MoveToIntent` |
| Route | `SetDestination` | plan cache, straight-walk samples, `move.ai_path_max_nodes` | `Motion.Path/MoveTarget`, `Pathfinder.FindPath` | `MoveToIntent` (a different search: the pathfinder's own budget, no straight walk, no plan cache) |
| Face, heal, remember | `face_target`, `heal_self`, `remember` (all unused) | target, amount, key | **`Transform.Face`**; **`Stats.AddResource("life")`** with no `Healed` event and no modifiers; memory | none |
| Follow or heel | `follow_owner`, `return_to_owner` | owner distance, rules | **`SetDestination`** via `MoveTo`; leash **`MinionService.Teleport`** | `MoveToIntent`; teleport: none |
| Step an order | `obey_command` -> `MinionCommands` | kind, mark, quarry, expiry | target, `Engage`, `MoveTo` | issuer: `CommandMinionsIntent` |
| Construct fires | `construct_cast`, `spring`, `echo_owner` -> `ConstructCasting.Fire` | readiness, range, owner's last cast | **`CompiledEffect.ExecuteAll`**, **`WeaveBook.ReadyAt`**, **`Face`**, **publishes `WeaveCast`**; trap **`MinionService.Dismiss`** | none. Bypasses `TryBegin` entirely: costs, cast time, gates, target kinds, held modes, release checks, presentation |

How the 21 call sites break down (shared helpers counted once):

- **By file:** 15 are in `BehaviorSystem.cs` and 6 in `MinionActions.cs`.
- **By kind:** `Motion.Stop` 5, `SetDestination` 5, `Face` 3, `TryBegin` 2, and one each for `AddResource`, `Teleport`, `Dismiss`, `ReadyAt`, `ExecuteAll` and `Publish`.
- **By intent equivalent:** 12 have one. 9 have no player equivalent.

Six registered actions appear in no shipped behavior: `idle`, `stop`, `face_target`, `use_weave`, `remember` and `heal_self`.

What the AI perceives:

- 16 scalar names, measured once per think.
- Self stats, plus `target.*`, `owner.*` and `memory.*`.
- Nothing about threats, allies, crowds, corpses, its own minions, terrain, line of sight, the target's facing or the target's cast.

Status flags authored for minds (`panicked`, `belligerent`, `blinded`, `taunted`, `prey`) have no reader in any behavior.

## 2. Mechanics the AI cannot use or never uses

| Mechanic | Today (measured or read) | Input it needs | Decision and score |
|---|---|---|---|
| Channel release | Never releases. `tide_torrent` ended at `max_hold` 2 of 2 times, once beating 2.7 s at a dead foe | held time, ramp value per beat vs beat cost, target inside the region, own threat | `release` when the next beat's value x P(target stays) < its cost; `cancel` on target loss |
| Aim: tracking channels, guards, guided projectiles, salvos, metas | Never sends `AimIntent`. Tracking falls back to the focus body; guided projectiles fly to the cast point | target velocity, turn rate, projectile speed | `aim` each think at the intercept point; one aim per salvo beat |
| Guard raise, parry, bash | Raised only by hint `life_pct < 0.7`. Held to `max_hold` 2 of 2 times, 0 parries | hostile cast in the frontal arc with its emission time | raise so the parry window covers the emission; `release` to bash a foe in reach; drop when no threat |
| Charge-up | 16 of 16 auto-released at `full` (1.60 s) | fill, charge scales, target leaving reach, threat | release at the fill that maximises value per second at acceptable risk |
| Overcharge | 6 of 6 ran all 3 stages and auto-released | stage cost, resources, threat | release at a stage; stop paying near the reserve |
| Timed presses (perfect, timed, multitude) | 19 `measured_cut` casts, 0 presses | cast start, duration, window, marker | `press` in the window; attribute `ai.precision` gives a seeded miss rate |
| Manual bank reload (convert, meta) | Arquebus: 1 shot in 30 s. Scattergun: 3 shots, then silent. The chooser skips an empty bank before reaching its convert face | `slot.uses`, the convert face, the meta weave, threat | press the empty slot (its reload face) or `UseMetaIntent` when safe |
| Trickle bank | `bramble_quiver` drip-fires one bolt per 2.5 s recharge and never restrings | uses, recharge time, restring time | restring when a full bank now beats dripping |
| Toggles off; aura toggles | Form toggles are never turned off. `lumen_presence` (effect `toggle: true`) was pressed 9 times, switching itself off each second press | reserve, upkeep, combat state, effect-level toggle detection | `toggle_off` when the reserve is needed or the fight ends |
| Optional spends | `haymaker` spent 2, 1 and 2 fury and never waited for 5. Only a hand-written `when` (`gyre_hurl`: `charge.gyre >= 2`) waits | charges held and max, per-charge scale, decay | value = base x (1 + per_charge x n); wait while gain outpaces decay |
| Target kinds and `require` | The chooser ignores the form's target spec (the mower's 22 refused `verdict` picks). Ally targeting collapses to self | the target spec, evaluated purely | veto at choice; heal the neediest ally in range |
| Corpses | No corpse perception. Raise and consume weaves only work when a corpse happens to lie within 4 of the living target | corpses in reach | `use` aimed at a corpse, chiefly after kills |
| Summons and commands | `call_wraiths` (weight 0.5) never cast beside `void_lash` (0.8); it wins about 0.3% of picks. Owner AIs never command | minion count vs cap, owner pressure | summon while there is room; `CommandMinionsIntent` to assault, hold or recall |
| Movement weaves (dash, leap, blink, mark/recall) | `quick_dash`'s hint range 6 raised `PreferredRange`. The pitfighter stopped at 4 units, beyond `one_two` and short of the dash's `when`, walked 0.0 units and died | the value of the landing point | a positional option to close, escape or get behind |
| Areas, cones, salvo placement | Point is always the target centre and direction always points at it. No lead, no clustering, ally lanes ignored | foes and allies as forecast candidates | point and direction that maximise candidates |
| Cancel | Never cancels; casts at the dead (kindler, t=7.30) | target death, threat | `CancelCastIntent` |

## 3. What the AI knows about a weave, and a capability-aware chooser

**Known today:** the hint (`weight`, `preferred_range`, `when`) plus readiness, cost, gates and `range`.

**Exposed by the compiled weave but ignored:** 54 registered mechanism probes (returning, homing, trajectory, chain, area, `shape`, `fire:salvo`, cascade, telegraph, shove, leap, dash, tether, summon, construct, corpse, orders, guard, channel, held, release, ramps, timing, toggle, uses, convert, meta, spends, `charges:x`, `rupture:x`, `consumes:x`, `affliction:x`, contagion, leech, surface, displaces ...); `Tags` (`mobile`, `melee`, `movement`, `aura`); `CastMode` specs (ramps, brim, charge scales, stages, guard pool, arc, parry, bash, timing); `Casting` (uses, spend, convert, metas, chain, triggers); costs with reserve and upkeep; cooldown, cast time, move factor; the form's target spec; `weave.*` attributes; the area forecast (`IDeliveryPlanSource`, `ResolvedAreaPlan.IsCandidate`).

**Proposal.** A fresh architecture whose only inputs are capabilities, published perception and content doctrines. It emits only the intents a player can send.

1. **Affordances (compile time, cached per compiled weave).** An open registry `Affordances`, with one provider registered beside each probe or casting spec. Providers emit verbs with parameters: `begin`, `release{fill|stage|bash}`, `press{window}`, `aim{turn_rate}`, `toggle_off`, `meta{weave}`, `reload{face}`, `spend{charge, per_charge}`, `summon{room}`, `command{kind}` and `move{kind, distance, behind}`. They also emit derived facts:
   - the reach band (forecast region, else range)
   - root seconds (cast time x (1 - move factor))
   - cost per resource
   - delivery family

   A mechanic the capability map adds registers its probe, an affordance provider and any new perception names. The chooser does not change. Two examples:
   - An interrupt mechanic adds `interrupt{window}`, fed by the foe's cast observation.
   - A facing shield adds a `target_arc` input.
2. **Perception, lazy.** Registered name providers compute a value only when a doctrine reads it, and each read is recorded for explanation. The name families:
   - `kit.*`: reach bands, ready count
   - `slot.*`: uses, stack, brim
   - `held.*`: fill, stage, pulses
   - `threat.*`: from `WeaveRuntime.DescribeCast` on nearby hostile casters
   - `crowd.*`: foes and allies in a radius, best cluster
   - `corpses.*`, `minions.*`
   - status flags
   - `hurt_by`: written by a `DamageDealt` subscriber. This revives the dead attacker memory and lets a monster answer fire from beyond its aggro range.
3. **Options** are an affordance bound to a target, point or direction this think. The target comes from the current foe, nearby foes, allies in need, corpses, or points clustered from forecast candidates. During a held cast the options are `release`, `aim`, `cancel` and `continue`.
4. **Vetoes, then considerations.**
   - Vetoes are the runtime's own pure checks: ready, cost, gates, forbidden tags, the target kind and its `require`, and reach by forecast where a provider exists. Each failed veto records a reason.
   - Considerations are registered scorers `(ctx, option) -> [0, 1]` with declared inputs, shaped by named curves (`Expressions/Curves.cs`). Examples: `in_reach`, `candidates`, `cost_margin`, `root_exposure`, `fill_value`, `charges_value`, `bank_need`, `ally_need`, `room`, `status_synergy` (`consumes:x` on a target bearing x), `threat_inside`, `novelty`.
5. **Score:**
   - `score = band(verb) x weight x prod(c_i ^ w_i) x makeup(n) x (1 + hysteresis if current)`.
   - Bands (`reflex`, `sustain`, `act`, `upkeep`) are ordered by the doctrine.
   - Selection takes the best option. With a doctrine temperature it instead picks seeded among options within that fraction of the best, from the `ai` stream, with ties broken by slot order.
   - Forecasts run only for the top k options, and position advice only on wakes or every few thinks, to stay inside `system.behavior` <= 3 ms and 1 MB.
6. **Doctrines are content** (`doctrines` kind, validated, `extends`). Each doctrine holds:
   - per-verb consideration lists with curves and weights
   - reserves, temperature and reaction delay
   - a `move` section for the position advisor

   Weights are expressions over the actor's stats, so statuses and passives bend minds through the one modifier engine (`ai.caution`, `ai.precision`, `ai.reaction`). A doctrine is named by:
   - a pattern (`behavior: { id, doctrine }`)
   - a state action (`fight: { doctrine }`)
   - a summon pool entry
   - later, Weft agent kinds

   Behaviors stay as the outer mode machine. The per-weave `ai` hint shrinks to overrides (`weight`, `when`, `never`); `role` and `preferred_range` retire because both can be derived.
7. **Output** goes through the intent seam (section 6). Each decision publishes `AiDecided` (section 5).

```yaml
kind: doctrines
entries:
  - id: skirmisher
    bands: [reflex, sustain, act, upkeep]
    temperature: 0.1
    reaction: "0.35 - 0.2 * ai.precision"
    reserve: { mana: 0.2 }
    begin:   [ { consider: candidates, curve: linear, weight: 1.5 }, { consider: root_exposure, curve: inverse, weight: 2 } ]
    release: [ { consider: fill_value, curve: smoothstep }, { consider: target_leaving, curve: linear } ]
    spend:   [ { consider: charges_value, curve: quadratic, min: 3 } ]
    move:    { band: kit, keep: [0.6, 0.95], terms: { threat_exposure: 3, target_arc: 0.5, ally_spacing: 0.7, orbit: 0.4 } }
```

Section 2's last two columns are the affordance catalogue for the held, bank, toggle, spend, target-kind, corpse, summon and movement mechanics. The other probes become considerations: returning (stand on the return path), homing (evasive targets), chain, contagion and cascade (clusters), shove and pull (into fields, out of melee), fields and telegraphs (stationary foes, chokes), constructs (the fight's centre), afflictions (when absent), ruptures and consumes (high stacks), leech (when hurt), triggered weaves (arm or disarm).

## 4. Positioning

**Today:**

- `chase` stops at 0.9 x the longest hinted range of any weave, dashes and summons included.
- `aggressive_ranged` kites with `flee 5` below a literal `target_distance < 2.5`. A successful cast stops the motion and `casting.cast_move` is 0, so the kite is a stutter-step: the kindler covered 1.9 units in 2.25 s of kiting.
- The boar's gore whiffed 2 of 4 times, because the chooser's range plus both radii exceeds the cone's real reach.
- Ranged bodies never open distance to their band. The fletcher and the evoker traded at 5.1 units with preferred ranges of 10.6 and 14.4, and the fletcher walked 0.0 units while the frozen orb passed over it.
- Fleeing is straight away from the target. Minions follow, heel and teleport; orders march to a mark.
- Missing entirely: avoidance, flanking, spacing, line of sight.

**Behaviors the mechanics should make possible** (from the prototype's catalogue, names only):

| Behavior | Needs |
|---|---|
| Orbit / strafe at reach | `reach_band` + orbit tangent; `MoveDir` while a `mobile` cast runs |
| Standoff / hold range / kite | `reach_band` from my kit and the foe's threat reach |
| Slide-cast | `root_exposure` on `begin` options; prefer `mobile` weaves on the move |
| Hit-and-run | `threat_exposure` with the foe's next emission time |
| Crossfire | `line_of_fire` angle against allies' lines |
| Surround / encircle | `ally_spacing` with claimed bearings |
| Formation, escort, interpose | `post` relative to a leader, owner or ward and the threat line |
| Flock | `ally_spacing` separation and alignment |
| Lurk, backstab, flank, beat a guard's arc | `target_arc` |
| Charge / gap close / escape | movement weaves scored at their landing point |
| Dodge | `threat_exposure` + reaction delay |
| Rout, refuge, ward | `panicked` flag + `post` at a refuge or guardian |
| Support from behind | `reach_band` toward allies + `ally_need` |

**Smallest set that covers them.** Seven movement considerations, each scored on 8 to 16 candidate points: rings at band radii around the target, the current spot, the retreat line, posts, and the landing points of movement weaves.

1. `reach_band`
2. `threat_exposure`: inside a hostile region before I can leave
3. `target_arc`
4. `ally_spacing`
5. `post`: home, leash, mark, formation or ward
6. `line_of_fire`
7. `travel_cost`: path time, roots, hazardous fields

Add an orbit tangent term, and steer through `MoveDir`, `MoveTo`, `Stop` and `Jump`.

**What PR #20 (`codex/shared-cast-forecasts-integration`, record `2026-10-17-positional-hits.md`) provides or touches:**

- **Provides:**
  - `ResolvedAreaPlan.IsCandidate` for areas, including the new melee sectors (`reach_band`, `threat_exposure`, frontal arcs for `target_arc`).
  - `WeaveRuntime.DescribeCast` (emission, ready and max-hold deadlines, direction commitment, region), the threat input.
  - `WeaveMissed` reasons (whiff feedback).
  - Release-time target checks, so moving out of a swing actually avoids it.
- **Touches `ChooseForAi`:** it adds `DeliveryForecasts.CandidateForAi`, which keeps closing until a single-root cast or instant area reaches. That fixes the gore whiffs, but it puts the chooser in an open PR.
- **Does not provide:** projectile, field or chain providers (those stay "unknown", which is not "safe"), an avoidance action, crowd queries, or steering.

## 5. Observability

**What exists today:**

- No AI event at all.
- The runtime publishes `WeaveStarted`, `WeaveCast`, `WeaveFailed` (with reason), `WeaveReleased` and `WeaveInterrupted`. Orders publish `MinionsCommanded`. Unknown or failing actions log a warning or an error.
- The `fight` trace prints casts, damage, beats, releases and triggers. It does not print `WeaveFailed`, so the mower's 22 refusals are invisible there, and it omits states, targets and positions.
- Nothing reaches the wire, and `bench` shows only `system.behavior` time.

**`AiDecided` should carry:**

- tick, actor, behavior and state, doctrine
- wake reason (cadence, hurt, threat, order)
- target and why it was chosen
- the perception names the considerations read, with their values
- the chosen option: verb, slot, weave or face, target, point, direction, intent type
- its score and top terms: name, input, curve output, weight
- up to 3 runners-up
- vetoed options with their first failed check (`cooldown`, `cost:mana`, `bank_empty`, `gate`, `target_require`, `out_of_reach`, `forbidden`)
- movement advice: point and top terms
- the outcome: accepted, or the runtime's refusal reason

**Gating:** rule `ai.explain` (`off`, `changes`, `all`) plus a watch set. Every publish allocates a log entry, so hundreds of thinkers cannot publish every think. It is never on the wire by default. `fight --explain` and the tournament read the same events.

## 6. Split of work

**Codex** builds positioning on its forecasting lineage, after #20 merges:

- `Ai/Positioning/`: the position advisor, the seven movement considerations, the candidate-point generator, steering, and movement weaves as positional options.
- `Ai/Perception/Threats.cs`: `threat.*` from `DescribeCast`.
- Forecast providers for projectiles (path envelope), fields and chains.
- `move` sections of doctrines for its orc, goblinkin and undead packs, whose behaviors already use only the old actions.

**This session** builds the rest:

- **The intent seam:** actors send intents through `IntentHandlers`, with `IntentContext` generalized from `Player` to any actor. The AI planner's straight-walk and budget policy moves into the `MoveTo` handler so a click and a chase plan alike. Constructs cast through `TryBegin` with a source override instead of `ExecuteAll`.
- The affordance registry.
- The chooser: options, vetoes, non-movement considerations and scoring, covering held modes, banks, toggles, metas, spends, target kinds, corpses, summons, presses and aim.
- The `doctrines` kind, its validator and the hint migration.
- `AiDecided` and the explain tooling.
- `hollowwake tournament` (the harness `design-grammar.md` names): seeded pattern x doctrine x seed matrices reporting win rate, time to kill, the share of refused intents, affordance coverage (every kit verb used), whiffs, damage avoided and release fill. It also serves the Weft balance harness (plan F5).

**Interface, so neither side blocks the other:**

1. **The seam ships first, as a small PR.** `AiIntents.Send(AiContext, IIntent) -> IntentOutcome` dispatches synchronously in Think (an enqueue would act a tick late, after Input). All movement leaves the AI through it as `MoveDirIntent`, `MoveToIntent`, `StopIntent` or `JumpIntent`.
2. **`IPositionAdvisor`** is a registered service: `Advise(ctx, PositionRequest) -> PositionAdvice`.
   - The request carries per-option reach bands (from affordances), the doctrine's move terms and the commitment under consideration (root seconds).
   - The advice returns the point or steering, its terms, `threat_now` (inside a region, seconds to emission) and per-option `reachable_now`.
   - This session ships a default advisor that reproduces chase and kite byte-identically. Codex replaces it by registration.
3. **One `Considerations` registry and one `PerceptionNames` registry,** each with an `act` and a `move` family owned by the respective side. Doctrine validation refuses unknown names.
4. **`AiDecided.Movement`** is filled by the advisor.
5. **Order:** #20 merges first. The new chooser lives in `Ai/Choice/` beside `ChooseForAi`, and `attack` keeps the legacy path for any body without a doctrine, so fight traces stay byte-identical until the switch-over PR. After the seam lands, both halves proceed in parallel.
