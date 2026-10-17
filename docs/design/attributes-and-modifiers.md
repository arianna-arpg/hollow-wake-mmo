# Attributes and modifiers

The one numeric engine. Characters, monsters, weaves, items and holdings all carry an `AttributeSet`; every number in the game is an attribute computed from it.

## Definitions

`attributes.yaml` declares attributes: id, name, category, base or formula, min, max, decimals, percent, hidden. Formulas read other attributes and `level` (`life.max: "40 + level * 10 + vigor * 4"`). Undeclared attributes still work as channels with base zero; declaring them adds bounds, names and validation. Hidden attributes are kept out of the character sheet the self state lists.

### Families

Attributes that exist once per member of a set are declared as a family (kind `attribute_families`) and expanded at load: `{ pattern, over, name, description, category, base, bounds, percent, hidden }`. The pattern's placeholders in braces range over a content kind's ids or a list (`status.{status}.chance` over `statuses`; several placeholders take a map from placeholder to kind or list), and every combination becomes an attribute with the family's category, base and bounds, its name and description templates filled with the members' display names. Members join the catalog like declared attributes, so validation, names and bounds exist for each; an attribute declared one by one with the same id wins. Families merge across files like every kind, so each area declares its own. Families declared ahead of the mechanics that read them stay `hidden` until then, and so do detail families with a member per status or per pair of Threads.

The combat families: the chance to inflict each status (`status.{status}.chance`), its leech and pop power, minions' chance to inflict it; flat added damage per Thread (`added.{thread}`) and damage taken as another Thread (`taken_as.{from}.{to}`); leech, on-hit and on-kill gains per resource; what each resource has reserved (`{resource}.reserved`); proc chances (`proc.{proc}`) and charge maxima (`charge.{charge}.max`).

An attribute set counts, per family, how many members its owner carries (has modifiers on or a base of its own), so a hot path asks `CarriesFamily` once before walking a family member by member, and `Carries` once before folding a member most owners never touch. Neither touches the fold.

## The fold

For an attribute in a tag context:

1. base: the owner's override if set, else the formula, else the catalog base, else zero
2. flat: sum of applicable flat modifiers plus links (ratio times the source attribute read with links disabled)
3. increased: sum of applicable increased modifiers, applied as `(1 + sum)`
4. more: product of `(1 + value)` over applicable more modifiers
5. override: the highest-priority applicable override replaces the result
6. clamp to min and max, round to decimals

A modifier applies when its scope tags are all present in the context, its condition (an expression on the owner) holds, and its gauge (a live quantity such as `status.poison.stacks` or `charge.fury`) is non-zero (scaling the value) or at least `gauge_at` (gating the value).

Query-local extra modifiers join the same fold. A weave computes any of its numbers as `caster.Attributes.Get(attr, scope, weave.Tags, weave.Modifiers, baseFromForm)`, so glyph modifiers and character modifiers add in the same layer. The damage pipeline uses `Apply(attr, baseAmount, ...)` to scale an external base through the same stack.

Cached results are keyed by attribute and tag context, never by who is asking, so a modifier must not depend on anything outside its owner and the context. Folds inside a hit are the exception that proves it: they go through the uncached `Apply` with the hit scope as owner, where `target.*`, `source.*`, `distance` and `angle` resolve (see `docs/design/combat.md`). A modifier reading the target is meaningful only on an attribute folded in a hit, and validation warns elsewhere; outside a hit its gauge reads nothing and it stays inert.

## Sources

Every modifier has a source string. Items use `item:{uid}`, passives `passive:{id}`, statuses `status:{id}:{key}`, glyphs `glyph:{id}`, rarities `rarity:{id}`, territory `territory:{holding}`, temporaries `temp:...`. Removing a source removes all of its modifiers. A temporary modifier (`modify`) takes a lease on its source each time it is applied, and only the latest lease's timer removes it, so refreshing it restarts its time.

## Entity scope

`Stats` is the entity's expression scope. It resolves attribute ids, `level`, `xp`, resource currents (`life`), fractions (`life_pct`), gauges (`gauge.x`, `status.x.stacks`, `charge.x`), and flags through `has()`: explicit flags, entity tags, status-granted flags (and the `forbid:{tag}` flags statuses set), `recently_*` from the recency ledger, the pools' state flags (`pool.{id}.broken`, `.rearming`, `.recharging`), and the derived `low_life`, `full_life`, `low_mana`, `full_mana`, drawn at the lines the attributes `low_life.line`, `full_life.line`, `low_mana.line` and `full_mana.line` give. The charge service keeps `charge.x` in step with the charges an entity holds.

A resource's usable maximum is its maximum attribute less `{resource}.reserved` (never below zero), mirrored into the gauge `reserved.{resource}`. A body holds a resource only where it has a maximum for it (life always): filling resources leaves out pools without one, and setting one the body lacks and has no maximum for leaves it out.

## Invariants

- Cycles between formulas throw; validation catches them at load where possible.
- Links are single hop by construction.
- Caches invalidate on any structural change or `Touch()`.
