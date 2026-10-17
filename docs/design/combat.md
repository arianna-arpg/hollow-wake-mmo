# Combat

## Threads

Nine Threads are the damage types: Ember, Tide, Storm, Stone, Bloom, Umbra, Lumen, Void, Mind. Each declares its resistance and penetration attributes, its mitigation rule (`armor` for Stone, `resist` for the rest) and the status it tends to leave.

## The damage pipeline

Every hit, including status ticks, ruptures and field pulses, takes one path:

1. **Build** (the attacker's side). The `damage` effect's `amount` is split across its Threads, and its per-Thread `amounts` (`{ ember: expr, stone: expr }`) join them, so a hit can carry mixed bases. The attacker's flat added damage per Thread (`added.{thread}`, scoped by the hit's tags) joins every Thread, including ones the hit lacked, scaled by the effect's `effectiveness` (or the weave's `damage.effectiveness`) times `added.effectiveness`. Each Thread's share runs through the attacker's `damage` fold scoped by the hit's tags plus the Thread (weave modifiers joining), folded in the hit scope, so flat, increased and more apply where their scopes say and may read the struck body. Victim tags join the hit's tags as `vs.*`. Conversions (`convert.{from}.{to}`) move fractions between Threads; `extra.{to}` adds a share as another Thread. Crit rolls `crit.chance` lowered by the victim's `crit.avoid`; a hit whose tags skip the crit step (damage over time) may still crit by the attacker's `crit.dot`.
2. **Stages** (the defender's ladder). The stages registered by name run in the order rule `combat.stages` lists; a stage may end the hit. The built-in ladder:
   - `immunity`: immunity flags refuse; nothing is published.
   - `guard`: a held guard absorbs hits in its arc; bodies without a guard pass.
   - `evade`: hits carrying an evadable tag (`attack` by rule) are evaded by the `combat.hit_chance` expression over accuracy and evasion, rolled per hit, or with rule `combat.evasion: entropy` accumulated on the target (seeded from its own stream) so evasion lands on schedule. An evaded hit is published as evaded and ends.
   - `block`: rolls `block.chance`. By default a block keeps `1 - block.amount` of every Thread; with rule `combat.block: guard` it stops up to the `combat.block_value` expression (the blocker's level and `block.value`), never more than `block.power` of the hit, so heavy blows seep through.
   - `taken_as`: the target's `taken_as.{from}.{to}` takes a fraction of each Thread as another before mitigation.
   - `mitigate`: per Thread, armor reduction by the `combat.armor_reduction` expression (armor, the raw amount, the target's level and the attacker's armor penetration `pen`; the clamp lives in the rule), or resistance capped by the per-Thread maximum and the rule cap, less the attacker's penetration down to the floor.
   - `taken`: the target's `damage.taken` fold scoped by tags and Thread, in the hit scope; what remains above zero is received.
   - `cap`: no hit lands more than the target's `hit.cap` of its maximum life; a capped hit is marked clamped.
   - `stagger`: the target's `stagger.fraction` of a landing hit is banked into the status rule `combat.stagger_status` names, which pays it out over its duration as hits tagged `deferred`.
   - `pools`: the pools before life take the hit (see Pools); the hit is recorded as recent (hurt, hit, crit).
   - `cheat_death`: a hit that leaves no life rolls the target's `cheat_death.chance`; on success one life is left and the status rule `combat.cheat_death_status` names keeps it from happening again while it lasts.
   - `cull`: targets at or below the attacker's `cull.threshold` of their life are finished; the hit is marked culled.
   - `leech`: the attacker regains `leech.{resource}` of the damage dealt and `on_hit.{resource}` flat, plus the hit's own `leech` on life and `status.{id}.leech` of the damage over time its statuses deal. A dead attacker regains nothing.
3. **Events and statuses.** A `DamageDealt` event is published with the breakdown, whether it was culled or clamped, and the effect depth. Each Thread that dealt damage may inflict its status by `ailment.chance` scoped to the Thread plus the attacker's chance for that status (and a crit bonus); then every other status the attacker has a `status.{id}.chance` for rolls once. Magnitudes come from the `combat.ailment_magnitude.{status}` expression, or the status's `baseline`, potency from `ailment.potency`. A blocked hit leaves none when rule `combat.block_stops_ailments` holds.
4. **Death.** Life at zero kills: the death service marks the body, credits the owner of minions and projectiles, drops loot, grants experience, propagates statuses flagged to spread, and schedules a player's respawn. The killer regains its `on_kill.{resource}`.

There is no unmitigated damage. A `damage` effect with `cannot_kill` leaves its target a point of life.

### Stages and skips

A stage implements `IHitStage` (a name and a `Run` over the hit) and registers with `HitStages.Register`; rule `combat.stages` places it. Rule `combat.stage_skips` maps a hit tag to the stages, declared steps and break bars that hits carrying it skip, so damage over time skips what it should without a second pipeline. The declared steps are the attacker's `crit` roll, the `armor` part of mitigation, `added` damage, the `damage_taken` fold inside the taken stage (skipping the stage itself would land nothing) and `ailments`; a pool id skips that break bar. The core rules send hits tagged `dot` (every status tick carries it) past guards, evasion, block, crit, armor, added damage, poise and ailments, hits tagged `deferred` (a stagger paying out) past everything that ran when they first landed, and payloads tagged `no_crit` past the crit roll. Validation refuses names nothing registered. Without the rule the order falls back to the built-in ladder.

### The hit scope

Each hit rents a pooled `HitScope`: the hit's packet, result and per-Thread working amounts for the stages, a small reusable scope of named numbers for the stages' rule formulas, and the expression scopes of its uncached folds. In those scopes `target.*` is the struck body, `source.*` the attacker, `distance` the distance between them and `angle` how far round the struck body the attacker stands (zero in front, 180 behind); every other name resolves on the fold's owner as its own scope would. The attacker's `damage` and the target's `damage.taken` fold this way (`HitFolds` lists the attributes folded in a hit; packages declare theirs), so a modifier can scale by the victim's stacks (`gauge: target.status.bleeding.stacks`) while cached queries never see a target. Validation warns about a modifier that reads the target on an attribute not folded in a hit. The rule formulas the stages read are parsed once into the ruleset's hit plan.

### Victim tags

Victim tags are the struck body's tags, flags and active statuses, prefixed `vs.`, plus `vs.{key}` for each entry of rule `combat.victim_tags` whose expression holds in the hit scope (`low_life`, `full_life` and `behind` in the core rules; every rule is read on every hit, so axes such as `near` or `higher_level` are added where content scopes by them). They are computed once per hit and join the hit's tags, so they scope modifiers (`damage` more against `vs.burning`) and keep the attribute cache valid.

## Pools

Rule `combat.pools` lists the pools hits reach before life, in order, then `life`; the interface lists the ones before life. A body holds a pool only where it has a maximum for it (life always), so a shield, poise or ward exists only where authored or granted, costs nothing elsewhere and draws no ring. Rule `combat.pool_rules.{id}` says what a pool does:

- An absorbing pool (the default) takes what reaches it, at most `fraction` of it. Absorption pools that statuses carry (`pool`) take their share first, newest first, and an emptied one ends its status and the statuses `bound_to` it.
- A break bar (`kind: break`) never absorbs: each landing hit wears it by the hit times the attacker's `dealt` attribute times its own `taken` attribute (`poise.damage`, `poise.damage.taken`), and it breaks when emptied.
- `on_hurt` names a status a landing hit applies when it reaches a pool the body holds (the shield's recharge delay), `on_break` one applied when a hit empties the pool (poise breaking into `poise_broken`), and `rearm` one that marks re-arming. The bearer applies these to itself.
- With `states` the status system keeps the pool's state flag for the interface, stamped (in the recency ledger, under the flag's own name) when it begins: `pool.{id}.broken` while the pool is empty or its `on_break` status lasts, `pool.{id}.rearming` while its `on_hurt` or `rearm` status lasts, `pool.{id}.recharging` while it refills; none once it is full. These are the flags a client reads for a pool's state (recharging, broken, re-arming).

The core pools: **insight** (`insight.max`; empty at first and filled by momentum, the bearer's own `on_hit.insight` and `leech.insight`, it turns blows aside before any other pool), **poise** (`poise.max`; a break bar; broken it is off balance and refills nothing, then `poise_rearming` refills it fast and keeps it from breaking again until it is whole), **ward** (`ward.max`, `ward.regen`), **shield** (`shield.max`; each landing hit applies `shield_delay` for `shield.recharge_delay` seconds, which stops `shield.recharge`; then it refills at `shield.recharge_rate` of its maximum a second). An emptied pool is published as `PoolBroken`, the start of a recharge as `PoolRecharging` and a refill as `PoolRestored`.

A resource's usable maximum is its maximum attribute less `{resource}.reserved`, which contracts and toggles add to; the status system mirrors what is reserved into the gauge `reserved.{resource}` that the self state reads.

## Statuses

Statuses carry modifiers (optionally per stack and scaled by potency), flags (`stunned`, `rooted`, `hidden`), forbidden weave tags (which set `forbid:{tag}` flags that refuse casts), periodic effects with an interval, apply and expire effects, a stack policy (refresh, strongest, stack, independent), a stack cap (raised by the applier's `status.stacks` scoped to the status) and a buildup target (chilled into frozen). Durations scale by the applier's `effect.duration` and shrink by the target's `status.resistance` for harmful ones, unless the application's duration is fixed (`apply_status` with `fixed_duration`). The bearer's gauges expose `status.{id}` and `status.{id}.stacks` to modifiers. Beyond the core:

- **Clocks.** `stack_clock: independent` gives each stack its own timer; stacks fall off one by one.
- **Banks.** `bank: { from: applications, cap }` banks each application's damage (magnitude times duration), capped at `cap` times the strongest application, and ticks deal the bank out over the time left. `bank: { from: taken, fraction, cap }` banks a fraction of what the bearer takes. Effects read `banked`; `rupture` spends it; `on_bank_lethal` runs once when the bank reaches the bearer's life.
- **Pools.** `pool` (an expression of the applier, with `magnitude` and `potency`) is an absorption pool the pools stage drains; `bound_to` ends a status with the pool status it names.
- **Hooks fed by the bearer's triggers.** `consume_on: { event, tags, count }` spends stacks when the bearer's trigger kind fires with those tags; `on_bearer_hit` runs against whatever the bearer's next hits strike; `on_bearer_struck` runs when a hit lands on the bearer, from the striker. Hooks ignore damage over time and refuse past rule `procs.max_depth`.
- **Pops.** `on_reapply` runs when the status is applied over a live instance, before the new application takes over, reading `remaining_damage` and the applier's `pop` (`status.{id}.pop`).
- **Riders.** `apply_status` exposes the hooks `on_status_tick` and `on_status_expire`; their effects (a glyph's hooks of those names land there) ride on the instance it applies.
- **Identity.** By default the newest applier takes the credit; `keep_source` keeps the first. `baseline` is the magnitude when an application has no hit to take one from.
- **Tick contexts** read `amount`, `stacks`, `magnitude`, `remaining`, `elapsed`, `total` and `banked`; tick effects carry the tags `status`, `status.{id}` and `dot`, other status effects `status` and `status.{id}`.

Effects move statuses: `remove_status` cleanses (harmful statuses by default, newest first, `kind` and `order` to change it), `consume_status` spends stacks into a variable for the effects after it, `spread_status` copies a struck body's statuses to its neighbours, `move_status` siphons statuses onto the source or pours the source's onto the target with a splash, and `rupture` bursts a fraction of a status's bank or remaining damage as damage over time arriving at once.

## Charges

Charges are named banks a body builds and spends (kind `charges`). The charge service counts, gains, spends, decays and drains them in the `Charges` component and mirrors each count into the gauge `charge.{id}` that modifiers and formulas read. The most a body holds is its attribute `charge.{id}.max` (declared per kind by a family, raised by any modifier). A kind's definition says how long charges last after the last gain and how fast they decay once that time runs out (all at once at zero); a drain burns them down at a rate and refuses gains while it lasts. The `charge` effect gains, spends (setting `charges_spent`) and drains. Gains, spends and losses are published.

## Procs

A proc (kind `procs`) is a chance, granted by any modifier source through an attribute (`proc.{id}` unless it names another), to run an effect graph when a trigger kind fires for its owner: `on` the kind, `tags` the event must carry, `excludes` tags it must not (damage over time by default), `when` on the owner, `victim` on the other body, a rate (a chance, an internal cooldown `icd`, or procs per minute `ppm`, the chance growing with the time since the owner's last check), `falloff` per level of depth (rule `procs.depth_falloff` by default), `target` (the event's other body or the owner) and `no_crit`. The payload runs one effect depth deeper than the event; at rule `procs.max_depth` and deeper a proc is refused (`ProcRefused`), and rule `procs.chance_cap` caps any chance. Each owner rolls from its own stream derived from the simulation's seed. The proc service subscribes per owner and kind, and only for bodies that carry a chance: the status system asks it to look again whenever a body's modifiers change.

## Triggers

`Sim/Triggers.cs` holds trigger kinds by name and a per-simulation `TriggerHub`. Subscribers name a kind and an owner (or every owner) and receive the kind, owner, source, target, amount, crit, tags, depth, status and the event that caused it. The built-in kinds are fed by the event bus: `hit`, `crit` (the attacker's landed hits), `hurt`, `block`, `evade` (the defender's), `kill` (the credited killer's), `death_near` (deaths within rule `triggers.death_near_radius`), `minion_died` (the owner's), `status_applied` (the applier's), `cast`, `heal` (the healer's) and `interval` (every tick). The combat package raises `break` (the owner emptied another body's pool or bar), `broken` (the owner's was emptied), `cull`, `cheat_death` and `charge_gained`, with the pool or charge id as the status. A kind's feed attaches when its first subscriber arrives. Packages register the kinds their own systems raise and raise them through the hub.

## Combat notes

Every combat event beyond the core derives from `CombatNote`, which describes itself in one line: leech paid, pools broken, recharging and restored, deaths cheated, staggers, ruptures, consumed stacks, procs fired and refused, charges gained, spent and lost. A tool prints them all with one subscription, as the `fight` command does.

## Relations

Factions declare stances; minions and projectiles inherit their owner's faction; unaligned creatures are hostile to everyone but their own kind. Everything that chooses targets asks `Relations`.

## Rules used

`combat.hit_chance`, `combat.armor_reduction`, `combat.resist_cap`, `combat.resist_floor`, `combat.pools`, `combat.pool_rules`, `combat.evadable_tags`, `combat.evasion`, `combat.block`, `combat.block_value`, `combat.block_stops_ailments`, `combat.stagger_status`, `combat.cheat_death_status`, `combat.stages`, `combat.stage_skips`, `combat.victim_tags`, `combat.default_thread`, `combat.cooldown_at`, `combat.global_cooldown`, `combat.corpse_seconds`, `combat.respawn_seconds`, `combat.ailment_magnitude.*`, `combat.crit_ailment_bonus`, `combat.propagate_radius`, `procs.max_depth`, `procs.depth_falloff`, `procs.chance_cap`, `triggers.death_near_radius`.
