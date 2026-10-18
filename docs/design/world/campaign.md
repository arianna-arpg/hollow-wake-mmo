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

Every world is its own generation: a new server, shard or reset draws its own foes and tells its own story, and the players on it are in a race. The world's campaign is a line, the ordered list of the foes it has lost, extended by whoever exterminates the next one. The character at the front chooses freely among the foes still alive and writes the line; everyone behind walks it in the order it was written, following in the footsteps of those who went first. The finishing blow is worldly: the chronicle names the one who struck it, a memorial pin stands where the stronghold stood, and the pool may raise a new foe at the end of the line so it never runs out.

### Stages

The line divides the world's story into stages: stage zero is the world with every drawn foe alive and none empowered, and each loss on the line opens the next. Every character stands at exactly one stage, the number of Odysseys they have finished in the line's order, and what a character sees of the foes is the world of their stage: at stage zero the Goblinkin camps stand and the Gnolls are quiet; at stage one the Goblinkin are ruins, the Gnolls are empowered and march from their cities; at the last stage before the end the last foe has grown into the wartorn country its empowerment makes of the far land. Because the order is total, a world has only as many stages as the line has entries, never one per combination of deaths, and the empowerment of each foe is defined once, by what died before it. That is the reconciliation of two players killing in different orders: they cannot. Only the front writes the line; everyone else inherits the order and the empowerments it produced, and two characters at the same stage see the same world.

Everything that is not the foes is shared across stages: the land, the weather and the seasons, the towns and the Knots, Holdings and claims, the populations of beasts and folk, the chronicle. The foes' presence is what the stage decides: their stocks and agents, their camps, strongholds and warfronts, the places they occupy or burn, the overlays and pins their empowerment lays on the land. Those entries of the ledger carry a stage range, and generation takes a stage as an input for them and for nothing else, so a stage-zero client and a stage-two client generate the same valley with and without the goblin camp from the same seed and the same ledger.

### Phases

A zone is simulated per stage where characters of that stage are: the zone key carries the stage, characters at different stages at the same place are in different phases and do not see each other, and a zone phases only where the stage changes something in it, so most of the world is one phase for everyone. A party shares its leader's phase: a member behind the leader sees the leader's world, helps, and advances no beat of a foe that is dead there; a member ahead visits a world where a foe they finished still stands, and takes no credit. Knots and towns the stage does not touch are one place for all stages, so players at different stages still meet inside a Snarl.

The front stage is live: the Weft simulates its foes as it simulates everything else. When the front extends the line, the Weft seals the stage it leaves: the foes' state at that moment (stocks, agents, camps, strongholds, empowerment) and the journal of what they did while the stage was current, extended by the resolvers to the stage's set length where the front was quick. A past stage's phase is built from the sealed stage, and its foes act out the sealed journal on a loop on the world clock, so everyone at that stage sees the same march on the same day and a character who comes later finds the Gnolls as the front found them. Deeds in a phase are real: a camp burned at stage zero is burned for every stage-zero character, and the sealed stage records it.

### Walking the line

A character partway through a foe's Odyssey when the front finishes that foe loses nothing: their phase does not change, every beat done and every counter stays, and the foe's open-world quests continue in their world. What the line decides is only the order of finishing blows: the finishing beat of an Odyssey is gated on the foe being the next loss at the character's stage, so a character at stage zero who is halfway through the Bandits finishes the Goblinkin first, then the Gnolls, then returns to the Bandits, who by then are the Bandits of that stage. An Odyssey's objectives are never completed by the world's fact and never concluded by a fallback flag, which is how other quests behave when the world overtakes them.

Two rules keep the line a line when several characters stand at the front. The finishing beat is a lease: entering a foe's last stand takes the world's one lease on its next loss, naming the foe, and the lease expires if the last stand is abandoned; a second character may enter the same foe's last stand and finishing it after the first still finishes that Odyssey for them, but no one enters a different foe's last stand while the lease stands. Credit for the blow goes to the lease holders whose progress is at the front; a party member behind them helps without being credited. A foe's stock never falls to nothing by the Weft's own arithmetic: starvation and defeat leave a last stand standing until a finishing blow takes it, so every loss on the line has a name.

The line is the decided model; the alternative, where an exterminated foe's chapter closes for everyone and latecomers find only its ruins, is recorded in the plan with the reasons it was not taken.

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
