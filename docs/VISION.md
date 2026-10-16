# Vision

Skein is an online action RPG with the feel of a classic isometric MMO, the horizon of a seamless open world, and the build depth of the deepest modern action RPGs. It is built on one commitment: **the game is data, and the engine is a set of mechanisms that data composes.** Emergent play comes from systems that combine, not from features that were scripted.

## The experience

You make a character, choose a Calling as a starting point rather than a cage, and walk out of a hamlet into a land that goes on forever. The land is generated, but it is not random noise: regions have character, zones have points of interest that make sense where they stand, towns give quests about the actual ruin on the actual hill nearby, Knots tangle and must be cleared, Founts can be bound and a Holding raised around them with decrees that change the land itself. You fight with weaves you compose from forms, Threads and glyphs. You find and reweave gear. You get stronger, and your build gets stranger, in whatever direction you push it.

By default you see this from above, isometric, like the games the genre began with. You can also drop the camera behind your character's shoulder, because the world was fully three dimensional all along: there are heights, bridges, tower floors, cliffs to jump down and ledges to jump up.

## The pillars

1. **Data over code.** Attributes, Threads, statuses, forms, glyphs, weaves, items, affixes, loot, patterns, behaviors, biomes, regions, points of interest, landmarks, quests, recipes, procedures, decrees, structures, world events, rules: all content. Code implements mechanisms and reads content. Adding a monster, a biome, a quest template or a decree is a content change.
2. **One modifier engine.** Every number flows through one layered, tag-scoped fold: base, flat, links, increased, more, override, clamp. Characters, monsters, weaves, items and holdings all use it. There is no second stat system anywhere.
3. **Tags are the universal join.** Modifiers scope by tags, glyphs gate by tags and mechanisms, affixes spawn by tags, quests count by tags, biomes and points of interest match by tags, victims expose tags to attackers. A new tag is free and immediately meaningful everywhere.
4. **Composition over bespoke.** A weave is a form plus Threads plus glyphs. An effect graph is a tree of small effect types. A point of interest is a template plus a landmark plus inhabitants plus quest templates. Content composes; the engine does not know what a fireball is.
5. **One pipeline for everyone.** Players, monsters and summons cast through the same weave runtime, take damage through the same pipeline, die through the same service. Nothing is special-cased for the player.
6. **Server authority, client presentation.** The server simulates; the client sends intents and draws state. The client runs the same core to generate terrain and will run it for prediction, but it never decides outcomes.
7. **A seamless, deliberate world.** The world is infinite and deterministic from a seed. Regions carry macro intent, zones carry points of interest and quests, chunks carry terrain and spawns. Hand-placed content pins over generation wherever a designer wants it. World events and decrees change generation itself through overlays.
8. **Observable and deterministic.** Every roll derives from a seed; a fight, a drop or a zone can be replayed. Tools can validate content, simulate duels, generate zones and probe servers without a client.

## What emergent means here

Emergence is not a feature; it is what happens when orthogonal systems meet. A Bloom decree raises Bloom density in a Holding, which shifts biomes toward growth, which changes flora and spawns, which changes what drops and what quests the nearby hamlet generates. A glyph that converts weapon blows to Ember makes Ember affixes, Ember passives and burning ailments all matter to a sword fighter. None of those links were written as features. They fall out of tags, modifiers, overlays and templates meeting in one simulation.

## What this is not

Not a graphics showcase. Presentation is deliberately placeholder until the systems are proven. Not a scripted theme park: hand-placed content exists, but it is pinned into a generated world, not the other way around. Not a client-trusting game: nothing the client says is believed.
