# Passives

The passive constellation is content: nodes (`passives`), the choice groups some nodes deal (`passive_choices`) and the realms beside the main star (`passive_realms`). `ProgressionService` allocates and refunds against it, and the character's picks live in the `PassiveState` component and on the character sheet. Clients draw the constellation from the same content and the hero's `passive_state` message; every rule is checked on the server.

## Nodes

A node has a kind (start, small, notable, keystone, attribute), a position, undirected links (declared on either side), modifiers, a cost, a realm (`star` unless it says otherwise) and optionally a choice. A node is allocated when the hero has the cost in the node's currency, the node's realm is open, and, unless the realm's adjacency is `free`, the node links to an allocated node. The calling's start node is granted free; another calling's start may be bought as travel like any node, but it never anchors the tree.

## Choices

A node with `choice: { group, pick, default }` deals options from a choice group. Allocating it names one option (`allocate_passive {node, option}`); a node that allows more picks takes the rest later with the same intent, free. A group's options are its own list, and a group may also generate options over an attribute category (`generate: { category: core, form: flat, value: 4 }`), which is how the selectable attribute nodes work. Options carry modifiers, applied under source `passive:{node}:{option}`.

| Field | Meaning |
|---|---|
| `pick` | Options a node of the group lets its owner pick (a node's own `pick` overrides it) |
| `unique` | `none`, `node` (an option once per node) or `character` (once across every node of the group) |
| `deal` | `each` (every node deals), `first` (the first node allocated claims the group and the others cannot be allocated while it holds), `sole` (only the first node allocated deals; its siblings can be allocated for their links and deal nothing) |
| `max` | The most picks from the group across the character |
| `default` | The option given to a node that was allocated before it dealt choices; a node's own `default` overrides it |

Refusals name the rule: `needs_option`, `unknown_option`, `no_picks`, `already_picked`, `group_full`, `exclusive`, `deals_nothing`, `not_a_choice`.

## Realms

A realm is a constellation shown as its own tab. It opens when its `unlock` expression holds over the hero (checked when the hero enters the world, levels, turns in a quest, gets a story flag or allocates); opening grants its `roots` free. Its nodes spend the realm's `currency`: points come from rule `passives.points.{currency}` (an expression of level) and from rewards (`points` on a quest's rewards), and are spent and refunded in that currency. `adjacency: tree` needs a linked allocated node; `free` does not.

A **vocation** is a realm that names a `calling`: it opens only to heroes of that calling, whatever else its unlock asks, and for a hero of another calling its sealed reason says whose it is (rule `passives.calling_sealed_text`). Its unlock usually reads a story flag found in the world (a conversation choice, a discovery), and its currency is granted by conversations and quests rather than by levels. The Warden is the example: a Holdfast mini-tree on the `vocation` currency, opened by the flag `warden_oath`, which the Quartermaster grants, with vocation points, when a Holdfast asks about the wardens.

## Grafts

A node, or an option of a choice group, may grant a glyph as a graft (`glyph: { id, level }`). Its owner binds the graft to one weave slot (`bind_graft {source, slot}`, the source being the node or `{node}:{option}`; a slot below zero unbinds it), and it is grafted onto that seat as slot grafts are: the attribute `glyph.slot{n}.{glyph}` at the glyph's level, under source `graft:{source}`, which the weave runtime reads when it arms the book (the book is re-armed when a binding changes). A socketed copy of the glyph wins over the graft, and a graft that misfits goes dormant. Binding needs the calm a loadout change needs (rule `casting.swap_requires`). Refunding the node or the pick takes the graft and its binding away; bindings save with the character.

## Refunds

A node is refunded at a font: an interactable of kind `font` within its reach of the hero (`refund_passive {node}`). The calling's start and the roots of open realms are anchors and are never refunded. A refund is refused when some other allocated node would no longer connect to an anchor without it (nodes of `free` realms need no path). A choice node's picks go with it, its points return to their own currency, and the price is rule `passives.refund_cost`, a map of currency to an expression of the hero, the node's cost (`node_cost`) and the refunds made so far (`refunds`), paid from the wallet.

A full reset at a font is a station service (`reset_passives`, see items and loot): every bought node comes back at a price, and the anchors stay.

## Linting

`hollowwake passives [--verbose]` lints the constellations as allocation reads them: links to unknown nodes and links across realms, orphans, nodes no anchor of their realm reaches, and for each calling the cheapest points from its start to every notable and keystone of the star. Broken links, orphans and unreachable nodes fail it.

## Gates

Expressions over the hero (realm unlocks, conversation gates, page gates) read the hero's attributes and flags plus `wallet.{currency}`, `rep.{faction}` and `points.{currency}`, and the flags `quest.active.{id}`, `quest.complete.{id}`, `quest.completed.{id}`, `quest.taken.{id}`, `flag.{name}`, `realm.{id}`, `passive.{node}` and `calling.{id}` (`HeroScope`).

## On the wire

`passive_state` carries the allocated nodes, the picks by node, unspent points per currency, the open realms, and whether a refund can be made here with its price as text. It is sent to interface clients when any of that changes, including stepping up to a font.
