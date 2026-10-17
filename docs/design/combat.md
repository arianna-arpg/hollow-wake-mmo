# Combat

## Threads

Nine Threads are the damage types: Ember, Tide, Storm, Stone, Bloom, Umbra, Lumen, Void, Mind. Each declares its resistance and penetration attributes, its mitigation rule (`armor` for Stone, `resist` for the rest) and the status it tends to leave.

## The damage pipeline

Every hit, including status ticks and field pulses, takes one path:

1. **Build** (the attacker's side). The effect's amount is split across the hit's Threads. Each share runs through the attacker's `damage` fold scoped by the hit's tags plus the Thread (weave modifiers joining), folded in the hit scope, so flat added damage, increased and more all apply where their scopes say and may read the struck body. Victim tags are added to the context as `vs.*`. Conversions (`convert.{from}.{to}`) move fractions between Threads; `extra.{to}` adds a share as another Thread. Crit rolls `crit.chance` and multiplies by `crit.multiplier`, unless the hit's tags skip `crit`.
2. **Stages** (the defender's ladder). The stages registered by name run in the order rule `combat.stages` lists; a stage may end the hit. The built-in ladder:
   - `immunity`: immunity flags refuse; nothing is published.
   - `guard`: a held guard absorbs hits in its arc; bodies without a guard pass.
   - `evade`: hits carrying an evadable tag (`attack` by rule) roll the `combat.hit_chance` expression over accuracy and evasion; an evaded hit is published as evaded and ends.
   - `block`: rolls `block.chance` and keeps `1 - block.amount` of every Thread.
   - `mitigate`: per Thread, armor reduction by the `combat.armor_reduction` expression, or resistance capped by the per-Thread maximum and the rule cap, less the attacker's penetration down to the floor.
   - `taken`: the target's `damage.taken` fold scoped by tags and Thread, in the hit scope; what remains above zero is received.
   - `pools`: pools absorb in rule order (`ward` then `life`); the hit is recorded as recent (hurt, hit, crit).
   - `cull`: targets at or below the attacker's `cull.threshold` of their life are finished.
3. **Events and ailments.** A `DamageDealt` event is published with the breakdown and the effect depth. Each Thread that dealt damage may inflict its status by `ailment.chance` scoped to the Thread (plus crit bonus), with magnitude from the `combat.ailment_magnitude.{status}` expression and potency from `ailment.potency`.
4. **Death.** Life at zero kills: the death service marks the body, credits the owner of minions and projectiles, drops loot, grants experience, propagates statuses flagged to spread, and schedules a player's respawn.

There is no unmitigated damage.

### Stages and skips

A stage implements `IHitStage` (a name and a `Run` over the hit) and registers with `HitStages.Register`; rule `combat.stages` places it. Rule `combat.stage_skips` maps a hit tag to the stages (or declared steps such as the attacker's `crit` roll) that hits carrying it skip, so damage over time can skip evasion and block without a second pipeline. Validation refuses stage and step names nothing registered. Without the rule the order falls back to the built-in ladder.

### The hit scope

Each hit rents a pooled `HitScope`: the hit's packet, result and per-Thread working amounts for the stages, and the expression scopes of its uncached folds. In those scopes `target.*` is the struck body, `source.*` the attacker and `distance` the distance between them; every other name resolves on the fold's owner as its own scope would. The attacker's `damage` and the target's `damage.taken` fold this way (`HitFolds` lists the attributes folded in a hit; packages declare theirs), so a modifier can scale by the victim's stacks (`gauge: target.status.bleeding.stacks`) while cached queries never see a target. Validation warns about a modifier that reads the target on an attribute not folded in a hit.

### Victim tags

Victim tags are the struck body's tags and flags, prefixed `vs.`, plus `vs.{key}` for each entry of rule `combat.victim_tags` whose expression holds in the hit scope (`low_life: "target.life_pct <= 0.35"`). They are computed once per hit and join the hit's tags, so they scope modifiers and keep the attribute cache valid.

## Triggers

`Sim/Triggers.cs` holds trigger kinds by name and a per-simulation `TriggerHub`. Subscribers name a kind and an owner (or every owner) and receive the kind, owner, source, target, amount, crit, tags, depth, status and the event that caused it. The built-in kinds are fed by the event bus: `hit`, `crit` (the attacker's landed hits), `hurt`, `block`, `evade` (the defender's), `kill` (the credited killer's), `death_near` (deaths within rule `triggers.death_near_radius`), `minion_died` (the owner's), `status_applied` (the applier's), `cast`, `heal` (the healer's) and `interval` (every tick). A kind's feed attaches when its first subscriber arrives. Packages register the kinds their own systems raise and raise them through the hub.

## Charges

The charge service counts, gains and spends named charges held in the `Charges` component, mirroring each count into the gauge `charge.{id}` that modifiers and formulas read.

## Statuses

Statuses carry modifiers (optionally per stack and scaled by potency), flags (`stunned`, `rooted`, `hidden`), forbidden weave tags, periodic effects with an interval, apply and expire effects, a stack policy (refresh, strongest, stack, independent), a stack cap and a buildup target (chilled into frozen). Durations scale by the applier's `effect.duration` and shrink by the target's `status.resistance` for harmful ones. The bearer's gauges expose `status.{id}` and `status.{id}.stacks` to modifiers. Consumable stacks (`consume_on`) and next-hit riders (`on_bearer_hit`) are declared and validated ahead of the mechanics that read them.

## Relations

Factions declare stances; minions and projectiles inherit their owner's faction; unaligned creatures are hostile to everyone but their own kind. Everything that chooses targets asks `Relations`.

## Rules used

`combat.hit_chance`, `combat.armor_reduction`, `combat.resist_cap`, `combat.resist_floor`, `combat.pools`, `combat.evadable_tags`, `combat.stages`, `combat.stage_skips`, `combat.victim_tags`, `combat.default_thread`, `combat.cooldown_at`, `combat.global_cooldown`, `combat.corpse_seconds`, `combat.respawn_seconds`, `combat.ailment_magnitude.*`, `combat.crit_ailment_bonus`, `combat.propagate_radius`, `triggers.death_near_radius`.
