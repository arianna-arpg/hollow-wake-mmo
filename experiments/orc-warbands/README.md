# Iron-Tusk warbands

This pass expands the prototype Orc Ravager into twelve Orc roles and adds six Gnashers. `provenance.json` names the original source and adaptations. The Ravager keeps its ID and existing Scrap Fort references, now under an Orc faction allied to Goblinkin. Four sites share the existing ruin budget: a muster, hold, chain camp and Gnasher pens. Higher level inhabitants remain gated. Social identity does not implement slavery or dynamic command hierarchy.

The actual shared client geometry is visible in `gallery.html`. Run `node experiments/orc-warbands/render.mjs` to capture both cameras and grayscale. Add `--write-fixture` only after reviewing a deliberate geometry change. That writes the full native/browser parity fixture including gear. The common-scale lineup includes the player and skeleton reference, ordinary Goblin, Orc Grunt, Warlord, unchanged whelp and larger Gnashers. Individual cards fit their own cameras for detail.

Player authority height was already 1.8m. Ordinary skeletons now share it; ordinary Goblins run 0.78-1.02m, with exceptional brutes and hobgoblins above that. Player and standard skeleton scale jitter is disabled. Cosmetic extents are not collision volumes. Physical width, depth, height, material mixture and fill feed the existing mass/shove query; authored estimates are not biological measurements.

Great Gnasher uses the existing `stone_leap` in place of prototype `crushing_leap`. Saddleback and Ironjaw have empty saddle geometry and design dimensions in metadata. Mounting, rider aggregation and control transfer are deferred. No collision algorithm or wire change is included.

World evidence preserves before/after maps for seed aureth. Origin terrain and chunks stay identical, but its generated scout quest points to a new ruin. The sampled eastern zone changes site/quest selection and chunks; the other sampled chunk hashes and climate hash remain stable. Updated goldens pin those intended changes. Gate evidence is under `evidence/gates`.
