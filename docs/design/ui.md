# Interface

The player's interface: the HUD cluster, the core pane and the panes the world opens beside it. The layout follows the earlier prototype, whose arrangement worked with the interface space; the texture does not, and is ours to set. This is the contract for the client packages; the data shapes it reads are owned by the protocol and listed at the end.

Three rules hold everywhere:

- **One glance.** Everything about the hero's condition sits in one cluster, each pool drawn on or around the orb it protects, so a look at the life orb says how healthy the hero is without reading numbers.
- **One home.** The core pane (the inventory) is the hub of the navigable experience. Every player page is a page of it, and every pane the world opens (a dialogue, a vendor, a station, a stash) opens beside it. There is no second hub.
- **Data over code, server over client.** The interface draws what the server sends and sends intents back. It never folds a number: previews, prices, requirements and fit come from the server. Which pages exist, their order, their gates, the palette and the nomenclature are content the client reads, so a mod can add a page or recolor a pool without touching the client.

## The cluster

The cluster stands at the bottom centre of the screen: the life orb on the left, the weave bar in the middle, the mana orb on the right. It publishes its drawn rectangle every frame so every pane can fit above it.

**The life orb** holds life as a fill that drains from the top. The pools that stand before life are drawn on it and around it, nearest first:

- **Shield** (the energy shield of the parity rows) is a translucent fill laid over the life orb from the top, so a full shield veils the whole orb and a spent one shows life bare. Its recharge shows as a soft pulse along its edge.
- **Ward** is drawn as an etched rim band hugging the orb, in the runic vocabulary of the Threads rather than a second glassy fill, so the two layers read differently at a glance.
- **Poise** and **insight** are concentric rings around the orb, poise inside, insight outside. A broken poise ring greys and cracks until it re-arms; insight fills with the momentum that feeds it. A hero with no base in a pool shows no ring: the pools exist only where authored or bought, as in the prototype.

**The mana orb** mirrors the life orb with mana as its fill, and **stamina** as its ring, so the two orbs read the same way: the fill is the pool, the rings are what stands around it.

**The weave bar** between the orbs holds the weave slots in rack order: the icon, the cooldown sweep, the cost, use and charge pips, a converted or meta face when the slot has one, a greyed face with its reason when the weave is dormant. A cast, channel or charge-up draws its bar above the weave bar, with overcharge stages and brim as segments of that bar. Reserved mana and life show as a darkened cap on their orb.

Around the cluster: status icons in a row above the weave bar with stacks and timers; the target line and the target's plate at the top centre; notices and combat text above the hero; the chat log in the lower left corner, above the life orb; the menu button beside the mana orb, which fans a tray of every page the account has, each with its key, greyed with a reason where it cannot be used from here, and wearing a pip when something on it is unspent. The button wears the sum of the pips while the tray is closed, so the button is the tell.

## The core pane

The core pane is the inventory. It opens with its key, from the menu, or whenever any page or world pane is requested, and it closes with the same key, with Escape after every other pane has closed, or with its glyph. It is draggable and remembers its seat; its pages move with it.

It shows the **bag board**, a grid of cells with item footprints and positions; the **equipment doll**, whose slots come from the content slots and whose sockets show their stones; the **wallet chips** for currencies; and the **ribbon rail** along its edge. Items move by drag and drop or click-lift carry: picking an item up and dropping it on a cell, a slot, a socket, a page, or the world sends one move intent, and the server answers with the new state. Right-click opens the item's quick actions. Hovering shows the item card, with comparisons against the worn item.

The **ribbon** holds the pages of the development workspace, each a tab on the rail:

- **Weaves**: the rack of weave slots. Every seat is drawn; a learned form sits in its seat with its glyph sockets as pips; a glyph stone dragged onto a pip sockets it; a misfit stays seated, greyed, with the reason in its tooltip; seats reorder by drag; a card lists which seated weaves a stone fits. The preview numbers on every card come from the server.
- **Passives**: the constellation, panned and zoomed, with a tab per unlocked realm, a searchable choice popup on choice nodes, a refund mode at a font, and node tooltips from the same data the server validates against.
- **Reliquary**: the account board whose seated relics are active, with its unlock rungs and mask.
- **Character**: a popout ribbon rather than a full page, listing the sheet by category and order from content, with one-line descriptions from the attribute definitions, resistances by Thread, and the quest log.
- Any container the account owns and can reach from here: a stash near its chest, a station's board near the station.

A page is requested by key, menu or ribbon; requesting a page opens the core pane and selects it; repeating the page's key closes the core pane; activating the selected page's ribbon dismisses only that page. Pages dock beside the core pane, share its height and fit above the cluster at the current scale; on a narrow screen they clamp inside the viewport, and the rail drops its badges before its chips.

## Panes the world opens

