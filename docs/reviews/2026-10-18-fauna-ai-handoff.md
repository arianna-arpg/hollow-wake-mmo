# Fauna AI handoff for Claude

2026-10-18. Proposal for joint design review, not an implemented AI architecture or an approved replacement plan. Content branch: `codex/fauna-critter-foundation`, stacked on Orc PR #33. The owner explicitly expects a substantial AI rebuild which uses the real combat mechanics. This pass intentionally adds no AI runtime code.

## What exists and what is provisional

Wildlife has authored dimensions, material mixtures, poise, faction, habitats, role tags and current weave kits. Ambient critters use the content-only `fauna_flee` state machine, have no attack weave or drops component, and opt out of culling contracts. Predators use `aggressive_melee`; territorial animals use `guard`; defensive prey uses `skittish`. The migrated boar retains its existing aggressive behavior. These assignments are replaceable scaffolding. The `meta.ai_handoff` ledger records author intent only; the simulation must not read it.

`BehaviorSystem` currently acquires nearby faction enemies, prefers recent attackers and applies ignore tags. It does not implement a food web, sensory uncertainty, persistent social knowledge or coordinated tactics. `WeaveRuntime.ChooseForAi` already checks readiness, costs, ranges, gates and AI hints and casts through the normal runtime. Preserve that authority. Its preferred range is the longest hinted range; a long support skill can strand a mixed melee/support kit unless content constrains chase distance. Orc leaders already illustrate this limitation.

All wildlife remains in `wilds`. Those bodies are allied with one another and hostile to players by current faction rules, including ambient critters that only flee. The relation is attack eligibility, not a plausible model of fear, diet or aggression. Current `skittish` only enters defence at low life and very close range, without detecting whether escape is blocked. Ground birds do not fly, otters do not swim and ibex do not climb. Crab limbs reuse the crawler pose, not an authored scuttling gait. Pack sizes are spawn groupings, not coordination.

## Decisions to settle together

1. Separate permission to attack from perceived threat, fear, territorial response, prey preference and social allegiance. Avoid encoding an entire food web as faction hostility or species-name branches. Decide which relationships are authored tags/profiles and which are short-lived observed state.
2. Agree on a bounded observation contract: visible/heard events, occlusion, memory expiry, uncertainty, retaliation, owner orders and last-known positions. No omniscient access to future player inputs. Define reaction time and readable commitment/cancellation windows in content.
3. Evaluate a layered planner: observations -> needs/goals -> feasible actions and positions -> budgeted scoring/selection -> existing intents/weave runtime. Compare a utility selector, state machines with utility actions and a bounded task planner using the same scenarios before choosing. Existing state machines can remain a fallback adapter during migration.
4. Derive action feasibility from compiled weaves and shared mechanics. Account for costs, resource reservation, cooldowns, banks, channels, ranges, line of sight, target volume and elevation, projectile travel, blast footprints, ally exposure, existing statuses, poise, mass and displacement. Do not duplicate damage or modifier calculations inside AI, and do not promise mechanisms absent from the combat runtime. Candidate forecasts must use the shared forecast contract where available.
5. Decide how group roles arbitrate: pack pursuit/flanking and retreat, herd alarm/escape, guarding young, leader orders, minion owner overrides and resource support. Coordination should exchange bounded observations/intent, not hand every creature the world's complete state. Define who may reserve a position or target and when reservations expire.
6. Preserve deterministic seeds, stable tie-breaking and replayable state transitions. Bound perception, candidates, forecast work and path searches per think; spread decisions across ticks without wall-clock inputs. Retain path caching, failed-search suppression, server authority and the single attribute/modifier engine.

## Simulation scenarios proposed for the next pass

| Scenario | Mechanics exercised | Failure evidence to capture |
|---|---|---|
| Hare disturbed, pursued, then left alone; same run with an escape blocked | observation, flight choice, recovery, hysteresis | running toward danger, permanent aggro, target churn, corner oscillation |
| Hungry and sated wolves approaching a herd with an intervening wall | diet/need, shared observations, reachable positions, pack roles | faction-driven indiscriminate aggression, omniscient flanks, repeated failed paths |
| Boar defending ground against a mobile player; narrow gap and wall variants | melee reach, commitment, collision, displacement and poise | attacking outside reach, cancel spam, ineffective shove choice |
| Bear and aurochs against the same control-heavy player kit | actual mass, poise breaks, status gates, recovery | choosing resisted control without learning, alternate stat formulas |
| Ibex or stag near a ledge and a safe longer path | body dimensions, ground traversal, fall risk | invented climbing ability, suicidal shortcut, unreachable-target loops |
| Melee Orc leader with a longer-range shout and wounded allies | support value, preferred distance, allies, cooldowns/resources | endless shouting outside melee, redundant healing, resource waste |
| Projectile caster facing moving targets, obstacles and mixed-height bodies | shared forecasts, travel time, hit volumes, elevation | free aim knowledge, invalid shots, mismatch with authoritative hits |
| Summoned squad receives hold, recall and assault during combat | ownership, orders, leash, command precedence | owner overrides ignored, repeated recall teleport, stale reservations |
| Ground birds disturbed repeatedly; frog/otter near water boundaries | sensory recovery and current locomotion limits | accidental flight/swimming claims, spawning on unusable terrain |

Use fixed seeds with small diagnostic arenas first, then terrain variants, mixed rosters and representative population loads. Change one capability at a time; retain baseline state-machine runs and compare event digests for deterministic replay. These scenarios are requirements for discussion, not evidence already gathered by the fauna pass.

## Observability and acceptance

A sampled debug decision should name tick/entity, observation age, target/goal, evaluated candidates, rejection reason, estimated utility, chosen intent, budget consumption and ensuing authoritative outcome. Keep this server-side/debug-only unless a separate protocol proposal justifies client exposure. Record hit conversion, wasted resource/cooldown use, idle time with feasible actions, target churn, unreachable retries, path work and decision allocations alongside survival and encounter duration. Win rate alone rewards unfair or unreadable AI.

Require exact deterministic event digests for repeated runs of the same seed and inputs; validate that every selected action still passes authoritative cost/target checks; show that unobserved player input cannot affect choice. Test players' opportunity to react as well as tactical effectiveness. Keep the current performance budgets: zone tick p95 <=25 ms; behavior <=3 ms and <=1 MB per tick in the standard Release benchmark; snapshot build <=1 ms. Record candidate counts and cost by subsystem before negotiating any new budget. Scenario-specific tactical targets need agreement after measuring the baseline, not invented pass percentages now.

## Migration and explicit review asks

Claude: review the above decomposition, propose the smallest observable scenario harness and compare planner options before implementation. Identify the shared capability/forecast interfaces that combat packages already supply, including pending PR #20. Agree on deterministic scheduling, reaction fairness and group ownership, then record the chosen contract/ADR and staged acceptance evidence. Keep experiments under `experiments/ai/` until the design is supported by measurements.

Replace behavior bindings per content profile behind an explicit migration path; preserve old profiles until new scenarios pass. Do not promote `meta.ai_handoff` into runtime configuration by reading presentation metadata. Add a validated definition kind if the agreed design needs AI profiles. The separate precise-hitbox chat must migrate these wildlife bodies along with the existing rosters; model decoration must not silently change authoritative mass or collision.

Adjacent issue observed: generated cull quests choose from the global matching-pattern catalog without a local-population/level filter. The expanded eligible set can offer a distant or higher-level beast. This pass only excludes ambient critters; habitat-aware quest selection belongs in a coordinated generation/quest change, with new seed evidence.

Acknowledged by: pending Claude design review.
