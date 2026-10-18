# Capability map, pass zero

Read from the source at worktree `combat-port-17f658`, head `14f7f3b`: handler signatures, probes, trigger kinds, hit stages and definitions in C#, attributes, rules and usage counts in `content/core`. A script cross-checked every transcribed handler against `EffectSignature.Of/.Hook/.Wrapping`: 48 handlers, 349 signature parameters (367 classified fields once blocks such as `place` or `intercept` are split), 34 hook lists, 6 wrappers. Per-field detail (what it reads, its default, the attribute behind it, the proposal for each flag) is in `capability-map.json`.

Legend: **A** axis, **N** node, **C** choice, **F** flag. `†` falls back to a weave attribute; `×attr` the attribute multiplies it. `°` a literal number, not an expression, so no stat reaches it. `[n]` uses in shipped content.

## 1. Capability inventory

### Deliveries

| Handler | AXIS | NODE | CHOICE | FLAG |
|---|---|---|---|---|
| area [121] | radius† (area.radius), angle ×area.angle, width† (area.width), inner, max_targets | effects (on_hit), require | shape, filter, center, pick, reach, occlusion | include_center |
| projectile [116] | count† (clamp 1..64), speed†, range†, spread ×projectile.spread, pierce†, forks†, chains† (projectile.chain), rehit†, bounce†, homing† guide† erratic† spiral† orbit† spin† wave† accel† recurve† converge†, radius°, shape_size, shape_angle, shape_width, salvo_interval, volley_spacing°, ring_jitter°, origin_range, duration, amplitude, orbit_radius, zigzag, zigzag_every, tick°, travel° | on_hit, on_first_hit, on_end, on_tick, on_travel, on_land, on_return; on_expire (a plain list, not a hook) | shape, fire, origin, return, occlusion, look | gravity, spread_by_aim |
| chain [2] | count† (chain.count), range (6) | effects (on_hit) | filter, pick | |
| field [14] | radius†, duration†, interval, angle ×, width†, inner, delay, exposure, pull, drift, seek, rotate, pulses | pulse (on_hit), on_tick, on_land, on_enter, on_exit, on_end, domain | shape, filter, domain_filter, exclusive, occlusion, look | follow, radius_curve, hit_once, rearm_on_exit, segments |
| aura [1] | radius†, duration†, interval | pulse, on_tick, on_enter, on_exit, on_end, domain | filter, domain_filter, key, occlusion, look | toggle |
| tether [1] | width, duration†, interval, max_length | pulse, on_end | link, filter, look | |
| scatter [1] | count† (area.strikes), radius† (2×area.radius), interval, jitter, telegraph, telegraph_radius† | effects (plain list) | at, filter | |
| cascade [2], wrapper | count† (area.cascade), step† (1.5×area.radius), scale_step, damage_step, interval, interval_step | effects | dir | |
| aim [4], wrapper | offset, spread ×aim.spread, sequence° | effects | | |
| detonate [0] | radius | effects (plain list) | tags, pick | |
| move [21] | distance (×knockback for knockback and buffet only), speed, width, air_time, arc, delay, travel° | on_collide, on_contact (on_hit), on_travel, on_land, on_end, status | kind (eight words), who, anchor, filter, mark | phase, behind, telegraph |
| mark [0] | | | key, at | |

Implicit reads: areas take `area.scatter` (aftershocks), `area.pulses` and, for melee-tagged anchored shapes, `melee.reach`; every occluding delivery reads `phasing`; flights read twelve `delivery.*` rules.

### Payload effects