A dialogue, a vendor, a quest offer, a crafting station, a stash and a bounty board are **world panes**. Opening one opens the core pane beside it: the world's pane on one side, the hero's bag on the other, so buying, selling, handing in and socketing happen between the two without a third surface. A world pane closes when the hero walks out of reach of what opened it, when the conversation ends, or with Escape, which closes the front pane first and the core pane last.

Panes that would overlap bind into one **book** with a tab strip on the drawn pane, in the manner of the prototype's folio: the first pane opened holds the front, a newcomer of the same rank arrives behind as a fresh tab, a decision the player must make (a choice, a calling, a pick) takes the front, and a station's pane fronts over a development page because the player walked to the station. Closing the front promotes the tab the player last looked at. The core pane and its pages never bind with a world pane; they are its companion, standing beside it.

**Dialogue** is content: a conversation is a definition with lines, speakers, choices, gates (expressions over the hero and the world) and effects (quest flags, rewards, a vendor opening, a station summoning). The server runs it; the client shows the speaker, the line and the open choices and sends the chosen one. A line can open a vendor or a station pane in the same book, so a merchant who talks and then trades is one conversation.

**Vendors** show stock, prices and buy-back from the server, with the item cards of the bag; a purchase or sale is one intent each, and the bag updates through the ordinary inventory state.

## Theme and nomenclature

The look is a theme the client reads from content: a palette per resource and per Thread, the orb and ring order, the fonts, the icon set, the glyphs for pools and statuses. Pools keep our names in every label and tooltip, in the runic register of the lore: ward is warded, a shield is a shield, poise and insight keep their names from the parity rows. Nothing in the simulation or the protocol carries a colour or a font.

The texture is open. The arrangement above is settled; the material, the frame, the orb glass, the ring etching and the type are to be designed and may change without touching this contract or the messages.

### The interface content

The interface reads its own content from `content/core/ui/`, validated by `Content/Kinds/InterfaceKinds.cs` with the definitions in `Definitions/Ui.cs`:

- `ui_themes`: a look. Each names a frame style both clients draw (`stone` or `brass`) and colours it: named surface colours (panes, edges, wells, ink, accents), material tunables (grain, fibre, seams, vignette, bevel, corner, glass and etching strength), a swatch per resource (its fill and the deep end of its gradient), a colour per Thread, per rarity and per notice level, interface state colours (reserved caps, broken rings, dormant and sealed faces, pips, badges, drop tints), fonts by role and the icon set. One theme is the default; a client may draw another (`--theme` on the desktop, `?theme=` in the browser).
- `ui_icon_sets`: glyphs as SVG path data in a square view box, drawn as strokes, and which glyph stands for each pool, status, weave look or form and equipment slot; anything unmapped wears the fallback.
- `ui_pages`: the pages, their names, the input action that requests each, glyphs and order, where each lives (the core pane itself, a ribbon page, a popout, or the menu alone, as Options does), and the gate and pip source the server evaluates for the page state.
- `ui_layouts`: the arrangement: the orbs with the pools drawn on and around them nearest first, the book's primacy ladder and each world pane's rank, the equipment doll's cells, and every size and tunable (pixels at scale one, the departure reach, notice and combat text times).
- `ui_sheets`: the character popout's sections, each listing attributes by name and category, one row per Thread from a pattern, or the quest log.
- `input_actions` and `ui_options`: every action the clients bind with its default keys and controller input, and every preference the Options pane draws; their contract is [options.md](options.md).

A mod recolours a pool by patching one swatch, adds a page with one entry (and a server-side gate), or reorders the rings in the layout, without touching a client.

### Texture directions

Two directions ship as themes, over the same arrangement, so the owner can judge them side by side; `godot --path src/HollowWake.Client -- --mockup DIR` renders both over generated terrain with a staged hero and no server. The shots are in [ui-mockups](ui-mockups/).

- **Etched stone** (`etched_stone`, the default): basalt panes with a fine grain and hairline seams, bevelled slate frames with an incised groove and diamond runes at the corners, glass orbs set in a slate bezel, bone-pale ink and old-gold accents, carved capitals for titles.
- **Runic brass** (`runic_brass`): parchment panes with fibres and darkened edges, written in iron-gall ink with crimson accents, framed in a banded brass with an engraved line and rivets, orbs set in brass rings, a brass plate behind the weave bar.

Both draw the shield as a frosted glass veil over the life orb, the ward as a gold band of cut runes in the bezel, rings with etched ticks that grey and crack when broken, and items as tiles on the pane's own inset in their rarity's ink.

## The web client

The browser client draws the same cluster, core pane, ribbon and world panes from the same theme content and the same messages, so a player sees one interface on either client. It lags the desktop client when a feature is new; a surface is done when both clients show it.

## What the client reads

