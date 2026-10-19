# Flight launch correction after Claude rereview

Date: 2026-10-18. PR #36. Prior published head: c07b6df017ac9c7f9a854db836d5689e70debe16. Launch correction checkpoint: f493c9c.

## Source and ownership

Claude's [rereview](https://github.com/arianna-arpg/hollow-wake-mmo/pull/36#issuecomment-6254640832) identified two blockers despite the earlier green suite: empty-ground direction shots pitched into the cursor, and nested flights added launch lift again. Launch/elevation changes in DeliveryHandlers, ProjectileSystem and ProjectileAims were coordinated with #20; its contact/height forecast work stays separate. No geometry, clearance reach, body dimensions or budgets change here.

This merge includes complete #33 e92de2270a9c39497a842d9ba063efeb56b04d2b and main17ca2f13a4c193d4b65d99ca4545aae267581092, including reviewed #54. Original Aim, delivery Point, sampled body AimOffset and LaunchLift remain separate. EffectContext.Copy and EffectHelpers.At retain the aim; the aim wrapper both turns that aim and preserves its emission lift. The test contract retains #54's closed lethal-resource exception. No #34 history or isolated foundation correction is copied backward.

The original checkpoint used provisional #33 e92de22 while the foundation owner reconciled the owner's Knot-resident level decision. The final propagation is recorded below. #50's separate CastSource addition must be retained when that branch later joins this source.

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

Claude rereview is requested on the published correction and its final incoming-source union.

## Definitive foundation and stable handoff

The final runtime/content source is **64dd292a5f4298508b995b67e8335094f03d2ad6**: complete #33 **6ac37940996e5d067fbdfd0ffb32e092221b87df** merged into the correction **415421c58b37640fee1741ed8c15da3c1e813220**. The correction checkpoint's [hosted CI](https://github.com/arianna-arpg/hollow-wake-mmo/actions/runs/39167285563) passed all jobs, including Windows determinism and aggregate ci. Hosted checks for the final publication are reported separately on the PR.

The incoming content sets all three Knot resident definitions to `use_site_level: true` and restores the clear-Knot foe window of at most two levels above the quest. Its regression tests verify the actual initial and respawned residents, and its golden changes and paired maps are retained from the foundation owner's complete branch. No runtime code or client source changed in this last merge, and there is no remaining foundation product decision. The existing launch, body geometry and #54 context changes remain intact.

Fresh final-source evidence uses the `final33-` prefix in the evidence directory:

- Debug solution and Tools Release builds: zero warnings/errors. Full tests: core 1860, server 59, native 132 and browser 269; **2320 passed**, with no .NET failures or skips. This includes the launch and authored anatomy regressions.
- Validation: zero errors/warnings. Starter zone load completes 200 ticks. All seven Release Windows replay hashes remain identical; the baseline was not changed. The literal census still matches exactly 499 keys.
- The complete proving report is structurally identical to the previous correction report, including all per-fight outcomes and hashes. It still exits 1 for the same three inherited win-rate ceiling breaches; this is not a passing balance gate. The full final JSON and explicit comparison are retained.
- Production web build passes with its existing large-chunk advisory. The app is 1665.62 kB and the separately loaded icon artwork is 109.60 kB.
- Final background run `run-i2p30g`: browser 30/30 and native 36/36 pass; cleanup errors are empty. Native rendering is not covered and the same shutdown diagnostics remain. Isometric screenshot inspection shows the expected world. The card screenshot still shows the separately owned #37 blank-world/tiny-viewport defect and is retained as failure evidence.

The final source is ready for the complete **#36 → #34 → #37** propagation. Do not cherry-pick an isolated flight fix or merge #34 backward. The foundation owner can now run its prepared child-only kin matrix against the complete correction. Claude rereview, actual combined #20 acceptance, #37 renderer acceptance and coordinated performance verification remain outstanding; no new performance sample or final combined approval is claimed.
The later aim-wrapper propagation defect, independent reproduction, owner-written correction and final docs/test foundation union are recorded in [the aim elevation follow-up](2026-10-18-aim-wrapper-elevation.md). That record supplies the newer source and handoff gates.

Acknowledged by: pending Claude rereview after final combined source.
