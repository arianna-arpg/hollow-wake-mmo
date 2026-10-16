# Combat

## Threads

Nine Threads are the damage types: Ember, Tide, Storm, Stone, Bloom, Umbra, Lumen, Void, Mind. Each declares its resistance and penetration attributes, its mitigation rule (`armor` for Stone, `resist` for the rest) and the status it tends to leave.

## The damage pipeline

Every hit, including status ticks and field pulses, takes one path:

1. **Build.** The effect's amount is split across the hit's Threads. Each share runs through the attacker's `damage` fold scoped by the hit's tags plus the Thread (weave modifiers joining), so flat added damage, increased and more all apply where their scopes say. Victim tags are added to the context as `vs.*`. Conversions (`convert.{from}.{to}`) move fractions between Threads; `extra.{to}` adds a share as another Thread. Crit rolls `crit.chance` and multiplies by `crit.multiplier`.
2. **Apply.** Immunity flags refuse. Hits carrying an evadable tag (`attack` by rule) roll the `combat.hit_chance` expression over accuracy and evasion. Block rolls `block.chance` and keeps `block.amount`. Per Thread: armor reduction by the `combat.armor_reduction` expression, or resistance capped by the per-Thread maximum and the rule cap, less the attacker's penetration down to the floor. The target's `damage.taken` fold scoped by tags and Thread applies. Pools absorb in rule order (`ward` then `life`). Culling finishes targets below the attacker's `cull.threshold`.
3. **Events and ailments.** A `DamageDealt` event is published with the breakdown. Each Thread that dealt damage may inflict its status by `ailment.chance` scoped to the Thread (plus crit bonus), with magnitude from the `combat.ailment_magnitude.{status}` expression and potency from `ailment.potency`.
4. **Death.** Life at zero kills: the death service marks the body, credits the owner of minions and projectiles, drops loot, grants experience, propagates statuses flagged to spread, and schedules a player's respawn.

There is no unmitigated damage.

## Statuses

Statuses carry modifiers (optionally per stack and scaled by potency), flags (`stunned`, `rooted`, `hidden`), forbidden weave tags, periodic effects with an interval, apply and expire effects, a stack policy (refresh, strongest, stack, independent), a stack cap and a buildup target (chilled into frozen). Durations scale by the applier's `effect.duration` and shrink by the target's `status.resistance` for harmful ones. The bearer's gauges expose `status.{id}` and `status.{id}.stacks` to modifiers.

## Relations

Factions declare stances; minions and projectiles inherit their owner's faction; unaligned creatures are hostile to everyone but their own kind. Everything that chooses targets asks `Relations`.

## Rules used

`combat.hit_chance`, `combat.armor_reduction`, `combat.resist_cap`, `combat.resist_floor`, `combat.pools`, `combat.evadable_tags`, `combat.default_thread`, `combat.cooldown_at`, `combat.global_cooldown`, `combat.corpse_seconds`, `combat.respawn_seconds`, `combat.ailment_magnitude.*`, `combat.crit_ailment_bonus`, `combat.propagate_radius`.
