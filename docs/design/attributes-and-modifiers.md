# Attributes and modifiers

The one numeric engine. Characters, monsters, weaves, items and holdings all carry an `AttributeSet`; every number in the game is an attribute computed from it.

## Definitions

`attributes.yaml` declares attributes: id, name, category, base or formula, min, max, decimals, percent. Formulas read other attributes and `level` (`life.max: "40 + level * 10 + vigor * 4"`). Undeclared attributes still work as channels with base zero; declaring them adds bounds, names and validation.

## The fold

For an attribute in a tag context:

1. base: the owner's override if set, else the formula, else the catalog base, else zero
2. flat: sum of applicable flat modifiers plus links (ratio times the source attribute read with links disabled)
3. increased: sum of applicable increased modifiers, applied as `(1 + sum)`
4. more: product of `(1 + value)` over applicable more modifiers
5. override: the highest-priority applicable override replaces the result
6. clamp to min and max, round to decimals

A modifier applies when its scope tags are all present in the context, its condition (an expression on the owner) holds, and its gauge (a live quantity such as `status.poison.stacks`) is non-zero (scaling the value) or at least `gauge_at` (gating the value).

Query-local extra modifiers join the same fold. A weave computes any of its numbers as `caster.Attributes.Get(attr, scope, weave.Tags, weave.Modifiers, baseFromForm)`, so glyph modifiers and character modifiers add in the same layer. The damage pipeline uses `Apply(attr, baseAmount, ...)` to scale an external base through the same stack.

## Sources

Every modifier has a source string. Items use `item:{uid}`, passives `passive:{id}`, statuses `status:{id}:{key}`, glyphs `glyph:{id}`, rarities `rarity:{id}`, territory `territory:{holding}`, temporaries `temp:...`. Removing a source removes all of its modifiers.

## Entity scope

`Stats` is the entity's expression scope. It resolves attribute ids, `level`, `xp`, resource currents (`life`), fractions (`life_pct`), gauges (`gauge.x`, `status.x.stacks`, `charge.x`), and flags through `has()`: explicit flags, entity tags, status-granted flags, `recently_*` from the recency ledger, and the derived `low_life`, `full_life`, `low_mana`, `full_mana`.

## Invariants

- Cycles between formulas throw; validation catches them at load where possible.
- Links are single hop by construction.
- Caches invalidate on any structural change or `Touch()`.
