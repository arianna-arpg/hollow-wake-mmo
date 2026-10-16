# Territory (Founts and Holdings)

Founts are entities placed by points of interest that declare one. A player of at least `territory.min_level` starts a binding rite by interacting; the rite takes `territory.bind_time` seconds (an expression over the holding's fount count), cancels if the binder leaves, dies or takes damage (when `territory.damage_interrupts`), and completes into a Holding owned by the character (one holding per character, up to `territory.max_founts_per_character` founts). Bound founts can be contested by another binder when `territory.contestable`.

A holding has decrees and structures from content. Decrees cost and upkeep currency from the treasury, require or exclude tags from structures and other decrees, apply modifiers to entities inside the holding's founts (conditions such as `has("player")` restrict them), and add generation overlays over the fount circles so the land itself changes. Structures cost, stamp a landmark into the terrain at a point inside the territory (the chunk is marked modified and persisted), grant tags, apply modifiers and yield currency.

Every `territory.yield_interval` seconds, founts yield by their biome's yields times influence, structures add their yields, decrees subtract upkeep, and the treasury updates. Holdings persist in the world save.

Guild ownership, taxes on visitors, sieges and diplomacy are built on this model, not in it yet.
