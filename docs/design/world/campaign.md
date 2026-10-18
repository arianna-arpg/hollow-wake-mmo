# The campaign

A campaign is authored story told through generated content. The lore gives the first chapter: a new Weaver arrives at Ninewells, learns to Reach, takes a Calling, is sent to the Barrow at Fallow's Cross, is asked about the Hob in the Ashdown bottom-field, hears Sev's name in an inn, and begins to understand that every power in the Marches is measuring them for Skyfold's Vigil. This page says how beats like these are written so that they spawn generated places, events and agents, gate by discovery and deeds, and survive in a world that changes under them.

## Beats

A beat is content: an id, a chapter, whether it is **personal** (advances per character) or **worldly** (advances once per world, for everyone), its gates, what it requests of the world, what it offers the player, and what it concludes with. Beats form a graph by their gates, not a line, so chapters branch and converge and two beats can be open at once.

- **Gates** are expressions over the player's story flags, discoveries, reputation and level for personal beats, and over the ledger for worldly ones: a deed counted across all characters (a number of Snarls cleared in a region), a place's state, a faction's standing, an event's outcome, the calendar, or another beat concluded. Gates make the campaign respond to what players did, collectively and alone.
- **Requests** name what the beat needs from the world in the vocabulary of the other pages: a place of a kind under constraints (a lair in a mountain region within so many zones of the start town), an event of a kind over a scope, an agent of a kind with an intent (a warband marching on a town the player has discovered), an NPC with a dialogue at a place. Requests go through the same providers the Weft and the zone plan use, so the campaign's dragon has a generated cave in generated mountains and the campaign's army walks the generated roads. A request resolves deterministically from the world seed and the beat's id, so two worlds with one seed tell the same story in the same places.
- **Offers** are what the player sees: quests (hand-authored or templates with slots filled by the request's results), dialogue lines keyed to the beat, rumors, map marks, a notice.
- **Conclusions** are what the beat writes when it ends: story flags, ledger entries (a place state, an overlay, an agent disbanded, a unique event raised), reputation, rewards, and which beats it opens.

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

What is authored: the beats, their gates, their requests and offers, the dialogue they key, the pinned places and the names. What is generated: the places, the events, the agents, the quests' slots, the instances, the encounters and the consequences. The owner decides how many chapters are authored and whether worldly beats run per server world or are the same for every world; the plan lists the questions.

## Observing the campaign

`hollowwake campaign --seed <seed> --character <sheet>` lists the beats open, their gates with the values that hold or fail them, and what each open request resolved to, so a designer sees the story's state without a client. The chronicle records worldly beats as they conclude.

## Rules used

The `campaign` group: chapter order, the fail-forward policy defaults, the reach of retargeting, how many beats a character may hold open.
