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
