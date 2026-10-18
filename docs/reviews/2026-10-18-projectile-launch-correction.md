# Flight launch correction after Claude rereview

Date: 2026-10-18. PR #36. Prior published head: c07b6df017ac9c7f9a854db836d5689e70debe16. Launch correction checkpoint: f493c9c.

## Source and ownership

Claude's [rereview](https://github.com/arianna-arpg/hollow-wake-mmo/pull/36#issuecomment-6254640832) identified two blockers despite the earlier green suite: empty-ground direction shots pitched into the cursor, and nested flights added launch lift again. Launch/elevation changes in DeliveryHandlers, ProjectileSystem and ProjectileAims were coordinated with #20; its contact/height forecast work stays separate. No geometry, clearance reach, body dimensions or budgets change here.

This merge includes complete #33 e92de2270a9c39497a842d9ba063efeb56b04d2b and main17ca2f13a4c193d4b65d99ca4545aae267581092, including reviewed #54. Original Aim, delivery Point, sampled body AimOffset and LaunchLift remain separate. EffectContext.Copy and EffectHelpers.At retain the aim; the aim wrapper both turns that aim and preserves its emission lift. The test contract retains #54's closed lethal-resource exception. No #34 history or isolated foundation correction is copied backward.

The foundation owner has subsequently marked e92de22 provisional while reconciling the owner's Knot-resident level decision. This published checkpoint supports review of the flight fixes; it is not the final stable #36 handoff for #34. The definitive complete #33 still needs to be merged and its affected gates rerun. #50's separate CastSource addition must be retained when that branch later joins this source.

## Reproduction and fix

The isolated initial 35-case regression suite fails 33 cases on unchanged runtime. Firebolt aimed at ground 3 m away ends at 3.564 m of its 16.250 m range; Piercing Arrow ends at 3.250 m of 19.380 m. The explicit point/horizontal controls pass. The failing logs are retained alongside the passing results in [flight-correction evidence](../../experiments/entity-hitboxes/evidence/flight-correction/). Original raw and exploratory output remains under the ignored artifacts/flight-correction directory.

The content default is target elevation: use a retained body's aim or the body-relative offset sampled by a direction press; otherwise fly horizontally. Explicit point aiming remains available. LaunchLift records the lift for the current emission anchor. Flight hooks supply zero because their positions are already elevated. Context copies and same-origin cast/aim wrappers preserve it; new placement anchors and an explicit origin at a different point use the content-defined ground lift. Later salvo beats keep the inherited From anchor. Source, Origin and CastId are preserved.

The final launch suite passes 54 cases: empty-ground presses at several cursor distances, a body beyond the cursor, seven nested emission paths (tick, travel, cast, cascade, delay, salvo, aim wrapper) over three ground elevations and two humanoid bodies, actual Tide Orb and Frozen Orb child hits, explicit elevation choices, and relocating an inherited emission to a fresh ground point. The original 882 actual-cast anatomy cases still pass, including small bodies and positive/negative target relief. The scheduled-child expansion exposed six further salvo failures before its anchor correction; those failures are retained.

The original shape-sensitivity fixture is unchanged. Its observer now records every hit target and tick, strengthening the comparison: bar and wave have equal first/last timing and totals in that sample but different intermediate contacts. All sensitivity families pass. No assertion, equivalence exception or budget was relaxed.

## Gameplay and replay comparison

An exact archived-source Release build of c07b6df reproduces all seven committed Windows hashes. The flight correction and the complete #54 combination also reproduce them. The baseline was not rewritten. All standard and proving report hashes match actual prior #36, and all proving outcomes match. The same three inherited win-rate ceiling breaches remain: Bulwark over Reaver, Plaguer over Hexer, and Pitfighter over Mower each win 10/10 against a 0.8 ceiling. Both proving commands exit 1, explicitly retained as failures rather than green gates.

Hashes omit spawn Z, so their equality does not mean flights are unchanged. The standard Skirmisher/Tide Caller seed-0 trace now has the caller's emitted shards at z0.9 instead of z1.9, with identical XY, damage and death events for that seed. The focused actual-orb tests independently show child flights hitting humanoids. The six other standard seed-0 narratives are byte-identical to prior #36.

For context, a separate exact main17ca build demonstrates the existing full-branch gameplay differences. These comparisons include the entity foundation and anatomy work and do not isolate the narrow launch correction:

| Pair and seed | Main17ca | Corrected #36 |
|---|---|---|
| Kindler/Boar, seed1 | Gore at 2.10 s has no hit event | Gore also hits Kindler for 6.9 at 2.10 s; Kindler still wins |
| Skirmisher/Boar, seed1 | Gore at 8.20 s has no hit event | Gore produces an evaded zero-damage hit at 8.20 s; Skirmisher still wins |
| Skirmisher/Hob, seed0 | Skirmisher hits for 5 at 2.80 s | That contact is absent; Skirmisher still wins |
| Skirmisher/Tide Caller, seed0 | No caller hit at 1.95 s; Skirmisher ends at 77/116 | Caller deals two 5.1 hits at 1.95 s; Skirmisher ends at 52/116 |

Across ten seeds, the first three pairings still have A win rate1.0 and the same median finish times (6.7, 7.6 and 5.85 s respectively). Skirmisher/Tide Caller changes from A/B/draw0.9/0.1/0 to0.6/0.3/0.1, with median7.5 s. Wolf/Hob, Tide Channeler/Spark Duelist and Reaver/Bulwark retain their hashes. Full reports, before/after hashes and complete representative traces are saved with the evidence. No gameplay was tuned to match a hash.

## Gates and limits on this checkpoint

- Debug solution and final Tools Release builds: 0 warnings/errors.
- Full tests: core1858, server59, native132, browser269, all2318 pass. Focused launch suite54/54. No .NET skips.
- Content validation0/0, exact499-key census, generated protocol current, production web build passes with its existing large-chunk advisory (app1665.53 kB and separate artwork109.60 kB).
- Composition lint:580 smells/197 notes; all777 finding keys identical to archived c07 after normalizing only the archive path prefix.
- Final Release standard replay identical; zone load completes200 ticks. Duel traces and loopback pass. Private probe grants all six negotiated features and receives catalog/interface state, enters and moves.
- Background browser30/30 and native36/36 pass with cleanupErrors empty. Native pixels remain skipped, and the same shutdown diagnostics remain in the log (five dummy textures, two shaped-text records, one font, one CanvasItem,26 ObjectDB instances).
- Card screenshot inspection still reproduces the separately owned #37 renderer defect. Its capture is retained as failure evidence; this is not card-mode visual acceptance.
- All owned heavy processes have stopped. No new performance sample was taken. Actual combined #20 forecast/release/contact acceptance and a coordinated timing run remain pending. Performance budgets are unchanged.

Claude rereview is requested on the published correction and its incoming-source union. The foundation owner receives the definitive stable full #36 only after its final #33 content decision arrives and affected gates pass.

Acknowledged by: pending Claude rereview after final combined source.
