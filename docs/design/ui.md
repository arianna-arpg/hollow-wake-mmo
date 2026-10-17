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

## The web client

The browser client draws the same cluster, core pane, ribbon and world panes from the same theme content and the same messages, so a player sees one interface on either client. It lags the desktop client when a feature is new; a surface is done when both clients show it.

## What the client reads

Every surface above reads a message and sends intents. The self state carries every resource and its maximum, the reserved amount, the pools before life with their state (recharging, broken, re-arming), charges and their banks, and the stamps the cluster animates. The weave slot state carries, per seat, the form and its level, the socketed glyphs with level and dormant reason, the cooldown, uses, cast mode and progress, the converted and meta faces, and the preview numbers. The inventory state carries the board with positions and footprints, the equipment with sockets, the wallet, and every container the hero can reach with its board and unlock rungs. The passive state carries the allocated nodes, picks, points per currency and open realms. The page state carries which pages exist, which are sealed and why, and their pips. A dialogue state carries the speaker, line and choices; a vendor state carries stock, prices and buy-back. Intents move items, socket stones, set and reorder weaves, allocate and refund nodes, choose dialogue lines, buy and sell. The parity gap analysis lists which package owns each message.

## Checking

The scripted smoke play-through opens the core pane, visits every ribbon page, talks to a villager, trades with a vendor and saves a screenshot of each, on both clients. A page that reads anything but a message, or a client that folds a number the server already sent, fails review.
