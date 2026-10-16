# Quests

## Two sources, one shape

Hand-authored quests live in `quests` with a giver (a pattern id or a point-of-interest id), level, prerequisites, objectives, rewards and text. Generated quests come from `quest_templates` instantiated by the zone planner for each point of interest that lists them: slots are filled with real nearby points of interest (by tags and distance), patterns (by tags) or items (by tags), and `$slot` placeholders resolve into a concrete `QuestDef` registered with the zone's quest service. Both kinds flow through the same service.

## Objectives

Objective types are a registry matching simulation events: `kill` (victim pattern or tags, credited to the killer or the owner of its minions), `collect` (bag count by base or tags, consumed on turn-in), `reach` and `discover` (point-of-interest discovery by walking into it or using its marker), `interact` (interactable kind or reference), `craft`, `bind`, `event` (custom events raised by effects), `level`, `cast` (by weave tags). Optional objectives do not block completion.

## Flow

Opening a quest giver raises `QuestsOffered` with offers (matching giver, prerequisites met, level within slack, not active or done unless repeatable) and quests ready to turn in. Accept, progress events, `QuestCompleted` when all required objectives are met, turn in at the giver for rewards: experience through the progression curve, items and loot tables, currency, reputation, unlocked weaves.

## Rules used

`quests.max_active`, `quests.level_slack`. Rewards' experience expressions read `level` and `quest_level`.
