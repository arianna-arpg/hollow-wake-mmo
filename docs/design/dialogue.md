# Dialogue and trade

Conversations and vendors are content (`dialogues`, `vendors`), run by the server. A client shows the speaker, the line and the choices it is sent, sends the one the player takes, and draws a vendor's stock and prices as received; nothing a client says is believed.

## Conversations

A conversation names the entities it opens for (`for`: patterns, or interactable refs) and, when several match, the highest `priority` whose `when` gate holds opens. Talking to an entity of interactable kind `talk` opens it; a `quest_giver` lists its quest offers as before and also opens its conversation, so a client that draws conversations fronts it while one that only shows quest offers keeps working.

A conversation is a map of lines. A line has the text (`{hero}` and `{speaker}` are filled in), an optional speaker, effects run when it is shown, and choices. A choice has an id and text, a `when` gate (offered only while it holds), an `enabled` gate (offered but closed while false, with a `reason`), effects run when it is taken, and either `goto` another line, `end: true`, or neither (the line is shown again). A line with no open choice offers only `leave`. Gates are expressions over the hero and the world (see the gates in [passives.md](passives.md)): quest states, story flags, wallet, reputation, points, level and attributes.

Effects name a dialogue action or, failing that, any effect type, which runs with the speaker as source and the hero as target (`grant_xp`, `grant_item`, `event`, `apply_status` ...). The actions are a registry (`DialogueActions`), open to more:

| Action | Parameters | Does |
|---|---|---|
| `set_flag`, `clear_flag` | `flag` | Sets or clears a story flag on the hero (`has("flag.{name}")` in gates) |
| `accept_quest`, `turn_in_quest` | `quest` | Accepts or turns in a quest through the quest service |
| `offer_quests` | | Sends the speaker's quest offers |
| `open_vendor` | `vendor` | Opens the vendor pane beside the conversation, with the speaker as the vendor |
| `open_station` | `tags` | Opens a crafting station pane with the speaker as the station (its interactable tags by default) |
| `give_item` | `item`, `count`, `rarity`, `level`, `glyph`, `form`, `weave` | Mints and gives an item (a stone or codex with its payload) |
| `give_currency`, `take_currency` | `currency`, `amount` | Adds to or takes from the wallet |
| `grant_points` | `currency`, `amount` | Grants passive points of a currency |

Choosing checks, in order: the hero is in this conversation, the choice was read from the current line (a choice sent with an old line is refused as `stale`), the speaker is alive and within the conversation's reach (rule `dialogue.reach` unless the conversation sets its own), the choice is offered and enabled. A conversation ends when a choice ends it, when the hero closes it, when they walk out of reach (checked every `dialogue.check_seconds`), or when another conversation replaces it. Events: `DialogueShown`, `DialogueChosen`, `DialogueEnded`.

## Vendors

A vendor has stock entries (a base item, rarity, level as an expression of the hero, units per purchase, a limited stock, prices per currency or the vendor's formula, a payload for stones and codices, `when` and `enabled` gates), the item tags it buys from a hero (`buys`), a currency, price formulas (`price`, `sell_price`, else rules `inventory.vendor.price` and `inventory.vendor.sell_price`, expressions of the item's value, item level, rarity rank, tier, quality and count over the hero), and how many sold items it keeps to sell back.

Opening a vendor mints its stock for the hero, deterministic for the vendor entity, the hero and the restock period (`inventory.vendor.restock_seconds`), so what is bought is what was shown. Buying pays the shown price per purchase and puts the copies in the bag; selling pays the quoted price and keeps the item for buy-back at what was paid times `inventory.vendor.buy_back_markup`. Every trade is all or nothing: without the coin, the room, the stock or the vendor in reach, nothing changes. Limited stock runs out for everyone trading with that entity. Events: `VendorOpened`, `VendorChanged`, `VendorClosed`, `ItemBought`, `ItemSold`.

## Stations

Interacting with a crafting station (or a conversation's `open_station`) opens its pane: the recipes whose station tags meet the station's, each with its inputs as text with what the hero has and needs, a card of what it makes, whether it can be made now and why not, and its cost. Crafting itself is unchanged (`craft_start`, `craft_submit`).

## On the wire

`dialogue_state` carries the source entity, the conversation, the line id, the speaker, the line, the choices with whether each is enabled and why not, the reach, and `ended`. `vendor_state` carries the source, the vendor, its name, the stock as item cards with price, units left and whether each can be bought now, the buy-back list, what the vendor pays for each bag item, the reach, and `closed`; it is sent on opening and again after anything changes what it shows. `station_state` carries the source, the name, the recipes and `closed`. Intents: `choose_dialogue`, `close_dialogue`, `close_pane`, `buy`, `sell`, `buy_back`.
