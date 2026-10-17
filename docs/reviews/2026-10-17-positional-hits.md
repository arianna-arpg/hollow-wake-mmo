# Positional hits: finding, rule and split of work

Date: 2026-10-17. Author: the Claude coordinator session. Base: main `07a2497`. This record answers the owner's report that a cast hits its target wherever the target moves, which is a downgrade from the earlier prototype's combat, and coordinates the fix with the Codex cast-cue and cast-forecasting work.

## Finding, with evidence

A form that targets an enemy or ally is range-checked only when the cast starts (`WeaveRuntime.TryBegin` through `ValidateTarget`: range plus radii and hostility). When the cast releases, `WeaveRuntime.Execute` runs the root effects straight against the stored target, and the damage, heal and status handlers apply to `ctx.Target` as given, with no reach, line-of-sight or arc test at that moment. Strike (`content/core/combat/forms.yaml`) is a timed cast whose root is a direct damage effect, so a swing lands on a foe who walked out of reach during the swing; a monster's bite does the same to a player who stepped away. Projectiles (collision along the flight; homing only with a seeking glyph) and areas (cones, rings, the registered shapes) are already positional. The gap is direct-target roots at release, and melee modeled as a target instead of an arc.

## The rule

A target is an aim, never a guarantee. Every application of an effect to a body tests the body's position at that moment against the delivery's geometry: a swing has an arc and a reach, a missile a flight and a collision, an area a shape, a targeted spell or heal a range and a line of sight at release. Moving out of the geometry before the application avoids it, for players and monsters alike, through the one pipeline. The rule enters `docs/design/combat.md` with the change below.

## Split of work

Updated the same day: the owner reports that Codex is already implementing this fix, so the Claude implementation was stopped before it changed anything and its branch removed. One engineer builds it; the other reviews.

**Codex** implements, on its own branch as a pull request:

- A release-time target check for Enemy, Ally and Any targeting at every point a cast applies root effects to a stored target (timed release, channel beats, charge and overcharge release, triggered casts, follow-ups and metas), driven by a rule such as `casting.recheck_target_at_release` and a form field such as `release_check` (full, range, none). A failed check misses: no effects, costs paid, cooldown as for a completed cast, and an event such as `WeaveMissed(Caster, Slot, Weave, Target, Reason)` with reasons out_of_reach, line_of_sight, dead and not_hostile, surfaced as a combat log line and available to the cast cues as a whiff phase.
- Melee forms with direct roots (strike, measured_strike, bash, monster melee) become sector areas: reach as radius, an attribute for the arc, one body, a pick that takes the aimed body when it is inside the arc and the nearest body otherwise. Cleave stays a cone. The forecast's region for a swing is that sector, from the same plan the hit test uses.
- Behaviors kept honest: monsters still close in, face and re-attack; the standard duels still end in a death, and their traces change in a named way.

**Claude** reviews the pull request against this acceptance list, each item a test the change should carry:

- A timed swing whose target steps out of reach during the swing lands nothing and misses with out_of_reach.
- A target that steps behind the swing escapes it; a bystander standing in the arc takes the blow when it is the nearest body.
- A target inside the arc at release is hit.
- A non-homing bolt flies to where its target was and misses a target that moved aside after release; a seeking glyph then hits.
- A heal on an ally out of range at release misses with the reason.
- A form that opts out of the check still lands.
- A wall between caster and target at release makes a full check miss with line_of_sight.
- The standard duels (loosed_kindler against boar, wolf against hob, the proving, channeler and skirmisher duels) still end as the contracts describe, with the before and after endings named.
- The Release bench stays within docs/design/performance.md, and the minion-heavy bench runs with zero errors.

## Open question for Codex, from the prototype

Does the prototype's melee swing commit its facing when the swing begins, or turn toward the target during the swing up to a turn rate? The branch commits facing at the start (a foe who steps behind the swing escapes it) and leaves re-aiming to held modes that track aim. If the prototype turned during the swing, say how it bounded that, and the owner decides which feel to keep.

Acknowledged by: this record, for the Claude side. Codex: add your record or pull request link here when you have read it.
