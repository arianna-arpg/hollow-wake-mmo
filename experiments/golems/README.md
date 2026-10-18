# The Unbound Works

Prototype reference: `arianna-arpg/arpg-game-world` at `118b510f8b5bfb781163c5bc13ea4908f698e75b`. Source anatomy and the existing comparison sprites were inspected read-only. `provenance.json` distinguishes direct stone/vault/bauble translations from original clay, iron, crystal, timber and ceramic adaptations.

Run `node experiments/golems/render.mjs` after installing the pinned web dependencies. This starts a private, headless browser and closes its own processes. Add `--write-fixture` only after deliberately reviewing changed geometry. Build caches go under ignored `artifacts/`. The gallery renders production body plans and poses, not an illustrative mockup.

The common-scale lineup uses the authoritative 1.8 m player and skeleton and 0.9 m ordinary Goblin. Nominal stature differs from mesh bounds where crowns, branches or fittings extend upward. Cards deliberately enlarge individual bodies. Materials are approximate occupied-volume estimates: timber has large open gaps; hollow iron assemblies use less fill than solid quarry blocks. Armor and poise are separate authored defenses. No material name grants an immunity.

Current skills are ordinary existing weaves. `golem_guard` closes to melee distance even when Ground Slam has a longer AI hint; `aggressive_ranged` serves the crystal artillery body. These are replaceable content state machines. Repair, reassembly, coordinated guarding, sensing, bodyguard interception and mass-aware tactics are deferred to the AI discussion in the fauna handoff. No collision, movement or AI runtime is changed.

Stone Warden keeps its ID, player allegiance, dimensions, original combat stats, 10% reduction and existing binding. Its explicit mass and resource pools are intentional additions. The salvage pool uses the same supported Warden exclusive group as Stone Warden and Bone Golem, so only one such contract remains. Wild reward tables never survive summon birth or respawn.

Evidence under `evidence/` records geometry, scale, world map changes, gates, fights and Release costs. Native behavior is tested headlessly; native pixels require an isolated graphical session and are not claimed here.

To reproduce the neighboring-site comparison, extract `content/core` from the base commit into an ignored directory, then run `dotnet run --project experiments/golems/WorldEvidence -- <base-content-path>` from the repository root. This writes the complete nearby-POI and quest comparison, including unchanged local zones whose neighboring candidates moved.

Vault and Bauble bodies are adapted as abandoned works under the new unbound faction. Their prototype Compact/Magpie allegiances and wage/hoard economies are not imported. Vault yields a richer ordinary loot table; no special currency-transfer or salvage crafting mechanic is implied.
