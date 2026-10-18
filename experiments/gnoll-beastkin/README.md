# Gnolls and Beastkin

The Laughing Packs keep the prototype's high shoulders, canine muzzles, rounded hyena or pointed coyote ears, exposed teeth, tails and paw claws. The Horned Tribes use separate caprine/bovine faces, swept or curled horns, pronounced hocks and split hooves. Shape, equipment, mass and size separate role silhouettes beyond color.

`provenance.json` records source revision, roster, supported kit substitutions and deferred systems. `evidence/` contains the actual browser body plans in both cameras and grayscale walking, with a shared-scale player/skeleton/Goblin/Orc comparison. Native tests compare every part, bone, socket and worn item against these same plans. Physical dimensions are gameplay estimates; fill and material mixture produce mass through the existing authoritative query.

Run `node experiments/gnoll-beastkin/render.mjs` from the repository root after installing web dependencies. Open `gallery.html` through Vite for the camera, grayscale and walking controls. Explicit `--write-fixture` regenerates the cross-client fixture after visual review. Build caches stay under ignored `artifacts/`.

The new camps compete within the existing ruin budget, with low weight, generous spacing and individually gated residents. They do not add an ambient spawn carpet. Factions are distinct but allied, following the prototype's winter-road relationship; Gnolls also ally with Goblinkin. Kits and simple chase/ranged behavior are live. Pack coordination, morale, commander orders, tactical traps, ally healing decisions and herd charges remain design intent for the AI overhaul. The Trapper's carried snares are decorative. No summons, crusades or precise damage volumes are implied by these models.
