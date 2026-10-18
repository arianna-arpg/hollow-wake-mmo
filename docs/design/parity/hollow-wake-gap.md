# Parity gap: the prototype's combat, skills, supports, statuses, passives and items

This document compares the combat, skill, support, status, passive and item systems of the earlier TypeScript prototype (the repository `arianna-arpg/arpg-game-world`, called "the prototype" below) with Hollow Wake MMO's data-driven core, row by row, and proposes how to reach parity in our terms. It is the planning contract for the work packages in section 3. It changes no system by itself.

Conventions used throughout:

- Code paths are relative to `src/HollowWake.Core/` unless they start with `src/`, `content/`, `tests/` or `docs/`. Prototype references name the prototype file and type, for example `skills.ts ProjectileDelivery`.
- Status: **present** means parity in substance, **partial** means the mechanism exists but misses parts, **missing** means nothing comparable exists.
- Size: **S** is a day or less for one agent in a file or two, **M** is a few days inside one subsystem, **L** is a week or more across subsystems.
- Priority: **P0** is parity the owner named explicitly: projectiles, cast times, summons, effects, supports, passives, inventory and skill UI. **P1** is the rest of combat depth, which includes statuses, defenses, constructs, auras, movement deliveries and the reliquary, except where a P0 row depends on them. **P2** is later.
- Every proposal is held to `docs/VISION.md`: data over code, one modifier engine, tags as the join, composition over bespoke, registries for open sets, events between systems, determinism and server authority.
- Tool commands named in acceptance checks (`validate`, `load`, `fight`, `bench`, `probe`) are the tools CLI in `src/HollowWake.Tools`, run as listed in `CLAUDE.md`.

## 1. Summary

### 1.1 The prototype's combat model

A skill is data: tags, costs, a cast mode, a **delivery** that says how the skill reaches its targets, and a list of **effects** that say what happens when it lands. Players, monsters and minions execute skills through one pipeline. Deliveries form a closed union of rich structs (projectile, melee, nova, cone, target, ground, storm, self, summon, construct, aura, dash, leap, blink, mark, carom, detonate), and most of a skill's behavior lives as typed fields on those structs: a projectile's trajectory axes, forks, returns, trails, emissions and sequels; a ground placement's linger, cascade, pulse, emitters and domains; a summon's pools, contracts and crew.

A **skill instance** is the definition plus a level, sockets holding support instances, derived grafts, spent skill-tree nodes and transient state (use charges, self-stacks, trigger latches, combo cursors). Every number a use needs is read from the caster's stat sheet with the instance's skill-local modifiers joining the same fold: level growth at the effective level, reached level thresholds, socketed support modifiers scaled by gem level, tree node modifiers and self-stack piles. Queries are scoped by the skill's context tags: its own tags, tags granted by its supports, a cast-scope tag (instant, timed, channel or held), its damage types and its minions' body tags.

**Supports** are two things at once. Most are modifiers on mechanic stats that deliveries read when they resolve: repeat count, fork, chain and pierce counts, projectile count and speed, area radius, cascade and pulse counts, summon count, trajectory powers, trigger chance. The rest carry typed **graft** specs that bolt a behavior onto the host: a trail, a sequel cast where a flight ends, contagion, a fuse, a cascade, a pulse, a trigger, an overcharge, a targeting rule, an aim transform, a tether, a follow-up, an echo, a munition conversion and many more. Each graft has a precedence rule: a socketed graft usually wins over the skill's own spec, some specs merge, and cascades and pulses lengthen the skill's native lane. Gates are tag any-of and none-of plus **mechanism** predicates over the live instance, computed to a fixpoint so supports can enable supports; a misfit stays socketed but dormant until something lifts it. Supports level to a small cap, can add levels to their host, can board a summon's minions, can be granted by gear to a bar seat and can be rolled per copy like an item.