Every surface above reads a message and sends intents. The self state carries every resource and its maximum, the reserved amount, the pools before life with their state (recharging, broken, re-arming), charges and their banks, and the stamps the cluster animates. The weave slot state carries, per seat, the form and its level, the socketed glyphs with level and dormant reason, the cooldown, uses, cast mode and progress, the converted and meta faces, and the preview numbers. The inventory state carries the board with positions and footprints, the equipment with sockets, the wallet, and every container the hero can reach with its board and unlock rungs. The passive state carries the allocated nodes, picks, points per currency and open realms. The page state carries which pages exist, which are sealed and why, and their pips. A dialogue state carries the speaker, line and choices; a vendor state carries stock, prices and buy-back. Intents move items, socket stones, set and reorder weaves, allocate and refund nodes, choose dialogue lines, buy and sell. The parity gap analysis lists which package owns each message.

### The messages

The shapes live in `src/HollowWake.Protocol` (`InterfaceMessages.cs` beside `Messages.cs`) and reach the browser as `web/src/net/protocol.ts`. A client that draws this interface lists the `interface` feature in its hello; the server then adds the cluster's fields to every self state and sends the hero's own states whenever they change, checked on the self-state cadence. A client that does not ask gets the plain protocol, unchanged. World panes answer what the hero does, so they reach every client.

| Surface | Message | What it carries |
|---|---|---|
| The cluster | `self_state` | Beside the resources and maxima: `reserved` per resource, `pools` before life with `state` (ready, recharging, broken, rearming) and `since` and `until` times, the hero's `statuses` with stacks, remaining and duration, the `cast` (slot, mode, progress, stage of stages, brim, held, duration), `charges` with count and max, and `stamps`, the server times of the recent moments the cluster animates (hurt, healed, blocked, evaded, hit, crit, cast). |
| Weave bar and rack | `weave_slots` | Every seat, empty ones included: key, form, name, level, icon, socket count, the socketed glyphs with level, stone and dormant reason, the weave's own dormant reason, `ready_in` and the full `cooldown`, uses and their cap, cast mode, targeting, costs, threads, the converted and meta faces, and `preview` lines (label and text formatted by the server). Previews follow the hero's numbers at most every `snapshots.preview_seconds`; any change to the seats goes out at once. |
| Core pane | `inventory_state` | The bag board (width, height, mask, items with x, y, w, h and their cards), the equipment slots from content with what occupies them, the worn items, the wallet, and every container the hero owns, each board with its own id, rungs, whether it is reachable and why not. Item cards carry requirements as text with whether they are met, sockets, and what stones and form items hold; a loose stone's card lists how it fits each seated weave. |
| Passives page | `passive_state` | Allocated nodes, the options picked per choice node, unspent points per currency, open realms, and whether a refund can be made here and its cost as text. The constellation itself is passive content. |
| Menu tray, ribbon | `page_state` | The pages the hero has (absent means none): sealed with a reason, and pips. Gates and pips are the `ui_pages` content evaluated on the server. |
| Dialogue pane | `dialogue_state` | The source entity, conversation, speaker, line, choices with whether each is enabled and why not, the reach, and `ended`. |
| Vendor pane | `vendor_state` | The source, stock as cards with price per currency and count, buy-back, what the vendor pays per bag item, the reach, and `closed`. |
| Station pane | `station_state` | The source, recipes with inputs (text, have, need), output card, craftable and reason, the reach, and `closed`. |

| Intent | Message |
|---|---|
| Move an item to a board cell, an equipment slot or the world | `move_item {item, container, x, y, slot?}` |
| Socket or unsocket a stone on a weave seat | `socket_stone {stone, slot, socket}`, `unsocket_stone {slot, socket, x?, y?}` |
| Learn or unlearn a form | `learn_form {item, slot}`, `unlearn_form {slot, x?, y?}` |
| Set or reorder weaves | `set_weave {slot, spec}`, `swap_weave_slots {a, b}` |
| Held input and the second ability | `release_weave {slot}`, `press_weave {slot}`, `aim {x, y, z, dir_x, dir_y}`, `use_meta {slot}` |
| Order minions | `command_minions {command, target, x, y, z, group?}` |
| Allocate or refund a node | `allocate_passive {node, option?}`, `refund_passive {node}` |
| Talk | `choose_dialogue {conversation, choice, node?}`, `close_dialogue {conversation}`, `close_pane {pane, source}` |
| Trade | `buy {vendor, stock, count}`, `sell {vendor, item, count?}`, `buy_back {vendor, item}` |

The server refuses an intent it cannot honor with `intent_rejected` and the reason; the hero's states then show what is true.

## Checking

The scripted smoke play-through opens the core pane, visits every ribbon page, talks to a villager, trades with a vendor and saves a screenshot of each, on both clients. A page that reads anything but a message, or a client that folds a number the server already sent, fails review.