| Handler | AXIS | NODE | CHOICE | FLAG |
|---|---|---|---|---|
| damage [272] | amount, amounts, effectiveness† (damage.effectiveness, added damage only), leech | on_kill | threads, tags | can_crit, cannot_kill |
| heal [6], resource [2] | amount | | resource, tags | self, exclude_self |
| apply_status [154] | duration, chance, stacks, magnitude (rule or baseline), radius (8), max_targets° | status, on_status_tick, on_status_expire | threads, to | self, fixed_duration |
| remove_status [2], modify [0] | count°; value, duration (5) | status | tags, order, kind; attribute, form, scope, key | self |
| charge [12] | gain, max (holder's charge.{id}.max), spend, drain | | id | all, self (default **true**) |
| consume / spread / move_status [1/1/0] | count°, radius (4 or 6), strength, max_targets°, splash | status, statuses | tags, kind, as, duration, from, to, filter | self |
| rupture [4] | fraction, scale | status | | self |
| summon [10], deploy [4] | count† (summon.count), max_active† (summon.max), duration/lifetime† (duration), place range/radius/spacing, contract reserve/respawn, decay ×3, waves ×3, replenish, search_radius, intercept radius/slow | pattern, pool, behavior, presence, on_birth, on_death | group, pool_group, exclusive_group, place at/shape, from_corpse, intercept.mode | inherit, inherit_caster, contract.toggle |
| spawn [1] | count (1..32), lifetime | pattern | | as_corpse |
| corpse and minion orders [0-3 each] | radius (8 or rule), duration, discipline, count | effects | command, affects, pick, of, group | self |
| taunt [2]; grant_item, grant_xp, event, log, interrupt [0] | duration (4); count°, amount, data | item, table | rarity, name, text | |

### Flow and casting layer

- Flow: `condition` [7] (if, then, else N), `random` [1] (choices, literal weights), wrappers `repeat` [3] (times 1..50, interval), `sequence` [1], `delay` [1] (seconds), `fuse` [1] (delay† fuse.delay, power† fuse.power).
- Casting nodes: `cast` [3] (weave N, at C, aim C, turn°, level, damage_scale), `minion_cast` [2], `cooldown` [0] (seconds, fraction, scope C), `restore_uses` [3] (amount, scope C), `guard` [1] (amount, self F).
- **Forms**: targeting C (enum), cast_mode C (instant, cast, channel, charge, overcharge, guard), range, cast_time, cooldown, costs, attributes, cast_move, recovery A; mechanisms C; effects N; AI hint (preferred_range°, weight°, min_range°, near_weight°; when N; range_attribute C; role, never read). Held modes: channel (interval, windup, jitter, move, turn_rate, max_hold; ramps N; release N; persist; brim with literal decay, bank_at, min_release, scale), charge-up (max_time, full_hold; scales N), overcharge (stages† overcharge.stages, time; per_stage N; spark window° bonus°), guard (arc°, pool ×guard.strength, move, turn_rate, parry, max_duration; bash N with threshold°; pulse N), timing (kind C, window°, bonus°), toggle, reserve, upkeep, target (kind C from TargetKinds, require N, search_radius, fallback C), tree N.
- **Slot behaviors** on forms, presets and glyphs: uses (max† uses.max, recharge, refill; empower N; bleed°), spend (charge C, amount°, minimum°, scales N), gates, convert, meta_action, equip_modifiers, follow_up (weave N; delay, chance A), cycle (count°), self_stack (max°, duration°, decay°), combo chain (window°).
- **Triggers**: on C, chance† (base of trigger.chance), icd°, life_fraction°, status N, power A, max_cast_time°, tags C (any of). 25 kinds: hit, crit, hurt, block, evade, kill, death_near, minion_died, status_applied, cast, heal, interval (event bus); break, broken, cull, cheat_death, charge_gained (combat); collision; channel_beat, channel_end, release, overcharge_stage, flawless, guard_beat, parry (cast modes).
- **Glyphs**: requires, excludes, requires_mechanisms, adds_tags, adds_mechanisms C; modifiers, per_level, adds_effects, hooks, patches (type, where, set, wrap, mode), rolled axes N; level, level_bonus, weight A; forwards F. Presets add attributes A and modifiers, effects, glyphs N.
- **Probes** (54, 11 parameterized): cooldown, strikes, projectile, flight:children|spends, area, surface:standing, summon, minion, construct:massed, periodic_construct, engagement, displaces, channel, held, guard, uses, bankless, affliction:status, status:status, landing; triggered, toggle, overcharge, timing, release, ramps, spends, repeats, convert, meta; charges:any, rupture:status, consumes:status, contagion, leech, bank:status; contract, pool, corpse, orders, unlife; returning, homing, trajectory, chain, shape:name, fire:style, cascade, storm, telegraph, shove, leap, dash, tether.
- **Hit stages** (13, ordered by rule `combat.stages`): immunity, guard, evade, block, taken_as, mitigate, taken, cap, stagger, pools, cheat_death, cull, leech; skippable steps crit, armor, added, damage_taken, ailments; `combat.stage_skips` skips by the tags dot, deferred, no_crit.
- **Statuses**: duration (read on the bearer, default 5), max_stacks°, tick.interval°, baseline, bank cap and fraction, pool A; modifiers and seven effect lists, buildup_into, bound_to N; stack_policy, stack_clock, bank.from, consume_on (event, tags, count°), flags, forbids_tags C; beneficial, dispellable, modifiers_per_stack, propagate_on_death, scales_with_potency, keep_source F. The service adds the stack policies, duration scaling, banks, pools, per-stack clocks, pops and trigger-fed riders; ticks run at depth 0.
- **Charges**: duration A (on the holder), decay°; the cap is the holder's `charge.{id}.max`. **Procs**: on, tags (all of), excludes, chance (attribute name), target C; when, victim, effects N; icd°, ppm°, falloff° A; no_crit F.

## 2. Classification

Handler fields: **166 AXIS, 74 NODE, 95 CHOICE, 32 FLAG**. Distinct definition fields: 103, 48, 38, 24. 48 handler fields read an attribute (as fallback or multiplier); 14 are literal numbers.

### Flags and number-hiding choices, and what each becomes

| Flag or choice | Becomes |
|---|---|
| `self` on heal, resource, modify, remove_status, consume_status, rupture, charge (inverted default), guard, consume_minion, apply_status; heal `exclude_self`; `apply_status.to`; transfer `filter`; proc `target` | one `who` CHOICE from the TargetKinds registry, validated once |
| projectile `gravity` | AXIS `projectile.arc` (0 = flat); a lob also silently disables return today |
| projectile `fire` | axes: spread degrees (ring = 360 evenly), lateral spacing (volley), stagger seconds (salvo) |
| projectile `return`, `origin` | AXIS `projectile.returns` plus an anchor CHOICE; AXIS share of the way to the aim point |
| projectile `spread_by_aim` | drop: expose `aim.distance`, spread becomes an expression |
| `occlusion` (four handlers), move `phase` | the weave axis `phasing` (dashes ignore it today) |
| area `include_center`, `reach` | a shared `spare: [target, source]`; AXIS reach offset merged with `melee.reach` |
| field `follow`, aura `toggle` | CHOICE anchor (point, source, target); toggling belongs to the cast layer |
| field `hit_once`, `rearm_on_exit` | AXIS rehit lockout, as `projectile.rehit` |
| field `radius_curve` | NODE envelope `{axis, from, to, curve}` for any delivery number |
| field `segments` | drop: a cascade with interval 0 and dir `across` |
| scatter `at`; detonate `pick` | AXIS share placed under bodies; AXIS max count with the shared pick order |
| cascade `dir`, cast `aim` | axes: bearing step, alternation, jitter (random = 360), as the aim wrapper has |
| move `kind` | one displacement: speed (infinite = teleport), arc, delay, shove bearing (0 away, 180 toward, 360 jitter = buffet) |
| move `behind`, `telegraph` | AXIS arrival bearing; AXIS warning seconds, as scatter's |
| deploy `intercept.mode` | AXIS flight speed factor inside the dome |
| damage `can_crit`, proc `no_crit` | drop: the `no_crit` hit tag does it |
| damage `cannot_kill`; apply_status `fixed_duration` | AXIS life floor; AXIS duration scaling 0..1 |
| charge `all`, spend `all` and `optional` | drop: `spend: charge.{id}`; `minimum: 0` |
| summon `inherit`, deploy `inherit_caster`; `contract.toggle` | AXIS inheritance share; the cast-layer toggle with reserve |
| spawn `as_corpse`; glyph `forwards` | CHOICE body state; CHOICE lane (host, crew, both) |
| status `beneficial`, `dispellable`; `modifiers_per_stack` | tags; drop (gauge `status.{id}.stacks`) |
| status `propagate_on_death`, `scales_with_potency`, `keep_source` | NODE on_death list; AXIS potency share; CHOICE credit |
| form `reflex`, target `consume` | drop: the `reflex` attribute; consume nodes in the graph |
| channel `track_aim`, `require_full`, `cooldown_on_end` | drop (turn_rate 0, min_hold = max_hold); a third `cooldown_at` word |
| release `pulses`, brim `spend`, `auto_release` | AXIS beat effectiveness, share spent, full-hold seconds |
| guard `end_on_parry`, bash `on_break` | AXIS parries survived; NODE on_break list |
| uses `manual`, `vent_all`, `steps_from_bank`, `drip`; timing `kind` | drop (uses.rate 0); AXIS uses per press; drop (repeat.count reading uses_spent); AXIS refill step; axes window position and presses |

### Parameters that duplicate each other

| Idea | Where, with differing defaults |
|---|---|
| radius | area, field, aura `area.radius` 2; scatter 2×; cascade step 1.5×; projectile literal 0.5; apply_status allies 8; spread 4; move_status 6; gather 8; recall and corpse reach by rule; intercept 3 |
| chain | the `chain` effect: `chain.count`, range 6; projectile chains: `projectile.chain`, rule range 8 |
| interval | field 0.5, aura 1, tether 0.5, scatter 0, cascade 0.15, repeat 0.3, flight tick 0.5, salvo and channel by rule, status tick 1; none an attribute |
| duration | `duration` for field, aura, tether, summon, deploy; taunt 4, modify 5, status 5, delay 1; fuse `fuse.delay` |
| who and relation | delivery filter (enemy, ally, ally_or_self, self, not_self, all); transfer filter (allies, enemies, any); TargetKinds; the `targeting` enum; the AI filter |
| pick | area and chain (five words, different defaults), detonate, consume_minion, scatter `at`, `from_corpse` |
| one mechanism, two doors | delay and fuse; aura and field; segments and cascade |
| bearing patterns | cast `aim` with `turn`; the aim wrapper; projectile ring with `ring_jitter`; cascade `dir` |
| crit, walls, telegraphs | `can_crit` and `no_crit`; `occlusion`, `phasing` and `phase`; scatter `telegraph`, field `delay`, leap `telegraph`, blink `delay` |
| form fields twice | `channel_interval` and `channel.interval`; `reflex` flag and attribute; form and attribute `range`; `guard.pool` and the shell's |
| trigger subscribers | TriggerSpec tags any of, procs and `consume_on` all of; depth limits 12 (effects), 4 (triggers), 3 (procs, status hooks) |
| effectiveness | the node parameter scales amount and added damage, the attribute only added damage; content multiplies it in by hand (101 reads) |

### Numbers a delivery reads that no glyph can reach

- **Rules**: `delivery.fork_children`, `fork_angle`, `chain_range`, `homing_radius`, `homing_reacquire`, `min_speed`, `return_arrive`, `return_max_seconds`, `bounce_restitution`, `aftershock_scale`, `aftershock_delay`, `pulse_interval`, `seek_radius`, `impulse_decay`, `impulse_min`, `collision_speed`; `move.gravity`; `minions.recall_radius`, `corpse_target_reach`, `cast_range`, `earshot`; `triggers.death_near_radius`; `combat.propagate_radius`.
- **Literals**: projectile radius, volley spacing, ring jitter, tick, travel, spread_by_aim, segments, radius_curve, cast turn, aim sequence, transfer counts and caps, guard arc, timing and spark numbers, brim numbers, self-stack, cycle and combo numbers, trigger icd and life fraction, proc icd, ppm and falloff, status max_stacks and tick interval, AI hint numbers.
- **Form-only**: cast time, base cooldown and the held-mode numbers; `cast.time`, `cooldown.base`, `channel.*`, `guard.*` and `charge.max_time` exist but only shell forms read them.
- **Declared, never moved by shipped content**: `projectile.spread`, `.spin`, `.recurve`, `area.width`, `area.pulses`, `melee.reach`, `knockback`, `aim.spread`, `fuse.delay`, `fuse.power`, `repeat.scale`, `repeat.lock`, `uses.max`, `uses.rate`, `guard.strength`, `cast.mobility`, `cooldown.added`.

## 3. Against the prototype

| Prototype | Here | Smallest carrier |
|---|---|---|
| caroms: presses plant anchors, the last releases a volley shuttling between them; hang arms arrows that collapse on prey | no waypoint flight; `mark` stores keyed points | NODE: a projectile path through the source's marks; hang = fields with on_enter, detonate |
| arcTo: the fan converges on the cast point and ends there | `converge` bends toward the point, then flies on | NODE `on_arrive` hook with CHOICE arrive end or pass |
| plantOnLand, embeds, sibling beams | on_end to deploy, or a field with filter self and on_enter | CHOICE tether link `network`; the rest composes |
| catch, catchSpot | on_return with charge; the spot composes as on_first_hit, cast at caster, scatter count 1, field filter self with on_enter | none: the flight to the circle is cosmetic |
| selectivePierce: one prey, harmless through the rest | flights strike every hostile body | projectile `require` plus a `cast_target` variable |
| shed shards: kink shards, end and return rings, a shatter fan sparing its victim | rings compose; shards cannot spare their victim; kinks raise no hook | `spare: [target]` on projectile and an `on_turn` hook |
| zap, projectile-borne aura | on_tick area | has |
| trail, fissure trail, lingers fed by flight speed | on_travel area or field; hooks cannot see the flight | flight variables (speed, age, traveled, resolved axes) in hook contexts |
| bearer auras: self, ally and enemy modifiers at once, deaths inside, charge upkeep; ledger, overdrive, seal | one domain per aura, no deaths-inside hook, upkeep pays resources only | NODE `domains: [{filter, modifiers}]`, field `on_death` hook, charges in cost maps; the economies not worth carrying now |
| suffuse, conduction: flights carry or take on fields they cross | flights never sense fields; only intercept domes touch flights, inside MinionSystem | NODE projectile hook `on_cross {tags}`; domes join that seam |
| emit rotating, random, forward with an `inherit` share; a child cap | on_tick cast or ring; nested flights inherit every axis, cast presets none; only `effects.max_depth` caps | flight variables (a child reads `flight.homing * 0.5`); a child-generation rule |
| recurve (a chance to whip back at the victim), caromOnHit | our `recurve` is a steady bend; bounce is terrain-only | AXIS `projectile.rebound`; AXIS `projectile.ricochet` |
| explode, sequel on hit, expire or any | on_hit and on_end compose; `on_expire` cannot be hooked | declare `on_expire` a hook, add `on_spent` |
| return re-throw; rehit flights that chain or fork | one pass; rehit skips pierce, forks and chains | AXIS `projectile.returns`; let chains and forks act |
| polar composition: homing steers a spiral, erratic wobbles an orbit | orbit and spiral bypass steering | steer their anchor with the same axes |
| projectileSize, pulse | radius is a literal | AXIS `projectile.size` with the envelope |
| ground: pendulum, sweep, fill, retract, pull radius, strobe, markers | rotate constant; strobe and markers compose | the envelope on facing, inner, interval; AXIS pull reach |
| dash grab and drag, carom motor | neither | CHOICE `carry: first_contact`; the motor not worth carrying now |
| spin epicycle, weave figure-eight, multiplicative accel, frame bounce, noise, paint, craters | | not worth carrying: presentation, equivalent formulas, or the client frame |

## 4. Composition smells

- **String switches in systems**: `Displacement.Kind` ("leap", "blink") in DisplacementSystem.cs:111-281; intercept modes in MinionSystem.cs:248; `ThreadDef.mitigation` ("armor", "resist") in MitigateStage.cs:39,53; `"life"` in PoolsStage.cs:44; target fallback and ramp `on` in WeaveRuntime.cs:761,1340; proc target in ProcService.cs:220; private who-switches in StatusHandlers.cs:34-47 and StatusTransferHandlers.cs:124.
- **Choices kept twice, filter kept nowhere**: DeliveryKinds.cs:20-33,136 re-lists each handler's words in a second `switch (type)`; `filter` is validated by neither copy, and EffectHelpers.cs:50 lets an unknown word strike everyone, the caster included.
- **Probes parse raw nodes**: Mechanisms.cs:134-228 and DeliveryProbes.cs:55-150 read `shape`, `gravity`, `follow`, `return`, `fire`, `kind`, `delay`, `contract` from JSON, duplicating handler semantics. `periodic_construct` reads keys no signature declares, so it never holds on valid content; `minion` repeats `summon`; `telegraph` misses leap telegraphs and blink delays.
- **Type checks where tags should gate**: flights strike only hostile bodies and skip anything with a Projectile or Field component (ProjectileSystem.cs:532), so no flight seeks allies; detonate turns the tags `projectile` and `field` into component queries (PlacementHandlers.cs:521-531); `restore_uses scope: still` tests `UsesSpec.Still` (CastHandlers.cs:249); triggers refuse by `IsHeld` and `IsToggle` beside the `untriggerable` tag (Triggers.cs:252).
- **Names in code**: "stunned" and "frozen" in seven places (WeaveRuntime.cs:397,1056; Triggers.cs:256; SlotBehaviors.cs:466; BehaviorSystem.cs:556; MinionHandlers.cs:237; MovementSystem.cs:47), "immune", "invulnerable", "immovable", "untargetable", "hidden"; tags `melee` (adds `melee.reach`, DeliveryHandlers.cs:216), `attack` (chooses attack.speed, WeaveRuntime.cs:307), `mobile`, `movement`, `dot`.
- **The runtime reads the form, not the compiled weave**: `Targeting`, `CastMode`, `IsToggle`, `IsHeld`, `Range`, `CastTime` and `Cooldown` (CompiledWeave.cs:204-275) and sixteen `weave.Form.Channel/Charge/Overcharge/Guard/Timing/Upkeep/Target/Recovery/Reflex` reads in WeaveRuntime.cs. No glyph or preset can grant a cast mode, a ramp, a guard, strike timing or a toggle; only slot behaviors merge. A form `range` expression bypasses the `range` attribute.
- **The AI reads hand-written hints**: `ChooseForAi` and `PreferredRange` (WeaveRuntime.cs:1706,1790) weigh `weight`, `min_range`, `near_weight`, `when`, `preferred_range` and the form's targeting; nothing reads the compiled delivery (radius against a crowd, heal against harm, flight speed). `ai.role` is never read. `DeliveryForecast` already describes areas.
- **Context and depth**: `repeat` reruns on its first run's context, with frozen `time` and `distance` and one shared variable dictionary (FlowHandlers.cs:116), unlike delay and fuse; follow-ups resolve at depth 1 (SlotBehaviors.cs:189), status ticks at 0 (StatusService.cs:769), cast trigger events at 0.
- **Zero and one use**: shipped content never uses detonate, mark, modify, move_status, gather_corpses, cooldown, event, interrupt, grant_item, grant_xp or log, nor shape_size, shape_angle, shape_width, on_first_hit, on_travel, on_land, exposure, rearm_on_exit, domain, domain_filter, exclusive, drift, rotate, follow, interval_step, damage_step, phase, on_collide, on_contact, amounts, effectiveness, cannot_kill, offset, splash. spread_by_aim, volley_spacing, gravity, zigzag, amplitude, pull, seek, pulses, jitter, inner, max_length, exclude_self, as_corpse, air_time, on_return and strength appear once. `Forms.Delivery.cs` and `Forms.Summon.cs` are empty partials.

## 5. Invariants

**Enforced**

- Determinism: every roll draws from a seeded stream: the cast stream, a per-flight stream for erratic flights, salted sub-streams for scatter, cascade and `cast`, per-book trigger streams, per-owner proc streams, per-status streams, the AI choice stream. No `System.Random` or wall clock in Core; a Stopwatch only profiles.
- Depth: `effects.max_depth` (12) stops `ExecuteAll`; `procs.max_depth` (3) with falloff and a chance cap; `triggers.max_depth` (4) with `_triggerDepth` for casts; status hooks refuse at the proc limit and guard re-entry; hook lists are never hooked; aftershocks and pulse repeats never echo; `cast` targets compile once behind a recursion guard.
- Bounds: projectile count 1..64, repeat 1..50, spawn 1..32, summon count by `minions.max_per_cast`, `casting.max_repeats` 50, eight on_tick or on_travel runs per flight per tick, floors on tick, travel, zigzag spacing, orbit radius, intervals and radii, attribute catalog clamps, glyph level cap, chance caps.
- Spatial safety: deliveries copy grid results before effects run.
- Authority: clients send intents; `TryBegin` checks life, stun, forbidden tags, busy, cooldown, uses, gates, charges, cost and target before paying; aim intents are rate-limited and pulled within range; triggered casts pay costs and cooldowns; deliveries whose source is gone end quietly.
- Content: unknown types, hooks, patches and choice words fail validation; dormancy only removes glyphs, so the fixpoint ends.

**Assumed**

- No entity budget: `projectile.forks` has no maximum and each fork doubles the flights; emitters multiply by count per depth level up to 12; `area.scatter`, `area.pulses`, `area.strikes`, `area.cascade`, `chain.count`, segments and aim sequences are unclamped.
- Expression counts skip attribute bounds (`projectile.count` stops at 20, `count` at 64).
- Depth restarts at follow-ups, status ticks and cast events; loops through them stop only on cooldowns, ICDs and costs.
- The trigger depth guard is a mutable runtime field, sound only while resolution is synchronous.
- `filter` words are trusted; probes are trusted to be pure and never to name weave ids.
- Hit order within a tick is spatial-grid order.
- Flights never strike allies, by construction.
