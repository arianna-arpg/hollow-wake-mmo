# Warren Warbands

Prototype reference: `arianna-arpg/arpg-game-world` at `118b510f8b5bfb781163c5bc13ea4908f698e75b`. Source definitions and sprite part grammars live in `src/data/monsters.ts` and `src/data/looks.ts`; weight behavior comes from `src/engine/mass.ts`, `defense.ts` and `Actor.ts`. This is a translation into MMO content and shared procedural anatomy, with deliberate artistic liberties.

The roster includes goblin scavengers, skirmishers, stoneflingers, shamans, sappers, brutes, chiefs and warbosses; a hobgoblin taskmaster, orc ravager, trolls, colossus, gnashers and warg. Scavengers, stoneflingers and whelps are new variations derived from the source families. `provenance.json` records the source, current kit, profile and deferred mechanics per body. Combat uses existing skills, with the sapper's Cinder Keg composed from the landed lobbed-projectile and area mechanisms. Shaman and command roles use Rallying Shout where the prototype's howl, war cry and incitement do not map directly.

## Visual evidence

Run `node experiments/goblinkin/render.mjs` after installing web dependencies. It owns a hidden browser and temporary local server, captures actual production geometry and closes both. `--write-fixture` intentionally refreshes the shared appearance fixture after a reviewed visual edit. Build output and Vite cache stay under ignored `artifacts/`.

[Isometric](evidence/goblinkin-isometric.jpg), [third person](evidence/goblinkin-third-person.jpg), and [grayscale walk](evidence/goblinkin-grayscale-walk.jpg) show the shared-scale lineup and individual detail cards. The runtime report records the actual body families, geometry cost and browser errors. The preview contains no combat simulation. Mass labels are the authored rest-volume estimate before rarity, modifiers or poise anchoring.

## Integration and evidence

The Warren Warbands are a separate combat faction. Foragers roam forest tables, gnashers pack the deep forest, and encampment, scrap fort and troll toll sites use existing ruin budgets. Every body has an active habitat reference and an AI kit that must land damage in its test. The colossus is held behind the fort's inhabitant level window. Site politics do not override the inhabitants' faction.

Before/after world captures use seed `aureth` and the undead PR as baseline. Adding weighted ruin choices changes selected sites and their nearby quest targets. Roaming forest populations also change chunk spawn output. Climate and region hashes remain unchanged. Golden changes are reviewed alongside these captures and the explicit seeded fort population test.

The physical contract is [physical-bodies.md](../../docs/design/physical-bodies.md). Densities, occupied fill and mixtures are game estimates rather than measured mesh mass. The collision footprint remains a conservative cylinder. Prototype mount/rider mechanics, command reinforcements, shared morale, detachable bosses, momentum impact damage, trample and body bowling remain deferred; classification tags do not pretend to enable those systems. The warg is an unridden warbeast. Exact prototype numerical parity is not claimed.

Gate evidence and the Claude handoff are recorded in [the review record](../../docs/reviews/2026-10-17-goblinkin-physicality.md). Native headless smoke does not verify GPU pixels; paired full-plan fixtures verify the native/browser geometry, and the browser captures provide the pixel evidence.
