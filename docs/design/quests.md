# Quests

## Two sources, one shape

Hand-authored quests live in `quests` with a giver (a pattern id or a point-of-interest id), level, prerequisites, objectives, rewards and text. Generated quests come from `quest_templates` instantiated by the zone planner for each point of interest that lists them: slots are filled with real nearby points of interest (by tags and distance), patterns (by tags and optional local populations) or items (by tags), and `$slot` placeholders resolve into a concrete `QuestDef` registered with the zone's quest service. Both kinds flow through the same service.

## Generated target eligibility

A slot's optional `level_delta: {min, max}` bounds target level minus quest level (giver level plus template offset). POI slots use the site's effective level. Pattern slots with `local_population: true` use actual generated spawn points within `within` of the giver, checking an explicitly site-scaled spawn's level or otherwise its pattern's level. Chunks are searched nearest first with stable coordinate ties; matching species in the first qualifying chunk are sorted before the seeded selection. This favors nearby available targets, rather than sampling every species globally. Each slot inspects at most `world.gen.quest_population_chunks` nearest chunks, counting cached chunks as well. If nothing qualifies before that limit, the template produces no offer, even when a qualifying population may exist farther away inside `within`. The limit bounds synchronous planning work; it never allows an absent, out-of-radius or wrong-level target.

Population inspection uses the normal terrain/landmark/flora/spawn pipeline and independent chunk RNG, without recursively planning zone quests. The planner reuses spawn facts for adjacent slots during one planning run. Shipped combat and scouting templates opt into level windows; creature culls and knot foes also require a local population. These are content constraints, not hard-coded creature or faction exceptions.

## Objectives

Objective types are a registry matching simulation events: `kill` (victim pattern or tags, credited to the killer or the owner of its minions), `collect` (bag count by base or tags, consumed on turn-in), `reach` and `discover` (point-of-interest discovery by walking into it or using its marker), `interact` (interactable kind or reference), `craft`, `bind`, `event` (custom events raised by effects), `level`, `cast` (by weave tags). Optional objectives do not block completion.

## Flow

Opening a quest giver raises `QuestsOffered` with offers (matching giver, prerequisites met, level within slack, not active or done unless repeatable) and quests ready to turn in. Accept, progress events, `QuestCompleted` when all required objectives are met, turn in at the giver for rewards: experience through the progression curve, items and loot tables, currency, reputation, unlocked weaves.

## Rules used

`quests.max_active`, `quests.level_slack`, `world.gen.quest_population_chunks` (zero disables local-population offers). Rewards' experience expressions read `level` and `quest_level`.
