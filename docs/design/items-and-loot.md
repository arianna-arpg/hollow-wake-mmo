# Items and loot

## Base families

A base item is a family (`short_sword`, `leather_jerkin`) with a class (`weapon.sword`, `armor.chest`, implied as tags), a slot and any slots it also occupies, local value windows (`weapon.damage.min`, `weapon.attack.time`, `weapon.range`), a defense mix priced by the slot budget rule, implicit lines with ranges, requirements, drop weight, minimum level, affix theme affinities and affix caps. Tier resolves from item level through `items.tier_breaks`; tier names prefix the display name; local values scale by `items.local_tier_scale`.

## Affixes

An affix family has a kind (prefix or suffix), a group (one per item), name particles by tier, lines (modifier shapes, local or wearer) and tiers best-first with level gates, ranges per line, weights and a magic-only flag. Rolling picks families eligible by item tags, excludes used groups, weights tiers by item level with a bias that favors higher tiers deeper in the ladder, multiplies by theme affinity, and stores one 0..1 roll per line. Values derive at read time, so retuning ranges retunes every existing item.

Rarity definitions set prefix and suffix caps, count distributions and base roll windows. Rare names mint from rule tables; magic names from affix particles; uniques pin lines to a base with their own ranges and tier scaling.

## Equipment

Equipping checks the slot's accepted tags and the item's requirements, displaces occupants, and recalculates: every `item:` source is removed and re-added from wearer lines; armor, evasion and ward sum from local values; weapon locals become `weapon.*` (main hand) and `offhand.*` bases on the wearer, which is what attack forms read.

## Loot

Tables hold rolls; each roll draws a count (an expression) of entries by weight: an item (by base or class filter, with rarity or rarity weights and a level bonus), a unique, a nested table, currency with an amount range, or nothing. Conditions on entries read the loot scope. Kills roll the pattern's `drops` table with its chance plus rarity bonus rolls; chests and resource nodes roll tables through interactables. Drops are entities with an owner window and expiry.

## Crafting hooks

Quality on an item scales local values by `items.quality_bonus` and chooses rarity through `crafting.rarity_by_quality`. Bench affix addition reuses the same roll code (`TryAddAffix` with `crafted`).
