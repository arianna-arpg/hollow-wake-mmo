# Options

Every preference a player can set, how it is declared, where it lives and how the Options pane draws it. The aim is a settings surface as deep as the big MMOs' and cheaper to extend than theirs: an option is a row of content, never a widget written by hand, so a theme, a mod or a later system adds its settings by adding data. This contract belongs to the interface package; the settings themselves are content under `content/core/ui/`.

## Options are content

The content kind `ui_options` declares pages, the groups on each page, and the entries in each group. An entry has an id, a label, a one-line description, a kind, a default, a scope, an order, and what it needs to take effect. Kinds:

- **toggle**: on or off.
- **choice**: one of a list; the list may be literal or drawn from another content kind, so the theme entry lists every `ui_themes` entry, the icon set entry every `ui_icon_sets` entry, and a new theme file appears in Options with no other change.
- **slider**: a number in a range with a step and a unit, shown with its value.
- **key**: a binding for a named input action, with modifiers, a secondary binding and a controller binding.
- **color**: a colour, for the palettes a theme lets the player override (combat text, chat channels, rarity).
- **text**: a short string, such as a server address.

An entry's **scope** says where its value lives: **client** (this machine and profile), **account** (follows the account between machines, through the server's account store), or **character** (per character, kept on the character). Its **applies** field says when a change takes effect: **live** (at once, with a preview while the control is held), **on close** (when the pane closes), or **restart**. A **gate** expression hides an entry where it does not apply (a platform, a client, a developer flag), and a **requires** expression greys it with the reason (a quality tier that needs a feature the device lacks). Both read the client's facts (`client.desktop`, `client.web`, `client.developer`) and other entries' values (`option.<id>`, a toggle reading as one or zero), so `requires: option.combat_text` greys the critical-hit row while combat text is off. Entries may **reset** to their default singly or by group or page.

The input map is content too: the kind `input_actions` declares every action the clients bind (move, interact, each weave seat, the pages, chat, cameras, the smoke's own steps), its default bindings per device, its group and its context (where the action is read: the world, or the book's walk while a book is open), and the Options pane's key entries rebind them with conflict detection within a context. A `bindings` group on an Options page lists every action of an input group as key entries, so a new action appears in Options with no other change. Clients register their input map from this content, never from code.

## Where values live

Each client keeps a settings file per profile for client-scoped entries, written on change and read at start, with defaults from the content for anything missing or malformed; the browser client keeps the same shape in its local storage. Account-scoped entries travel through an account settings message to the server's account store and arrive with the welcome; character-scoped entries ride the character sheet. A client that lacks the account store (an offline tool, a stale server) treats account entries as client entries. Nothing in the simulation reads an option: a preference that would change gameplay is a rule or a character choice, not an option.

## The pane

Options is a page reached from the menu tray, from its own key, from Escape when no other pane is open and no cast is running, and from the login screen. It is drawn from the content: a column of pages, the groups of the selected page with their entries as rows, a search box that filters every page by label and description, a reset for the row, the group and the page, and the restart notice where a changed entry needs one. Live entries preview while the control moves: dragging the interface scale rescales the cluster behind the pane, choosing a theme recolours it, and the Escape key undoes an unconfirmed live change. The pane is a world pane under the book rule of [ui.md](ui.md): it stands beside the core pane when both are open and never binds with it.

Key entries capture the next press, show conflicts within the action's context and let the player swap or clear the other binding. Controller bindings share the row.

## The catalog

The pages ship with these groups; each grows as its system lands, and the schema makes adding one entry one row.

- **Interface**: theme, icon set, interface scale, the cluster's seat and the panel layout mode (fixed or movable with remembered seats), tooltip detail and delay, combat text (amounts, crits, statuses, colours), nameplates (whom, what, when), the chat channels shown and their colours, the quest tracker, the minimap.
- **Camera and controls**: the default camera, isometric rotation steps and zoom range, third-person sensitivity and inversion, click to move, hold to move, attack in place, target lock behaviour, cursor confinement, controller dead zones and vibration.
- **Graphics**: window mode and monitor, resolution and scale, vertical sync and frame cap, a quality preset that sets the rows below, terrain draw distance and detail, chunk streaming radius, shadows, water, foliage density, effects density, anti-aliasing, field of view in third person, screen shake and flash.
- **Audio**: output device, master and per-bus volumes (music, effects, interface, voice, ambience), mute when unfocused, subtitles.
- **Gameplay**: auto pickup rules, item filter level, confirmations (sell, destroy, refund, trade), loot and currency notices, the duel and trade request policies, the profanity filter.
- **Accessibility**: colour-vision palettes (themes that remap the pool and rarity colours), text size, high contrast, reduce motion, hold or toggle for held actions, cooldown and status readouts as numbers, screen reader hints.
- **Network**: the server address book and the last server, the snapshot stream choice (deltas or full states, for diagnostics), the latency and traffic overlay, connection timeouts.
- **Developer**: gated by a developer flag; the smoke, the mockup renderer, verbose logs, the profiler overlay.

## Checking

Validation rejects an entry whose default is outside its range or choices, a choice list that names an unknown kind, a key entry whose action no input action declares, and two entries with one id. The scripted smoke opens Options, switches the theme and confirms the cluster recolours, rebinds a key and uses it, changes the interface scale and confirms the panes refit, and resets a group. An option that the simulation reads fails review.
