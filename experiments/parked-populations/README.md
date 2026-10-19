# Parked population proposals

These files preserve the Orc sites and expanded wildlife population proposal that travelled with PR #20. They are an optional content layer, outside `content/core`. The main server, browser playtest, desktop playtest and release packages do not load it.

Creature appearances, anatomy and combat kits may exist in the core catalog while their encounters wait. Existing wolves, boars, stags, undead and goblinkin populations remain available for combat testing. A habitat tag is an authoring proposal, not an instruction to spawn that body.

Tests explicitly load this layer after core to keep its references and intended habitats valid. Those authoring tests do not approve a rollout. The playtest isolation tests separately verify that core has no proposed Orc sites or wildlife tables, and that generated spawns and quest targets cannot select the parked bodies.

The owner has deferred activation until combat can be playtested and we can review population placement with Claude alongside the living-world plan. Do not add this directory to `content_layers` or release packaging as part of combat work. The full source history remains in PR #20 and the archive index in `docs/reviews/2026-10-19-branch-archive.json`.
