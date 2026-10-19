# Playtest Hollow Wake

**Double-click `Play.cmd` in the main checkout.** It prepares the game, starts its local server and opens the browser. First preparation takes longer; later starts reuse the build. Keep the preparation window open until it says ready. If preparation fails, the window stays open with the error.

Choose a Calling vessel in Mu, give the character a name and press **Name this vessel and wake**. For an existing character, choose its vessel and press **Wake**. The Crossing closes when the character and nearby terrain are ready. Characters are saved locally. Double-click `Stop Playtest.cmd` when finished to save and stop the server.

Use `Play Desktop.cmd` only if you want the Godot client; it needs the Godot .NET executable configured as described in [local playtesting](README.md#local-playtesting). The browser route is the default and needs no Godot editor. A first build requires the installed development prerequisites in [local-dev.md](docs/infrastructure/local-dev.md).

## A first combat session

1. Start with **Holdfast** for melee (Strike, Cleave, Rallying Shout, Lumen Ward), **Kindler** for spells (Ember Bolt, Storm Nova, Lumen Ward, Quick Dash), or **Sower** for healing and summons (Bloom Mend, Call Sprig, Tide Bolt, Quick Dash).
2. Walk around the hamlet, speak to the quartermaster and take a nearby quest. Walk out toward enemies; do not expect every prepared creature family to appear.
3. Hover an enemy or aim at the ground, then use the numbered weave slots. Try movement, taking damage, healing or guarding, killing and collecting a drop. If defeated, press **R** to respawn.
4. Open the weave, inventory and passive pages through the menu to inspect your build. A starter's equipped weaves show only a small part of the engine's supported mechanics.

| Action | Default control |
| --- | --- |
| Move | WASD, arrows or left-click ground |
| Cast equipped weave | 1-8, aiming with the pointer |
| Jump / nearby interaction / nearby loot | Space / F / G |
| Change camera / rotate | V / Q and E |
| Cancel or close / respawn | Escape / R |

## What is actually integrated

This describes the main playtest after the 2026-10-19 cleanup, based on gameplay source `f7ef072c`. It is a development build with placeholder presentation. Engine support is not a claim that every mechanic is equipped, balanced or visually complete.

| Area | On main now | Remaining boundary |
| --- | --- | --- |
| Casting | Costs, cooldowns, charges, triggers, channels, charge-up/overcharge, guards, timed casts, toggles and bank/reload mechanisms | Starting kits expose a subset; the complete combat grammar is still being refined. |
| Hits and effects | Shared authoritative damage pipeline; modifiers, crit/defense, healing, statuses, status interactions and charge/proc economy | Precision anatomy, committed targeting and full shared cast forecasts remain in the integration candidate. |
| Deliveries | Areas, projectiles, chains, fields, auras, movement and nested effect graphs | Some presentation families remain partial; later contact/return-flight corrections are not yet on main. |
| Summons | Summoning, inheritance, capacity, contracts, orders and corpse use | Revised paid construct casting and queued echoes remain in the integration candidate. Prepared undead summons are not all granted by progression. |
| Character builds | Forms, Threads, glyphs, weave items, equipment, passives, realms, vendors and crafting interfaces | Per-weave tree pages, some parity mechanics and progression/balance work remain. |
| Enemies and world | Existing generated terrain, sites, quests and populations, including the already merged undead and goblinkin work | Expanded Orcs/fauna are inside the candidate; Gnolls/Beastkin, Kobolds, golems and elementals are archived. The proposed living-world simulation is still a plan. |
| AI | Existing behaviors and shared actor-intent foundation | The proposed affordance/doctrine chooser and richer positioning are not finished. Enemies do not yet use every mechanic intelligently. |

Source contracts: [weaves](docs/design/weaves-and-effects.md), [combat](docs/design/combat.md), [deliveries](docs/design/deliveries.md), [minions](docs/design/minions.md), [AI](docs/design/ai.md), [parity follow-ups](docs/design/parity/hollow-wake-gap.md#15-where-this-stands). Some older parity matrix cells describe the original gap; they are not the current completion ledger.

## What happens next

The single pending integration candidate is [PR #20](https://github.com/arianna-arpg/hollow-wake-mmo/pull/20). It already contains the separate construct, precision-hitbox, Orc and fauna histories. It is **not on main**. Functional checks exist, but final performance acceptance remains open, and its retained balance failures must stay visible. Review its included population changes separately before landing it; the presence of prepared assets does not require those spawns.

Playtest main first and turn concrete failures into fixes. Finish the combat candidate and its gates, then decide which prepared content belongs in the living-world work. No new creature rollout or parallel feature expansion is needed to unblock today's playtest.

Known limits: placeholder bodies/terrain, no shipped audio, incomplete visual convergence, rough camera collision, unfinished AI and balance, and native renderer/controller coverage beyond the headless tests. Passing automation does not certify that the combat feels good. Report the Calling, action, expected result and observed result; that is the next useful input.

The [reset record](docs/reviews/2026-10-19-project-reset.md) indexes the preserved work and verification evidence. Archived means recoverable and deferred, not deleted or merged.
