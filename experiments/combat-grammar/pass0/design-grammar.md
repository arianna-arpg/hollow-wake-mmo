# Design grammar: pass zero

Read-only scout at `14f7f3b` (later merges change nothing here); siblings `capability-map.md` and `ai-inventory.md`. Counts are approximate, from a text scan of `content/core/combat` (62 forms, 245 presets).

## 1. The combat grammar

A cast is a point in a product of axes. The compiler already owns the grammar's four operators: **quantities fold** (attributes through the one modifier engine), **choices take precedence** (patches), **riders append** at named moments (hooks), and **graphs nest** under depth caps. Prior art agrees. Path of Exile decides which supports apply from what a skill does, not from its tags [1], tiers each support's *more* multiplier by the severity of its drawback, makes triggered skills pay their costs [2] and lets them earn no trigger fuel [3]. Noita's wands draw spells like cards from a deck [4], and its runaway combinations were answered with caps on recursion and on what one spell may add, not with per-combination fixes [5]. Diablo III replaced thousands of rune items with one rankless choice per skill [6]. An axis is real when the compiled weave states its value (probes, folded attributes, the patched graph) and every value composes with every value of every other axis without a special case.

| Axis | Decides | Unit and range today | Moved by |
|---|---|---|---|
| **Geometry** | where a resolution applies | shape (10 registered); radius and width in units (areas 2.4 to 6); angle in degrees; `inner` a fraction; counts | shape patches; the `area.*` fold |
| **Kinematics** | how deliveries and bodies move | units a second (flights 15 to 25); degrees a second (12 trajectory axes); pierce, fork, chain, bounce counts; return; shove distance | the `projectile.*` fold; `fire`, `return` patches; `immovable`, `rooted` |
| **Payload** | what lands on a body | amount per Thread (9); status magnitude, duration, stacks; resources; displacement; summons; sequels | graphs, hooks, Threads; `added.*`, `convert.*`, `ailment.*` |
| **Economy** | what a cast costs and banks | costs 2 to 25; cooldowns 0 to 10 s; uses 1 to 8; charges (19 kinds, 3 to 6 held); brim 0 to 1; stacks; reserve; upkeep; corpses and minions spent | the `cost` fold, `cost.added`, `uses`, `spend`, `self_stack`, `cooldown.*`, `charge.*.max` |
| **Timing** | press to last resolution, and the exposure it costs | cast 0 to 1.5 s; beats 0.4 to 0.5 s; holds 3 to 8 s; stages; fill 0 to 1; windows 0.2 to 0.25 s; fuses, telegraphs; movement kept 0 to 1 | the cast mode and its block; `cast.time`, `channel.*`, `repeat.*`; speeds; stun, freeze, `forbid:` |
| **Targeting** | what is selected, and when it is checked | mode (6), target kinds, filter (6), pick (5), caps, `require`, aim, homing and chain picks | target block; `pick`, `filter` patches; `aim.spread`; `vs.*` scopes; taunt, `untargetable` |
| **Agency** (new) | who acts after the press: the caster, an inert carrier (flight, field, tether), a reactive device (trap, totem), a deliberating proxy (minion) | autonomy, live count, lifetime, leash | summon, deploy, raise; `brood`, `resonant_key`; `minion.*` |
| **Causality** (new) | what starts a resolution and how far it cascades: press, reflex, release, trigger, proc, follow-up, repeat, cycle, meta, sequel, hook | depth (effects 12; procs 3 at falloff 0.5; triggers 4); chance, icd, ppm; repeats up to 50 | `trigger`, hooks, `follow_up`, `repeat.*`; `proc.<id>`, `trigger.chance`; status hooks |
| **Coupling** (new) | state one cast writes on a body and another reads | status, charge, gauge, flag and victim-tag keys with magnitudes and caps | hooks write; `vs.*`, gauges, `require`, `spend`, `consume_status`, `rupture` read |

Forms and presets set every axis; glyphs, passives, items, statuses and weave trees move them (modifiers by tag scope; glyphs and trees also patch and hook). Two faces are **derived, never authored**: *legibility* (where and when a cast can apply, from the plan `DeliveryForecasts` shares with execution) and *value* (output per cost and per second of exposure, from the harness). A telegraph that is not the forecast, or a power number that is not measured, is a second truth.

