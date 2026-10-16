# Weaves and effects

A weave is what a character casts. Players compose weaves; presets exist for monsters and starting kits.

## Composition

`WeaveSpec` = a preset id, or a **form** plus one or two **Threads** plus up to four **glyphs** (limits per form). Compilation:

- the form supplies targeting, cast mode, timing and cost expressions, local attribute bases (`base_damage`, `area.radius`, `projectile.count`, `range`, `duration`, ...), mechanisms (projectile, area, duration, summon, chain, ...) and an effect graph
- Threads add their ids and tags and feed `$threads` and `$thread_status` in the graph
- glyphs are gated by tags (any of `requires`, none of `excludes`) and by mechanisms (all of `requires_mechanisms`, from the form or any other glyph), then add modifiers, tags, mechanisms and effects; `per_level` modifiers scale with weave level
- the result is a `CompiledWeave` with a tag set, mechanisms, bases, modifiers, compiled effects and costs

Compilation fails with a reason when a gate or limit is violated; the server rejects the slot change.

## Runtime

`WeaveRuntime.TryBegin` validates everything in one place: alive, not stunned, no forbidden tag (silence forbids `spell`), not busy, off cooldown, affordable, a valid target or point or direction within range. It pays costs, starts a cast, a channel or resolves instantly, and raises events. `Execute` runs the effect graph with an `EffectContext` whose scope mounts `source`, `target` and `weave`.

Cast time divides by `attack.speed` for attacks or `cast.speed` for everything else. Cooldowns divide by `cooldown.recovery`. Costs run through the `cost` channel scoped by resource and weave tags.

AI chooses weaves through the same runtime with `ChooseForAi`, using each form's `ai` hint and range.

## Effects

A registry of handlers compiles typed specs into executable effects: `damage`, `heal`, `resource`, `apply_status`, `remove_status`, `modify`, `area` (circle, cone, line), `projectile`, `chain`, `field`, `summon`, `spawn`, `move` (teleport, dash, knockback, pull), `condition`, `random`, `repeat`, `sequence`, `event`, `interrupt`, `taunt`, `grant_item`, `grant_xp`, `log`. Each reads its parameters as expressions, so `amount: "weave.base_damage * spell.power"` is ordinary.

The context carries depth; chains and areas deepen it, and a rule caps recursion. `last_damage` and `amount` are set by damage effects so a following `apply_status` can size an ailment from the hit.

## Projectiles and fields

Projectiles are entities with velocity, range, pierce and hit effects; the projectile system flies them, tests hostile bodies by radius, runs on-hit effects, and expires them on range or terrain. Fields are entities that pulse effects on bodies inside them until they expire; they can follow their caster.
