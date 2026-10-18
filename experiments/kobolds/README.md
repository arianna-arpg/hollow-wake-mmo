# Copper-Crest Scaleclans

Original Kobold faction and roster, authored from the owner's reptilian direction. The exact prototype revision is `118b510f8b5bfb781163c5bc13ea4908f698e75b`. It has no Kobold definitions; `roost_dragon` supplies the visual reference for scutes, dorsal ridge, tail, claws and serpent head. The cave gnasher and goblin prodder are contrast references. `provenance.json` records the sources, actual skills and substitutions.

## Reproduce the gallery

Install the pinned web dependencies, then run `node experiments/kobolds/render.mjs`. It uses the actual browser body builder, equipment and animator in private headless Edge, writes small JPEG evidence and reports parts/triangles. `--write-fixture` explicitly regenerates `tests/fixtures/kobolds-appearance.json`; both native and browser tests compare against those same plans. Inspect the gallery before accepting a fixture update.

To reproduce the reference sprites, run `node experiments/kobolds/prepare-reference.mjs "PATH_TO_PROTOTYPE_REPOSITORY"`, then add `--reference` to the render command. Only committed files at the reference revision are extracted, into ignored `artifacts/prototype`. The prototype checkout is read-only. Reference sprites are rendered from its own body baker and part grammar, not reconstructed illustrations.

`gallery.html` can also be served through the renderer's Vite configuration for interactive camera, grayscale and walk-pose comparison. Individual portraits deliberately enlarge small creatures; the lineup uses one orthographic world scale and includes the 1.8 m player/skeleton reference and 0.9 m Goblin. `scale-report.json` distinguishes authoritative heights from drawn bounds, which include crests and the chief's banner. The inherited player/skeleton drawn bounds are slightly shorter than their authoritative heights.

## What ships

Dedicated pattern, faction, material, loot, behavior, appearance and POI files supply fourteen roles. Ordinary bodies stand 0.82–1.15 m; the exceptional breaker, bulwark and chief stand 1.35–1.48 m. Scavenger mass is 19.349 kg and Bulwark mass is 148.850 kg under the existing physical query. Equal requested shoves moved them 15.219 m and 1.175 m respectively in the controlled fixture, including their authored poise. These are game approximations, not measured biological weights.

Four biome-constrained sites compete within existing ruin budgets at weight 0.35, each with three or four single-member inhabitants and level gates. No biome roaming tables or global population budgets change. The hold's existing ruin landmark also provides a chest. `evidence/world-review.txt` explains the golden changes and gives naturally generated example coordinates.

All melee roles use a content-only `kobold_press` profile with a 1.15 m chase stop; ranged roles reuse `aggressive_ranged`. Sustained-damage tests use two windows without replenishing the Kobold's resources. Between windows the target and attacker are repositioned because knockback can end an encounter by pushing the target beyond the leash. Live shove behavior is checked separately.

## Reviewed limits and handoff

The shared biped rig gives ordinary locomotion, attack and death poses; the tail follows hips and has no separate gait. Snouts, tails, claws, scales and equipment remain visible in both camera captures. Vial belts, the Rimeweaver's dorsal sail, Cinderweaver's censer and Stormweaver's paired yoke distinguish caster roles in grayscale. Some fine claws and slit eyes disappear at far zoom; major silhouettes and role equipment carry readability there.

No new simulation, collision or AI runtime is introduced. These remain conservative physical envelopes; tail tips and frills do not extend collision. All elemental attacks are existing hand-cast projectiles. Hurl Debris substitutes for a thrown salvage attack. Backstab does not promise stealth or flanking. Rallying Shout does not implement command tactics. Shield artwork does not create a new blocking mechanism. No flight, breath, traps, climbing, burrowing, mining or innate resistance is implied.

The AI handoff in `docs/reviews/2026-10-18-fauna-ai-handoff.md` remains a proposal. For a future Kobold AI pass, evaluate shield-role positioning, reach-aware stopping, projectile line of sight, fair flanking, ally-aware rally use and disengagement using that reviewed capability/observation design. Do not make `meta.ai_handoff` a simulation input.

Native headless smoke covers behavior and scene loading, not native pixels. Browser captures and the native/browser geometry fixture provide the current visual evidence. A graphical native check still requires an isolated session. Ask Claude to review silhouette separation, physical estimates, habitat fit and substitutions before integration; recompute world goldens after combining concurrent faction content.
