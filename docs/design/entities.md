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

A pattern's faction is its native wild allegiance. Summoned bodies keep the anatomy and combat kit, then receive the existing minion owner, inherited attributes and minion behavior. Their identity and `faction.*` stat tag are stamped with the owner's effective faction at birth; the pattern definition keeps its native faction. `minions.encounter_tags` removes encounter-only tag roots and descendants from owned bodies. Relations follow the owner, and public snapshots carry that effective allegiance for presentation. Contact-mark color remains the client's faction palette policy; it is not an authoritative stance classification. Summon birth removes the wild `Drops` component, including its experience reward. Creating and killing one's own summons must never mint the wild encounter's loot or experience.

Summoning presets live in weave content: caps and replacement groups, melee or ranged minion behavior, weighted body pools, contracts, reservations, decay and corpse consumption all use existing mechanisms. The new presets are available to authoring and tests, not granted by the current calling progression. Independent caps have no common undead pool; resource costs and progression remain provisional. Prototype-specific court upgrades, custom passives and special attacks stay in the translation ledger until their mechanisms land. A substituted attack is named explicitly; importing a body does not imply exact prototype combat parity.

## Population

Biome spawn tables express roaming density, pack size, level windows and respawn time. Point-of-interest inhabitants express local populations under the existing per-candidate place weight multipliers, with their level windows evaluated against the site's level including its authored offset. Site occupants use that level at birth and respawn; naturally selected sites must have an eligible inhabitant. Pinned authoring overrides can intentionally bypass placement eligibility. A place's `factions` filter restricts eligible regions; it does not assign the inhabitants' combat faction. Hostile ruins can appear within a settled region while their bodies retain their hostile pattern faction.

Region budgets currently multiply every matching candidate's weight. Adding ruin definitions therefore changes the total weight of ruins relative to other kinds; it does not partition a fixed ruin allocation.

Population changes intentionally move generation goldens. Preserve seed-specific before/after evidence, verify the intended sites and level gates, and only then refresh those goldens. Climate and untouched chunk hashes should remain stable when their inputs have not changed.

## Appearance and provenance

Appearance definitions and provenance metadata are presentation or authoring information and never drive simulation decisions. Translate the prototype's anatomy, silhouette, equipment and material grouping into shared client families rather than assigning every new body a generic fallback. Shape and palette choices remain in appearance content. New geometry families register additively in both clients and the appearance validator, and reuse the shared rigs and equipment sockets.

The appearance contract governs geometry, directional cards, animation and accessibility. A reviewed shared fixture compares native and browser plans including worn gear, with tolerance for float precision. Browser screenshots demonstrate the rendered translation in both camera angles and grayscale; native headless checks verify behavior but do not claim native pixel coverage.

Related contracts: [minions](minions.md), [AI](ai.md), [appearance](appearance.md), [places](world/places.md), [networking](networking-and-persistence.md).

Physical creatures use the registered [physical body contract](physical-bodies.md) for dimensions, material density, mass and displacement. Allegiance is independent of species: warband beasts can share their handlers' faction without reclassifying unrelated wildlife.

## Assembled golems

Mobile golems are creatures with constructed bodies, distinct from deployed stationary devices. Their material mixture supplies mass, and their ordinary armor, poise and modifiers supply combat defenses. A stone, metal, crystal or ceramic surface does not imply elemental resistance, bleed immunity, repair or heat damage. The `assembly.*` classifier describes construction independently of `body.*` material and `family.golem`.

Unbound works use sparse point-of-interest populations with level windows. Any summon-only body may instead name `habitat.bound_service` when a real summoning preset supplies that path. Such a body must have no wild biome or site population reference; tests must exercise its named binding, owner allegiance and reward-free birth/respawn. Its player allegiance and lack of encounter rewards stay explicit. Shared summon pools can reuse wild golem patterns through the ordinary owner, behavior, reservation, respawn and exclusive-group mechanisms; the common summon path removes wild rewards. Material-specific tactics and reconstruction remain authoring intent until separately reviewed AI or combat mechanisms support them.

Named prototype golem bindings also use the shared Warden replacement group. Elemental resistance and blood leech, when translated from a source body, are explicit pattern modifiers tested through actual hits. They are independent of material density and appearance. A bound melee guardian can use the ordinary owner/order actions with a content-authored stopping distance so its longer-range spell cannot strand its close-range kit. This adds no tactical reasoning or environmental immunity.

Grafted biological constructs use their own family and encounter allegiance. A fixed Amalgamation pattern and sparse site do not imply the prototype's selectable body-part hunt, event progression or part-specific rewards; those require a separate event-system contract.

## Stature and warband identity

Patterns may bind simulation damage anatomy through `hit_body`, independently of physical mass and conservative movement clearance. Owned bodies retain the same profile as wild bodies. Live height scales its parts; neither allegiance nor appearance selects collision. See [damage anatomy](physical-bodies.md#damage-anatomy).

The player reads the reference adult stature from `physicality.reference_height`. Ordinary skeletal humanoids share that authoritative height; ordinary Goblins are roughly half as tall. Hobgoblins and exceptional brutes remain intermediate, while Orcs are adult-height or taller with substantially broader bodies. Rare and summoned scaling still uses the existing authoritative transform. Appearance-only scale jitter is disabled on the standard player and skeleton looks and profiled warband bodies; palette variation remains.

Species and allegiance are independent. The broad `goblinkin` tag intentionally includes Orcs for shared ancestry filters; `family.orc` and `orc` select Orc anatomy, while `faction.*` selects allegiance. Orc and Goblin warbands can be allied and share a site's inhabitants without losing their native factions. An overseer, slaver or commander role describes the authored encounter and combat kit, not a newly implemented capture, ownership or command system. Leaders assigned `close_melee` close to a content-authored center distance independent of their longer-range rally weave, so support skills cannot strand them outside their damaging attack's reach.

## Wildlife and incidental critters

Wildlife uses ordinary patterns, physical profiles and biome populations. `fauna` identifies wildlife; `critter` marks incidental non-attacking bodies. `ambient` distinguishes non-monster population and can also describe defensive wildlife. `prey` also lets existing minion acquisition filters pass them over. These tags do not supply ecological AI. Only eligible combat beasts opt into `quest.cull`; the cull template requires it alongside `beast`, so a shared species classifier cannot accidentally turn incidental animals or templates into kill contracts. Quest slot matching reads direct pattern tags, so that eligibility must be authored on concrete candidate definitions.

Critters extend a template without drops and carry an empty weave list. Defensive wildlife can carry existing melee skills, while their current behavior remains provisional under the AI contract. Ground locomotion is explicit: visual birds, amphibians and shoreline animals do not gain flight, swimming or climbing from their appearance or habitat. Physical fill and material densities are authored game approximations, not measured anatomy.