**Composition.** Geometry with kinematics is a swept region, with timing area-time, with targeting candidates. Economy meets timing where costs land (press, beat, stage, upkeep) and causality where triggered casts pay honestly and free procs are capped by chance, icd and depth. Glyphs should gate by capability (probes) and modifiers scope by tag, the distinction Path of Exile arrived at [1]. Coupling is the join: a setup writes a state key, a payoff on any axis reads it (a radius by stacks, a release by a bank, a pick by lowest life); the archetypes live there. Agency recurses: a proxy casts on all nine axes with its owner's stats.

**Invariants across every combination.**
1. One pipeline: every hit, tick, rupture and pulse runs the stage ladder, for every actor; nothing is unmitigated.
2. Termination: every flight, hold, field, toggle and chain ends; every bank caps and decays.
3. Bounded fan-out: a press yields finitely many resolutions (depth rules, icd, chance caps, `casting.max_repeats`); hook-added effects are never hooked; an event fires at most one triggered seat.
4. Determinism: every roll draws from a seeded stream; reading a bank, forecast or hint changes nothing.
5. A target is an aim, never a guarantee: each application re-tests position, arc and line of sight.
6. Quantities fold, choices patch: a literal in a graph is a frozen axis.
7. Execution and forecast share one plan.
8. A cast that resolves has paid; an interrupted one fires nothing.
9. Steady state allocates nothing and stays inside `docs/design/performance.md`.

**Where content and engine fit, and where they do not.**

| Axis | Fits | Does not fit (file, field) |
|---|---|---|
| Geometry | shapes, `area.*` | flight hit size is literal (`radius:` on 103 flights, `weaves-projectiles.yaml`; no `projectile.radius`); `chain` `range: 6` (`forms.yaml`, `bolt`) |
| Kinematics | trajectory attributes | `fork_children`, `fork_angle`, `chain_range`, `homing_radius`, `orbit_radius` are global rules (`rules-delivery.yaml`), so no glyph forks into three; `dash` `speed: 24` (`forms.yaml`) |
| Payload | Threads, hooks | the scaling law is copied into graphs (the weapon formula 95 times, `0.08 * (weave.level - 1)` 119 times; `nova` grows nothing, `mend` 0.1); a named basis per family, like `combat.ailment_magnitude.*`, would let a band move it |
| Economy | costs, banks, reserve | `riposte` (`charges.yaml`) is gained by nothing; `brim` lives only on a form (`forms-casting.yaml`) until the held-mode preset seam lands |
| Timing | six modes, timing, toggles | presets cannot carry `channel`, `charge`, `overcharge`, `guard`, `timing` blocks; `concentration` (row A13) is absent; the AI never sends `ReleaseWeaveIntent`, `PressWeaveIntent` or `AimIntent` (`Ai/BehaviorSystem.cs`, `AttackTarget` calls `TryBegin`), so holds run to their limit |
| Targeting | target kinds, filters, picks | the AI's target is `acquire_target` (nearest, recent attacker), never scored |
| Agency | summons, orders | minions decide with the same hints; `minion_*` behaviors read assist and command only |
| Causality | two dozen trigger kinds | content subscribes six (`hit`, `hurt`, `crit`, `channel_beat`, `kill`, `cast`); `parry`, `flawless`, `release`, `guard_beat`, `block`, `evade`, `break`, `collision`, `charge_gained` have none |
| Coupling | banks, pops, gauges, `vs.*` | read verbs in 8 of 307 entries; `detonate` and `move_status` unused |
| AI hint | `when`, `min_range`, `range_attribute` | `ai.role` (`Definitions/Combat.cs`) is read by nothing; 165 of 166 preset hints say `damage`; one reads an economy (`gyre_hurl`) |

## 2. The playstyle space

Archetypes are a setup verb, a payoff verb and the economy between them. Columns: FL flights; AR areas, chains; LG fields, auras, tethers; PL scatter, cascade, delay, detonate; MV displacement; SC status coupling; BK banks; HM held and timed modes; CS triggers, procs, repeats, sequels; PX proxies; TG toggles. **●** defining: target three exemplars differing on two axes or more, each played by a doctrine in a scenario test. **○** supporting: target one. Numbers are today's forms and presets; `g` is a glyph or proc only.

