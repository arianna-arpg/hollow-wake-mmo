# Bound elements and the Amalgamation

An extension of PR #39 from `9d3ef741efe0154d3b0ed26d43ae16ebda8b7a6e`, using the same read-only prototype revision as the first pass. The assembled golem and summon comparison sprites were inspected alongside the source anatomy. These are production body plans, not concept illustrations.

| Body | Source and translation | Current path |
| --- | --- | --- |
| Stone Golem | `stone_golem`, `golem_stone_assembled`: hewn blocks, oversized hands, runes and moss. Ground Slam retained; Strike replaces claw. Explicit 10% damage reduction. | `summon_stone_golem` |
| Fire Golem | `fire_golem`, `golem_fire_assembled`: kindled torso, tapered limbs and licking hands. Firebolt retained; Ember Blast substitutes for Flame Wave. Explicit 75% Ember resistance. Source fire-damage bonus and lava/magma immunity are not imported. | `summon_fire_golem` |
| Ice Golem | `ice_golem`, `golem_ice_assembled`: prisms, shoulder fans and hanging ice. Frostbolt retained; Rime Nova composes the existing Tide nova form. Explicit 75% Tide resistance. | `summon_ice_golem` |
| Lightning Golem | New Storm adaptation: an open conductor cage, suspended core, coil cuffs and rods. No separate lightning golem was found in the inspected source revision. Existing Storm Bolt and Storm Nova, with authored 60% Storm resistance. | `summon_lightning_golem` |
| Blood Golem | `blood_golem`, `golem_blood_assembled`: unequal clots, sinews, heart and droplets. Heavy Strike retained; Strike replaces claw. Explicit 5% life leech and 40% Umbra resistance, translating the source grave/blood chaos theme. | `summon_blood_golem` |
| Amalgamation | `amalgam_horror`, `amalgam_stitched`: a grafted trunk, mismatched arms, ribcage, spare skull, stitches and belly maw. Heavy Strike and Poison Nova substitute for claw and pestilent_nova; explicit 40% Umbra resistance. | One guardian at a sparse Suture Works site |

The five named golems are reward-free bound bodies, as in the prototype's main summon roster. Their fixed contracts share the existing Warden group, reserve 30 mana and respawn after eight seconds. They replace Stone Warden, Bone Golem and Salvaged Golem contracts instead of creating an extra slot. Stone Warden and Quarry Golem keep their existing IDs, kits and appearances. Blood leech uses the existing hit pipeline. Resistance never implies immunity, flight or phasing.

The Amalgamation is a fixed creature at a provisional site in this pass. The source Bonewright, parts hunts, selectable graft assembly, Crown promotion and part-selected rewards are deferred, along with broader encounter design. `src/packages/defs/amalgamation.ts` was reviewed to keep that larger event distinct from importing its creature. No Amalgamation summon, new event engine, AI tactics, reconstructing limbs or environmental interactions are claimed. Wild Amalgamations have ordinary creature rewards and cannot leave a reusable corpse.

Physical profiles use authored effective densities and occupied volumes. The fire body's cinder matrix and magical substance are a gameplay mass approximation, not literal flame physics. Blood uses a clot material mixed with bone; the Amalgamation uses flesh, bone and iron. Nominal heights are 2.15-3 m; conductor tips, flames and graft spurs may extend above them. The actual relative lineup retains the 1.8 m player/skeleton and 0.9 m Goblin reference. Every new body has deterministic color variation and no random scale variation.

Reproduce with `node experiments/golems/prototype-render.mjs`. The script uses a private headless browser, saves evidence under `evidence/prototype/`, and closes its own processes. `--write-fixture` deliberately updates `tests/fixtures/golems-prototype-appearance.json` after visual review. The original gallery and fixture remain separate, so changes to existing golems are checked too.

World comparison: archive the extension base's `content/core` into an ignored folder, then run `dotnet run --project experiments/golems/WorldEvidence -- <base-content-path> experiments/golems/evidence/prototype/world-neighbors.json`. The written review names every change in the sampled neighborhood. Native headless smoke covers behavior, not native rendered pixels. Dedicated browser gallery captures cover all six new bodies in isometric, low-angle and grayscale walk views.
