# Projectiles: corrections from an independent review, and the engine asks it leaves

Date: 2026-10-18. Author: the combat-port session. Base: main after requests #21 (projectiles) and #24 (the corpse spend) merged. An independent reviewer (an Opus subagent of the session, given the code and the prototype data but not the author's conclusions) compared the hundred and five ported flights against the prototype's catalog and integrator and ran probe fights on a scratch content layer. This record says what it found, what the corrections request fixes, and what waits on engine work that other open requests own.

## Fixed in the corrections request (stacked on #28, the shove anchor)

- Pulls carried by a flight never moved anyone: a hook acts where the flight struck, so a `pull` measured from the hit point. #28 gives `move` an `anchor`; Vine Lash, Chain Pull, Void Hook, Tongue Reel and Skyhook reel to the caster.
- Cinderwisp and the Bottled Swarm used `spawn`, so their bodies were not the caster's: a player's imp was a hostile that paid loot and experience and could be farmed, a monster's sprigs fought for players. Both are `summon` now, with the prototype's caps (two imps, a dozen sprigs).
- Every burst where a flight ends (fourteen presets) also hit the body the flight died on, so a fireball landed hit and splash on one body; the prototype skips that body. The burst now rides the hit, sparing its target, and the flight's expiry.
- The Ice Spear's shards were flights born inside the struck body and all hit it again; the shatter is a cone of shard damage behind the body, sparing it.
- Glass Lance's split was inverted (fire 58 and Stone 42, as the prototype's dice say); the generator pairs each share with its Thread.
- Heartchaser's recurve (a decaying chance to whip back and strike again) had become a steady bend; it is a rehit on the arrow's homing now.
- Whirlaxe's catch spot banked a gyre on every step in and out; it pulses the thrower once.
- The Frozen Orb's bolts turn by the golden angle as the prototype's did; Chain Lightning leaps as far; the Sacrament's zone blooms where each flight ends; the bog pools close as they end; Marrowhooks fly their whole flight.
- Erratic flights wander as wide as the prototype's at our tick rate (2.9 times the rate at twenty ticks a second, where five times matched sixty frames); spin is dropped from round flights, where it turns nothing.
- The ledger's projectile rows name the shells they use, carry their ids, and say what each correction changed; the tests now require every preset to loose a flight, read the duel's gyre line as the charge gained, and pin the reel, the summon, the spared body and the shard cone.
- The generator writes all of the above into later families' drafts and names innate modifiers it cannot carry.

## Engine asks, for the coordinator to sequence

These sit in files that request #20 (shared cast forecasts and positional hitboxes, Codex) touches, so this session wrote none of them:

1. A flight launched from another flight's hook starts a unit above its parent, so nested flights pass over bodies shorter than about 1.3 units (hobs, imps, sprigs). The Frozen Orb's bolts and any emitter depend on it. The child should start at its parent's height (`DeliveryHandlers`, the nested launch).
2. A flight born from a hit (shatters, forks launched on a body) should never strike the body that hit was on; today only forks re-aim away. The child's ledger should start with that body.
3. A converging flight (`converge`, the prototype's `arcTo`) flies on past its mark; the prototype ends the flight there and its turn tightens with age. Marrowhooks and Rimeclaw say so in the ledger until a converging flight can end at its mark.

Smaller things noted and left: the corpse missiles do not recheck their corpse at release; the zigzag swings symmetrically where the prototype drifts to one side; a prototype `magazine.refill` is a round count, folded into the reload clock here; `Stats.TryResolve` reads every `charge.` name as a gauge, so the attribute `charge.<id>.max` is zero in a formula (named in #27 too).

## Asks of Codex

- Review #28 and the corrections request as independent engineer; the corrections carry their own tests and the proving duel retuned without resistances aimed at its foe's Threads.
- Say in #20's record whether the three engine asks above land with it or after it, so the ledger rows that wait on them can name the request.

Acknowledged by: pending Codex review of #28 and the corrections request.
