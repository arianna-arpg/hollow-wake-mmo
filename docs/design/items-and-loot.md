# Items and loot

## Base families

A base item is a family (`short_sword`, `leather_jerkin`) with a class (`weapon.sword`, `armor.chest`, implied as tags), a slot and any slots it also occupies, local value windows (`weapon.damage.min`, `weapon.attack.time`, `weapon.range`), a defense mix priced by the slot budget rule, implicit lines with ranges, requirements, drop weight, minimum level, affix theme affinities and affix caps. Tier resolves from item level through `items.tier_breaks`; tier names prefix the display name; local values scale by `items.local_tier_scale`.

## Affixes

An affix family has a kind (prefix or suffix), a group (one per item), name particles by tier, lines (modifier shapes, local or wearer) and tiers best-first with level gates, ranges per line, weights and a magic-only flag. Rolling picks families eligible by item tags, excludes used groups, weights tiers by item level with a bias that favors higher tiers deeper in the ladder, multiplies by theme affinity, and stores one 0..1 roll per line. Values derive at read time, so retuning ranges retunes every existing item.

Rarity definitions set prefix and suffix caps, count distributions and base roll windows. Rare names mint from rule tables; magic names from affix particles; uniques pin lines to a base with their own ranges and tier scaling.

## Equipment

Equipping checks the slot's accepted tags and the item's requirements, displaces occupants, and recalculates: every `item:` source is removed and re-added from wearer lines; armor, evasion and ward sum from local values; weapon locals become `weapon.*` (main hand) and `offhand.*` bases on the wearer, which is what attack forms read. Displaced items go to the bag, which by then includes the cells the equipped item left; when one of them finds no room the equip is refused and nothing changes, so an item is never lost to a full bag.

## Boards

The bag, every stash page and the reliquary are boards: a grid of cells with an optional mask (rows of `X` and `.`) that switches cells off. An item covers its base's `width` by `height` cells and records its top-left cell on itself, so its place persists with it. The bag's shape is rule `inventory.bag` (`width`, `height`, `mask`), read when a hero enters the world; a saved item keeps its cell while it still fits there, otherwise it takes the first space that fits (row by row from the top left), and an item with no room at all is kept without a cell (shown as an overflow board) until room is made. Nothing merges or is lost on loading.

`InventoryService.Move` is the one move: an item goes to a cell of the bag or a container's board, to an equipment slot, or to the world. On the bag, moving onto exactly one other item swaps the two (the other takes the moved item's old cell, or the first space that fits it); a target under two items, out of bounds or with no room for the swap is refused, and nothing changes. Moves from the equipment doll or a container need free cells at the target. Containers and the weave rack register as places with the service, so a move between any two of them is the same call.

## Weave items

Heroes own their weaves as items (question Q1 of the parity plan). A **codex** teaches a form, composed with Threads the hero is attuned to, or a whole preset weave, at a level; its rarity sets the sockets its weave slot gets (rule `inventory.codex_sockets`, capped by what the form can hold beside a preset's own glyphs). A **glyph stone** holds a glyph at a level with the row it rolled on each of the glyph's axes. Both are ordinary items with a payload (`ItemInstance.Payload`); one base each (rules `inventory.codex_base` and `inventory.stone_base`), named from rule templates, with payload rollers registered by item class on the item generator.

The **rack** is the weave book's slots: seat `i` holds a learned codex and the stones socketed beside it, and builds weave slot `i` from them (the codex's preset or form at its level, the seat's Threads, the stones in socket order). Learning a codex into an occupied seat swaps the old one back to the bag (a better copy swaps in place); stones stay while the new codex has sockets for them. A stone whose gate fails stays socketed and dormant with its reason (question Q4); the slot compiles without it and keeps casting. Socketing, unsocketing, learning and unlearning check the item's requirements (on the form or glyph) and rule `casting.swap_requires` (question Q11, waived in safe zones); everything that leaves the rack goes back to the bag, all or nothing, and nothing is destroyed. Seats follow their weave slots when the slots are reordered. A loose stone's card lists how it fits each learned seat: it fits, it would sit dormant (with the reason), the seat is full, or it has no sockets.

While rule `casting.require_ownership` holds, a hero's set-weave must name exactly what the seat holds: the codex's preset or form and level, the stones in order with their levels and rows, and attuned Threads (the calling's, and any granted by an attribute `attune.{thread}` above zero). New characters receive their calling's weaves as codices already learned; a save from before the rack gets codices for its weaves once, when it loads. Quest rewards that name weaves give their codices and stones to the bag. Monsters, tools and tests compose freely.

Stones and codices drop from loot entries that name the stone or codex base; an entry may fix the payload (`glyph`, `form`, `weave`), and otherwise one is drawn by the glyphs' and forms' `drop_weight` above their `min_level`, leaning toward the forms and glyphs the dropper weaves with by rule `inventory.drop_lean`.

The **preview** a weave's card shows is computed by the server from the same fold a cast uses: each damage effect's lowest and highest roll split over its Threads and folded through the caster's damage with the weave's modifiers, cast time after speed, cooldown after recovery, costs, range, and the weave's own attributes named by their definitions. Labels come from rule `inventory.preview_labels`.

## Loot

Tables hold rolls; each roll draws a count (an expression) of entries by weight: an item (by base or class filter, with rarity or rarity weights and a level bonus), a unique, a nested table, currency with an amount range, or nothing. Conditions on entries read the loot scope. Kills roll the pattern's `drops` table with its chance plus rarity bonus rolls; chests and resource nodes roll tables through interactables. Drops are entities with an owner window and expiry.

## Crafting hooks

Quality on an item scales local values by `items.quality_bonus` and chooses rarity through `crafting.rarity_by_quality`. Bench affix addition reuses the same roll code (`TryAddAffix` with `crafted`).