| Archetype (economy) | FL | AR | LG | PL | MV | SC | BK | HM | CS | PX | TG |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Banks and vents (charges, uses, brim) | ○5 | ○8 | | | | | ●13 | ○2 | ○0 | ○0 | |
| Holds and releases (exposure) | ○4 | ○4 | | ○0 | ○0 | | ○3 | ●11 | | | |
| Marks and detonates (victim stacks) | | ○3 | | ○0 | | ●6 | | | ○0 | | |
| Spreads and infects (crowds, deaths) | | ○1 | ○0 | | | ●1g | | | ○g | ○0 | |
| Commands and sacrifices (minions, corpses) | | | | | ○0 | | ○0 | | ○3 | ●31 | ○2 |
| Pulls and punishes (walls, clumps) | | ●10 | ○2 | ○2 | ●22 | ○1 | | | | | |
| Places and denies (area-time) | | | ●18 | ●6 | ○4 | | | ○0 | | ○1 | |
| Guards and answers (the foe's tempo) | | ○2 | | | | | ○0 | ●2 | ●g | | |
| Echoes and cascades (depth, chance) | ○3 | ○2 | | | | | ○1 | | ●8g | ○3 | |
| Strides and strikes (angle, cooldowns) | ○0 | ○2 | | | ●6 | ○g | | ○0 | | | |
| Reserves and sustains (reservation) | | | ○2 | | | ○2 | ○0 | | | | ●6 |

The AI plays every cell by weighted chance: it never releases, presses, aims or turns a toggle off, scores no target and reads no wall, crowd or mark; one hint reads a bank. Least covered: **guards and answers by causality** (one glyph; nothing subscribes `parry`, `block`, `evade` or `guard_beat`; `riposte` never gained), **spreads and infects by coupling** (`plague_rite` and the `contagion` glyph; `move_status` unused), **holds and releases as played** (eleven exemplars, no AI competence). The mass sits elsewhere: 136 of 245 presets are on `missile`, `swing` and `shot`.

**Exemplars to port**, picked for the axes they stress (none waits on a blocked row):

| Archetype | Prototype exemplars |
|---|---|
| Banks and vents | `galvanic_reserve` (banked by walking and being struck, vented at the nearest five), `soul_harvest` (deaths as currency), `head_of_steam` (a channel filling still banks) |
| Holds and releases | `flame_blast` (silent gather, scaled release), `immolation_rush` (charge-up dash), `arcswarm` (release detonates the channel's orbs) |
| Marks and detonates | `living_bomb` (bursts on expiry), `boil_over` (ruptures banked scald), `corpse_explosion` (a corpse as the mark) |
| Spreads and infects | `contagion` (leaps on death), `epidemic` (copies statuses to neighbors), `transfusion` (pours your afflictions onto a foe) |
| Commands and sacrifices | `command_assault` (orders, pinned quarry), `last_rite` (every minion detonates), `hell_rift` (a construct firing along its owner's aim) |
| Pulls and punishes | `drowning_grasp` (pull and root), `shield_charge` (a battering dash), `lodestone` (a construct foes must attack) |
| Places and denies | `stone_rampart` (walls with life), `creeping_ice` (a crawling field), `rune_of_power` (an ally domain to hold) |
| Guards and answers | `riposte` (parry answered at 220%), `iron_ward` (bank what lands, release it), `pain_hounds` (blows taken loose hounds) |
| Echoes and cascades | `time_dilation` (shaves running cooldowns), `verdict_release` (a bank earned by another weave's uses) |
| Strides and strikes | `iai_strike` (a timed phasing dash), `mark` (rune, then recall), `phoenix_dive` (untargetable leap) |
| Reserves and sustains | `righteous_fire` (a ring priced in life), `mana_shield` (damage paid from mana), `soul_link` (wounds flow to minions) |

**Mechanic by counter.** Each mechanic needs two counters a player can execute; **✗** is a missing verb.

| Mechanic | Avoid (space) | Interrupt (time) | Mitigate (numbers) | Strip (state) | Punish (commitment) |
|---|---|---|---|---|---|
| Flights | sidestep, walls, leaps | stun the bar | evasion, guard arc, block, resist | a dome catches them | close on the kiter |
| Areas | leave the forecast | stun, freeze, `forbid:` | guard arc, resist | | the recovery |
| Fields, auras | leave; `exposure` | kill an aura's bearer | resist | **✗** nothing ends a hostile field or domain | |
| Placements | read the telegraph | a vanished source cancels them | resist | | the delay |
| Displacement | `immovable`, `rooted` | stun the dasher | **✗** no graded resistance | | walls punish both sides |
| Status coupling | evasion; block stops ailments | kill before the rupture | `status.resistance`, immunity | `remove_status` | spread out |
| Banks | deny the gaining events | stun the vent | `crit.avoid` | charge drain | burst the banker |
| Held and timed | leave the tracked arc | stun, silence, poise break | strike behind; damage over time skips guards | break the pool | bait the release |
| Causality | deny the trigger event | | | | fewer, larger hits against `hurt` |
| Proxies | outrun the leash | stun the summoning | areas | consume corpses first | dive the owner |
| Toggles | | **✗** silence leaves it on | | starve its upkeep | the lowered maximum |

## 3. The AI as a player

What the literature asks of an AI that plays the mechanics, as data:
- **Score an action with a target** every think: each consideration is an input id with parameters, normalized between bookends, shaped by a preset curve and multiplied; a zero vetoes, so cheap inputs run first [7][8][10].
- **Tiers apart from scores** (rank or score bands, then the best or a weighted draw among the top few) and **hysteresis** (a commitment bonus, runtime and cooldown curves), since dithering destroys legibility [9][10][11][18].
- **The ability carries its AI knowledge**: Dragon Age: Inquisition registers a behavior snippet per ability that returns a score and a target [11]; Paragon digests ability tags into cached range, cost and cooldown, so every hero runs one tree [12].
- **Space computed once**: per-faction proximity and threat maps stamped from templates sized by speed and reach, refreshed about each second, combined per query [14]; positions from generate, filter and score queries [15]; steering as its own layer [16]; planning over world-state symbols for multi-step plays [17].
- **Fair and legible**: beliefs filtered by perception, no cheating, intent shown [18][19]; no queries into fog of war [20]; attack-capacity budgets, not separate behaviors, set pacing and difficulty [13].

Mapped onto this engine:
- **Inputs**: a registry `AiInputs` beside the trigger kinds and probes, each a pure read of perception or the compiled weave: range fit (distance over the folded `range`), forecast candidates (`DeliveryForecasts.Describe`), `CanAfford`, bank fill (`UsesNow`, `charge.<id>`, brim), `cast.fill`, `cast.stage`, target statuses, guard facing, walls, clump size, minions, corpses, threat.
- **Curves**: `Expressions/Curves.cs` plus parametric families; an unknown curve name, silently linear today, must fail validation.
- **Doctrines**, a content kind: decision templates binding an intent (use, release, press, aim, cancel, toggle off, command, move to a scored point) to a weave query by mechanisms and tags, a target selector, considerations, a rank and a weight; knobs for reaction delay, noise, commitment, risk and attack capacity. `behaviors` keep the outer states and call `decide { doctrine }` when engaged; a weave's `ai` hint shrinks to optional extra considerations, and `role` goes.
- **Explainability**: `AiDecided` (decision, weave, target, score, runner-up, inputs), published only when subscribed, printed by `fight --explain`, counted by the tournament. **Space**: a faction threat layer from hostile reaches and forecasts, inside `system.behavior` (3 ms).

It must never:
- **read hidden state**: random streams, unapplied aims, cooldowns, banks or numbers the interface does not show, forecasts of casts not begun;
- **act outside the simulation**: it enqueues the intents a client sends (`UseWeaveIntent`, `ReleaseWeaveIntent`, `PressWeaveIntent`, `AimIntent`, `CancelCastIntent`, `MoveToIntent`, `CommandMinionsIntent`) through their validation, never `TryBegin`; difficulty is content and modifiers, never extra rolls, free costs or hidden stats;
- **carry bespoke code**: no switch on pattern, weave, form or glyph ids; a new mechanic becomes playable by registering an input or action and writing content;
- **break determinism or budget**: randomness from `sim.RngFor(entity, "ai-...")`, early exit on a zero, less thinking with distance.

## 4. Measurement

Per fight, aggregated by archetype and scenario with 95% intervals:
- **Time to kill and damage taken per kill** (medians, spreads, 90th percentiles); in PvE scenarios, the proficiency score of Pfau and colleagues: the mean of win, unused time, own life left and the foe's life taken [21].
- **Engagement**: the share of fights in which the archetype's defining payoff happened (a release at fill 0.5 or more, a vent of two charges or more, a payoff on a marked target, a triggered answer). **Restricted play**: how often a build withholding that mechanic beats the full build; near one half, the mechanic does not matter [22].
- **Mechanics per fight** (distinct probes among resolved casts) and **decision diversity** (entropy H of `AiDecided` choices, reported as e^H).
- **Win-rate band and counter cap**: each archetype's interval against the field inside a band (Overwatch keeps heroes within 45 to 55% [23]; Riot sets thresholds per skill audience [24]), no matchup past a cap such as 70 to 30, mirrors at one half as the harness's own check. A two-point interval needs about 2,400 games (n ≈ 0.96/h²), so the nightly run judges bands.
- **Variety index**: V = 1/ΣP_a², where P_a sums the maximum-entropy Nash mass of archetype a's builds over the logit win-rate matrix: the effective number of viable archetypes, Sirlin's test of balance made a number [26]. Nash averaging, unlike Elo, is not moved by duplicated builds [25], and summing by archetype keeps V so. A MAP-Elites map over build descriptors shows where viable builds live and flags must-picks [27].
- **Causal fan-out**: the most resolutions one press caused.

**Harness.** `hollowwake tournament <roster> --scenarios duel,walled,pack,elite --seeds N --json --check` turns the `fight` arena into a runner returning a record, seeded by `Rng.Hash64("tournament", a, b, scenario, i)` so `fight --seed` replays any fight. The roster is ordinary patterns tagged `archetype.<name>`; scenarios are arena variants (open, walled, crowded) and PvE packs, elites and a boss from existing patterns. One JSON line per fight: winner, time to kill, damage by Thread, events, mechanisms, decisions, fan-out. `bench --only tournament` keeps `system.behavior` in budget. Simulated balancing has precedent [21][28].

**Bands are content**: a `balance` rule group keyed by archetype tag and scenario (`ttk.median`, `win_rate.mean`, `win_rate.matchup`, `engagement`, `restricted`, `entropy`, `variety`), written before the run they judge; `tournament --check` names each failure as validation does.

**Human rubric**, per build and session: the Player Experience Inventory (30 validated seven-point items over ten constructs, among them mastery, challenge, autonomy, curiosity and ease of control) [29], plus five items: I saw what the enemy was about to do; I could name its archetype (scored blind); my last two builds played differently; when I lost I knew what I could have done; I want to try another build. Within-subject, counterbalanced, think-aloud, logged with the harness's events; 25 to 35 players a test, as in Microsoft's consumer playtests [30].

**Evidence from outside the team**: outside testers name archetypes blind well above chance; harness power and perceived power agree in rank; human telemetry falls inside the AI's distributions, as Pfau and colleagues validated replicants against Aion players [21]; someone outside reproduces the numbers from the seeds.

**Gate first on engagement with its restricted-play control.** Until the AI demonstrably plays each archetype's mechanic and withholding it demonstrably costs, win rates and the variety index measure the AI's noise, not the design.

## 5. The passes

Each slice proves one axis or doctrine end to end with a review record, keeping the standard duel hashes unless it names why: grammar, instrument, AI, tuning.

1. **Axes read from the compiler** (pass one): `weave --axes` places every form and preset on the nine axes from probes, folds and the graph, with a warning baseline for literal quantities. An axis the compiler cannot state is not an axis.
2. **The instrument** (pass one): `tournament` on the `fight` arena; mirrors within the interval of one half, identical reruns, a recorded baseline of today's state machines.
3. **Geometry and kinematics as attributes** (pass one, the morphable deliveries): `projectile.radius`, `chain.range`, fork children and angle, orbit radius and dash speed become attributes whose rule defaults keep traces; one exemplar per delivery morphed by glyphs alone through a pairwise covering of axis values, every invariant asserted per case; bench inside budget.
4. **Intents and decisions** (pass two): the AI enqueues client intents; `AiDecided`; `fight --explain`; traces change only where holds are now released.
5. **Doctrine: holds and releases** (pass two): release by fill and region, presses under an authored reaction-time distribution; engagement and restricted play pass on the eleven held exemplars.
6. **Doctrine: positioning** (pass two, Codex, on its forecasting and positional-hit work): forecasts as perception, a threat layer, steering stances; dodge rates against telegraphs inside a band; a test that every input is client-visible.
7. **Doctrine: banks and vents, marks and detonates** (pass three): port `galvanic_reserve`, `soul_harvest`, `living_bomb`, `boil_over`; their cells reach target.
8. **Doctrine: guards and answers, commands and sacrifices** (pass three): port `riposte`, `iron_ward`, `pain_hounds`, `command_assault`, `last_rite`, `hell_rift`; fan-out stays bounded.
9. **Bands and variety** (pass three): bands in rules, ceilings, the variety index, a nightly round robin.
10. **People** (pass three): rubric sessions with outside testers; agreement with the harness.

## 6. Open design questions

1. **What fight defines balance?** (a) Symmetric duels at equal level and item budget: cheap and comparable, but they reward burst and control that the world does not. (b) PvE scenarios (pack, elite, boss) scored by clear time and survival: the real loop, but the reference monsters' AI is itself under test. (c) Both, banded separately: honest, and twice the tuning surface.
2. **What may the AI perceive?** (a) What a player's client shows, after an authored reaction delay: fair, and forecasts become its eyes, which forces telegraphs to be good. (b) Everything the server knows, blurred by noise: cheaper, but perfect dodges read as cheating. (c) Parity by default, more as an authored and telegraphed trait of elites and bosses.
3. **Where does difficulty live?** (a) In competence (reaction, noise, commitment as doctrine content): difficulty players learn to read. (b) In numbers: simple, makes fights longer, not smarter. (c) Both, banded apart.
4. **How many axes may a glyph move?** (a) One, plus a price on payload or economy (`fork` today): legible and measurable. (b) Several: richer, entangled, harder to band.
5. **How is a build's archetype known?** (a) Declared by tag: simple, gameable. (b) Inferred from which mechanics carried its damage: honest, needs a classifier. (c) Declared and checked against inferred.
6. **How is recursion bounded?** (a) Per-kind depth caps, as today. (b) One causal budget per press shared by triggers, procs, repeats and sequels: one number to band, one new mechanism. (c) Internal cooldowns only: simplest, permits bursty loops.

## Sources

All opened; video-only talks are cited through written successors.

1. Roberts (Mark_GGG) 2019, supportability and tags (devtracker mirror). https://devtrackers.gg/pathofexile/p/26973540-questions-thread-may-31-2019
2. Grinding Gear Games 2021, Game Balance in Path of Exile: Expedition. https://www.pathofexile.com/forum/view-thread/3147157
3. Grinding Gear Games 2025, Path of Exile 2 patch 0.2.0. https://www.pathofexile.com/forum/view-thread/3740562
4. Loginova 2019, interview with Petri Purho, 80.lv. https://80.lv/articles/noita-a-game-based-on-falling-sand-simulation/
5. Nolla Games 2020 to 2021, Noita release notes. https://noitagame.com/release_notes/
6. Wilson 2012, Skill and Rune Changes, Diablo III. https://news.blizzard.com/en-us/article/4475014
7. Mark, Dill 2010, Improving AI Decision Modeling Through Utility Theory, GDC. https://media.gdcvault.com/gdc10/slides/MarkDill_ImprovingAIUtilityTheory.pdf
8. Mark, Infinite Axis Utility System manual (his GDC 2013 and 2015 talks are video only). http://www.gameai.com/manuals/index.php/IAUS
9. Dill 2015, Dual-Utility Reasoning, Game AI Pro 2. http://www.gameaipro.com/GameAIPro2/GameAIPro2_Chapter03_Dual-Utility_Reasoning.pdf
10. Lewis 2017, Choosing Effective Utility-Based Considerations, Game AI Pro 3. http://www.gameaipro.com/GameAIPro3/GameAIPro3_Chapter13_Choosing_Effective_Utility-Based_Considerations.pdf
11. Hanlon, Watts 2017, Behavior Decision System (Dragon Age: Inquisition), Game AI Pro 3. http://www.gameaipro.com/GameAIPro3/GameAIPro3_Chapter31_Behavior_Decision_System_Dragon_Age_Inquisition’s_Utility_Scoring_Architecture.pdf
12. Zieliński 2017, Paragon Bots, Game AI Pro 3. http://www.gameaipro.com/GameAIPro3/GameAIPro3_Chapter32_Paragon_Bots_A_Bag_of_Tricks.pdf
13. Dawe 2013, Beyond the Kung-Fu Circle, Game AI Pro. http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter28_Beyond_the_Kung-Fu_Circle_A_Flexible_System_for_Managing_NPC_Attacks.pdf
14. Mark 2015, Modular Tactical Influence Maps, Game AI Pro 2. http://www.gameaipro.com/GameAIPro2/GameAIPro2_Chapter30_Modular_Tactical_Influence_Maps.pdf
15. Jack 2013, Tactical Position Selection, Game AI Pro. http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter26_Tactical_Position_Selection.pdf
16. Reynolds 1999, Steering Behaviors For Autonomous Characters, GDC. https://www.red3d.com/cwr/steer/gdc99/
17. Orkin 2006, Three States and a Plan: The A.I. of F.E.A.R., GDC. https://pages.cs.wisc.edu/~dyer/cs540/handouts/gdc2006_orkin_jeff_fear.pdf
18. Isla 2005, Handling Complexity in the Halo 2 AI, GDC. https://www.gamedeveloper.com/programming/gdc-2005-proceeding-handling-complexity-in-the-i-halo-2-i-ai
19. Butcher, Griesemer 2002, The Illusion of Intelligence, GDC. https://www.jmeiners.com/shamans/papers/ai/the_illusion_of_intelligence.pdf
20. Valve, Dota Bot Scripting (read through a mirror). https://developer.valvesoftware.com/wiki/Dota_Bot_Scripting
21. Pfau, Liapis, Volkmar, Yannakakis, Malaka 2020, Dungeons & Replicants, IEEE CoG. https://www.antoniosliapis.com/papers/dungeons_and_replicants_automated_game_balancing_via_deep_player_behavior_modeling.pdf
22. Jaffe et al. 2012, Evaluating Competitive Game Balance with Restricted Play, AIIDE. https://cdn.aaai.org/ojs/12513/12513-52-16035-1-2-20201228.pdf
23. Kennedy 2025, The Balancing Act, Overwatch. https://overwatch.blizzard.com/en-us/news/24214498
24. Riot Games 2019, Champion Balance Framework. https://www.leagueoflegends.com/en-us/news/dev/dev-champion-balance-framework/
25. Balduzzi, Tuyls, Pérolat, Graepel 2018, Re-evaluating Evaluation, NeurIPS. https://arxiv.org/abs/1806.02643
26. Sirlin, Balancing Multiplayer Games, Part 1. https://www.sirlin.net/articles/balancing-multiplayer-games-part-1-definitions
27. Fontaine et al. 2019, MAP-Elites over Hearthstone decks, GECCO. https://arxiv.org/abs/1904.10656
28. Volz, Rudolph, Naujoks 2016, Automatic Game Balancing, GECCO. https://arxiv.org/abs/1603.03795
29. Vanden Abeele et al. 2020, Player Experience Inventory, IJHCS. https://lirias.kuleuven.be/retrieve/580761
30. Davis, Steury, Pagulayan 2005, the consumer playtest, Game Studies. https://gamestudies.org/0501/davis_steury_pagulayan/
