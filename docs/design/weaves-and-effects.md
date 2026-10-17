# Weaves and effects

A weave is what a character casts. Players compose weaves; presets exist for monsters and starting kits.

## Composition

`WeaveSpec` = a preset id, or a **form** plus one or two **Threads** plus up to four **glyphs** (limits per form). Glyph order is socket order. A glyph reference carries a level and, for glyphs with rolled axes, the row each axis rolled: `fork`, `fork@3`, or `{ id: fork, level: 3, rows: { axis: row } }`. A reference without a level takes the level the glyph declares; levels are capped by rule `glyphs.max_level`. A reference without a row on an axis takes that axis's first row, the canonical roll of granted copies.

- the form supplies targeting, cast mode, timing and cost expressions, local attribute bases (`base_damage`, `area.radius`, `projectile.count`, `range`, `duration`, ...), declared mechanisms (projectile, area, duration, summon, chain, ...) and an effect graph
- Threads add their ids and tags and feed `$threads` and `$thread_status` in the graph
- the compiler adds a cast-scope tag from the cast mode and base cast time: `cast.instant`, `cast.timed`, `cast.channel` or `cast.held`, so modifiers can scope by commitment
- glyphs are gated by tags (any of `requires`, none of `excludes`) and by mechanisms (all of `requires_mechanisms`, plus those of their rolled rows), then add modifiers (their own, their rows', and `per_level` scaled by the glyph's level minus one), tags, mechanisms, effects at the root, hooks and patches; `level_bonus` and `level_bonus_per_level` raise the weave's level
- the result is a `CompiledWeave` with a tag set, mechanisms (declared and probed), bases, modifiers, compiled effects, costs, the patched graph it compiled from (`Specs`), its active glyph references and its dormant glyphs

### The fixpoint and dormant glyphs

Glyphs can enable each other: one adds a tag, a mechanism, a cooldown or a projectile another needs. The compiler starts with every glyph active, builds the weave (tags, mechanisms, modifiers, the patched and hooked graph, then the probes), and checks every active glyph's gate. The last one in socket order that fails goes dormant with its reason and the weave is rebuilt without it, until every active gate holds. Only removals happen, so this ends, and the result does not depend on socket order except where glyphs exclude each other, where the earlier socket stays active.

A dormant glyph stays socketed and contributes nothing, not even costs. `WeaveCompiler.Compile(spec, GlyphFit.Dormant)` compiles that way and lists dormant glyphs with their reasons; `GlyphFit.Strict` (the default) refuses a weave with a misfit, naming the first one. Presets and starting weaves in content must fit: validation compiles each and warns when a glyph would be dormant.

### Hooks

A glyph's `hooks` map hook names to effect lists. Effect types declare which of their lists run at which named moment through their `EffectSignature`: a projectile's `on_hit`, an area's or chain's per-target `effects` and a field's `pulse` are `on_hit`; a damage effect's `on_kill` runs when its hit kills. The compiler appends each glyph's hook list, in glyph order, into every node of the weave's graph that exposes the hook, wherever it sits in the graph; `on_cast` lists join the root list and run once each time the weave resolves. Effects a hook adds are never themselves hooked. A hook name no registered type exposes fails validation.

### Patches

A glyph's `patches` edit the parameters of the form's and preset's effect nodes before compiling, in glyph order: `{ type, where, set, wrap, mode }`. `type` names the effect type to match; `where` is `any` (default), `root` (nodes that were in the root list, even once wrapped) or `first` (the first match walking the graph). `set` assigns parameters; `wrap` replaces the matched node with a wrapper effect holding it in the list the wrapper declares (`effects` for `repeat`, `sequence` and `delay`). Modes:

- `first` (the default for `set`): the first glyph to set a parameter wins over the form and over later glyphs
- `default`: sets a parameter only where the node has none
- `append`: appends list values to the node's lists
- `wrap` (the default for a patch with only `wrap`): every glyph wraps, each wrapper directly around the node, so the first glyph's wrapper ends outermost; in `first` or `default` mode a node is wrapped only once per wrapper type

