# Entity content

An entity's pattern is its reusable body and combat kit. A wild encounter and a summon use the same pattern; the spawn path supplies allegiance, control and lifetime. Entity names are not branches in simulation code. The current prototype-derived roster and translation ledger live with the content and in `experiments/undead-entities/provenance.json`.

## Composition and classification

Templates extend the common creature components. Concrete patterns declare dimensions, level and attributes, available weaves, ordinary AI behavior, and wild rewards. Attribute formulas scale through the existing modifier engine. A kit must have enough resource regeneration to keep acting, and every damage role must demonstrate an actual AI-driven hit against a hostile body.

Hierarchical tags provide independent joins:

| Tag family | Meaning |
| --- | --- |
| `body.*` | Material or corporeal form, independently of species |
| `family.*` | Related bodies for content filters |
| `role.*` | The combat role its current kit can perform |
| `habitat.*` | Intended environments, checked against authored population references |
| `size.*` | Authored scale category for content filters; exact dimensions live in the physical profile |

Existing broad tags such as `undead`, `monster`, `summonable`, `beast`, `melee`, `ranged` and `caster` remain available to existing filters. Tags describe what is currently implemented. They do not grant unimplemented immunities, attacks, collision behavior or spawning by themselves. A habitat requires an actual biome spawn-table or point-of-interest inhabitant reference. `no_corpse` uses the existing corpse rule; it does not change movement or grant invulnerability.

## Wild encounters and owned bodies

A pattern's faction is its native wild allegiance. Summoned bodies keep that identity and combat kit, then receive the existing minion owner, inherited attributes and minion behavior. Relations follow the owner, and public snapshots carry that effective allegiance so contact marks agree with combat targeting. Summon birth removes the wild `Drops` component, including its experience reward. Creating and killing one's own summons must never mint the wild encounter's loot or experience.

Summoning presets live in weave content: caps and replacement groups, melee or ranged minion behavior, weighted body pools, contracts, reservations, decay and corpse consumption all use existing mechanisms. Prototype-specific court upgrades, custom passives and special attacks stay in the translation ledger until their mechanisms land. A substituted attack is named explicitly; importing a body does not imply exact prototype combat parity.

## Population

Biome spawn tables express roaming density, pack size, level windows and respawn time. Point-of-interest inhabitants express local populations under the existing place budget, with their level windows evaluated against the site's level including its authored offset. A place's `factions` filter restricts eligible regions; it does not assign the inhabitants' combat faction. Hostile ruins can appear within a settled region while their bodies retain their hostile pattern faction.

Population changes intentionally move generation goldens. Preserve seed-specific before/after evidence, verify the intended sites and level gates, and only then refresh those goldens. Climate and untouched chunk hashes should remain stable when their inputs have not changed.

## Appearance and provenance

Appearance definitions and provenance metadata are presentation or authoring information and never drive simulation decisions. Translate the prototype's anatomy, silhouette, equipment and material grouping into shared client families rather than assigning every new body a generic fallback. Shape and palette choices remain in appearance content. New geometry families register additively in both clients and the appearance validator, and reuse the shared rigs and equipment sockets.

The appearance contract governs geometry, directional cards, animation and accessibility. A reviewed shared fixture compares native and browser plans including worn gear, with tolerance for float precision. Browser screenshots demonstrate the rendered translation in both camera angles and grayscale; native headless checks verify behavior but do not claim native pixel coverage.

Related contracts: [minions](minions.md), [AI](ai.md), [appearance](appearance.md), [places](world/places.md), [networking](networking-and-persistence.md).

Physical creatures use the registered [physical body contract](physical-bodies.md) for dimensions, material density, mass and displacement. Allegiance is independent of species: warband beasts can share their handlers' faction without reclassifying unrelated wildlife.

## Assembled golems

Mobile golems are creatures with constructed bodies, distinct from deployed stationary devices. Their material mixture supplies mass, and their ordinary armor, poise and modifiers supply combat defenses. A stone, metal, crystal or ceramic surface does not imply elemental resistance, bleed immunity, repair or heat damage. The `assembly.*` classifier describes construction independently of `body.*` material and `family.golem`.

Unbound works use sparse point-of-interest populations with level windows. A legacy summon-only body may instead name bound service as its habitat when a real summoning preset supplies that path. Its player allegiance and lack of encounter rewards stay explicit. Shared summon pools can reuse wild golem patterns through the ordinary owner, behavior, reservation, respawn and exclusive-group mechanisms; the common summon path removes wild rewards. Material-specific tactics and reconstruction remain authoring intent until separately reviewed AI or combat mechanisms support them.

Named prototype golem bindings also use the shared Warden replacement group. Elemental resistance and blood leech, when translated from a source body, are explicit pattern modifiers tested through actual hits. They are independent of material density and appearance. A bound melee guardian can use the ordinary owner/order actions with a content-authored stopping distance so its longer-range spell cannot strand its close-range kit. This adds no tactical reasoning or environmental immunity.

Grafted biological constructs use their own family and encounter allegiance. A fixed Amalgamation pattern and sparse site do not imply the prototype's selectable body-part hunt, event progression or part-specific rewards; those require a separate event-system contract.

## Stature and warband identity

The player is the reference adult stature. Ordinary skeletal humanoids share that authoritative height; ordinary Goblins are roughly half as tall. Hobgoblins and exceptional brutes remain intermediate, while Orcs are adult-height or taller with substantially broader bodies. Rare and summoned scaling still uses the existing authoritative transform. Appearance-only scale jitter is disabled on the standard player and skeleton looks and profiled warband bodies; palette variation remains.

Species and allegiance are independent. Orc and Goblin warbands can be allied and share a site's inhabitants without losing their native factions. An overseer, slaver or commander role describes the authored encounter and combat kit, not a newly implemented capture, ownership or command system. Melee leaders close to a content-authored distance independent of their longer-range rally weave, so support skills cannot strand them outside their damaging attack's reach.

## Wildlife and incidental critters

Wildlife uses ordinary patterns, physical profiles and biome populations. `fauna` identifies wildlife; `ambient` and `critter` mark incidental non-attacking bodies. `prey` also lets existing minion acquisition filters pass them over. These tags do not supply ecological AI. Only eligible combat beasts opt into `quest.cull`; the cull template requires it alongside `beast`, so a shared species classifier cannot accidentally turn incidental animals or templates into kill contracts. Quest slot matching reads direct pattern tags, so that eligibility must be authored on concrete candidate definitions.

Ambient bodies extend a template without drops and carry an empty weave list. Defensive wildlife can carry existing melee skills, while their current behavior remains provisional under the AI contract. Ground locomotion is explicit: visual birds, amphibians and shoreline animals do not gain flight, swimming or climbing from their appearance or habitat. Physical fill and material densities are authored game approximations, not measured anatomy.
