# Golem review corrections

2026-10-18. PR #39, responding to Claude's review of `0f29605` at https://github.com/arianna-arpg/hollow-wake-mmo/pull/39#issuecomment-6246008175.

## Ownership before edits

This cleanup owns the golem/Amalgamation content, population and stance regressions, Stone Warden compatibility evidence and documentation. Shared overlaps are `docs/design/entities.md`, the prototype port table/ledger and world generation goldens; changes stay additive to the other entity owners' work. The entity-stack owner is correcting mass/shove, site level propagation and quest reachability in #31-34. Consume that published base instead of implementing a second runtime fix here.

Keep the Amalgamation creature and provisional fixed Suture Works. The owner explicitly deferred the full Bonewright hunt-and-graft event and broader encounter redesign.

## Corrections and evidence

- All five sites retain their authored offsets and have a first inhabitant eligible at `1 + offset`. Pinned tests preserve those offsets and require actual spawn points at region level 1, then verify higher-level occupants and replay. Natural placement tests no longer skip empty low-level sites.
- Both new factions explicitly oppose Wilds, Loosed, Tangled and Remnant. Tests check both directions and actual AI damage against Hobs and Remnant Shades without a test faction override.
- Site spacing is 64 m instead of 180/220 m. Low selection weights remain; explicit appropriate region sets further restrict placement. Natural witnesses show every site can coexist with another site within the formerly excluded 180 m radius.
- `habitat.bound_service` applies to any summon-only body backed by a real binding. Tests reject wild population references and exercise allegiance, replacements and reward-free birth/respawn. A shared bound-body template supplies repeated resources/classifiers.
- Stone Warden again extends `creature`, restoring inherited mana and regeneration. An independent copy of the original stats is compared at levels 1, 3 and 20. Its deliberate 80 to 1,905.904 kg mass migration, added construct tags and new geometry without scale jitter are now explicit. The actual owner-bound Warden attacks a native Remnant skeleton in its regression; an ownerless CLI duel is not the summoning path.
- Wild/salvaged golems now share `160 + (level - 1) * 14` base life and `7/12 + (level - 1) * 0.7/1.1` weapon damage. Fixed named bodies use 140-180 starting life with the same growth. Existing vigor links still apply. Mass, speed, armor, poise and kits remain distinct. Salvage remains deliberately rerollable under existing contract behavior; it no longer offers the former 2.3x base-life lottery. These are provisional specialist alternatives, not a claim of exact DPS parity with the legacy Warden or Bone Golem.
- Amalgamation now scales base life as `260 + (level - 1) * 30` and weapon damage as `10/16 + (level - 1) * 0.8/1.2`; XP is `28 + level * 8` (140 at authored level 14, previously 440). Its two shard rolls and one common roll remain provisional creature rewards. This does not expand the encounter or introduce phases, hunts or graft choices.
- Blood and Amalgamation source grave/poison chaos defenses map to Umbra consistently with the ledger and Poison Nova. Porter mappings now resolve Stone/Fire/Ice/Blood to their dedicated bodies, with a regression; Lightning stays explicitly an original Storm adaptation.

Historical evidence remains dated to its original head; it is not evidence for these corrections. Current gate evidence will be recorded after consuming the corrected shared foundation.

## Local checkpoint before shared foundation

The original 84 focused golem cases pass. The broader suite passes 795 core cases excluding the five new live-site-level dependency regressions, and 39 server cases. After keeping summon eligibility and AI on concrete bodies, all 117 native appearance/logic tests and 269 web tests pass. Both intermediate appearance failures are retained. Browser production build passes with the existing size advisory. Validate reports zero errors/warnings, porter mapping tests are 11/11, protocol check is current, origin loads for 200 ticks, and the Amalgamation/Stone duel completes.

The five dependency regressions intentionally fail before the shared fix: kiln expects level 3 but creates 4, quarry 5/6, foundry 7/9, sawmill 4/5 and Suture Works 9/14. They load actual living residents, not just spawn specifications, and also check a level-21 region. Do not call this checkpoint merge-ready.

`experiments/golems/evidence/review/world-review.txt` explains every sampled placement/quest change before refreshing goldens, including the changed ruin/camp stamp in chunk(13,6). Initial golden failures are retained. The actual Warden resources match the baseline: life 122/142/312 and mana 47/57/142 at levels 1/3/20; effective mana regeneration 1.32 and stamina regeneration 12 throughout. The salvage pool now has 216 life at level 1 and 482 at level 20 across all eight rolls, before summon modifiers.

Acknowledged by: pending Claude rereview.
