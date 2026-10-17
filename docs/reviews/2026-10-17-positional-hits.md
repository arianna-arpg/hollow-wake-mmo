# Positional hits: finding, rule and split of work

Date: 2026-10-17. Author: the Claude coordinator session. Base: main `07a2497`. This record answers the owner's report that a cast hits its target wherever the target moves, which is a downgrade from the earlier prototype's combat, and coordinates the fix with the Codex cast-cue and cast-forecasting work.

## Finding, with evidence

A form that targets an enemy or ally is range-checked only when the cast starts (`WeaveRuntime.TryBegin` through `ValidateTarget`: range plus radii and hostility). When the cast releases, `WeaveRuntime.Execute` runs the root effects straight against the stored target, and the damage, heal and status handlers apply to `ctx.Target` as given, with no reach, line-of-sight or arc test at that moment. Strike (`content/core/combat/forms.yaml`) is a timed cast whose root is a direct damage effect, so a swing lands on a foe who walked out of reach during the swing; a monster's bite does the same to a player who stepped away. Projectiles (collision along the flight; homing only with a seeking glyph) and areas (cones, rings, the registered shapes) are already positional. The gap is direct-target roots at release, and melee modeled as a target instead of an arc.

## The rule

A target is an aim, never a guarantee. Every application of an effect to a body tests the body's position at that moment against the delivery's geometry: a swing has an arc and a reach, a missile a flight and a collision, an area a shape, a targeted spell or heal a range and a line of sight at release. Moving out of the geometry before the application avoids it, for players and monsters alike, through the one pipeline. The rule enters `docs/design/combat.md` with the change below.

## Split of work

**Claude**, on branch `claude/positional-hits`, as a pull request for Codex to review:

- A release-time target check for Enemy, Ally and Any targeting at every point a cast applies root effects to a stored target (timed release, channel beats, charge and overcharge release, triggered casts, follow-ups and metas), driven by rule `casting.recheck_target_at_release` and a form field `release_check` (full, range, none). A failed check misses: no effects, costs paid, cooldown as for a completed cast, and a new event `WeaveMissed(Caster, Slot, Weave, Target, Reason)` with reasons out_of_reach, line_of_sight, dead and not_hostile, surfaced as a combat log line.
- Melee forms with direct roots (strike, measured_strike, bash, monster melee) become sector areas: reach as radius, a new attribute `melee.arc`, one body, a new pick `target` that takes the aimed body when it is inside the arc and the nearest body otherwise, facing the direction captured when the swing began. Cleave stays a cone.
- Tests pinning the whole family: a swing whiffs when its target steps out of reach or behind it, a bystander in the arc takes the blow, a bolt misses a target that moved after release unless it seeks, a heal on an ally out of range at release misses, `release_check: none` still lands, a wall at release misses with line_of_sight.
- Behaviors kept honest: monsters still close in, face and re-attack; the standard duels still end in a death, and their traces change in a named way.

**Codex**, in its cast-cue and forecasting work:

- `WeaveMissed` is the seam for a miss as a presentation phase: a swing that whiffs should read as a whiff, never as an impact, and never as a release that landed.
- The forecast's region for a melee swing is the sector the area root resolves, from the same plan the hit test uses, as the forecasting document already proposes; no second geometry.
- When the pull request opens, review it for defects, compatibility risks and missing regressions, then rebase the cast-cue branch onto it; the runtime change is kept to one commented place at the release so the rebase is small.

## Open question for Codex, from the prototype

Does the prototype's melee swing commit its facing when the swing begins, or turn toward the target during the swing up to a turn rate? The branch commits facing at the start (a foe who steps behind the swing escapes it) and leaves re-aiming to held modes that track aim. If the prototype turned during the swing, say how it bounded that, and the owner decides which feel to keep.

Acknowledged by: this record, for the Claude side. Codex: add your record or pull request link here when you have read it.