**Statuses** tick damage over time locked at application, apply modifiers to the bearer (optionally per stack and scaled by the applier's power) and carry a stack policy, buildup into a stronger status, propagation on death, banks that later rupture, pops on reapplication, consumable stacks and next-hit riders. Every status generates stat families: chance to apply it on hit, damage versus its stacks, minion carry, pop power and damage-over-time leech.

The **stat engine** is one sheet per actor with flat, increased, more, override and single-hop link modifiers, tag scopes, a fixed list of actor conditions, gauges that scale or gate a modifier, victim-scope tags folded from the struck body, and stat trades that read one stat as another while forgoing part of it. The **damage pipeline** rolls damage per type, adds flat damage, scales, converts, rolls crit, then walks a fixed defensive ladder: immunity and avoidance, evasion by entropy, block by guard value, crit avoidance, armor, capped resistances with penetration, slayer multipliers, damage taken, then the pools (insight, poise, endurance, a deferral ledger, ward, absorption, energy shield, mana shield, stagger) before life, with a per-hit cap, plies and a cheat-death check, then culling, leech and procs. Damage over time takes a shorter path that skips evasion, block and armor.

Around combat sit the **passive star** (choice nodes, realms, quest-earned vocation trees, refunds at a station), skill progression (essence-bought levels, ability points and per-skill trees), **items** (tiers, affixes, uniques, gear sockets for vestige stones), skill and support gems as bag items in a tetris inventory, the **rack** of learned skills, gacha **memories**, paged **stashes** and the account **reliquary** whose seated relics are active.

### 1.2 How it maps onto ours

| Prototype | Ours | Note |
|---|---|---|
| skill (`SkillDef`) | weave: a preset `WeaveDef`, or a composed `WeaveSpec` of a form, Threads and glyphs | ours composes; the prototype ships finished skills |
| delivery | form (`FormDef`) plus the delivery nodes in its effect graph | timing, costs and targeting live on the form; geometry lives in effect handlers |
| effect list | effect graph compiled from `TypedSpec` through the effect registry | ours nests effects under delivery hooks such as `on_hit` and `pulse` |
| support (`SupportDef`) | glyph (`GlyphDef`) | `requiresTags`, `excludeTags`, `requiresMechanisms`, `grantsTags`, `mods`, `perLevel` map to `requires`, `excludes`, `requires_mechanisms`, `adds_tags`, `modifiers`, `per_level` |
| graft fields on supports | missing; proposed as patches and hooks (D7, D8) | |
| skill instance | `WeaveSlot` in a `WeaveBook`, with its `CompiledWeave` | |
| skill-local modifiers (`instanceMods`) | `CompiledWeave.Modifiers`, passed as query extras | the same single-fold layering |
| skill context tags | `CompiledWeave.Tags` | cast-scope tags missing (A1) |
| mechanism registry | declared `mechanisms` on forms and glyphs | probes proposed (D3) |
| stat and stat sheet | attribute and `AttributeSet` on `Stats` | |
| modifier kinds | `ModifierForm` | the same forms |
| `when: <condition id>` | `condition:` expression with `has()` | ours is more general |
| gauge and gauge gate | `gauge` and `gauge_at` | |
| `vs:` victim scope | `vs.` victim tags | |
| damage type | Thread | resistance, penetration, mitigation rule and ailment per Thread |
| status and active status | `StatusDef` and `StatusInstance` | ours adds apply, expire and tick effect hooks |
| procs, charges, the gauge fabric | missing; the `charge.*` gauge names are already reserved | D19, A14, C7 |
| `world.useSkill`, `resolveHit` | `WeaveRuntime.TryBegin` and `Execute`, `DamagePipeline.Build` and `Apply` | one pipeline for everyone in both |
| class | calling | |
| passive node | `PassiveNodeDef` | |
| item base, affix, unique, rarity | the same names with nearly the same shapes | our item fabric was modeled on the prototype's |
| skill gem and support gem items | missing | question Q1 |

### 1.3 Where we stand

Already at parity: the one pipeline for every actor; the modifier engine (forms, links, gauges, scopes, conditions, sources); item bases, tiers, affixes with magic-only tiers, uniques with tier scaling and local lines; the status core (stack policies, buildup, propagation, potency, per-stack modifiers); tag and mechanism gates on glyphs; amounts as expressions.

Ours is ahead in places the prototype hard-codes: composition of forms, Threads and glyphs; control-flow effects (`condition`, `random`, `repeat`, `sequence`, `event`) that let content express what the prototype needs bespoke fields for; arbitrary apply, tick and expire effects on statuses; weapons and Threads as first-class content; a deterministic, server-authoritative core.

The biggest gaps, in the order the owner cares about:

1. **Supports beyond modifiers.** Glyphs can only add modifiers, tags, mechanisms and top-level effects. They cannot add riders inside a projectile's on-hit list, convert a delivery's style, wrap a placement in a cascade, trigger, repeat, or carry levels of their own.
2. **Cast modes.** We have instant, cast and a basic channel. Channel windup, ramps, release payloads, charge-up, strike timing, guard, overcharge, use charges, gates and moving while casting are missing.
3. **Projectiles.** Ours fly straight. Hit shapes, firing styles, trajectory axes, forks, returns, rehits, in-flight emitters, trails, sequels, projectile chaining and occlusion are missing.
4. **Summons.** Ours spawn a pattern with a cap. Pools, owner stat inheritance, minion AI that follows and assists, commands, contracts, corpses and constructs are missing.
5. **The trigger economy.** Procs, cast-on-X triggers, charges and charge taps are missing.
6. **Passives.** Choice nodes, realms, vocations, passive grafts and per-node refunds are missing.
7. **Items and UI for weaves.** Glyph and form items, the rack, a grid inventory, gear sockets, stash, the reliquary and every client surface beyond the HUD are missing.

Defects noticed during the survey, to be fixed by the owning package:

- `StatusDef.forbids_tags` is declared but nothing sets the `forbid:<tag>` flags that `WeaveRuntime.TryBegin` checks, so silence does not stop spells (E4, WP5).
- The `modify` effect removes its modifier on the first application's timer, so a refreshed temporary modifier ends early (C3, WP5).
- Status ticks can be blocked, and Stone ticks are armored, because ticks take the full hit path (G14, WP5). A partial block (`block.amount` below one) still lets ailments roll on what seeps through, where the prototype's block always stops a hit's effects (G6).
- `GlyphDef.level` is declared but unused; glyph `per_level` scales by the weave's level (D6, WP1).

### 1.4 The shape of the proposal

Five moves carry most of the parity, each in our idiom rather than a port of the prototype's field-per-behavior style:

1. **Compose behavior instead of growing fields.** Glyphs gain **hooks** (named effect lists such as `on_hit` and `on_end` appended into every delivery node that exposes them) and **patches** (compile-time edits to effect parameters with explicit precedence: first glyph wins, default, append, wrap). A new `cast` effect casts another weave at a point, which covers sequels, emitters, follow-ups, meta payloads and contagion releases. Statuses gain event hooks (consume on, on bearer hit, on reapply).
2. **Mechanism probes.** A registry of named, optionally parameterized probes reads the compiled weave (its effect graph, timing and modifiers), so gates self-lift exactly as the prototype's do, without lists of weave ids.
3. **One trigger registry fed by events.** Glyph triggers, procs, charge taps, consumable buffs and event-fed gauges all subscribe to the same event-driven trigger kinds.
4. **A hit stage registry.** The damage pipeline keeps its stage order in rules and registers stage implementations by name, so guard, shields, poise and leech become registrations.
5. **An item model for weaves.** Forms are learned from items whose rarity sets socket count; glyphs become stones with levels and rolls; the rack of weave slots becomes the skill UI (question Q1).

### 1.5 Where this stands

The foundations package and the six packages of section 3 have landed in the order the plan gave, each proving the standard duel traces unchanged and staying within the performance budgets. Everything the owner named as P0 exists: cast modes and triggers, projectiles with shapes and trajectories, summons with inheritance and orders, the status, charge and proc economy with the shield, poise and insight pools, the weave item model with codices and stones, passives with choice nodes, realms, vocations and refunds, dialogue and vendors, the grid bag, stashes and the reliquary, and the interface on both clients with its Options pane. Rows still open, each the owner of its files may take in a later round: A13, C11, C20, C23, C29, C33, C35 to C38, C47, C48 (the P2 cast and effect rows), D21 skill grants, E14 movement states, E15 weak spots, G11, G17, G18, H7 conduits, I9 memories, I13 salvage, and J6 per-weave tree pages on the clients. Account-scoped options wait on an account settings message, and there is no audio yet.

The prototype's content is being ported onto these mechanisms family by family; the [port ledger](port-ledger.md) has one row per prototype skill, support, status, proc, passive notable and item family with what it became, the mapping the port used (units, Threads, statuses, stats) and the engine seams it found.

## 2. Parity matrix

Rows are grouped by area: A cast modes and timing, B deliveries and trajectories, C effect types, D supports, E statuses, F modifiers, G damage pipeline, H passives, I items, J UI surfaces. Row ids are referenced from dependencies and from the work packages. YAML sketches for the main proposals follow the tables in 2.11.

### 2.1 A. Cast modes and timing

| ID | Prototype | Ours | Proposal | Deps | Size | Pri |
|---|---|---|---|---|---|---|
| A1 | Cast bar: `useTime` divided by attack or cast speed by tag; zero is instant; a cast-scope tag (instant, timed, channel, held) joins every query (`skills.ts castScopeTag`) | Partial: `cast_time` expression divided by `attack.speed` or `cast.speed` (`Weaves/WeaveRuntime.cs CastDuration`); no cast-scope tags | Keep the bar. The compiler adds `cast.instant`, `cast.timed`, `cast.channel` or `cast.held` to `CompiledWeave.Tags` from the cast mode and base time, so modifiers can scope by commitment. | none | S | P0 |
| A2 | Moving while casting: per-skill `castMove` factor plus the `castMobility` stat; zero roots | Partial: a cast roots unless the weave carries tag `mobile` (`Movement/MovementSystem.cs`) | Form field `cast_move` (expression, default from rule `casting.cast_move`) plus attribute `cast.mobility` in weave scope; the runtime writes the clamped sum to `CastState.MoveFactor` and movement multiplies speed by it. Tag `mobile` stays as content sugar for a factor of one. | WP1 seam | S | P0 |
| A3 | Cooldown clock start per skill: `cooldownAt` press or complete | Partial: one global rule `combat.cooldown_at` (`WeaveRuntime.cs`) | Form field `cooldown_at` overriding the rule; presets may override the form. | none | S | P0 |
| A4 | Cooldown levies: the `addedCooldown` stat stands a clock up on any skill; tag-scoped `cooldownRecovery` divided against the global rate (`skillCooldownSeconds`) | Partial: form `cooldown` expression and `cooldown.recovery` queried with weave tags (`WeaveRuntime.StartCooldown`) | Cooldown becomes the form's value plus attribute `cooldown.added` in weave scope, so a glyph can levy a clock; the `cooldown` probe (D3) reads the compiled value, so glyphs that need a cooldown self-lift. | D3 | S | P0 |
| A5 | Channel core: interval, windup, interval jitter, movement rule and factor, turn rate, aim tracking, cooldown on end, maximum hold; pays per pulse (`ChannelSpec`) | Partial: `channel_interval`, pays after the first pulse, ends at the cast duration or when cancelled or unaffordable; no windup, movement rule, turn rate, aim tracking or cooldown on end (`Systems/CombatSystems.cs CastingSystem`) | Form block `channel` with `interval`, `windup`, `jitter`, `move`, `turn_rate`, `track_aim`, `max_hold`, `cooldown_on_end`; interval and windup divide by the speed attribute like cast time; jitter draws from the slot's Rng stream. | A15 for aim tracking | M | P0 |
| A6 | Channel ramps: damage, area, arc, movement and cost grow with held time on linear, quadratic or exponential curves (`RampSpec`) | Missing | `channel.ramps` as a list of `{attribute, form, per, max or min, curve, on: weave or caster}`; each pulse the runtime builds cast-local modifiers from held seconds (weave ramps join the weave fold as extras, caster ramps sit on the caster under source `cast:<slot>`), so ramps are ordinary modifiers. `cast.held` is readable in expressions. | A5 | M | P0 |
| A7 | Channel release and gather: ending the hold fires a payload scaled by held time; `pulses: false` gathers silently; minimum hold; fire only on a full hold; detonate own projectiles on release; persist pulsing after release | Missing | `channel.release` with `effects`, `min_hold`, `require_full`, `pulses`; the release runs its effects (or the form graph) with `cast.held` and the ramp modifiers. `channel.persist` with `per_held_second`, `max_duration`, `fade` leaves a following field. Release arrives by intent (A15) or when resources run dry. | A5, A6, A15 | M | P0 |
| A8 | Brim: a persistent per-skill gauge filled by holding, decaying at rest, with a banked plateau, a fizzle floor, auto-release and a power curve | Missing | `channel.brim` with `fill_time`, `decay`, `bank_at`, `min_release`, `spend`, `auto_release`, `scale: {at_empty, at_full, curve}`; stored in `WeaveSlot.State`; published as gauge `brim.<slot>` for modifiers. | A7 | M | P1 |
| A9 | Charge-up: hold to charge, release scaled; maximum time scales with effect duration; damage scale at zero and full; area at full; arc taper; speed at full (`ChargeSpec`) | Missing | `cast_mode: charge` with `charge: {max_time, scales: [{attribute, form, at_zero, at_full}]}`; the release builds cast-local modifiers from the fill fraction. | A15 | M | P0 |
| A10 | Strike timing: perfect (golden tail window), timed (random marker), multitude (one hit per press during the bar), each with a bonus (`StrikeTimingSpec`) | Missing | Form field `timing: {kind, window, bonus}` with kinds perfect, timed and multitude; a second press during the bar arrives as `PressWeaveIntent`; the marker is drawn from the slot Rng; success adds a cast-local `damage` more modifier and raises the `flawless` trigger kind. | A15 | M | P1 |
| A11 | Guard stance: a held frontal shield with its own pool, arc, movement factor and turn rate; parry window and counter; a bash on release or break with an arming line and an arm clock; a guard pulse (`GuardSpec`, `GuardBashSpec`) | Missing; only passive `block.chance` | `cast_mode: guard` with `guard: {arc, pool, move, turn_rate, parry, max_duration, end_on_parry, bash: {effects, threshold, arm_time, on_break}, pulse: {weave, interval}}`; a `GuardState` component and a `guard` hit stage (G0) absorb hits inside the arc; attribute `guard.strength` scales the pool. | G0, A15 | L | P1 |
| A12 | Overcharge: the held bar refills up to a number of stages, each banking more damage and area; a spark window bonus; cost per stage (`OverchargeSpec`) | Missing | `cast_mode: overcharge` with `overcharge: {stages, time, per_stage, cost_per_stage, spark: {window, bonus}}`; `stages` is the base of attribute `overcharge.stages`; glyphs grant it with a `first` patch (D7); raises the `overcharge_stage` trigger kind. | A15, D7 | M | P1 |
| A13 | Concentration: the bar fills only while the cursor rides the target; break policy cancel, release or drain | Missing | Timing kind `concentration` with `on_break`, `drain_rate`, `min_release`, `slack`; the server checks the streamed aim point (A15) against the target's position. | A15 | M | P2 |
| A14 | Use charges: a per-skill bank with trickle recharge, magazine reloaded on the skill's own cooldown, drip reload, manual reload, optional-fuel empower bank, still-only recovery with bleed on movement, vent-all, steps-from-bank (`SkillDef.useCharges`) | Missing | Form block `uses: {max, recharge, magazine: {refill, drip}, empower, still: {bleed}, vent_all, steps_from_bank}`; `max` and rates read attributes `uses.max` and `uses.rate` in weave scope; the bank lives in `WeaveSlot.State`; reload through `restore_uses` (C34) or a convert (D10). | WP1 slot state | M | P1 |
| A15 | Held input and aim: hold, release, second press, aim tracking, salvo re-aim and cursor-guided flight all read live input | Missing: intents are the one-shot `UseWeaveIntent` and `CancelCastIntent` (`Intents/Intents.cs`) | New intents `ReleaseWeaveIntent(slot)`, `PressWeaveIntent(slot)` and `AimIntent(point, direction)`; aim is accepted only while a held or tracking cast is live and is rate-limited by rule `casting.max_aim_rate`; messages are wired by WP6. | none | S | P0 |
| A16 | Costs: mana and life costs, pool-scaled costs (percent of maximum or current, priced from maximum or available), added costs, cost conversion, cost as damage | Partial: `costs` expressions per resource through the `cost` channel scoped by `cost.<resource>` (`WeaveRuntime.Costs`) | Expressions already cover pool-scaled costs (`mana.max * 0.1`). Add attribute families `cost.added.<resource>` and `cost.convert.<from>.<to>` read by `Costs`; cost as damage is a link from a stamped `last_cost.<resource>` gauge. | F9 | S | P1 |
| A17 | Reservation and upkeep: contract summons and toggles reserve maximum mana or life; upkeep per second, ramping or charge-fed | Missing | `<resource>.reserved` attribute lowering the usable maximum in `Stats.ResourceMax`; form field `reserve: {mana: expr}` for toggles and contracts; `upkeep: {resource: expr, ramp}` charged each tick by an upkeep system; starvation turns the toggle off. | none | M | P1 |
| A18 | Gates: a charge floor, a worn buff, a resource floor, a missing-pool thirst gate, a held guard, a running toggle, a recent wound, a held grab; innate or levied by supports (`GateSpec`) | Missing | Form and glyph field `gates` as a list of expressions on the caster scope (`charge.fury >= 3`, `has("guarding")`, `life_pct < 1`, `has("recently_hurt")`); unmet gates refuse with a reason and grey the slot. | none | S | P1 |
| A19 | Fuse: resolutions bank per landed hit and arrive after a fixed delay at the caster's live stats (`FuseSpec`, the Time Fuse support) | Missing | Wrapper effect `fuse {delay, effects}` scheduling its list with a fresh context; glyphs apply it with a `wrap` patch on `on_hit`; attributes `fuse.delay` and `fuse.power`. | D7 | M | P1 |
| A20 | Reflex: instant presses that pierce the caster's own commitment, such as a flask mid-cast (`REFLEX_CFG`) | Missing | Form flag `reflex` (and attribute `reflex` for glyph grants) lets `TryBegin` resolve an instant weave past the busy check without touching the running cast. | none | S | P2 |
| A21 | Interrupt and fizzle: casts can be interrupted; statuses can make casts fizzle with a short stun; casts can be scrambled into another ready skill | Partial: `interrupt` effect; stun and freeze interrupt (`CastingSystem`) | Attribute `cast.fizzle_chance` on the caster read by `TryBegin`; statuses carry it as a modifier. Scramble is P2. | none | S | P1 |
| A22 | Use lock after instant presses | Partial: rule `combat.global_cooldown` | Keep; add a per-form `recovery` lock in seconds. | none | S | P2 |
| A23 | AI hints: range, a range-scaling stat, weight, keep distance, soft minimum range with a near weight | Partial: `ai` hint with role, preferred range, weight and a `when` expression (`Definitions/Combat.cs AiHint`) | Add `min_range` and `near_weight`; range may name an attribute. | none | S | P2 |
| A24 | Charge costs: a use requires and spends combo charges (a fixed amount or all, with a minimum), optional spending that only scales the use, and more damage, extra projectiles or extra repeats per charge spent (`SkillDef.chargeCost`) | Missing: nothing writes the reserved `charge.*` gauges | Form and glyph field `spend: {charge, amount, minimum, optional, scales: [{attribute, form, per_charge}]}`; the runtime spends through the charge service when the cast resolves and builds cast-local modifiers from the count spent, exposed as var `charges_spent`; a glyph's spend wins over the form's own (`first`). | C7 | S | P1 |

### 2.2 B. Deliveries and trajectories

| ID | Prototype | Ours | Proposal | Deps | Size | Pri |
|---|---|---|---|---|---|---|
| B1 | Projectile core: speed, collision radius, range, pierce, count as a fixed number or a range, fan spread (`ProjectileDelivery`) | Present: `projectile` with count, speed, range, spread, pierce, radius, `on_hit`, `on_expire` (`Effects/Handlers/DeliveryHandlers.cs`; flight in `Systems/CombatSystems.cs ProjectileSystem`) | Keep. Count ranges become expressions with an Rng-backed `randi(lo, hi)`; spread reads attribute `projectile.spread` as a multiplier. | none | S | P0 |
| B2 | Hit shapes: projectile forms (vortex, circle, square, line, triangle, octagon, bar, arc, wave) that collide where they are drawn, with a circle opt-out (`projForms.ts`) | Missing: a circle radius only | A shape registry in `Effects/Shapes.cs` (circle, bar, arc, wave, square, triangle, line, sector, crescent, band) shared by projectiles, areas and fields; projectile params `shape` and `shape_size`; the client draws by the same name. | none | M | P0 |
| B3 | Firing styles: fan; salvo (one shot per beat after the cast, re-aimed at the live aim); volley (parallel ranks); ring (a full circle with phase jitter); spread by aim distance; origin at the cursor | Partial: fan only | Projectile params `fire` (fan, salvo, volley or ring), `salvo_interval`, `volley_spacing`, `ring_jitter`, `spread_by_aim: {near, far, range}`, `origin` (caster or point), `origin_range`; glyphs convert the style with patches (D7); salvo reads the aim stream and falls back to the cast aim. | A15, D7 | M | P0 |
| B4 | Trajectory axes: homing, cursor guide, erratic, spiral, orbit, spin, weave, amplitude, orbit radius, acceleration, zigzag with shed shards, terrain bounce, recurve, selective pierce, carom on hit, converge on the cast point; each axis is a stat whose base is the skill's value (`TrajectorySpec`) | Missing: straight flight with optional gravity (`ProjectileSystem`) | Attribute channels `projectile.homing`, `.guide`, `.erratic`, `.spiral`, `.orbit`, `.spin`, `.wave`, `.accel`, `.bounce`, `.recurve`, `.converge`; form `attributes` set the bases and glyphs add or scale them; params for amplitude, orbit radius and zigzag; a deterministic integrator in `ProjectileSystem` using a per-projectile Rng stream. | A15 for guide | L | P0 |
| B5 | Projectile lifecycle: forks split on impact and the children inherit the trajectory; returns to the origin or the caster re-hitting on the way; rehit (never dies, per-target lockout); duration-driven flight | Missing | Attributes `projectile.forks` and `projectile.rehit`; params `return` (none, origin or caster) and `duration`; fork children copy the motion state with forks reduced by one; the hit ledger clears on the return leg. | B4 | M | P0 |
| B6 | In-flight behavior: periodic emission of another skill, periodic area zaps, a carried damage aura, a trail of blasts or zones every so many units, a fissure trail | Missing | Projectile hooks `on_tick: {interval, effects}` and `on_travel: {every, effects}`; emission is `on_tick` with `cast` (C39); a carried aura is `on_tick` with a small `area`. | C39 | M | P0 |
| B7 | Completion behavior: explode where the flight dies; an end zone; a sequel cast at the death point by hit, expiry or either; shatter into forward shards on the first impact; plant an embed where it lands; returning blades caught for charges | Partial: `on_hit` and `on_expire` already express explode and end zone | Add hooks `on_first_hit`, `on_end` (any ending) and `on_return`; sequels and shatters use `cast` (C39); planting uses `deploy` (B16); catching uses `charge` (C7). | C39, B16 | M | P0 |
| B8 | Projectile chaining: a chain count redirects the flight to the nearest fresh enemy instead of ending it | Partial: the `chain` effect jumps instantly from the hit target (`DeliveryHandlers.cs ChainHandler`) | Attribute `projectile.chain`; with chains left a hit retargets the flight toward the nearest unhit enemy within rule `delivery.chain_range`. The instant `chain` effect stays for arcs. | B5 | S | P0 |
| B9 | Occlusion and phasing: deliveries stop at walls by default; `occlusion: free` and the `phasing` stat pass through | Missing: projectiles end only below the terrain height; areas ignore walls | `World/LineOfSight.cs` ray query over the height field and structure cells; projectiles stop at walls; area and field filters drop victims with no line from the origin unless `occlusion: free` or attribute `phasing` is positive. | none | M | P1 |
| B10 | Melee: reach from the attacker's edge, a swing arc, a swing shape (sector or band), reach and arc stats | Partial: `strike` form (one target) and `cleave` form (cone) in `content/core/combat/forms.yaml` | Attributes `melee.reach` and `area.angle` read by the cone and band shapes. | B2 | S | P0 |
| B11 | Nova: around the caster or the target; affects enemies, all or allies; spare the caster; edge-only ring; nearest few targets; a status filter | Partial: `area` with circle shape, filter, random `max_targets`, `include_center` | Area params `inner` (edge-only fraction), `pick` (random, nearest, farthest, lowest life, highest life), `require` (a per-target expression such as `target.status.burning`), and filter value `all`. | none | S | P0 |
| B12 | Cone and beam: a breath along the facing; far-edge only; a laser presentation for hitscan beams; bar-shaped projectiles for force walls | Partial: area shape `cone` with `angle`; the `beam` form uses shape `line` with a fixed width | Add `inner`; attribute `area.angle` scales the arc; `line` gains a `width` param defaulting to attribute `area.width`; force walls are bar-shaped projectiles (B2); presentation through `look`. | B10 | S | P0 |
| B13 | Ground placement: cast range; telegraph delay; lingering zone with a tick; exposure (stand inside for some seconds first); pull; fill inward; a line of segments; drift; seek; rotate; grow; size envelope on a curve; no impact; sweep semantics (hit once, re-arm on exit); shapes; emitters; occupant domains; terrain; fissures; linked markers; exclusive; follow; retract; end burst; pendulum; single sweep; strobe (`GroundDelivery`) | Partial: `blast` form (area at a point, no delay) and the `field` effect (radius, duration, interval, filter, pulse, follow) | Extend `field`: `delay` (the client draws `look` until it lands), `shape`, `exposure`, `pull`, `drift`, `seek`, `rotate`, `radius_curve: {from, to, curve}`, `hit_once`, `rearm_on_exit`, hooks `on_land`, `on_enter`, `on_exit` and `on_end`, `domain` (modifiers applied to occupants while inside, by filter), `exclusive` (one per caster and key), `segments: {count, spacing}`. Pulses and emitters are `on_tick` lists with `area` or `cast`. | B2, C39 | L | P0 |
| B14 | Cascade and aftershocks: a placement repeats at displaced points (along the axis, forward, backward or random) with radius, damage and interval steps; native lanes lengthen and other directions re-cast; area scatter adds secondary explosions | Missing | Wrapper effect `cascade {count, dir, step, scale_step, damage_step, interval, interval_step, effects}` with `count` defaulting to attribute `area.cascade`; glyphs apply it with a `wrap` patch on the form's top placement node. Aftershocks read attribute `area.scatter` inside `area`. | D7 | M | P0 |
| B15 | Storm: strikes scattered in a disc, sequenced or at once, placed under enemies first, with jitter, an option to arm during a channel and fire on release, a telegraphed disc, sky strikes that hit everyone and spare the roofed, a ground-kind filter (`StormDelivery`) | Missing | Effect `scatter {count, radius, interval, at, jitter, telegraph, effects}` with `at` random or enemies and `count` defaulting to attribute `area.strikes`; each strike runs its effects at a point. Release arming uses A7. | A7 | M | P0 |
| B16 | Constructs: totem, sentry, trap, mine, pylon, decoy, pad, gate, barrier, dome, eruptor, echo, tree, embed, relic and pod; ride minion ownership and caps; cast real skills with the deployer's build; rings and walls of segments; domes intercept projectiles; breakable with a death burst; clear the footprint; follow the owner; cast when struck; taunt (`ConstructDelivery`) | Partial: `spawn` places a pattern with a lifetime, and patterns can carry weaves and behaviors (`Effects/Handlers/SummonHandlers.cs`) | Effect `deploy {pattern, count, max_active, group, place, segments, lifetime, inherit_caster}` sharing summon capacity; with `inherit_caster` a construct's hits scale from the owner's stats and credit the owner; trigger behaviors (interval, enemy enters radius, owner casts, struck) are AI actions; a dome is a field with `intercept` (dissolve, deflect or slow) read by `ProjectileSystem`. | B13, B17 | L | P1 |
| B17 | Summon core: a fixed monster or a weighted pool; count; maximum active with the oldest replaced; duration; placement at the caster or the cursor with scatter; pool groups sharing capacity; exclusive groups (`SummonDelivery`) | Partial: `summon` with pattern, count, duration, max_active and group; ring placement; the oldest is destroyed past the cap (`SummonHandlers.cs`, `Minion` in `Components/Play.cs`) | Params `pool: [{pattern, weight, tags}]`, `place: {at, range, scatter}`, `pool_group` (shared capacity read from attribute `minion.max` scoped by the group tag), `exclusive_group`; count and cap keep `summon.count` and `summon.max`. | none | M | P0 |
| B18 | Minion inheritance: the owner's minion stats (damage, life, damage taken, speed, haste, regeneration, threat, area avoidance, apply chances) fold onto each body at birth with the summoning skill's tags and supports (`minionInheritance.ts`) | Missing: a minion spawns at the owner's level only | Summon param `inherit` (on by default): attribute family `minion.*` on the owner, queried with the summon weave's tags and glyph modifiers; results land on the minion as modifiers under source `owner:<key>` and refresh when the owner's attribute version changes. | B17 | M | P0 |
| B19 | Minion behavior: follow or escort the owner, assist the owner's target, leash to the owner, ignore incidental prey, obey commands with an obedience roll | Missing: summons run monster behaviors that return to their spawn point (`content/core/entities/behaviors.yaml`) | AI actions `follow_owner`, `assist_owner`, `obey_command`, `return_to_owner` registered in `Ai/BehaviorSystem.cs`; minion behaviors as content; summon param `behavior` overrides the pattern's behavior; perception exposes `owner_distance` and `owner_target`. | B17 | M | P0 |
| B20 | Persistent contracts: toggled summons reserve mana per slot and respawn fallen bodies after a timer; recasting turns them off; slots share pool groups | Missing | Summon param `contract: {reserve, respawn, toggle, slots}`; the contract lives in `WeaveSlot.State`; a minion system respawns. | A17, B17 | M | P1 |
| B21 | Corpse summons: raise the targeted corpse's own kind; attune a studied form; plural batches | Missing: dead bodies despawn after `combat.corpse_seconds` (`LifetimeSystem`) | A `Corpse` marker on dead non-player bodies until they despawn; targeting kind `corpse` (B27); summon `from_corpse` spawns the corpse's pattern and consumes it. | B27 | M | P1 |
| B22 | Summon extras: waves over time with a per-wave cost; exponential unlife; an apex presence that buffs other minions; devouring lesser minions; a body worn as a shell; passive replenishment while slotted | Missing | Summon params `waves: {count, interval, track_aim, cost_factor}`, `decay: {delay, fraction, growth}`, `presence` (a minion domain of modifiers), `replenish: {interval}`. Devour and shell are P2. | B17 | M | P1 |
| B23 | Dash: travel over time at a speed through a corridor of hits with a corridor damage scale; leave a decoy; trailing zones; phase through bodies; carry what it catches (`DashDelivery`) | Partial: `move` kind dash is an instant teleport toward the target (`Effects/Handlers/MovementHandlers.cs`) | Displacements processed over time by a new `Systems/DisplacementSystem.cs`: `move` kind `dash` with `distance`, `speed`, `width`, `on_contact`, `on_travel: {every, effects}`, `phase`; the mover wears flag `dashing` while it lasts. | none | M | P1 |
| B24 | Leap: airborne and untargetable for an air time, landing with a shockwave; a telegraphed landing for enemy dives (`LeapDelivery`) | Missing | `move` kind `leap` with `distance`, `air_time`, `on_land`; flags `airborne` and `untargetable` during the flight; the telegraph through `look`. | B23 | M | P1 |
| B25 | Blink, mark and recall, carom: a teleport with an optional delay or arriving behind the target; mark a point then recall to it; the caster as a bouncing ball | Partial: `move` kind teleport | `move` kind `blink` with `delay` and `behind_target`; mark and recall through slot state (a `mark` effect stores a point, a convert rule swaps to the recall); carom is P2. | D10 | S | P1 |
| B26 | Aura and toggle: an area on the bearer with self, ally and enemy modifiers; enemy damage per second, optionally drained from the bearer's life; siphon; heal pulses; death spawns; toggle or duration; upkeep and reservation; movement factor; maximum duration; a deactivation payload; seals, overdraft debt, life locks and ledgers (`AuraDelivery`) | Missing; the `ward` form is a timed status | `aura` effect attaching a following field with `domain` modifiers and `on_tick` effects; toggle state on the weave slot (pressing again turns it off) with `reserve`, `upkeep` (A17) and `on_deactivate`. Seals, overdraft and ledgers are P2. | B13, A17 | L | P1 |
| B27 | Targeting specs: enemy, corpse, minion or ally; required statuses or monster tags; consume the status or the corpse; plural corpses; search radius; fallback fail, self or aim; corpse life as damage or restoration; drain the target's life (`TargetingSpec`) | Partial: the `targeting` enum (self, enemy, ally, ground, direction, any) with range (`Definitions/Combat.cs`) | Form block `target: {kind, require, search_radius, fallback, consume}` checked in `ValidateTarget`; a consumed status's magnitude is exposed as var `consumed`. | B21 | M | P1 |
| B28 | Tethers: a live line between a spawned object and its caster, between sibling objects, or between the caster and a target; hurts enemies that cross it and heals allies inside (`TetherSpec`) | Missing | Effect `tether {link, width, duration, filter, pulse: {interval, effects}}` creating a `Tether` component that `Systems/TetherSystem.cs` tests as a segment each pulse. | B16 | M | P1 |
| B29 | Aim transforms: a random bearing inside a sector per use (scaled by a spread stat) or a played sequence of bearings with pauses (`AimSpec`) | Missing | Form block `aim: {random: {offset, spread}, sequence: {steps, pause}}`; spread reads attribute `aim.spread`; glyphs graft it with a patch. | D7 | S | P1 |
| B30 | Area shapes: circle, square, triangle, crescent, sector and band as a registry with a shape stat, swapped by sigil supports | Partial: circle, cone and line (`DeliveryHandlers.cs AreaHandler`) | Shapes from B2 serve `area` and `field`; glyphs swap shapes by patch. | B2 | S | P0 |
| B31 | Per-cast variance and named curves (linear, quadratic in, quadratic out, breath) | Partial: expressions can roll with `rand()` | Expression function `curve(name, t)` over a curve registry; variance stays an expression. | none | S | P1 |
| B32 | Detonate: set off all of the caster's mines; detonate the caster's own in-flight projectile nearest the aim | Missing | Effect `detonate {tags, pick, effects}` acting on owned entities by tag (all or nearest to the point); a detonated projectile runs its `on_end` where it is. | B16 | S | P1 |

### 2.3 C. Effect types

Every prototype `SkillEffect` kind, then the skill-level fields that behave like effects. Ours names our handler or the proposed one.

| ID | Prototype | Ours | Proposal | Size | Pri |
|---|---|---|---|---|---|
| C1 | `damage`: the skill's rolled damage by type to each target | Present: `damage` (`Effects/Handlers/DamageHandlers.cs`) | Add `amounts: {thread: expr}` for mixed-Thread bases and added damage (G2). | S | P0 |
| C2 | `status`: chance to apply a status with a magnitude; a fixed duration that ignores effect duration | Present: `apply_status` (`Effects/Handlers/StatusHandlers.cs`) | Add `fixed_duration`. | S | P0 |
| C3 | `buff`: a named modifier bundle on the caster, its minions or allies in a radius; shared or independent stack clocks; stacks per application or per power; clear on being hit; consumed on use, hit, crit, kill, hurt, block or evade; next-hit riders; power-scaled; life bond | Partial: beneficial statuses with modifiers; `modify` adds one temporary modifier but a refreshed one is removed by the first timer | Buffs are statuses, one status system: `apply_status` gains `to` (self, minions or allies) and `radius`; status hooks and stack clocks per E11 and E12; fix the `modify` refresh by keying its removal to an application counter. | M | P0 |
| C4 | `knockback`: an impulse on a decaying velocity, shove or random buffet, with collision consequences | Partial: `move` knockback teleports (`MovementHandlers.cs`) | An impulse velocity on `Motion` integrated by the displacement system (B23); `buffet` mode; a wall arrest raises the `collision` trigger kind. | M | P0 |
| C5 | `pull`: yank to the caster through the push physics, optionally stunning across the drag | Partial: `move` pull is instant | The same impulse path; an optional status for the drag. | S | P0 |
| C6 | `spawnZone`: a lingering damage zone at the impact | Present: `field` | Keep. | - | - |
| C7 | `gainCharge`: bank a named charge on use | Missing | `charge {id, gain, max}` effect over the charges system (D19 neighbor, WP5). | S | P1 |
| C8 | `drainCharge`: burn a charge down per second and block gains while draining | Missing | `charge {id, drain}` sets a drain on the charge. | S | P1 |
| C9 | `absorb`: a temporary absorption pool that evaporates | Partial: `resource ward` grants a regenerating ward pool | A status with `pool` consumed by the pools stage (G12). | S | P1 |
| C10 | `restore`: immediate life, mana, energy shield or poise | Present: `resource`, `heal` | Keep. | - | - |
| C11 | `terrain`: real terrain (ice, mud, bog, water) for a while | Missing | A timed surface overlay through the chunk modification path. | M | P2 |
| C12 | `recallMinions`: teleport mobile minions to a ring at the caster | Missing | `recall_minions {radius, group}`. | S | P0 |
| C13 | `heal`: flat plus a percent of the target's maximum; exclude the caster; chain to the most wounded ally | Present: `heal` with expressions, plus `chain` | `chain` gains `pick: lowest_life`; `heal` gains `exclude_self`. | S | P0 |
| C14 | `cleanse`: strip harmful statuses, newest first | Partial: `remove_status` by tags and count | Add `order` (newest or oldest); beneficial statuses are skipped by default. | S | P0 |
| C15 | `shed`: the caster removes one named status from itself | Present: `remove_status` with `self` | Keep. | - | - |
| C16 | `commandMinions`: order minions or a squad onto the aim to assault or hold, with duration, discipline and obedience | Missing | `command_minions {command, duration, affects, radius}` writes a command into minion behavior memory read by `obey_command` (B19). | M | P0 |
| C17 | `ironWard`: less damage taken while banking what lands, then a burst on expiry | Missing | A status with `bank: {source, fraction, cap}` and an `on_expire` area reading var `banked` (E9). | M | P1 |
| C18 | `guardSurge`: mana converted into bonus guard pool | Missing | `resource` on the guard pool once A11 lands. | S | P1 |
| C19 | `reduceCooldowns`: shave other skills' running cooldowns | Missing | `cooldown {seconds, fraction, scope}` with scope others, all or tags. | S | P1 |
| C20 | `relocateConstruct`: move one living device of the host | Missing | `relocate {group}` on owned deployables. | S | P2 |
| C21 | `restoreOverTime`: flask streams (flat, per level, percent of maximum, per charge, surge and settle lanes) | Partial: statuses with tick heals | Regeneration statuses with magnitude expressions; lanes become attributes. | S | P1 |
| C22 | `ward`: grant ward, per charge or on each hit | Present: `resource ward` | Per charge through var `charges_spent`. | S | P1 |
| C23 | `siphonOrb`: each hit knocks loose a resource orb that homes back to the caster | Missing | `spawn` of an orb pattern with a seek-owner behavior and pickup effects. | M | P2 |
| C24 | `detonateMinions`: minions charge the nearest foe and explode for a fraction of their life | Missing | `minion_cast` (C28) of a self-destruct weave. | S | P1 |
| C25 | `spawnCorpse`: raise fresh corpses at the aim | Missing | `spawn` with `as_corpse` (B21). | S | P1 |
| C26 | `dragCorpses`: gather corpses into a pile | Missing | Later, with corpses. | S | P2 |
| C27 | `shatterConstructs`: destroy the caster's totems for bursts | Missing | `detonate` on owned constructs (B32). | S | P2 |
| C28 | `minionCast`: every minion of the host skill executes a skill at the aim or at its nearest enemy | Missing | `minion_cast {weave, at, range, group}`. | S | P0 |
| C29 | `payLedger`: pay down a toggle's banked balance | Missing | With ledgers. | S | P2 |
| C30 | `spreadStatus`: copy statuses from a struck victim to its neighbors with strength and clock scaling | Missing | `spread_status {radius, statuses, tags, strength, duration, max_targets}` with duration remaining or refresh. | S | P1 |
| C31 | `siphonStatus`: draw afflictions from nearby allies or enemies onto the caster, healing per status | Missing | `move_status` from an area filter to the caster. | S | P1 |
| C32 | `transfuseStatus`: pour the caster's afflictions onto a target with a splash | Missing | `move_status` from the caster to the target with `splash`. | S | P1 |
| C33 | `recallImpales`: wrench lodged spears free, pop their banks, fly them home | Missing | Later, from `rupture` plus `projectile`. | M | P2 |
| C34 | `restoreSkillCharges`: reload use-charge banks for the host, all banks, or still-only banks | Missing | `restore_uses {amount, scope}` (A14). | S | P1 |
| C35 | `tame`, `whistleCompanion`, `companionStance`: claim a creature as a companion, recall and revive it, cycle its stance | Missing | Later: a companion fabric over persistent summons. | L | P2 |
| C36 | `conjure`, `kindle`, `vent`, `lure`, `litePour`, `townPortal`: cloud bridges, lightwells, fog banks, false attraction, lightweight swarms, a portal home | Missing | Later: each needs a world fabric we do not have; `event` can stand in for portal requests. | L | P2 |
| C37 | `grabSeize`, `grabThrow`: hold a struck body (carry, drag, pin, swallow) and throw it | Missing | Later: a grab fabric with mass rules. | L | P2 |
| C38 | `possess`, `possessEnd`, `shapeshift`, `mimicSelect`, `throngDirect`: control another body, take a form, pick a captured art, direct a gathered swarm | Missing | Later. | L | P2 |
| C39 | Casting another skill: sequels, emitters, follow-ups, meta payloads and contagion releases all cast a catalog skill at a point at the host's level | Missing as an effect | New `cast {weave, at, aim, level, damage_scale}` effect, with `at` one of point, target or caster and `aim` one of onward, out, random or rotating: compiles the named preset once, runs it free of cost and cooldown at `Depth + 1` with the host's level, tags and origin. The backbone of B6, B7, D11 and D18. | M | P0 |
| C40 | `rupture`: consume a fraction of a banked status as an immediate burst of its type | Missing | `rupture {status, fraction, scale}` over banks (E9). | S | P1 |
| C41 | `birth`: bodies stand where the delivery resolved | Present: `spawn` at the focus | Keep. | - | - |
| C42 | Skill field `shatterStatus`: consume listed statuses for more damage, optionally per stack | Missing | `consume_status {status, tags, as}` before `damage`, whose amount reads the named var. | S | P1 |
| C43 | Skill field `currentLifeDamage`: strip a fraction of current life without killing | Partial: expressions can read `target.life` | `damage` flag `cannot_kill`. | S | P1 |
| C44 | Skill field `siphon`: a fraction of landed hits returns as healing | Missing | `damage` param `leech` or attribute `leech.life` (G13). | S | P1 |
| C45 | Skill field `backstabMult`: more damage from behind | Missing | Victim tag `vs.behind` (F5). | S | P1 |
| C46 | Skill field `poiseReap`: against a broken poise bar, add maximum poise as damage | Missing | An amount expression over `target.poise.max` once poise exists (G12). | S | P1 |
| C47 | Skill field `empower`: weighted enemies near the caster raise damage or grant stacks | Missing | Expression function `count_near(radius, filter)` in the effect scope. | S | P2 |
| C48 | Skill fields `selfCleanse`, `chrono`, `ultimate`, `linkedHexes`, `amalgam`, `retaliate`, `invokes`: toll off own afflictions; hold time; a super-art cut-in; linked curses; fuse minions; spawn when struck; rune sequences | Missing | Later. `retaliate` is a proc (D19); global time holds are rejected for shared zones (Q10). | - | P2 |
| C49 | (ours only) `condition`, `random`, `repeat`, `sequence`, `event`, `interrupt`, `taunt`, `grant_item`, `grant_xp`, `log`, `chain`, `spawn`, `modify` | Present (`FlowHandlers.cs`, `UtilityHandlers.cs`, `DeliveryHandlers.cs`, `SummonHandlers.cs`, `StatusHandlers.cs`) | Keep; they let content compose what the prototype adds fields for. Add `delay {seconds, effects}`. | S | P0 |

### 2.4 D. Supports

| ID | Prototype | Ours | Proposal | Deps | Size | Pri |
|---|---|---|---|---|---|---|
| D1 | Tag gate: `requiresTags` any-of and `excludeTags` none-of against the skill's tags plus granted tags | Present: `requires`, `excludes` (`Weaves/CompiledWeave.cs`) | Keep. | none | - | - |
| D2 | Granted tags count for other supports' gates and for stat contexts, resolved to a fixpoint | Present: `adds_tags` are unioned before any gate is checked, which is order-independent | Keep. | none | - | - |
| D3 | Mechanism gate: an open registry of predicates over the live instance from any source: cooldown, engagement, affliction, strikes, flight, displaces, bankless, guard, surface, construct, status, pour, landing, periodic construct (`SUPPORT_MECHANISMS`) | Partial: `requires_mechanisms` against `mechanisms` declared on the form plus glyph `adds_mechanisms` | `Weaves/Mechanisms.cs`: a registry of probes run on the compiled weave (its patched effect specs, timing and modifiers); a weave's mechanisms are the declared set plus every probe that holds. Built-in probes: `cooldown`, `strikes`, `projectile`, `area`, `surface`, `summon`, `minion`, `construct`, `periodic_construct`, `engagement` (a toggle, a contract or a held guard), `displaces`, `channel`, `held`, `guard`, `uses`, `bankless`, `affliction`, `status` (the weave applies a status whose strength can scale), `landing`. The prototype's flask-lane probe waits for flasks. Packages register more probes in their own files. | WP1 | M | P0 |
| D4 | Parameterized mechanisms such as `affliction:bleed`, `flight:children`, `flight:spends`, `surface:standing`, `construct:massed` | Missing | Probe names may carry a parameter after a colon, passed to the probe; unknown parameters fail validation. | D3 | S | P0 |
| D5 | Dormant misfits: socketing is permissive; a misfit contributes nothing, not even costs, and self-lifts when an enabler arrives; the host lists which lane each gem serves | Missing: a misfit fails compilation and the slot change is rejected | The compiler keeps misfit glyphs in `CompiledWeave.Dormant` with their reasons and compiles without them; the UI shows them greyed with the reason; whether players may slot a misfit at all is settled by Q4. | D3 | S | P0 |
| D6 | Support levels and level bonus: gem levels up to a cap; `perLevel` scaled by gem level minus one; `levelBonus` adds levels to the host, optionally growing per gem level | Partial: `per_level` scales by the weave's level and `GlyphDef.level` is unused | Glyph entries in a `WeaveSpec` carry a level (`fork@3` or `{id: fork, level: 3}`); `per_level` scales by the glyph's level; `level_bonus` and `level_bonus_per_level` raise the weave's effective level; rule `glyphs.max_level`. | none | S | P0 |
| D7 | Graft precedence: a socketed graft wins over the skill's own spec; some specs merge (tethers, follow-ups, conduits, charge taps); cascades and pulses lengthen the native lane | Missing: glyphs only append top-level effects | Effect patches: glyph `patches` as a list of `{type, where, set, wrap, mode}` applied by the compiler to the form's effect specs in glyph order before compiling. Modes: `first` (the first glyph wins over the form and later glyphs), `default` (only where the form has none), `append` (concatenate lists), `wrap` (wrap matching nodes in a wrapper effect such as cascade, fuse or repeat). Validation rejects patches naming unknown params. | WP1 | M | P0 |
| D8 | Riders at named moments: on hit, on kill, on expiry, while flying, on landing, on cast | Missing: `adds_effects` appends to the root of the graph, so a glyph cannot add to a projectile's on-hit list | Glyph `hooks` keyed by hook name (`on_hit`, `on_kill`, `on_end`, `on_tick`, `on_land`, `on_cast`); handlers declare which of their params are hook lists; the compiler appends each glyph hook list into every node that exposes it, and `on_cast` into the root. | WP1 | M | P0 |
| D9 | Mechanic stat channels: repeat count, fork, chain, pierce and return; projectile count and speed; area radius, cascade, scatter and pulse counts; storm count; summon count and minion caps; splash radius; knockback; trajectory powers | Partial: `projectile.count`, `.speed`, `.pierce`, `chain.count`, `area.radius`, `duration`, `summon.count`, `summon.max`, `range` (`content/core/attributes.yaml`) | Declare the missing channels in the weave category with bounds, and default handler params to them as `projectile` already does with `weave.projectile.count`. Each package declares its channels in its own attributes file. | B rows | S | P0 |
| D10 | Convert rules: while a rule holds, pressing the skill casts another (companions full, charges empty, seat away); the slot shows the converted face | Missing | Form and glyph field `convert: {when, weave}`; `when` is an expression over slot state (`slot.uses`, `slot.mark`) and the caster; the slot message carries the converted face. | A14 | S | P1 |
| D11 | Meta actions: a second ability on the same slot; the skill's meta first, then each support's in slot order, one beat apart | Missing | Form and glyph field `meta: {weave, label}`; `UseMetaIntent(slot)` runs the chain through `cast` with the host slot as context, so `minion_cast` scopes to the host's minions; rule `casting.meta_interval`. | C39, A15 | M | P1 |
| D12 | Triggers (cast on X): crit, damage taken by accumulated life fraction, channel beat, overcharge stage, channel finish, status applied (banking power), block, kill, high roll, guard beat, flawless press, melee hit. Rules: one cast per event chosen round-robin down the bar, a cast-time gate unless a permit support rides along, untriggerable kinds, a chain depth limit, honest costs and cooldowns, an internal cooldown per gem, a chance cap; the slot key arms and disarms | Missing | Glyph and form field `trigger: {on, chance, icd, life_fraction, status, power, max_cast_time}`; trigger kinds come from the shared trigger registry (WP1); a trigger runtime picks one armed slot per event round-robin and applies rules `triggers.max_cast_time` (lifted by mechanism `trigger_permit`), `triggers.max_depth` and `triggers.chance_cap`; costs and cooldowns are paid; attribute `trigger.chance` scales the chance. | WP1 trigger registry | L | P0 |
| D13 | Repeats: repeat count (fractions accumulate), repeat scale (each repeat larger), retarget to the nearest enemy, locked flurry; spell echo, multistrike, cascade, crescendo; unleash banks seals while the skill rests | Partial: the `repeat` effect wraps a list | Runtime repeats driven by attributes `weave.repeat.count`, `.scale`, `.interval`, `.retarget`, `.lock`; `Execute` schedules re-executions of the compiled graph at `Depth + 1`; unleash is a slot-state bank. | none | M | P0 |
| D14 | Global lane: defensive support stats (armor, evasion, life, regeneration, shield) apply to the wearer while the host sits on the bar; skills can carry passive modifiers while equipped | Missing: glyph modifiers only join weave-scoped queries | Glyph and form field `equip_modifiers`, applied to the caster under source `weave:<slot>` while slotted. Explicit, never an implicit list of attributes. | none | S | P1 |
| D15 | Crew forwarding: supports on a summon skill board the minions' own skills when a resonance key rides the summon; construct copies are priced; boarding supports bill their cost modifiers to the host; fit may be judged against the crew's skills | Missing | Glyph flag `forwards` and mechanism `resonance`; when present the minions' weaves compile with the forwarded glyphs that fit; rules `minions.forward_requires` and `minions.construct_forward_scale`; forwarded cost modifiers join the host's `cost` fold. | B17, D5 | M | P1 |
| D16 | Levies and spender conversions: supports demand a gate, make the host spend a charge bank (optional or hard), or add costs | Missing | Glyph fields `gates` (A18) and `spend` (charge cost, WP5); added costs are modifiers on `cost.added.<resource>`. | A18 | S | P1 |
| D17 | Munition and granted banks: convert a bar cast into an ammunition skill that reloads through its convert; or stand up a bank shaped by the host's clock (a magazine on cooldown hosts, an empower bank otherwise) | Missing | Glyph grants of `uses` with a `first` patch, plus a `convert` to a reload preset. | A14, D10 | S | P1 |
| D18 | Follow-ups, cast cycles, self-stacks, combo chains: a payload a beat after each real use; every so many uses grant a buff; a per-skill frenzy pile that peels or drops; one key walks a sequence | Missing | Form and glyph fields `follow_up: {weave, delay, chance}` (merging), `cycle: {count, effects}`, `self_stack: {modifiers, max, duration, decay}` (weave-local modifiers scaled by slot gauge `stack.<slot>`) and `chain: {steps, window}`, all kept in `WeaveSlot.State`. | C39 | M | P1 |
| D19 | Procs: `proc_<id>` chance stats granted by any modifier; triggers (hit, kill, collision, status applied, block, evade, shield and poise events, charge or buff gained, orb pickup, hurt, miss, foiled, cast, condition edge, pulse, minion death, heal, cheat death, struck); owner and victim gates; rates by chance, internal cooldown or procs per minute; depth gating with falloff; payloads (extra hit, explosion, arc, displace, collision damage, charge, buff, summon, status, heal, restore, burst, delayed burst) (`data/procs.ts`) | Missing | Content kind `procs`: `{on, tags, when, victim, no_crit, chance, icd, ppm, effects}` where `chance` names an attribute and `effects` is our effect graph; a proc service subscribes through the trigger registry and rolls from per-owner Rng streams; rules `procs.max_depth`, `procs.depth_falloff`, `procs.chance_cap`. | WP1 trigger registry | L | P1 |
| D20 | Slot grafts: a stat family grants a support at a level to whatever skill sits in bar seat N; any modifier source can grant it; a socketed copy wins | Missing | Attribute family `glyph.slot<N>.<glyph>` whose value is the level; slot compilation injects them as glyphs, dormant if misfit, yielding to a socketed copy. | D5, D6 | S | P1 |
| D21 | Skill grants: a stat family grants a skill at a level; the granted skill's sockets and tree persist on the granting item | Missing | Attribute family `weave.grant.<preset>` producing granted slots; their glyphs are saved on the item. | I8 | M | P2 |
| D22 | Support base chassis: one support whose payload is rolled per copy (one weighted row per axis, fixed at mint; a canonical roll for granted copies; rows carry their own mechanism demands) (`supportbase.ts`) | Missing | Glyph `axes: [{id, rows: [{id, weight, modifiers, requires_mechanisms, text}]}]`; a glyph reference may carry its rolled rows; the compiler folds the rows' modifiers and gates; granted copies use the first row of each axis. | I8 | M | P1 |
| D23 | Per-skill trees: a graph of nodes bought with ability points at level bands carrying modifiers, typed overrides, grafts, triggers, follow-ups and charge taps; links, exclusions and ranks; reset at a station | Missing | Form field `tree: {opens_at, nodes: [{id, links, excludes, ranks, modifiers, patches, glyph}]}`; `WeaveSpec.tree` lists spent nodes; points from rule `casting.tree_points`, an expression of weave level; node patches reuse D7. | D6, D7 | M | P1 |
| D24 | Echo riders: ghost casters that hover and shoot, glide and strike once, or replay the owner's uses | Missing | Deployables whose behavior re-casts the host weave. | B16 | M | P2 |
| D25 | Curse on hit and curse fields: a socketed curse is drawn (reserving mana) and rides every hit, or becomes a following or ground field that re-afflicts | Missing | An `on_hit` hook casting the curse at a scale, plus toggle reservation (A17). | D8, A17 | S | P1 |
| D26 | Spawn and kill riders: kills raise the slain; consume a minion for damage; consumed corpses crawl back; ticking statuses hatch minions; summons born with a buff; minion auras | Missing | Compositions: an `on_kill` hook with `summon` from the victim's pattern; a `consume_minion` effect; status riders (E10); summon param `on_birth` effects. | D8, B17 | M | P1 |

### 2.5 E. Statuses

| ID | Prototype | Ours | Proposal | Deps | Size | Pri |
|---|---|---|---|---|---|---|
| E1 | Duration scaled by the applier's effect duration; stacking with a cap the applier can raise (`ailmentStacks`); refresh or strongest re-application | Present: duration expression, `max_stacks`, policies refresh, strongest, stack and independent; duration scaled by `effect.duration` and reduced by `status.resistance` (`StatusEffects/StatusService.cs`) | Add attribute `status.stacks` on the applier (scoped by `status.<id>`) added to `max_stacks`. | none | S | P1 |
| E2 | Damage over time per second locked at application, ticked by type | Present: tick effects with `amount` equal to magnitude times interval | Keep. | none | - | - |
| E3 | Modifiers per stack; non-damaging statuses scale by applier power; power-inert exemptions | Present: `modifiers_per_stack`, `scales_with_potency` | Keep. | none | - | - |
| E4 | Flags and forbidden tags: hard crowd control; silence, disarm and root forbid tags; panic, prey scent, never retreat and short attention read by AI | Partial: `flags` work; `forbids_tags` is declared but never applied, so silence does not stop spells; AI can read flags through behavior expressions | `StatusService.Refresh` sets `forbid:<tag>` flags from `forbids_tags`, matching the check in `TryBegin`; AI flag conventions (`panicked`, `prey`) live in behavior content. | none | S | P1 |
| E5 | Buildup: reaching maximum stacks consumes them into another status | Present: `buildup_into` | Keep. | none | - | - |
| E6 | Propagation on death to the victim's allies | Present: `propagate_on_death` with rule `combat.propagate_radius` | Keep. | none | - | - |
| E7 | Generated families: chance to apply each status on hit, a wet-target variant, minion carry, damage versus its stacks, pop power, damage-over-time leech | Missing: only each Thread's own status rolls, through `ailment.chance` (`Combat/DamagePipeline.cs ApplyAilments`) | Attribute families `status.<id>.chance` (rolled on every hit for every status), `minion.status.<id>.chance`, `status.<id>.leech` and `status.<id>.pop`; victim tag `vs.<status>` for every active status; gauge `target.status.<id>.stacks` readable in the hit scope (F6) for per-stack damage. | F6, F9 | M | P0 |
| E8 | Pop on reapply: reapplication detonates a fraction of the remaining banked damage, then the new application takes over | Missing | Status hook `on_reapply` effects with var `remaining_damage` (magnitude times stacks times remaining seconds). | none | S | P1 |
| E9 | Banks and rupture: authored applications add to a standing bank capped at a multiple of the strongest application; a rupture spends part of the bank as a burst; doom detonates early once the bank meets remaining life | Missing | Stack policy `bank` with `bank_cap`; `rupture` effect (C40); hook `on_bank_lethal`. | C40 | M | P1 |
| E10 | Riders stamped by the applier: expiry ruptures from supports, hatch clauses on ticking damage, leech fractions | Missing | `StatusApplication.riders`: extra effect lists attached by the applier's hooks (`on_status_tick`, `on_status_expire`) and stored on the instance. | D8 | M | P1 |
| E11 | Consumable stacks and next-hit riders: stacks spent on use, hit, crit, kill, hurt, block or evade with tag filters; the bearer's next landed hit carries a status, added damage or a proc | Missing | Status fields `consume_on: {event, tags, count}` and hook `on_bearer_hit` (effects run against the struck target), fed by the trigger registry. | WP1 trigger registry | M | P0 |
| E12 | Independent stack clocks: each stack lives on its own timer | Partial: the independent policy keeps one instance per source, not a timer per stack | `stack_clock: independent` keeps a timer per stack inside one instance. | none | S | P1 |
| E13 | Cast fizzle and scramble from statuses | Missing | Modifiers on `cast.fizzle_chance` (A21); scramble later. | A21 | S | P1 |
| E14 | Movement and time states: flight as a state, inverted movement, personal time scale, a frozen view frame | Partial: `Motion.Flying` exists | Flag `flying` read by movement; inverted movement and time scale later; frame locks rejected (Q10). | none | S | P2 |
| E15 | Weak spot: a band on the victim's life bar takes bonus damage until the victim is driven below it | Missing | A status storing its window and a victim tag `vs.weak_spot` from the hit scope. | F6 | M | P2 |
| E16 | Periodic neighbor effects and damage curves: brands zap the victim's allies, madness lashes at the nearest body, damage ramps or tapers over the status's life | Present by composition: tick effects with an `area` around the bearer; curves need the elapsed time | Add vars `elapsed` and `total` to tick contexts. | none | S | P1 |
| E17 | Baseline magnitude: a caster-independent floor by level for caster-less sources | Partial: rules `combat.ailment_magnitude.<status>` | Status field `baseline` (expression) used when no hit magnitude exists: fields, zones, procs. | none | S | P1 |
| E18 | Discharge on hit: an impale's bank discharges into the bearer's next qualifying hit | Missing | Hook `on_bearer_struck` with `rupture`. | E9 | S | P1 |
| E19 | Presentation: status voices, body effects, screen cues, concealment, ghosting | Partial: `color`, `icon` | Presentation keys under `meta`, read by the client and never by the simulation. | none | S | P2 |
| E20 | Status roster: poison, taunted, vulnerable, weakened, brittle, disarmed, blinded, exposed, doom, impaled and many themed states | Partial: the Thread ailments plus control and buff statuses (`content/core/combat/statuses.yaml`) | Content: port statuses into the Threads' vocabulary as each mechanism lands; no engine work beyond the E rows. | E rows | M | P1 |
| E21 | Applier and identity fields: the newest applier takes over credit on refresh (taunts) while damage over time keeps its first applier; a fraction of the hit per second when applied by a stat-granted chance; an element for ailment resistance; beneficial statuses that cleanses skip; states bound to an absorption shield that shatter with it | Partial: refresh and stack policies hand the source to the newest applier; magnitudes come from rules `combat.ailment_magnitude.<status>`; Thread tags on statuses scope `status.resistance`; `beneficial` and `dispellable` exist | Status field `keep_source` for statuses whose first applier keeps the credit; `bound_to` naming a status pool (C9) so a bound state ends with the shield. | C9 | S | P1 |
| E22 | Active status state: per-source lanes, remaining time, stacks, locked damage per second, applier power, total span, applier id, rupture payload and radius, a life-bar window, zap and lash clocks, hatch and leech accumulators, a pop accumulator, bank peak and fraction | Partial: `StatusInstance` has id, source key, source, remaining, total, stacks, magnitude, a values bag holding potency, and the next tick time (`Components/Stats.cs`) | Keep the shape: clocks, accumulators and windows live in `StatusInstance.Values`; add only `Riders` (E10) and `BankPeak` (E9). | E9, E10 | S | P1 |

### 2.6 F. Modifiers

| ID | Prototype | Ours | Proposal | Deps | Size | Pri |
|---|---|---|---|---|---|---|
| F1 | Kinds flat, increased, more, override and single-hop link | Present (`Attributes/Modifier.cs`, `Attributes/AttributeSet.cs`) | Keep. | none | - | - |
| F2 | Gauges that scale by a live quantity or gate at a count | Present: `gauge`, `gauge_at` | Keep. | none | - | - |
| F3 | Tag scopes, all-of | Present | Keep. | none | - | - |
| F4 | Conditions from a fixed list: low and full life, mana and shield; has shield; guarding; poised; poise broken; shield recharging; stationary; moving; varied or repeated casts; recently hit, crit, killed, hurt, blocked, evaded, moved or healed; not hurt recently; strided | Partial: `has()` over low and full life and mana, stationary, moving, airborne and `recently_*` stamps (`Components/Stats.cs`) | The life and mana lines become rules and attributes (`low_life.line`); add stamps for kills and movement skills; shield, poise and guard conditions arrive with G12 and A11; a recent-cast ring on the weave book exposes `combo_varied` and `combo_repeated`. | G12, A11 | S | P1 |
| F5 | Victim scope: `vs:` tags folded from the struck body's registered conditions and every status it carries | Partial: `vs.<entity tag>`, `vs.<flag>`, `vs.low_life`, `vs.full_life` (`DamagePipeline.Build`) | Victim tags defined by rule expressions (`combat.victim_tags`: behind, higher level, near, far and so on) evaluated once per hit, plus `vs.<status>` for every active status. Tags keep the attribute cache valid. | WP1 | S | P0 |
| F6 | Modifiers that read the target, such as damage per stack of a status on the victim | Missing: `Stats.Mounted` exists but is never set, so gauges and conditions cannot read the target | During a hit the pipeline passes a pooled hit scope with `target.*` and `distance` mounted as the owner scope of its uncached `Apply` calls; validation warns when a target-reading modifier sits on an attribute queried outside hits. | WP1 | S | P0 |
| F7 | Stat trades: gain a rate of one stat's baseline as another while forgoing a fraction of the source; both dials are ordinary stats; single hop (`STAT_TRADES`) | Missing | A rules list `trades: [{from, to, rate, forgo}]` where rate and forgo name attributes, applied in the fold after links; validation forbids dials that are themselves traded. | none | M | P1 |
| F8 | Attribute set: attributes grouped in triads with per-point modifiers that drive tooltips; percent attribute grants; attribute requirements checked at learn and at cast | Partial: a small core set (might, grace, will, vigor) feeding formulas (`content/core/attributes.yaml`) | Keep our core set (Q13) but declare `per_point` modifiers on attribute definitions so tooltips derive from data; form and glyph `requirements` checked when slotting and when casting. | none | S | P1 |
| F9 | Generated families per status, per damage type, per proc, per charge, per bar seat, per skill grant, per class | Partial: `convert.{from}.{to}`, `extra.{to}`, `pen.<thread>` and `resist.<thread>` work as undeclared channels | Content kind `attribute_families`: `{pattern, over, name, category, bounds}` expanded at load (for example `status.{status}.chance` over statuses), so validation, names and bounds exist for every member. | WP1 | S | P0 |
| F10 | Provenance per modifier for removal and display | Present: `Source` strings | Keep. | none | - | - |

### 2.7 G. Damage pipeline

| ID | Prototype | Ours | Proposal | Deps | Size | Pri |
|---|---|---|---|---|---|---|
| G0 | (structure) a fixed ladder of rungs, each a stat or a rule | Partial: one method with fixed stages (`Combat/DamagePipeline.cs Apply`) | Stage order in rules (`combat.stages`: immunity, guard, evade, block, crit avoid, mitigate, taken, pools, cap, cull, leech) with implementations registered by name under `Combat/Stages/`; the first refactor preserves today's order and output exactly. | WP1 | M | P0 |
| G1 | Base damage rolled as ranges per type | Partial: one amount split evenly across the weave's Threads | `damage` param `amounts` (C1); ranges stay expressions such as `lerp(lo, hi, rand())`. | none | S | P0 |
| G2 | Added flat damage per type from any source, scaled by added effectiveness | Missing: a flat `damage` modifier scoped to a Thread only applies when the hit already carries that Thread | Attribute family `added.<thread>` read in `Build` for every Thread, multiplied by `damage.effectiveness` from the form or by `added.effectiveness`, then folded as usual. | F9 | S | P0 |
| G3 | Conversion between types capped at the whole source; extra damage gained as another type | Present: `convert.{from}.{to}`, `extra.{to}` | Add defender-side `taken_as.{from}.{to}` before mitigation. | none | S | P1 |
| G4 | Crit chance and multiplier; crit avoidance on the victim; damage-over-time crits | Partial: chance and multiplier | Attributes `crit.avoid` on the victim and `crit.dot` on the attacker. | none | S | P1 |
| G5 | Evasion by entropy: an accumulator per victim so high evasion slips the opening burst, then hits land on schedule | Partial: an independent roll per evadable hit through rule `combat.hit_chance` | Rule `combat.evasion` (entropy or roll); entropy keeps an accumulator gauge on the victim seeded from its Rng. | G0 | S | P1 |
| G6 | Block by guard value: a block stops up to a value (a level floor plus block value, times guard strength); heavier wounds seep; block power caps the stoppable fraction; a block stops statuses and knockback | Partial: `block.chance` and a `block.amount` fraction (a full stop by default); ailments still roll on what a partial block lets through | Rule `combat.block` (fraction or guard) with attributes `block.value`, `block.power` and `guard.strength`; rule `combat.block_stops_ailments`. | G0 | S | P1 |
| G7 | Armor: hyperbolic and uncapped, with armor penetration | Partial: rule expression `combat.armor_reduction`, clamped in code | Expose `pen.armor` to the armor expression and move the clamp into the rule. | none | S | P1 |
| G8 | Resistances: a soft cap per type the defender can raise, a hard cap, penetration after the caps down to a floor | Present: `resist.<thread>.max`, `combat.resist_cap`, `combat.resist_floor`, `pen.<thread>` | Keep. | none | - | - |
| G9 | Slayer multipliers against heavier, higher-level, elite, composite, rooted, spent, uprooted, bonded or faltering victims | Partial: through `vs.*` tags where a matching victim tag exists | Victim tag rules (F5) express each axis as data. | F5 | S | P1 |
| G10 | Damage taken, tag-queried including the type | Present: `damage.taken` | Keep. | none | - | - |
| G11 | Hit immunity and bounded area avoidance | Partial: `immune` and `invulnerable` flags | Attribute `area.avoidance` rolled for area-tagged hits in the evade stage. | G0 | S | P2 |
| G12 | Defense pools: insight (avoidance fed by momentum), poise (a break bar with poise damage, a broken status, brackets and re-arming), endurance (a flat reduction pool), energy shield (a second life with recharge delay and rate, steadfastness, shred, damage-over-time bypass and resistance), mana shield, stagger (spread over time), absorption, a per-hit cap, plies, cheat death, recuperation | Partial: pools in rule order (`combat.pools: [ward, life]`) | Pool entries become objects in `combat.pools` (`{resource}`, `{status_pool}`, `{resource, fraction}`); new resources `shield` (with `recharge_delay` and `recharge_rate` attributes) and `poise` (empty breaks into a status; `poise.damage` on attackers); stagger, hit cap and cheat death as stages. Insight, endurance and plies are P2. | G0 | L | P1 |
| G13 | Leech and on-hit sustain: life, mana, shield and ward leech with caps; life on hit, on kill, on block, on evade | Missing | Attributes `leech.<resource>` (a fraction of damage dealt), `on_hit.<resource>` and `on_kill.<resource>`, paid by a `leech` stage after the pools; caps from attribute bounds. | G0 | S | P1 |
| G14 | Damage over time skips evasion, block, armor and crit; the shield is a second life pool for it | Partial: ticks take the full pipeline, so a block can stop a tick and armor applies to Stone ticks | Rule `combat.stage_skips: {dot: [evade, block, crit, armor]}` keyed by hit tag. | G0 | S | P1 |
| G15 | Culling at or below the attacker's threshold | Present: `cull.threshold` | Keep. | none | - | - |
| G16 | Hit results: received amounts after defender conversion, blocked amount, poise broken, culled, clamped, the roll fraction for high-roll triggers | Partial: `DamageDealt` carries total, crit, evaded, blocked and amounts by Thread | Add `Culled`, `Clamped` and `Roll` (the position within the rolled range) to the hit result and the event. | none | S | P1 |
| G17 | Luck: lucky and unlucky rolls, damage spread, a high-roll window | Missing | Expression function `lucky(lo, hi)` and attribute `damage.luck`. | none | S | P2 |
| G18 | A taunted attacker deals less damage to anyone but its taunter | Partial: `taunt` forces AI targets | Victim tag `vs.not_taunter` from the attacker's taunt memory, carried by a status modifier. | F5 | S | P2 |

### 2.8 H. Passives

| ID | Prototype | Ours | Proposal | Deps | Size | Pri |
|---|---|---|---|---|---|---|
| H1 | Node kinds start, small, notable, keystone and attribute; flat and percent attribute grants; modifiers; links | Present (`Definitions/Entities.cs PassiveNodeDef`, `content/core/entities/passives.yaml`) | Keep. | none | - | - |
| H2 | Choice nodes: a node deals options from a group; pick limits; options unique per node or per character (masteries); a group's deal can be independent per node, claimed by the first node (a mutually exclusive cluster) or dealt by the first node only (shortcut siblings grant nothing); pathing groups; a default pick for nodes already allocated (`data/passiveChoices.ts`) | Missing | Content kind `passive_choices` with `{id, name, options: [{id, name, modifiers, glyph}], pick, unique, deal}`; node field `choice: {group, pick}`; allocation names an option (`AllocatePassiveIntent(node, option)`); picks persist in a `PassiveState` component and the character sheet. | none | M | P0 |
| H3 | Selectable attribute nodes: single-attribute nodes offer any attribute | Missing | A choice group generated over the attribute catalog's core category. | H2 | S | P0 |
| H4 | Realms: parallel constellations shown as tabs; tree or free adjacency; roots granted free; their own point currency; an unlock condition (`data/passiveRealms.ts`) | Missing: `realm` is a string nothing reads | Content kind `passive_realms` with `{id, adjacency, roots, currency, unlock}`; points per currency from rules or quest rewards; allocation checks the node's realm and adjacency. | none | M | P1 |
| H5 | Vocations: quest-earned mini-trees tied to a class start node, spending vocation points, with secret variants found in the world (`data/vocations.ts`) | Missing | A realm per vocation whose `unlock` reads a quest flag and whose `gate` names a calling start node; quest rewards grant the realm currency. | H4 | M | P1 |
| H6 | Passive grafts: a node or option grants a support bindable onto one learned skill | Missing | Node and option field `glyph: {id, level}` granting a graft the player binds to a weave slot (`BindGraftIntent(source, slot)`), injected like slot grafts (D20). | D20 | M | P1 |
| H7 | Passive conduits: a node grants an actor-level resource pump | Missing | Later, with conduits. | none | M | P2 |
| H8 | Refunds: refund single nodes at a station with a connectivity check from starts, crests and realm roots; free roots never refund; choice nodes refund all their picks; points return to their own pool (`passiveRefund.ts`) | Partial: a full reset only (`Progression/ProgressionService.cs Reset`) | `ProgressionService.Refund(node)` with a graph walk from the anchors; an interactable kind `font` gates it; rule `passives.refund_cost` is a currency expression. | H2, H4 | M | P0 |
| H9 | Starts: the class start is free; other starts may be bought as travel but never become anchors | Partial: start nodes cannot be allocated at all | Allow other callings' starts as ordinary nodes; anchors are the calling's own start and realm roots. | none | S | P1 |
| H10 | Notable mechanics beyond modifiers: relays (one school's skill prepares the next hit of another), distance bands, cast-scope scoping | Partial: modifiers only | Relays are procs (D19) granting a consumable buff (E11); distance bands are victim tags (F5); cast scopes come from A1. | D19, E11, F5 | S | P1 |
| H11 | Tree content: a large star of specializations, crossroads and routes with an in-game editor | Partial: a small constellation | Authoring in our themes (Q17) plus a passives lint command in the tools CLI (orphans, unreachable nodes, route costs); a visual editor is P2. | H2 | L | P1 |

### 2.9 I. Items

| ID | Prototype | Ours | Proposal | Deps | Size | Pri |
|---|---|---|---|---|---|---|
| I1 | Bases, tiers from item level, defense budgets per slot, implicits, names by tier | Present (`Definitions/Items.cs`, `Items/ItemGenerator.cs`, `Items/ItemResolver.cs`) | Keep. | none | - | - |
| I2 | Rarity contracts: commons roll the widest base window and only commons can be superior; magic items may roll exquisite magic-only tiers and may overroll above their level gate; rares weight their counts and bias tiers by level | Partial: base roll windows, magic-only tiers, count weights and tier bias; superior and overroll missing | Rarity fields `superior: {chance, window}` and `overroll: {chance, max_steps, step_decay}`. | none | S | P1 |
| I3 | Affix lines: a shared roll across lines; local lines; links, gauges, conditions, tags | Partial: all but the shared roll | Mod line field `shared_roll`. | none | S | P1 |
| I4 | Affix pool law (open or explicit per base), rarity floors, affix caps per base | Partial: `affix_cap` | Base fields `affix_pool` and `min_rarity`. | none | S | P1 |
| I5 | Uniques: lines scale with tier; rolled choice families; a copy limit per container | Partial: tier scaling | Unique fields `choices` and `max_per_container`. | none | S | P1 |
| I6 | Grid inventory: a tetris bag with footprints, positions, first fit, swaps, masks and expansions; drag and drop; quick actions; locks | Partial: a list of slots, with base `width` and `height` unused (`Components/Play.cs Inventory`) | A board with `width`, `height` and an optional mask; item positions; operations place, move, swap and first fit; rules `inventory.bag`; `MoveItemIntent(uid, container, x, y)`. | none | M | P0 |
| I7 | Gear sockets holding vestige stones; chance and count by rarity; caps per category; bench chiseling | Missing | `ItemInstance.sockets` holding socketable item ids; item class `socketable` with lines; caps by slot in rules `inventory.sockets`; a bench procedure adds sockets. | I6 | M | P1 |
| I8 | Gems as items: skill and support gems are bag items that never stack; skill rarity sets the socket count; support levels and rolled payloads; learned skills reside in the rack and supports in their host; a better copy swaps in place | Missing: weaves are composed freely from content | Per Q1: glyph stones (item class `glyph`, payload `{glyph, level, rows}`) socket into weave slots; forms are learned from form items whose rarity sets the slot's socket count; learning a better copy swaps it in. Drop weights and level floors live on forms and glyphs. | Q1, I6, D6 | L | P0 |
| I9 | Memories: a stacking gacha item whose units record the dropper and a sealed seed; recall picks a unit; the dropper's kit leans the roll; a banner lane | Missing | Item class `memory` with `units: [{source, seed}]`; recall is a crafting procedure that rolls from the source pattern's weave forms and glyphs with the unit's seed. | I8 | M | P2 |
| I10 | Stash: paged grid storage with page purchases, search and a move planner; personal and account owners (`stash.ts`) | Missing | Content kind `stashes` with `{board, pages, page_cost, accepts, owner}`, an interactable and a store per owner. | I6 | M | P1 |
| I11 | Containers and the reliquary: secondary boards whose seated items are active; relic categories from one cell to two by two; a mask; an unlock ladder; account ownership; an experience penalty per occupied cell; account power scaling; swaps need calm (`containers.ts`, `accountReliquary.ts`) | Missing | Content kind `containers` with `{accepts, owner, active, board: [{unlock, cells}], modifiers}`; the equipment service folds seated items under source `container:<id>`; the penalty is a modifier with gauge `container.<id>.cells`. | I6 | L | P1 |
| I12 | Station services: merge duplicates into a higher rarity, reset skill trees, refund passives, level skills with essence | Missing | A `font` interactable whose services are crafting procedures (merge, reset, refund, level) priced by rule expressions. | I8, H8 | M | P1 |
| I13 | Salvage into rarity essences that level gems and buy stock | Missing | A `salvage` procedure and essence currencies. | none | M | P2 |
| I14 | Attribute and level requirements | Present: `requirements` | Keep. | none | - | - |

### 2.10 J. UI surfaces

| ID | Prototype | Ours | Proposal | Deps | Size | Pri |
|---|---|---|---|---|---|---|
| J1 | Inventory: tetris grid, equipment doll, drag and drop with click-lift carry, right-click quick actions, locks, sell and break modes, refresh that keeps unchanged views (`docs/ui/inventory.md`) | Missing: the character sheet lists gear and bag as text (`src/HollowWake.Client/UI/Hud.cs`) | A Godot inventory panel drawing the board from item info with positions and footprints; drags send a move message; equipment slots come from content `slots`. | I6 | L | P0 |
| J2 | Skill rack: every seat drawn, learned means seated, supports as socket pips, drag a support onto a pip, drag seats to reorder, misfits greyed with the reason, converted and meta faces (`docs/design/skill-items.md`) | Missing: the HUD weave bar shows name, cooldown and costs | A weave rack panel fed by an extended weave slot message (glyph ids, levels, dormant reasons, use pips, cast mode, meta label); it sends set-weave, socket-glyph and swap-slot messages. | I8, D5 | L | P0 |
| J3 | Previews: tooltips with exact numbers from the same fold (damage bands per type, cast time, cooldown, cost, area, counts) (`skillPreview.ts`) | Missing | The server computes previews from `CompiledWeave.Attr` and effect introspection on request, never client-side math. | WP1 introspection | M | P0 |
| J4 | Support compatibility: a support's card lists the equipped skills with fit, misfit, sockets full or no sockets (`docs/ui/support-tooltips.md`) | Missing | The server answers with `TryCompile` results per seated weave alongside the preview. | D5 | S | P0 |
| J5 | Passive tree: pan and zoom, realm tabs, a searchable choice popup, refund mode, node tooltips | Missing | A passive tree view reading passive content locally (the client loads the same content) plus a passive state message (allocated nodes, picks, points per currency, open realms). | H2, H4, H8 | L | P0 |
| J6 | Per-skill tree pages | Missing | With D23. | D23 | M | P1 |
| J7 | Container boards and the stash screen with search | Missing | The board widget from J1 reused. | I10, I11 | M | P1 |
| J8 | A workspace of tabbed pages with primacy and departure rules (`docs/ui/folio.md`, `docs/ui/inventory-pages.md`) | Missing | A simple tab container around inventory, rack, passives and containers; the full folio rules are P2. | J1 | S | P2 |
| J9 | HUD combat state: cast and channel bars, charge-up fill, overcharge stages, use and charge pips, brim bars, cooldown sweeps, meta buttons, toggle states, status icons | Partial: cast progress in name labels and cooldowns on the weave bar | Fields on the weave slot and self-state messages; one HUD widget per field. | A rows | M | P0 |
| J10 | Character sheet with categorized stats and one-line descriptions from data | Partial | Sheet layout as content (categories and order in rules); descriptions from attribute definitions. | none | S | P1 |

### 2.11 Sketches

A glyph with a level curve, a hook, a patch and a trigger. Hooks append into every node that exposes the hook; patches edit params in glyph order with an explicit mode.

```yaml
kind: glyphs
entries:
  - id: forking
    requires_mechanisms: [projectile]
    modifiers:
      - { attribute: projectile.forks, form: flat, value: 1 }
    per_level:
      - { attribute: damage, form: increased, value: 0.06 }

  - id: returning
    requires_mechanisms: ["flight:spends"]
    patches:
      - { type: projectile, set: { return: origin }, mode: first }

  - id: parting_gift
    requires_mechanisms: [projectile]
    hooks:
      on_end:
        - { type: cast, weave: ember_blast, at: point, damage_scale: 0.8 }

  - id: scattered_cascade
    requires_mechanisms: [area]
    patches:
      - { type: area, where: root, wrap: { type: cascade, dir: random, interval: 0.2 }, mode: first }

  - id: cast_on_crit
    excludes: [channel, guard, aura, movement]
    trigger: { on: crit, chance: 0.7, icd: 0.15 }
    per_level:
      - { attribute: trigger.chance, form: flat, value: 0.06 }
```

A channel form with ramps and a release. Ramps become cast-local modifiers each pulse; `cast.held` is readable in expressions.

```yaml
kind: forms
entries:
  - id: torrent
    targeting: direction
    cast_mode: channel
    costs: { mana: 3 }
    channel:
      interval: 0.45
      windup: 0.45
      move: 0.7
      max_hold: 6
      cooldown_on_end: true
      ramps:
        - { attribute: damage, form: more, per: 0.16, max: 1.1, curve: linear }
        - { attribute: move.speed, form: more, per: -0.24, min: -1, on: caster }
      release:
        min_hold: 0.5
        effects:
          - type: area
            shape: sector
            radius: "weave.area.radius * (1 + cast.held * 0.2)"
            effects:
              - { type: damage, amount: "weave.base_damage * spell.power", threads: $threads }
    effects:
      - type: area
        shape: cone
        effects:
          - { type: damage, amount: "weave.base_damage * spell.power * 0.4", threads: $threads }
```

A projectile with trajectory axes, an in-flight emitter and a completion sequel. Axis values are attribute bases that glyphs can raise from nothing or dampen.

```yaml
  - id: frost_orb
    targeting: direction
    attributes: { projectile.speed: 4, projectile.spin: 6, range: 12, base_damage: 9 }
    mechanisms: [projectile, spell, hit]
    tags: [spell, projectile]
    effects:
      - type: projectile
        shape: circle
        duration: 3
        on_tick:
          interval: 0.25
          effects:
            - { type: cast, weave: tide_shard, at: point, aim: rotating }
        on_end:
          - { type: area, radius: 2.5, effects: [ { type: damage, amount: "weave.base_damage * spell.power", threads: $threads } ] }
```

A summon with a pool, inherited owner stats, minion behavior and a contract.

```yaml
      - type: summon
        pool:
          - { pattern: remnant_shade, weight: 3 }
          - { pattern: remnant_archer, weight: 1 }
        count: "weave.summon.count"
        max_active: "weave.summon.max"
        group: remnants
        pool_group: undead
        place: { at: point, range: 8, scatter: 1.5 }
        behavior: minion_melee
        inherit: true
        contract: { reserve: { mana: "mana.max * 0.1" }, respawn: 8, toggle: true }
```

Status additions: a bank, a pop on reapplication, a consumable next-hit buff.

```yaml
kind: statuses
entries:
  - id: scalding
    tags: [ailment, ember, dot]
    stack_policy: bank
    bank_cap: 4
    tick:
      interval: 0.5
      effects:
        - { type: damage, amount: amount, threads: [ember], tags: [dot], can_crit: false }

  - id: hemorrhage
    tags: [ailment, stone, dot]
    on_reapply:
      - { type: damage, amount: "remaining_damage * 0.5", threads: [stone], tags: [dot, pop] }

  - id: honed
    beneficial: true
    max_stacks: 2
    consume_on: { event: hit, tags: [melee] }
    modifiers:
      - { attribute: damage, form: more, value: 0.5, scope: [melee] }
```

A proc. The chance is an attribute any modifier source can grant; the payload is our effect graph.

```yaml
kind: procs
entries:
  - id: brutal_strike
    on: hit
    tags: [melee]
    chance: proc.brutal_strike
    icd: 0.5
    effects:
      - { type: damage, amount: "last_damage * 0.5", threads: $threads }
```

A choice node, its group, and a realm.

```yaml
kind: passive_choices
entries:
  - id: doctrine_of_war
    name: Doctrine of War
    pick: 1
    unique: character
    deal: each
    options:
      - { id: iron, name: Iron Doctrine, modifiers: [ { attribute: armor, form: increased, value: 0.2 } ] }
      - { id: splitting_thought, name: Splitting Thought, glyph: { id: forking, level: 1 } }
```

```yaml
kind: passive_realms
entries:
  - { id: devotion, adjacency: tree, roots: [devotion_root], currency: devotion, unlock: 'has("quest.devotion_shrine")' }
```

A container. The board is the union of the rungs the owner has unlocked.

```yaml
kind: containers
entries:
  - id: reliquary
    accepts: [relic]
    owner: account
    active: true
    board:
      - { unlock: "account.reliquary >= 1", cells: ["..X..", ".XXX.", "XXXXX", ".XXX.", "..X.."] }
    modifiers:
      - { attribute: xp.gain, form: increased, value: -0.02, gauge: container.reliquary.cells }
```

## 3. Implementation plan

### 3.1 Order and rules

One foundation package (WP1) lands first and alone. It freezes the shared seams: the compiler's levels, hooks, patches, probes and dormant glyphs; the `cast` and `delay` effects; the trigger registry; the hit stage registry and hit scope; attribute families; slot and cast state; the schema for every P0 field on forms, glyphs and statuses; and the file splits listed below. Then six packages run in parallel on disjoint files: WP2 to WP6 in the simulation and on the wire, WP7 on the clients against the shapes WP6 freezes first. Each package does its P0 rows first and its P1 rows after its P0 acceptance passes.

Rules for every package:

- Never edit a file another package owns. Cross-package additions go through registries frozen by WP1: effect handler families, mechanism probes, trigger kinds, hit stages, intent handlers, AI actions, content kinds and validators, attribute families, expression functions.
- New content goes in package-owned files. Rule groups use new ids (`casting`, `triggers`, `delivery`, `minions`, `procs`, `inventory`, `passives`) so no two packages write the same rules entry.
- New events go in a package-owned file under `Sim/Events/`; no package edits `Sim/Events.cs` after WP1.
- New attributes and attribute families go in package-owned files (`attributes-<area>.yaml`, `attribute-families-<area>.yaml`); both kinds merge across files.
- New P1 schema goes in the package's own partial definition file (WP1 makes `FormDef`, `GlyphDef` and `StatusDef` partial records).
- New tests go in package-owned test files. The existing test files are frozen after WP1; a package whose change legitimately alters an existing expectation asks the coordinator.
- When a package needs a change in a file it does not own, the coordinator sequences it with the owner.
- The verification gates in `CLAUDE.md` apply to every package; packages touching hot paths also run `bench` and stay within `docs/design/performance.md`.

### 3.2 The effect handler split and its owners

`Effects/BuiltinEffects.cs` has already been split into one file per handler family under `Effects/Handlers/`, with shared targeting helpers in `EffectHelpers.cs` and the projectile and field components in `Effects/ProjectileComponents.cs`. WP1 changes the hub so each family file exposes a `Register(EffectRegistry)` method called from `BuiltinEffects.RegisterAll`, creates the new family files below (empty or with their first handlers), moves `TauntHandler` into the AI family, and then freezes the hub. Owners after WP1:

| File | Handlers | Owner |
|---|---|---|
| `Effects/Handlers/DamageHandlers.cs` | damage, heal, resource | WP5 |
| `Effects/Handlers/StatusHandlers.cs` | apply_status, remove_status, modify | WP5 |
| `Effects/Handlers/ChargeHandlers.cs` (new) | charge | WP5 |
| `Effects/Handlers/StatusTransferHandlers.cs` (new) | consume_status, spread_status, move_status, rupture | WP5 |
| `Effects/Handlers/DeliveryHandlers.cs` | area, projectile, chain, field, aura | WP3 |
| `Effects/Handlers/PlacementHandlers.cs` (new) | scatter, cascade, tether, detonate | WP3 |
| `Effects/Handlers/MovementHandlers.cs` | move (teleport, dash, leap, blink, knockback, pull) | WP3 |
| `Effects/Handlers/EffectHelpers.cs` | shared filters, centers, pick modes | WP3 |
| `Effects/ProjectileComponents.cs` | Projectile, Field and new delivery components | WP3 |
| `Effects/Handlers/SummonHandlers.cs` | summon, spawn, deploy | WP4 |
| `Effects/Handlers/MinionHandlers.cs` (new) | recall_minions, command_minions, minion_cast, consume_minion | WP4 |
| `Effects/Handlers/AiHandlers.cs` (new) | taunt | WP4 |
| `Effects/Handlers/FlowHandlers.cs` | condition, random, repeat, sequence | WP2 |
| `Effects/Handlers/CastHandlers.cs` (new) | cast, delay, fuse, cooldown, restore_uses | WP2 (WP1 writes cast and delay) |
| `Effects/Handlers/UtilityHandlers.cs` | event, interrupt, grant_item, grant_xp, log | WP2 |
| `Effects/BuiltinEffects.cs` | registration hub | frozen after WP1 |

### 3.3 Work packages

#### WP1 Foundations (lands first, one agent)

Goal: every shared seam exists, behavior is unchanged, and the other packages can work without touching each other's files.

Rows: A1, C39, the `delay` part of C49, D3, D4, D5, D6, D7, D8, F5, F6, F9, G0; the P0 schema for the A, B, D and E rows; the trigger registry used by D12, D19 and E11.

Owns, then hands over as noted:

- `Weaves/CompiledWeave.cs` (glyph levels, level bonus, folding a glyph reference's rolled rows, patches, hooks, probes, dormant glyphs, cast-scope tags), new `Weaves/Mechanisms.cs`, new `Weaves/EffectPatches.cs`, new probe files `Weaves/Probes/CastingProbes.cs`, `DeliveryProbes.cs`, `MinionProbes.cs`, `CombatProbes.cs` (each with an empty `Register`); afterwards the compiler, mechanisms and patches go to WP2 and each probe file to its namesake package.
- `Definitions/Combat.cs` and `Definitions/Core.cs` (partial records; glyph references with level and rolled rows; every P0 field as schema with validation); new partial files `Definitions/Forms.Casting.cs` and `Definitions/Glyphs.Casting.cs` (WP2), `Definitions/Forms.Delivery.cs` (WP3), `Definitions/Forms.Summon.cs` and `Definitions/Glyphs.Minion.cs` (WP4), `Definitions/Statuses.cs`, `Definitions/Charges.cs`, `Definitions/Procs.cs` (WP5), `Definitions/Progression.cs`, `Definitions/Containers.cs`, `Definitions/Glyphs.Items.cs` (WP6).
- `Content/ContentKinds.cs` and `Content/BuiltinKinds.cs` (several validators per kind; the `attribute_families` kind); new `Content/Kinds/CastingKinds.cs` (WP2), `DeliveryKinds.cs` (WP3), `MinionKinds.cs` (WP4), `CombatKinds.cs` (WP5), `ProgressionKinds.cs` (WP6), each called from `BuiltinKinds.RegisterAll`.
- `Effects/BuiltinEffects.cs` (frozen afterwards); `Effects/EffectContext.cs` (hook metadata, compile-time access to the weave compiler, cast vars; afterwards WP2); new `Effects/Handlers/CastHandlers.cs` with `cast` and `delay` (afterwards WP2); the empty new family files of 3.2 (afterwards their owners there).
- `Combat/DamagePipeline.cs` refactored into stages with today's order and output, new `Combat/HitScope.cs`, new `Combat/Stages/*.cs` for the existing stages (afterwards WP5, except a new empty `Combat/Stages/GuardStage.cs` for WP2).
- `Attributes/AttributeCatalog.cs`, `Attributes/AttributeSet.cs`, `Attributes/Modifier.cs` (families, hit-scope cache safety; afterwards WP5).
- `Systems/CombatSystems.cs` split into `Systems/CastingSystem.cs` (WP2), `ProjectileSystem.cs` and `FieldSystem.cs` (WP3), `StatusSystem.cs` (WP5), `LifetimeSystem.cs` and `SpawnerSystem.cs` (WP4); empty shells `Systems/UpkeepSystem.cs` (WP2), `DisplacementSystem.cs` and `TetherSystem.cs` (WP3), `MinionSystem.cs` (WP4).
- `Systems/IntentSystem.cs` turned into a dispatch over an intent handler registry, with handlers moved to `Systems/Intents/WeaveIntentHandlers.cs` (WP2), `ItemIntentHandlers.cs`, `ProgressionIntentHandlers.cs` and `WorldIntentHandlers.cs` (WP6), `MovementIntentHandlers.cs` (frozen); `Intents/Intents.cs` split into `Intents/MovementIntents.cs` (frozen), `WeaveIntents.cs` (WP2), `ItemIntents.cs`, `ProgressionIntents.cs` and `WorldIntents.cs` (WP6).
- `Components/Play.cs` split into `Components/Weaves.cs` (weave slot with a state bag, cast state with held time, fill, stage, move factor and cast-local modifiers; WP2), `Components/Inventory.cs` (WP6), `Components/Minions.cs` and `Components/Ai.cs` (WP4), with the quest and player components left in `Components/Play.cs` (WP6).
- `Components/Body.cs` (impulse and displacement fields on `Motion`; afterwards WP3) and `Movement/MovementSystem.cs` (reads `CastState.MoveFactor` instead of the `mobile` tag; afterwards WP3).
- New `Sim/Triggers.cs` (trigger kinds registered by name and fed by events; built-in kinds hit, crit, kill, hurt, block, evade, status_applied, cast, heal, death_near, minion_died, interval), `Sim/Events.cs` (a `Depth` on damage and status events), `Sim/ZoneBuilder.cs` (registers every new service and system shell), `Expressions/ExprFunctions.cs` (`randi`, `curve` with a curve registry).
- Shells `Combat/ChargeService.cs` and `Components/Charges.cs` with count, gain and spend, so WP2 can pay charge costs while WP5 builds the charge mechanics (afterwards WP5); `Components/PassiveState.cs` as an empty component (afterwards WP6).
- `src/HollowWake.Tools/Program.cs` (a `weave` command printing a compiled weave's tags, mechanisms, dormant glyphs and preview numbers; afterwards WP6).
- Content: `content/core/rules.yaml` (stage order, victim tags, stage skips defaulting to today's behavior; afterwards WP5), new `content/core/attribute-families.yaml` (afterwards WP5).
- Docs: `docs/design/weaves-and-effects.md` (compiler and new effects; afterwards WP2), `docs/design/attributes-and-modifiers.md` and `docs/design/combat.md` (families, hit scope, stages; afterwards WP5), `CLAUDE.md` (the registries in the repository map).
- Files in this list without an "afterwards" owner are frozen once WP1 lands; later changes to them go through the coordinator.

Tests: new `tests/HollowWake.Core.Tests/WeaveCompilerTests.cs` (glyph levels and level bonus; each patch mode; hooks reaching a projectile's on-hit list; probes self-lifting through `cooldown.added` and `affliction:<status>`; dormant reasons; cast-scope tags), `HitStageTests.cs` (default stage order reproduces a recorded seeded duel; victim tags from rules; the hit scope reads a target's stacks without breaking the attribute cache), `CastEffectTests.cs` (`cast` runs a preset at a point with the host's level and a deeper depth; `delay` fires deterministically). Existing tests stay unchanged and green.

Acceptance: build clean; all tests pass; `validate` reports no errors and no new warnings; a seeded `fight` and a `load` produce the same event trace as before the package (record both before starting); `bench` stays within the attribute and zone budgets; every shell file of this section exists and is registered.

#### WP2 Cast modes and the weave runtime

Goal: cast times and every cast mode, glyph triggers, repeats and the slot behaviors.

Rows: P0 first: A2, A3, A4, A5, A6, A7, A9, A15, D12, D13. Then A8, A10, A11, A12, A14, A16, A17, A18, A19, A20, A21, A22, A23, A24, C19, C34, D10, D11, D14, D16, D17, D18, D20, D23, D25, and the toggle half of B26.

Owns: `Weaves/WeaveRuntime.cs`, `Weaves/CompiledWeave.cs`, `Weaves/Mechanisms.cs`, `Weaves/EffectPatches.cs`, `Weaves/Probes/CastingProbes.cs`, new `Weaves/Triggers.cs`, `Weaves/UseCharges.cs`, `Weaves/SlotBehaviors.cs`, `Weaves/Toggles.cs`; `Systems/CastingSystem.cs`, `Systems/UpkeepSystem.cs`, `Systems/Intents/WeaveIntentHandlers.cs`; `Intents/WeaveIntents.cs`; `Components/Weaves.cs`; `Combat/Stages/GuardStage.cs` and a new `Components/Guard.cs`; `Effects/EffectContext.cs`, `Effects/Handlers/FlowHandlers.cs`, `CastHandlers.cs`, `UtilityHandlers.cs`; `Definitions/Forms.Casting.cs`, `Definitions/Glyphs.Casting.cs`; `Content/Kinds/CastingKinds.cs`; new `Sim/Events/CastingEvents.cs`.

Content: new `content/core/combat/forms-casting.yaml`, `glyphs-casting.yaml`, `weaves-casting.yaml`, `attributes-casting.yaml`, `rules-casting.yaml` (rule ids `casting` and `triggers`).

Docs: `docs/design/weaves-and-effects.md`.

Tests: new `tests/HollowWake.Core.Tests/CastModeTests.cs` (moving while casting; cooldown timing per form; channel windup, ramps, release and maximum hold; charge-up scaling; overcharge stages; use charges with trickle, magazine and reload; gates; guard absorption and bash) and `TriggerTests.cs` (round-robin choice, internal cooldown, depth limit, the cast-time gate and its permit, costs paid; repeats with scale and retarget; metas in slot order; converts).

Acceptance: the common gates; a seeded `fight` between a channeling caster and a caster with a triggered glyph shows channel beats, a release and trigger casts in round-robin order, identical across two runs; `bench` zone tick within budget with casting players.

#### WP3 Deliveries and projectiles

Goal: projectile parity, placements, area shapes, movement deliveries, tethers and occlusion.

Rows: P0 first: B1, B2, B3, B4, B5, B6, B7, B8, B10, B11, B12, B13, B14, B15, B30, C4, C5, and the delivery channels of D9. Then B9, B23, B24, B25, the aura field of B26, B28, B29, B31, B32, C11.

Owns: `Effects/Handlers/DeliveryHandlers.cs`, `PlacementHandlers.cs`, `MovementHandlers.cs`, `EffectHelpers.cs`; `Effects/ProjectileComponents.cs`; new `Effects/Shapes.cs`; `Systems/ProjectileSystem.cs`, `FieldSystem.cs`, `DisplacementSystem.cs`, `TetherSystem.cs`; `Movement/MovementSystem.cs`; `Components/Body.cs`; new `World/LineOfSight.cs`; `Weaves/Probes/DeliveryProbes.cs`; `Definitions/Forms.Delivery.cs`; `Content/Kinds/DeliveryKinds.cs`; new `Sim/Events/DeliveryEvents.cs`.

Content: `content/core/combat/forms.yaml` and `content/core/combat/glyphs.yaml` (the existing files), new `forms-delivery.yaml`, `glyphs-delivery.yaml`, `attributes-delivery.yaml`, `rules-delivery.yaml` (rule id `delivery`).

Docs: new `docs/design/deliveries.md`.

Tests: new `tests/HollowWake.Core.Tests/DeliveryTests.cs` (shapes hit where drawn; firing styles; homing and erratic flights deterministic for a seed; forks inherit motion; returns re-hit; rehit lockouts; projectile chaining; emitters and sequels through `cast`; storms and cascades place deterministically; field delay, hit once, re-arm on exit and domains applied and removed) and `MovementDeliveryTests.cs` (dash corridor hits; leap untargetable then landing; knockback impulses and wall arrests; line of sight blocking).

Acceptance: the common gates; `load` smoke; a seeded `fight` with forking, returning and homing weaves shows split children and return hits; `bench` zone tick and allocation within budget (add a `projectiles` group if a new hot path appears).

#### WP4 Summons, minions and constructs

Goal: summon parity, minion inheritance and AI, commands, contracts, corpses and constructs.

Rows: P0 first: B17, B18, B19, C12, C16, C28. Then B16, B20, B21, B22, the corpse and minion kinds of B27, C24, C25, C26, C27, D15, D24, D26.

Owns: `Effects/Handlers/SummonHandlers.cs`, `MinionHandlers.cs`, `AiHandlers.cs`; `Systems/LifetimeSystem.cs`, `SpawnerSystem.cs`, `MinionSystem.cs`; `Components/Minions.cs`, `Components/Ai.cs`; `Ai/BehaviorSystem.cs`; `Patterns/PatternFactory.cs`; `Combat/DeathService.cs`, `Combat/Relations.cs`; `Weaves/Probes/MinionProbes.cs`; `Definitions/Forms.Summon.cs`, `Definitions/Glyphs.Minion.cs`; `Content/Kinds/MinionKinds.cs`; new `Sim/Events/MinionEvents.cs`.

Content: `content/core/combat/weaves.yaml`, `content/core/entities/patterns.yaml`, `content/core/entities/behaviors.yaml` (the existing files), new `content/core/combat/forms-summon.yaml`, `glyphs-minion.yaml`, `attributes-minion.yaml`, `rules-minions.yaml` (rule id `minions`).

Docs: new `docs/design/minions.md`; `docs/design/ai.md`.

Tests: new `tests/HollowWake.Core.Tests/MinionTests.cs` (pools and pool groups; inheritance of the owner's `minion.*` attributes with the summon's tags; follow, assist and leash; commands and `minion_cast` scoped to the host; contracts reserving mana and respawning; corpse raising; decay) and `ConstructTests.cs` (deploy caps; constructs casting with the owner's stats and credit; trap proximity triggers; dome interception).

Acceptance: the common gates; a seeded scenario where a summoner's minions inherit damage, follow, assist, obey a command and respawn under a contract runs identically twice; `bench` behavior system within budget in a minion-heavy zone.

#### WP5 Statuses, charges, procs and the damage pipeline

Goal: status depth, the proc and charge economy, defenses and hit sustain.

Rows: P0 first: C1, C2, C3, C13, C14, E7, E11, G1, G2. Then C7, C8, C9, C17, C18, C21, C22, C30, C31, C32, C40, C42, C43, C44, C45, C46, D19, the remaining E rows, F4, F7, F8, and G3 to G18.

Owns: `StatusEffects/StatusService.cs`; `Systems/StatusSystem.cs`; `Effects/Handlers/DamageHandlers.cs`, `StatusHandlers.cs`, `ChargeHandlers.cs`, `StatusTransferHandlers.cs`; `Combat/DamagePipeline.cs`, `Combat/HitScope.cs`, `Combat/Stages/*.cs` except `GuardStage.cs`; `Combat/ChargeService.cs` and `Components/Charges.cs` (shells from WP1); new `Sim/ProcService.cs`; `Components/Stats.cs`; `Attributes/*`; `Weaves/Probes/CombatProbes.cs`; `Definitions/Statuses.cs`, `Definitions/Charges.cs`, `Definitions/Procs.cs`; `Content/Kinds/CombatKinds.cs`; new `Sim/Events/CombatEvents.cs`.

Content: `content/core/combat/statuses.yaml`, `content/core/attributes.yaml`, `content/core/attribute-families.yaml`, `content/core/resources.yaml`, `content/core/threads.yaml`, `content/core/rules.yaml` (the existing files), new `content/core/combat/charges.yaml`, `procs.yaml`, `glyphs-affliction.yaml`, `statuses-extra.yaml`.

Docs: `docs/design/combat.md`, `docs/design/attributes-and-modifiers.md`.

Tests: new `tests/HollowWake.Core.Tests/StatusTests.cs` (forbidden tags stop casts; refreshed temporary modifiers last their full time; banks and ruptures; pops; consumable stacks; next-hit riders; independent stack clocks; apply-chance families), `ProcTests.cs` (internal cooldowns, per-minute rates and depth falloff deterministic for a seed), `ChargeTests.cs` (gain, decay, drain, spend), `DefenseTests.cs` (added damage on a Thread the hit lacked; leech; crit avoidance; guard-value block; shield recharge; poise break; stage skips for damage over time; defender-side conversion).

Acceptance: the common gates; a seeded `fight` trace shows leech, shield recharge, a poise break, a rupture and proc depth limits; `bench` attribute apply and zone tick within budget, with the hit scope pooled so per-hit allocation does not grow.

#### WP6 Progression, items, dialogue and the wire

Goal: passive parity, the item model for weaves, the grid inventory, stash and containers, dialogue and vendors in the simulation, and every message and intent the interface reads and sends. The interface itself is WP7, so WP6 lands its message shapes first: within its first commits it freezes, as records in `HollowWake.Protocol`, the self state, weave slot, inventory, container, passive, page, dialogue and vendor states and the intents that `docs/design/ui.md` lists, served with placeholder data where the simulation is not ready, so WP7 builds against real shapes while WP6 fills them in.

Rows: P0 first: H2, H3, H8, I6, I8, J3, J4, the message wiring for every new intent and for every state in `ui.md`, and dialogue and vendors (new, from `ui.md`: conversations as content with lines, speakers, choices, gates as expressions and effects, run by the server; vendor stock, prices and buy-back from content and rules; intents to choose a line, buy and sell). Then H4, H5, H6, H9, H10, H11, I2, I3, I4, I5, I7, I10, I11, I12, D21, D22, and P2 rows H7, I9, I13 when time allows.

Owns: `Progression/*`; `Definitions/Entities.cs`, `Definitions/Items.cs`, `Definitions/Play.cs`, `Definitions/Progression.cs`, `Definitions/Containers.cs`, `Definitions/Glyphs.Items.cs`, new `Definitions/Dialogue.cs`; `Items/*`, `Loot/*`, `Crafting/*`, `Quests/*`, `Interaction/*`, new `Dialogue/*`; `Characters/CharacterSheet.cs`; `Components/Inventory.cs`, `Components/PassiveState.cs`, `Components/Play.cs`; `Intents/ItemIntents.cs`, `ProgressionIntents.cs`, `WorldIntents.cs`, new `DialogueIntents.cs`; `Systems/Intents/ItemIntentHandlers.cs`, `ProgressionIntentHandlers.cs`, `WorldIntentHandlers.cs`, new `DialogueIntentHandlers.cs`; `Content/Kinds/ProgressionKinds.cs`; new `Sim/Events/ProgressionEvents.cs`; everything under `src/HollowWake.Protocol/`, `src/HollowWake.Server/` and `src/HollowWake.Tools/` (after WP1); and `web/src/net/protocol.ts`, regenerated with every message change.

Content: `content/core/entities/passives.yaml`, `content/core/entities/callings.yaml`, `content/core/items/*.yaml`, `content/core/slots.yaml`, `content/core/play/*.yaml` (the existing files), new `content/core/entities/passive-choices.yaml`, `passive-realms.yaml`, `content/core/items/containers.yaml`, `stashes.yaml`, new `content/core/dialogue/*.yaml` and `content/core/vendors.yaml`, `content/core/rules-inventory.yaml` (rule ids `inventory`, `passives`, `dialogue`).

Docs: `docs/design/items-and-loot.md`, `docs/design/networking-and-persistence.md`, new `docs/design/passives.md`, new `docs/design/dialogue.md`, and the message section of `docs/design/ui.md`.

Tests: new `tests/HollowWake.Core.Tests/PassiveTests.cs` (pick limits; unique per character; each, sole and first deals; realm adjacency and currencies; refund connectivity and pools), `InventoryTests.cs` (placement, swap and first fit; glyph stones socketing into weave slots; dormant glyphs reported; learning a better form copy), `ContainerTests.cs` (seated relics fold, unseated do not; masks and unlock rungs; stash pages), `DialogueTests.cs` (gates, choices, effects, a line that opens a vendor, buy and sell at server prices); additions to `tests/HollowWake.Server.Tests/LoopbackTests.cs` (move item, socket glyph, allocate a choice, refund, weave preview, a conversation, a purchase) and to the protocol contract tests for every new message.

Acceptance: the common gates; loopback tests pass; run the server and `probe`; the WebSocket tests pass and the protocol module is current; every state and intent named in `ui.md` exists on the wire with a test that serializes it.

#### WP7 The interface

Goal: the interface of `docs/design/ui.md` on the Godot client first and the browser client second: the cluster with its orbs, overlays and rings, the weave bar and the menu tray; the core pane with the bag board, the equipment doll, the wallet and the ribbon with the weaves, passives, reliquary and character pages; the world panes (dialogue, vendor, quest offer, station, stash) opening beside the core pane under the book rule; and the theme as content.

Rows: J1, J2, J5, J9 first, against the shapes WP6 freezes (where a message is still served with placeholder data the surface is built and the data arrives when WP6 fills it), then J6, J7, J8, J10, the browser client's parity, and the smoke extensions `ui.md` asks for.

Owns: everything under `src/HollowWake.Client/` and `web/src/` except `web/src/net/protocol.ts`; new `content/core/ui/*.yaml` (theme, pages, cluster order) with its validator in a new `Content/Kinds/InterfaceKinds.cs`, the only core file it owns; `docs/design/client.md`, `docs/design/web-client.md` and the look sections of `docs/design/ui.md`.

Dependency rule: WP7 reads messages and never core types. When it needs a field a message lacks it asks the coordinator, who sequences the change with WP6; WP7 never edits `HollowWake.Protocol`.

Tests: the Godot smoke extended as `ui.md` describes, with a screenshot of every page and pane; the web smoke likewise; unit tests for the board widget (placement, footprints, drag targets) and for the book rule (binding, fronting, promotion, departure) in each client.

Acceptance: the client assembly and the web client build; both smokes pass against a local server with every page and pane opened; the coordinator reviews the screenshots; no surface folds a number the server already sent (review).

#### Deferred rows

Rows marked present need no package. The P2 rows not named above (A13, C20, C23, C29, C33, C35, C36, C37, C38, C47, C48) wait for a later phase; each joins the package that owns the files it touches once its fabric (aim streaming, companions, grab, possession, clouds, lightwells, fog, orbs, ledgers) is designed.

### 3.4 Contracts WP1 freezes

| Seam | Shape |
|---|---|
| Effect families | each `Effects/Handlers/*Handlers.cs` exposes `Register(EffectRegistry)`; handlers declare their hook param names |
| Hooks and patches | glyph `hooks` and `patches` applied by the compiler in glyph order; patch modes first, default, append, wrap |
| Mechanism probes | `MechanismRegistry.Register(name, probe)` where a probe reads the compiled weave and an optional parameter |
| Triggers | `TriggerHub.Subscribe(kind, owner, handler)` and `TriggerHub.Raise(owner, kind, event)`; events carry depth, tags, source, target, amount and crit |
| Hit stages | `IHitStage` registered by name; order and skips from rules |
| Hit scope | a pooled scope with `target.*` and `distance` for uncached folds; victim tags from rule expressions |
| Slot and cast state | `WeaveSlot.State` for banks, latches and cursors; `CastState` with held time, fill, stage, move factor and cast-local modifiers |
| Intents | `IntentHandlers.Register<T>(handler)` |
| Content kinds | `Content/Kinds/*Kinds.cs` per package; several validators per kind |
| Composition | `ZoneBuilder` registers every service and system shell named in this plan |
| Events | package-owned files under `Sim/Events/` |

## 4. Open questions

**Q1. Who owns a weave: free composition or items?** The prototype's skills and supports are items: a skill gem's rarity sets its sockets, supports level and roll per copy, learned skills sit in a rack, duplicates merge upward at a station, and drops lean toward the dropper's kit. Ours lets any player compose any form, Threads and glyphs, which keeps composition but removes the loot chase and the skill UI the owner wants. *Recommendation:* a hybrid that keeps composition. Forms are learned from form items whose rarity sets the weave slot's socket count, and a better copy swaps in place; Threads are attuned through the calling, passives and holdings; glyphs become stones with levels and rolled rows that socket into weave slots and occupy bag cells when loose; the weave book's slots are the rack. Drops lean toward the dropper's preset weaves. The server validates ownership in `SetWeaveIntent`; free composition stays available to monsters, tools and tests.

**Q2. Bespoke fields or composition?** The prototype grows a typed field for nearly every behavior; its support and status definitions carry fields that only one or two entries use. *Recommendation:* port behaviors as compositions of hooks, patches, effects, statuses with hooks and attributes. Add an effect type or a probe only when composition cannot express the behavior, and never port single-use fields one to one.

**Q3. Mechanisms as code predicates or data?** The prototype's mechanism gates are functions over the live instance; ours are declared sets. *Recommendation:* both. Named, optionally parameterized probes in a registry read the compiled weave for structural facts; declared mechanisms cover what probes cannot see. Probes are pure, run at compile time only, and never name weave ids.

**Q4. Misfits: reject or keep dormant?** The prototype keeps misfits socketed and inert so builds can be assembled before their enabler arrives. *Recommendation:* dormant with reasons for player slots, shown greyed in the rack; preset weaves in content must fit, and `validate` warns when one does not.

**Q5. Randomness.** Parts of the prototype roll with the platform's unseeded random generator (count ranges, some damage rolls) and some seeds come from wall-clock time. *Recommendation:* every roll draws from an `Rng` stream derived from the simulation seed: per caster, slot and pulse for casts (as today), per projectile for erratic flight, per owner for procs and triggers, and from the world seed plus a drop counter for memory seeds. Never wall-clock time.

**Q6. Units.** The prototype measures in screen units, each far smaller than one of our world units (a cell); its speeds, ranges and radii cannot be copied. *Recommendation:* convert once in a porting script, deriving the factor from body radii and melee reach, and author our content in world units; no runtime factor.

**Q7. Damage types.** The prototype's damage types are physical, fire, cold, lightning and chaos; ours are the Threads. *Recommendation:* map by role when porting content (physical to Stone, fire to Ember, cold to Tide, lightning to Storm, chaos to Void or Umbra by theme) and keep Threads as the vocabulary; each Thread's ailment carries the ported status behavior.

**Q8. One damage path or two?** Ours promises one path for every hit, including ticks; the prototype sends damage over time down a shorter path that skips evasion, block and armor. *Recommendation:* keep one path and skip stages by hit tag through rules (G14), which gives the prototype's feel without a second pipeline.

**Q9. Cursor-dependent mechanics on an authoritative server.** Guided flights, concentration, aim tracking, salvo re-aim and spread by aim read the live cursor. *Recommendation:* an `AimIntent` accepted only while such a cast is live, rate-limited by rule and clamped by the server; client prediction comes with the movement prediction work on the roadmap.

**Q10. Camera and time mechanics.** The prototype has mechanics bound to the player's view frame (frame bounces, frame locks, a caroming body sealed to the screen) and global time holds (super-art cut-ins that pause the world, time stops). *Recommendation:* reject view-frame mechanics, since players in one zone see different frames; reject global holds in shared zones; allow per-entity time-scale statuses later.

**Q11. Loadout changes in combat.** The prototype forbids unsocketing and unlearning while enemies press or blows were recently exchanged, waived in sanctuaries. *Recommendation:* rule `casting.swap_requires` as an expression (for example no recent hits taken or dealt), waived by a zone tag; re-ordering the rack stays free.

**Q12. Death rules.** The prototype is a roguelite: final death destroys carried relics and essences, and the account reliquary is protected. *Recommendation:* no destruction in an MMO with respawns; the reliquary is an account container that persists, and its experience penalty per occupied cell is a rule that may be off.

**Q13. Attribute set.** The prototype groups a larger attribute set into triads so every class's requirements differ; ours is a small core set. *Recommendation:* keep our core attributes and their lore, adopt per-point modifier declarations and attribute requirements on forms and glyphs, and revisit if requirement diversity proves thin.

**Q14. How weaves and glyphs level.** The prototype levels skills and supports with essence at a station, with level bands granting points for per-skill trees. Our weave level exists but players have no way to raise it. *Recommendation:* store a level per learned form and per glyph stone, raised by a station procedure that costs currency (a sink), with curves in rules; avoid levels per use, which a macro can farm.

**Q15. Positional semantics.** The prototype's "first socketed graft wins" depends on socket order, and the rack's order is meant to matter for future adjacency mechanics. *Recommendation:* glyph order in a `WeaveSpec` is the socket order, shown in the rack; patches in `first` mode respect it; adjacency between rack seats waits for its own design.

**Q16. Enumerations as numeric stats.** The prototype encodes some choices as override stats with numeric codes (a return mode, an area shape). *Recommendation:* patches for choices, attributes only for quantities.

**Q17. Porting the passive star.** The prototype's star is large and themed for its own classes. *Recommendation:* port the mechanisms (choices, realms, vocations, refunds, attribute choices) and author our own constellation in our themes, reusing the prototype's shape of entries and feeders around notables as a pattern rather than copying nodes.

**Q18. Performance.** Richer projectiles, per-hit scopes, victim tags and trigger fan-out land on the hottest paths. *Recommendation:* pooled scopes, no LINQ or closures in per-tick systems, triggers indexed by kind and owner, and `bench` gates on every package that touches these paths, per `docs/design/performance.md`.
