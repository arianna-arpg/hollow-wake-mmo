# Playtest Hollow Wake

**Double-click `Play.cmd` in the main checkout.** It prepares the game, starts its local server and opens the browser. First preparation takes longer; later starts reuse the build. Keep the preparation window open until it says ready. If preparation fails, the window stays open with the error.

Choose a Calling vessel in Mu, give the character a name and press **Name this vessel and wake**. For an existing character, choose its vessel and press **Wake**. The Crossing closes when the character and nearby terrain are ready. Characters are saved locally. Double-click `Stop Playtest.cmd` when finished to save and stop the server.

Use `Play Desktop.cmd` only if you want the Godot client; it needs the Godot .NET executable configured as described in [local playtesting](README.md#local-playtesting). The browser route is the default and needs no Godot editor. A first build requires the installed development prerequisites in [local-dev.md](docs/infrastructure/local-dev.md).

## A first combat session

1. Start with **Holdfast** for melee (Strike, Cleave, Rallying Shout, Lumen Ward), **Kindler** for spells (Ember Bolt, Storm Nova, Lumen Ward, Quick Dash), or **Sower** for healing and summons (Bloom Mend, Call Sprig, Tide Bolt, Quick Dash).
2. Walk around the hamlet, speak to the quartermaster and take a nearby quest. Walk out toward enemies; do not expect every prepared creature family to appear.
3. Hover an enemy or aim at the ground, then use the numbered weave slots. Try movement, taking damage, healing or guarding, killing and collecting a drop. If defeated, press **R** to respawn.
4. Open the weave, inventory and passive pages through the menu to inspect your build. A starter's equipped weaves show only a small part of the engine's supported mechanics.

To assess the integrated combat, try moving out of a melee swing after it starts, firing past the edge of an enemy, and aiming on a raised surface. Cast forecasts show the server's supported primary attack region; they do not promise every later bounce or effect. For anatomy inspection, enable **Show body hit volumes** in the developer options before connecting, then reconnect: cyan is damage anatomy and yellow is movement clearance. The rendered rest-pose anatomy can differ from animated limbs.

| Action | Default control |
| --- | --- |
| Move | WASD, arrows or left-click ground |
| Cast equipped weave | 1-8, aiming with the pointer |
| Jump / nearby interaction / nearby loot | Space / F / G |
| Change camera / rotate | V / Q and E |
| Cancel or close / respawn | Escape / R |

## What is actually integrated

This checkout contains the combat integration from [PR #20](https://github.com/arianna-arpg/hollow-wake-mmo/pull/20) and the browser rendering repair from [PR #57](https://github.com/arianna-arpg/hollow-wake-mmo/pull/57). It is a development build with placeholder presentation. Engine support is not a claim that every mechanic is equipped, balanced or visually complete.

| Area | On main now | Remaining boundary |
| --- | --- | --- |
| Casting | Costs, cooldowns, charges, triggers, channels, charge-up/overcharge, guards, timed casts, toggles and bank/reload mechanisms | Starting kits expose a subset; the complete combat grammar is still being refined. |
| Hits and effects | Shared authoritative damage pipeline; modifiers, crit/defense, healing, statuses and charge/proc economy; precise damage anatomy, dodgeable committed melee and shared cast forecasts | Forecasts cover supported primary geometry; anatomy is a rest-pose approximation. Later effects and unsupported geometry do not receive invented forecasts. |
| Deliveries | Areas, projectiles, chains, fields, auras, movement and nested effect graphs; body contact, frozen launch samples, layered aiming and return-flight support | Some presentation families remain partial. |
| Summons | Summoning, inheritance, capacity, contracts, orders and corpse use; paid device casts and queued echoes through the shared combat rules | Prepared summons and constructs are not all granted by starter kits or progression. |
| Character builds | Forms, Threads, glyphs, weave items, equipment, passives, realms, vendors and crafting interfaces | Per-weave tree pages, some parity mechanics and progression/balance work remain. |
| Enemies and world | Existing generated terrain, sites, quests and populations, including the already merged undead and goblinkin work | New Orc, Gnasher and wildlife populations are parked outside the live content layer. Gnolls/Beastkin, Kobolds, golems and elementals remain archived. Living-world implementation waits for collaboration with Claude. |
| AI | Existing behaviors and shared actor-intent foundation | The proposed affordance/doctrine chooser and richer positioning are not finished. Enemies do not yet use every mechanic intelligently. |

Source contracts: [weaves](docs/design/weaves-and-effects.md), [combat](docs/design/combat.md), [deliveries](docs/design/deliveries.md), [minions](docs/design/minions.md), [AI](docs/design/ai.md), [parity follow-ups](docs/design/parity/hollow-wake-gap.md#15-where-this-stands). Some older parity matrix cells describe the original gap; they are not the current completion ledger.

## What happens next

Use this build to assess combat feel and turn concrete failures into fixes. The [integration record](docs/reviews/2026-10-19-combat-playtest-integration.md) holds acceptance evidence and the [parked population proposal](experiments/parked-populations/README.md) states what is waiting. Creature definitions can remain ready in the catalog without being placed in the world.

Population placement and living-world work resume with Claude after this combat playtest. Existing balance failures for Bulwark, Plaguer and Pitfighter remain recorded in the prior evaluation evidence; integration does not certify balance.

Known limits: placeholder bodies/terrain, no shipped audio, incomplete visual convergence, rough camera collision, unfinished AI and balance, and native renderer/controller coverage beyond the headless tests. Passing automation does not certify that the combat feels good. Report the Calling, action, expected result and observed result; that is the next useful input.

The [reset record](docs/reviews/2026-10-19-project-reset.md) indexes the preserved work and verification evidence. Archived means recoverable and deferred, not deleted or merged.