Validation rejects a patch naming an unknown type, a parameter the type does not declare, a wrapper that is not one, or an append with a value that is not a list. Choices are patches; quantities are attributes.

### Mechanisms and probes

A weave's mechanisms are the ones its form and active glyphs declare plus every probe that holds on it. Probes are pure functions over the weave being compiled (its patched graph, form timing, bases and modifiers) registered by name in `MechanismRegistry`; each area registers its own in `Weaves/Probes/`. A required mechanism may carry a parameter after a colon (`affliction:bleeding`, `surface:standing`, `flight:children`); the probe receives it, and an unknown probe or an unacceptable parameter fails validation. Plain names that are not probes are declared mechanisms and stay open. Probes never name weave ids. So a glyph that levies `cooldown.added` lifts a glyph that needs `cooldown`, and a Thread whose ailment is burning lets a weave meet `affliction:burning`.

## Runtime

`WeaveRuntime.TryBegin` validates everything in one place: alive, not stunned, no forbidden tag (silence forbids `spell`), not busy, off cooldown, affordable, a valid target or point or direction within range. It pays costs, starts a cast, a channel or resolves instantly, and raises events. `Execute` runs the effect graph with an `EffectContext` whose scope mounts `source`, `target` and `weave`. Runtimes that know how a cast was made set the cast variables (`cast.held`, `cast.fill`, `cast.stage`, `charges_spent`) on its contexts.

Cast time divides by `attack.speed` for attacks or `cast.speed` for everything else. Cooldowns divide by `cooldown.recovery`. Costs run through the `cost` channel scoped by resource and weave tags.

AI chooses weaves through the same runtime with `ChooseForAi`, using each form's `ai` hint and range.

## Effects

A registry of handlers compiles typed specs into executable effects. Handlers are grouped in families, one file each under `Effects/Handlers/` with a static `Register(EffectRegistry)`: damage and healing, statuses, charges, status transfer, deliveries, placements, movement, summons, minions, AI, control flow, casting, utilities. `BuiltinEffects.RegisterAll` only calls the families; a new handler goes into its family's file. Each handler reads its parameters as expressions, so `amount: "weave.base_damage * spell.power"` is ordinary, and declares an `EffectSignature`: the parameters it reads, the lists that are hooks, and for wrappers the list a wrap patch fills.

The context carries depth; nested deliveries, repeats, delays and casts deepen it, and rule `effects.max_depth` caps recursion. Damage, healing, death and status events carry the depth they happened at. `last_damage` and `amount` are set by damage effects so a following `apply_status` can size an ailment from the hit.

### Casting another weave

`cast { weave, at, aim, turn, level, damage_scale }` runs a preset's effect graph from inside another graph: sequels where a flight ends, emitters, follow-ups, contagion releases. The preset compiles once per level. It runs free of cost and cooldown, raises no `WeaveCast`, one level deeper than its host, with the host's weave level (or `level`), the host's tags joined to its own, and the host's origin for ownership and credit. `at` is `point` (default: the context's point), `target` or `caster`; at a point or a target its deliveries start there rather than at the caster's body. `aim` is `onward` (default: the host's direction, else out from the host's center), `out`, `random` (from the cast's random stream) or `rotating` (turning by `turn` degrees, default 45, each time the effect runs for the same host). `damage_scale` scales its damage effects only.

### Delay

`delay { seconds, effects }` schedules its list through the simulation scheduler, at least one tick later, one level deeper, with a detached copy of the context: it reads the stats of the moment it fires and its own copy of the variables. Nothing fires once its source no longer exists.

## Projectiles and fields

Projectiles are entities with velocity, range, pierce and hit effects; the projectile system flies them, tests hostile bodies by radius, runs on-hit effects, and expires them on range or terrain. Fields are entities that pulse effects on bodies inside them until they expire; they can follow their caster.
