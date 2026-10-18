# The campaign

A campaign is authored story told through generated content. The lore gives the first chapter: a new Weaver arrives at Ninewells, learns to Reach, takes a Calling, is sent to the Barrow at Fallow's Cross, is asked about the Hob in the Ashdown bottom-field, hears Sev's name in an inn, and begins to understand that every power in the Marches is measuring them for Skyfold's Vigil. This page says how beats like these are written so that they spawn generated places, events and agents, gate by discovery and deeds, and survive in a world that changes under them.

## Beats

A beat is content: an id, a chapter, whether it is **personal** (advances per character) or **worldly** (advances once per world, for everyone), its gates, what it requests of the world, what it offers the player, and what it concludes with. Beats form a graph by their gates, not a line, so chapters branch and converge and two beats can be open at once.

- **Gates** are expressions over the player's story flags, discoveries, reputation and level for personal beats, and over the ledger for worldly ones: a deed counted across all characters (a number of Snarls cleared in a region), a place's state, a faction's standing, an event's outcome, the calendar, or another beat concluded. Gates make the campaign respond to what players did, collectively and alone.
- **Requests** name what the beat needs from the world in the vocabulary of the other pages: a place of a kind under constraints (a lair in a mountain region within so many zones of the start town), an event of a kind over a scope, an agent of a kind with an intent (a warband marching on a town the player has discovered), an NPC with a dialogue at a place. Requests go through the same providers the Weft and the zone plan use, so the campaign's dragon has a generated cave in generated mountains and the campaign's army walks the generated roads. A request resolves deterministically from the world seed and the beat's id, so two worlds with one seed tell the same story in the same places.
- **Offers** are what the player sees: quests (hand-authored or templates with slots filled by the request's results), dialogue lines keyed to the beat, rumors, map marks, a notice.
- **Conclusions** are what the beat writes when it ends: story flags, ledger entries (a place state, an overlay, an agent disbanded, a unique event raised), reputation, rewards, and which beats it opens.

## The foe factions

Every world draws its enemies. The content holds a pool of **foe factions**: the hostile powers a campaign can revolve around, each with its agent kinds, its populations, its place kinds (camps, towns, strongholds), its lieutenants and its authored questline, called its **Odyssey** after the prototype's word for it. When a world is created the Weft draws a number of them from the pool by the world seed (a rule sets how many), places their homes beyond the start region's horizons through pins, and seeds their populations. From then on they are the campaign's spine and the Weft's antagonists at once: they raid, settle and march like any faction, and their questlines are the authored chapters of the world.

An Odyssey is authored in its beats and generated in everything they touch: the beats say that the faction has a stronghold, a captain, a cruelty and a weakness, and the world supplies a generated stronghold in generated country, generated camps along generated routes, generated agents from its populations, and quests whose slots fill with them. Players choose the order: every drawn faction's Odyssey opens on the discovery of its first generated place, and nothing forces one before another until the world's line says so. Defeating one changes the rest by rule: the survivors' populations flee or are absorbed, a rival takes the emptied country, the remaining factions grow bolder or wearier, the pool may raise a new foe into the emptied country, and the worldly beats that end the chapter wait for the number of factions the rule names. The pool is content, so the lore names the foes in Aureth's idiom (the Tangled's packs, the Remnants' columns, the marauders of the hedgerows, the Unmade) and a mod adds its own.

## One line, many walkers

Every world is its own generation: a new server, shard or reset draws its own foes and tells its own story, and the players on it are in a race. The world's campaign is a line, the ordered list of the foes it has lost, extended by whoever exterminates the next one. Every character walks that line at their own pace, and the character at the front writes it. The rule that keeps a shared world consistent is that a character's progress is always a prefix of the world's: a foe the world has already lost may be fought by a latecomer only in the order the world lost it, and only through the faction's instances (its stronghold, its last stand) and remnant pins (a band that fled the extermination; a camp the world remembers as a ruin but a beat raises as a private instance), never in the open world, where the faction is gone and its country belongs to whoever took it. The character at the front chooses freely among the foes still alive, and the finishing blow is worldly: populations end, places fall abandoned, the chronicle names the one who struck it, a memorial pin stands where the stronghold stood, the escalation rules move the survivors, and the pool may raise a new foe so the line never runs out.

A latecomer's chapter is therefore the same beats in the same order with the open-world parts phased into instances, which every Odyssey is written to allow: an Odyssey that cannot be completed from instances and pins alone is a content error validation reports. The line gates only the finishing beat of each Odyssey; the open-world quests of every living foe stay open to everyone, so a character may be partway through several Odysseys at once. A character partway through an Odyssey when the front finishes it keeps every beat done; the beats left phase into instances and pins, and the log says the world has moved on. Deeds in the open world count for everyone (a camp a latecomer burns is burned); deeds in a private instance count for the walker. The line is the decided model; the alternative, where an exterminated foe's chapter closes for everyone and latecomers find only its ruins, is recorded in the plan with the reasons it was not taken.

## The world beat and the personal beat

Worldly beats are the Weft's: the Loosening quickens, Skyfold's Vigil is called, the Fray stirs. They advance by the ledger and they change the world for everyone, through the same consequences any event leaves. Personal beats are the character's: the first Barrow, the first Hob, the first Vigil at Hobsmere. They advance by flags and discoveries on the character and they change nothing for others, though they may request generated content: a personal beat that needs a Snarl gets a private instance of one.

Where the two meet, the rule is that a personal beat may read the world but never freeze it: the new Weaver's first Barrow is at Fallow's Cross whatever the Weft has done, because the Barrow is pinned and indestructible, but the Hob in the bottom-field is a generated Tangle, and if the Weft has already resolved it the beat fails forward to another Hob within reach.

## Gates by discovery and deeds

Discovery is the first currency: a beat may open only when its place has been found by walking, by rumor or by the map. Deeds are the second: killed, bound, cleared, re-lit, held, delivered, recovered, each a counted ledger fact or a story flag. The quest system's objective registry supplies the counting, the ledger supplies the facts, and the beat's gate reads both; a beat never polls a system. Worldly gates may require deeds across many characters, which is how a server's population opens the Fray together.

## Coexistence with an emergent world

The campaign proposes; the world disposes. Three rules keep an authored story alive in a world that moves on its own:

- **Beats request kinds, not instances.** A beat says a lair, not this cave; the world finds or makes one that fits, and records the choice in the ledger so later beats find the same lair.
- **Beats fail forward.** Every request names what to do when the world cannot satisfy it or has overtaken it: retarget within reach, substitute a kind, or conclude with the fallback flags so the next beat opens anyway. A beat that would otherwise hang is a content error that validation reports.
- **Authored pins are few and durable.** The lore fixes the start town, the Firstworks, the Lanterns, the Last Stitch and the named Founts; those are pinned and marked indestructible or protected, and beats may lean on them. Everything else a beat touches is generated and may change, including by other players, and the beat's text is written for that (a rumor says the Knot near the mill, not a fixed name).

Chapters are states of the world as much as of the player: a chapter's opening beat may change region archetypes' weights for new generation, raise standing events, or open regions that were hostile (the Fray stops widening, or widens).

## Authored versus generated

What is authored: the pool of foe factions and each one's questline, the personal first chapter, the beats' gates, requests and offers, the dialogue they key, the pinned places of the lore and the names. What is generated: which foes a world has, where their homes and strongholds stand, the places, the events, the agents, the quests' slots, the instances, the encounters and the consequences. Worldly beats run per world: each world draws its own foes and tells its own story at its own pace.

## Observing the campaign

`hollowwake campaign --seed <seed> --character <sheet>` lists the beats open, their gates with the values that hold or fail them, and what each open request resolved to, so a designer sees the story's state without a client. The chronicle records worldly beats as they conclude.

## Rules used

The `campaign` group: chapter order, the fail-forward policy defaults, the reach of retargeting, how many beats a character may hold open.
