# Crafting (Reweaving)

A recipe names station tags, inputs (by base or tags, optionally not consumed), an output grant, a procedure (inline or by reference to a `procedures` entry), a level, a quality expression over `score`, experience and currency.

Starting a craft checks the station (tags and range), inputs, level and currency, then asks the procedure for a seeded challenge and sends it to the client. Procedures: `none`, `timing` (hit windows at seeded times), `sequence` (repeat a seeded symbol sequence under a time limit), `alignment` (turn dials to seeded angles within tolerance). New procedures register from code.

Submitting sends raw inputs; the server scores them against the challenge it issued, maps score to quality through the recipe's expression plus the crafter's `crafting.quality`, consumes inputs and currency, mints the output with that quality (rarity by `crafting.rarity_by_quality`, local values by `items.quality_bonus`), and raises `ItemCrafted`.

A challenge expires after its time limit plus `crafting.grace_seconds`.
