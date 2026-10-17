# Port ledger: the prototype's combat content on Hollow Wake MMO

The [parity analysis](hollow-wake-gap.md) compared the prototype's systems with ours and the packages built the mechanisms. This ledger is the content side of that work: one row per prototype skill, support, status, proc, passive notable and item family, saying what it became here, which mechanism carries it, whether it is ported, and what proves it. The prototype is `arianna-arpg/arpg-game-world`; its skills, supports, statuses, procs, passives and items are the reference, never its code. Vocabulary: a prototype skill becomes a **preset weave** (a form, Threads and glyphs with a graph of effects), a support a **glyph**, a status a **status**, a proc a **proc**, a passive notable a node of our own constellation, an item family a **base**, an **affix family** or a **unique**.

## Statuses in the tables

- **ported**: the entry exists here with its mechanics and numbers converted.
- **ported (difference)**: ported, with the difference named in the note (the legend below expands the codes).
- **seam**: waits on an engine seam named in the note, built in its own pull request as a reusable registered mechanism, never as a special case for one entry.
- **blocked**: waits on an open row of the parity analysis named in the note (rows the plan listed as open: C11, C20, C23, C26, C27, C29, C33, A13, E15, G17, H7, D21, I9, I13).
- **not ported**: deliberately not ported, with the reason: a monster kit (the bestiary port brings its kit), or a fabric the plan put later (companions, grabs, possession, clouds, lightwells, fog, orbs, ledgers, the fourth wall, time, flasks).
- **to author**: passives only; the constellation is authored in our themes (question Q17), and the row records the notable's mechanism so the authoring pass places it.

Note codes: `req` the prototype's attribute requirements go on the codex when the item slice lands; `grow` level growth is the form's formula, not the prototype's per-level table; `tree` the prototype's per-skill tree is not carried (trees belong to forms here); `thr` level thresholds are not carried; `inn` innate modifiers became preset modifiers; `kit` a monster kit skill; `seat1` the prototype's eight per-seat graft affixes fold into one affix on the first rack seat; `sum:ours<theirs` the summon raises our pattern in place of the prototype's monster; `fabric` a world or fabric state outside this port; `ult` a super art keeps its price floor as a cooldown and no cut-in; `taps` passive charge taps become a proc; `drop:` a modifier line whose stat has no attribute here; `roster:` the status already existed in our roster; `third:` a third damage type folded into the first two; `pct-cost` a pool-scaled cost through the cost.pct_max attributes.

## The mapping

The porting script `scripts/port/port-prototype.mjs` holds the whole mapping as tables (`port-tables.mjs`) and runs once over a snapshot of the prototype's catalogs to write drafts that are then authored by hand; nothing in it runs at game time, and no factor survives into content. Regenerating adds rows and drafts; the statuses in the tables below are kept by hand as the slices land.

```bash
gh repo clone arianna-arpg/arpg-game-world <folder> -- --depth 1                       # the prototype, outside this repository
npx --yes tsx@4 scripts/port/dump-prototype.mts <folder> prototype.json                 # its catalogs as one JSON snapshot
node scripts/port/port-prototype.mjs prototype.json <drafts-folder>                     # drafts in our content shapes, the ledger tables, a report of what did not map
node --test scripts/port/port-prototype.test.mjs                                        # the mapping on a synthetic snapshot
```

- **Units.** The prototype measures in screen units; ours is cells. Three anchors agree within a third: a body radius of 15 against our 0.45, a basic melee reach of 55 from the attacker's edge against Strike's 1.9, a walking pace of 200 a second against our 5. The script converts every distance, speed, radius and knockback once at 32 screen units per world unit, which keeps a 55-unit swing at 1.7, a 95-unit nova at 3.0 and a 460-unit cast at 14.4, beside the hand-authored forms. Times and angles are the same in both.
- **Damage types to Threads by role.** Physical is Stone, fire Ember, cold Tide, lightning Storm; chaos is Umbra where the entry's theme is decay, the grave, poison or a curse, and Void otherwise. A skill's Threads come from its damage dice, largest first, two at most; a third type folds proportionally into the first two, preserving total damage, and is noted. Attacks read the weapon's dice as our attack forms do, with `damage.effectiveness` carrying the ratio of the prototype's dice to its basic Cleave; spells carry the mean of their dice as `base_damage` and keep the spread as `lerp(lo, hi, roll)` over it. Attack costs become stamina at three times the prototype's mana cost; spell costs stay mana.
- **Statuses to our roster.** The prototype's burn, bleed, chill, frozen, shock, stun, poison, scalded, hemorrhage, impaled, doom, vulnerable, weaken, brittle, exposed, blind, disarm, silence, rooted and taunted are the roster's burning, bleeding, chilled, frozen, shocked, stunned, poison, scalding, hemorrhage, impaled, doom, vulnerable, weakened, brittle, exposed, blinded, disarmed, silenced, rooted and taunted; the rest keep their ids as new statuses in our vocabulary, and the states of fabrics outside the port (water, clouds, vents, the fourth wall, time) are not ported. A damaging status sized from a hit reads `last_damage` times the prototype's magnitude; a status's `hitMagnitude` becomes its `combat.ailment_magnitude` rule.
- **Stats to attributes.** Plain stats map one to one where ours has the attribute (`damage`, `effect.duration`, `area.radius`, `cast.speed`, the resistances and penetrations per Thread, `leech.life`, `on_kill.life`, `minion.damage` and the rest of the table); percent regeneration becomes a link from the maximum. Generated families map by pattern: `apply_<status>` to `status.<status>.chance`, `damageVs_<status>` to `damage` scoped by `vs.<status>`, `convert_<a>_<b>` to `convert.<a>.<b>`, `addedMin` and `addedMax` to one `added.<thread>`, `proc_<id>` to `proc.<id>`, `dotLeech_<status>` to `status.<status>.leech`, `popPower_<status>` to `status.<status>.pop`, `slotGraft_<n>_<glyph>` to `glyph.slot<n>.<glyph>`, `chargeCap_<c>` to `charge.<c>.max`, `takenAs_<a>_<b>` to `taken_as.<a>.<b>`. A stat with no attribute here (the mass and grab fabrics, orbs, flasks, stealth, luck, conduits, mirages, proc power dials) drops its line and the row says so.
- **Tags, mechanisms, triggers and conditions.** `aoe` is `area`, `warcry` is `shout`, damage-type tags come from the Threads, `vs:<x>` is `vs.<x>`, and the tags the prototype folds on at load (construct, conjure, throng) are not authored. Mechanism gates map onto our probes (`strikes`, `cooldown`, `engagement`, `displaces`, `bankless`, `guard`, `landing`, `surface:standing`, `affliction:<status>`, `flight:mints` to `flight:children`, `periodicConstruct` to `periodic_construct`, `status:power` to `status`). Cast-on-X triggers map onto our trigger kinds (`meleeHit` to `hit` with the melee tag, `damageTaken` to `hurt`, `channelFinish` to `channel_end`, `statusApply` to `status_applied`, `guardBeat`, `flawless`, `crit`, `block`, `kill`); proc triggers likewise (`esBreak` and `poiseBroken` to `broken`, `poiseBreakDealt` to `break`, `chargeGain` to `charge_gained`, `minionDeath` to `minion_died`, `lastGasp` to `cheat_death`, `pulse` to `interval`, `struck` to `hurt`). Conditions become expressions over the bearer (`lowLife` is `has("low_life")`, `poiseBroken` is `has("pool.poise.broken")`, `notHurtRecently` is `!has("recently_hurt")`). Victim scopes that are not statuses become victim tag rules (`closeRing` is `near`, `elite`, `higher_level`, `heavier`, `immobile`, `spent`, `rooted`, `faltering`), one line each in `combat.victim_tags`.
- **Shells.** The prototype ships finished skills; ours composes. Each ported skill is a preset on a shell form named for its delivery (`swing`, `shot`, `missile`, `burst`, `breath`, `smite`, `lay`, `hail`, `boon`, `cry`, `stride`, `vault`, `flicker`, `call`, `pact`, `plant`, `trap`, `mantle`, `spark`), with a cast-mode suffix for channels, guards, draws, surges and timed beats, and an instant twin where the prototype's cast is instant. A shell carries targeting, cast mode, mechanisms and the structural tags, and reads its cast time, cooldown, costs, channel beat, guard pool and reservation from the preset's attributes (`cast.time`, `cooldown.base`, `cost.base.<resource>`, `channel.*`, `guard.*`, `reserve.*`, `upkeep.*`), so one shell serves every preset of its kind; the preset carries the graph. A form is added by hand only where no shell composes a delivery, and then as a reusable form.
- **Summons and constructs.** A prototype monster a summon raises becomes the nearest of our patterns (the skeleton rosters are the remnants, golems the stone warden, sprites and swarms the sprig, imps the cinder imp, doubles the shade echo); a construct that casts a skill becomes a device pattern of ours carrying the ported weave. Every such row is a named difference.

## Seams this port needs

Each is a reusable registered mechanism in the package that owns the files, in its own pull request with tests, never a special case for one entry; the rows that wait on it say `seam:` with its name.

| Seam | What it is | Where |
|---|---|---|
| preset tags and behaviors | A preset carries tags and the casting behaviors a glyph can carry (gates, a spend, a bank of uses, a convert, a meta action, worn modifiers, a follow-up, a cycle, a self stack, a combo chain, a toggle's deactivation), folded between its form and its glyphs. Without it every prototype skill with such a behavior needs a one-skill form. | the weave compiler (`Weaves/CompiledWeave.cs`, `Definitions/Combat.cs`) |
| cast of the host | `cast { weave: host }` runs the hosting weave's graph again at a point or a target, at a scale, so contagion re-releases, curse-on-hit strikes and fields that re-afflict compose as hooks. | the cast handlers |
| trigger kinds | A `high_roll` trigger kind raised by hits whose roll lands in the top of their range, and proc triggers for a condition's rising edge; a `status` filter on procs that reads the event's status. | triggers, the proc service |
| charge modifiers | Modifiers a charge kind grants its bearer per charge held, applied by the charge service with gauge `charge.<id>`, so a kind like Frenzy carries its own haste. | the charge service |
| per-preset trees | A tree on a preset, where the prototype gave a skill its own tree; today trees belong to forms, which the rows note as a difference rather than a seam. | the weave compiler |

Open rows of the parity analysis that rows wait on (`blocked:`): C11 timed terrain, C20 relocating constructs, C23 resource orbs, C26 corpse piles, C27 shattering constructs, C29 toggle ledgers, C33 recalling impales, A13 concentration casts, E15 weak spots, G17 luck, H7 conduits, D21 skill grants on items.

## Proofs

Every slice lands with a seeded duel the fight tool runs twice to the same trace, and a test that pins the ported behavior; the proof column names them. The standard duels of the contracts stay unchanged unless a row names why.

<!-- generated tables: everything below comes from scripts/port/port-prototype.mjs; keep the statuses by hand as slices land -->

<!-- Generated from the prototype snapshot of 2026-10-17 by scripts/port/port-prototype.mjs; the statuses below are kept by hand as the slices land. -->

## Skills

### strikes and melee (128: not ported 71, ported (difference) 38, seam 14, ported 3, blocked 2)

| Prototype | Ours | Mechanism | Status | Note | Proof |
|---|---|---|---|---|---|
| hive_venom_mandibles |  | melee kit skill | not ported | kit |  |
| beast_fallback_art |  | melee kit skill | not ported | kit |  |
| beast_hound_art |  | melee kit skill | not ported | kit |  |
| beast_horn_art |  | melee kit skill | not ported | kit |  |
| beast_brute_art |  | nova kit skill | not ported | kit |  |
| beast_bird_art |  | cone kit skill | not ported | kit |  |
| beast_serpent_art |  | melee kit skill | not ported | kit |  |
| beast_shell_art |  | melee kit skill | not ported | kit |  |
| beast_mollusc_art |  | nova kit skill | not ported | kit |  |
| beast_fish_art |  | cone kit skill | not ported | kit |  |
| beast_small_art |  | melee kit skill | not ported | kit |  |
| beast_spirit_art |  | nova kit skill | not ported | kit |  |
| beast_flora_art |  | nova kit skill | not ported | kit |  |
| beast_grub_art |  | melee kit skill | not ported | kit |  |
| beast_stalker_art |  | melee kit skill | not ported | kit |  |
| court_challenge |  | nova kit skill | not ported | kit |  |
| court_twin_cut |  | melee kit skill | not ported | kit |  |
| court_abomination_slam |  | cone kit skill | not ported | kit |  |
| court_searing_bite |  | melee kit skill | not ported | kit |  |
| court_reaping_edge |  | cone kit skill | not ported | kit |  |
| marrow_sweep |  | cone kit skill | not ported | kit |  |
| warding_sweep |  | nova kit skill | not ported | kit |  |
| rubble_nudge |  | melee kit skill | not ported | kit |  |
| burr_rake |  | melee kit skill | not ported | kit |  |
| hingejaw_snap |  | melee kit skill | not ported | kit |  |
| shard_waltz |  | melee kit skill | not ported | kit |  |
| showstopper |  | cone kit skill | not ported | kit |  |
| cleave | cleave | preset on swing: area (stone) | ported (difference) | req; tree | fight port duel: strikes |
| heavy_strike | heavy_strike | preset on swing: area (stone) | ported (difference) | req; tree | fight port duel: strikes |
| ground_slam | ground_slam | preset on whirl: area (stone) | ported (difference) | req | fight port duel: strikes |
| frenzy | frenzy | preset on swing: area (stone) | ported (difference) | req | fight port duel: strikes |
| tide_lash | tide_lash | preset on swing: area (stone+tide) | ported (difference) | status sodden is not ported; req; grow | fight port duel: strikes |
| mirage_knife | mirage_knife | preset on swing: area (stone+ember) | ported (difference) | req; grow | fight port duel: strikes |
| hellfire_lash | hellfire_lash | preset on swing: area (ember) | ported (difference) | req; grow | fight port duel: strikes |
| gore_rend | gore_rend | preset on swing: area (stone) | ported (difference) | req; grow | fight port duel: strikes |
| machete_arc | machete_arc | preset on sweep: area (stone) | ported (difference) | req; grow | fight port duel: strikes |
| zeal | zeal | preset on swing: area (stone+ember) | ported (difference) | req; crowd empowerment (row C47, open); grow | fight port duel: strikes |
| zanshin_cut | zanshin_cut | preset on swing: area (stone) | seam | req; tree; grow; seam: preset behaviors (cycle) | fight port duel: strikes |
| surgical_strike | surgical_strike | preset on sweep: area (stone) | ported (difference) | req | fight port duel: strikes |
| buckler_strike | buckler_strike | preset on swing: area (stone) | ported (difference) | req; aim transforms (an aim wrapper by hand); tree | fight port duel: strikes |
| static_strike | static_strike | preset on swing: area (stone+storm) | ported (difference) | req; static discharge (a bank status by hand) | fight port duel: strikes |
| rend | rend | preset on swing: area (stone) | ported (difference) | req; tree | fight port duel: strikes |
| bloodlet | bloodlet | preset on whirl: area (stone) | ported (difference) | req; grow | fight port duel: strikes |
| eviscerate | eviscerate | preset on thrust: damage (stone) | ported (difference) | req; tree; grow | fight port duel: strikes |
| reckoning | reckoning | preset on swing: area (stone) | seam | req; tree; grow; seam: preset behaviors (spend) | fight port duel: strikes |
| reprisal | reprisal | preset on swing: area (stone) | seam | req; tree; grow; seam: preset behaviors (gates) | fight port duel: strikes |
| riposte_thrust |  | melee kit skill | not ported | kit |  |
| sunder_maul | sunder_maul | preset on swing: area (stone) | ported (difference) | req; tree; grow; inn | fight port duel: strikes |
| verdict | verdict | preset on swing: area (stone) | ported (difference) | req; tree; grow; an amount over target.poise.max, by hand | fight port duel: strikes |
| severing_lash | severing_lash | preset on swing: area (stone) | ported (difference) | req; grow; inn | fight port duel: strikes |
| thousand_cuts | thousand_cuts | preset on swing: area (stone) | seam | req; grow; seam: preset behaviors (self_stack) | fight port duel: strikes |
| backstab | backstab | preset on swing: area (stone) | ported (difference) | req; tree; a vs.behind modifier, by hand | fight port duel: strikes |
| acid_spray | acid_spray | preset on sweep: area (void) | ported (difference) | req | fight port duel: strikes |
| skewer | skewer | preset on swing: area (stone) | ported (difference) | req; tree; grow; inn | fight port duel: strikes |
| scythe_sweep | scythe_sweep | preset on swing: area (stone) | ported (difference) | req; aim transforms (an aim wrapper by hand) | fight port duel: strikes |
| execution | execution | preset on swing: area (stone) | ported (difference) | req; consume_status before the hit, by hand | fight port duel: strikes |
| toppling_stroke | toppling_stroke | preset on swing: area (stone) | ported (difference) | req; tree; grow | fight port duel: strikes |
| tuning_strike | tuning_strike | preset on swing: area (stone) | seam | req; tree; grow; seam: preset tags (duration) | fight port duel: strikes |
| sanctified_strike | sanctified_strike | preset on swing: area (stone+ember) | seam | req; tree; grow; seam: preset tags (heal) | fight port duel: strikes |
| talon_rake | talon_rake | preset on swing: area (stone) | ported (difference) | req; grow | fight port duel: strikes |
| lash_roots | lash_roots | preset on swing: area (stone) | ported (difference) | req; grow | fight port duel: strikes |
| snap_shut |  | nova kit skill | not ported | kit |  |
| shadow_slash |  | melee kit skill | not ported | kit |  |
| whirling_reap | whirling_reap | preset on swing: area (stone) | ported (difference) | req; aim transforms (an aim wrapper by hand); tree | fight port duel: strikes |
| winters_sweep | winters_sweep | preset on whirl: area (tide+stone) | ported |  | fight port duel: strikes |
| festering_bite |  | melee kit skill | not ported | kit |  |
| zealots_cadence | zealots_cadence | preset on swing: area (stone) | seam | req; thr; seam: preset behaviors (uses) | fight port duel: strikes |
| siphon_strike | siphon_strike | preset on swing: area (stone) | blocked | req; blocked: siphonOrb: resource orbs (row C23, open) | fight port duel: strikes |
| forgebound | forgebound | preset on swing: area (stone) | ported (difference) | req; inn | fight port duel: strikes |
| harvest_stroke | harvest_stroke | preset on swing: area (stone) | seam | req; seam: preset behaviors (follow_up) | fight port duel: strikes |
| lance_burst |  | nova kit skill | not ported | kit |  |
| piledriver | piledriver | preset on swing: area (stone) | ported (difference) | req; tree; grow; inn | fight port duel: strikes |
| one_two | one_two | preset on swing: area (stone) | seam | req; tree; grow; seam: preset behaviors (chain) | fight port duel: strikes |
| cross_jab | cross_jab | preset on swing: area (stone) | ported (difference) | band swing drawn as a bar shape; grow | fight port duel: strikes |
| haymaker | haymaker | preset on swing: area (stone) | seam | req; tree; grow; inn; seam: preset behaviors (spend) | fight port duel: strikes |
| mantra_strike | mantra_strike | preset on swing: area (stone) | seam | req; tree; grow; seam: preset behaviors (self_stack) | fight port duel: strikes |
| improvised_strike |  | melee kit skill | not ported | kit |  |
| possession |  | preset on swing: area | not ported | req; grow; seam: preset tags (possession); seam: preset behaviors (convert); later: possess: possession (row C38, later) | fight port duel: strikes |
| seize |  | preset on swing: area (stone) | not ported | req; grow; seam: preset tags (grab); later: grabSeize: the grab fabric (row C37, later) | fight port duel: strikes |
| mauler_clinch |  | melee kit skill | not ported | kit |  |
| gulp |  | melee kit skill | not ported | kit |  |
| yeti_snatch |  | melee kit skill | not ported | kit |  |
| tempest_gathering | tempest_gathering | preset on swing: area (stone+storm) | ported (difference) | req; static discharge (a bank status by hand); thr | fight port duel: strikes |
| trisect | trisect | preset on swing: area (stone) | seam | req; seam: preset behaviors (chain) | fight port duel: strikes |
| bisect_cut | bisect_cut | preset on swing: area (stone) | ported |  | fight port duel: strikes |
| trisect_finisher | trisect_finisher | preset on swing: area (stone) | ported |  | fight port duel: strikes |
| carve | carve | preset on swing: area (stone) | ported (difference) | req | fight port duel: strikes |
| deep_carve | deep_carve | preset on swing: area (stone) | ported (difference) | req | fight port duel: strikes |
| harvester_scythe |  | melee kit skill | not ported | kit |  |
| caul_lash | caul_lash | preset on swing: area (stone) | ported (difference) | req; grow | fight port duel: strikes |
| devouring_maw | devouring_maw | preset on swing: area (stone) | blocked | req; grow; blocked: siphonOrb: resource orbs (row C23, open) | fight port duel: strikes |
| tendril_lash | tendril_lash | preset on swing: area (stone+void) | ported (difference) | req; grow | fight port duel: strikes |
| lancing_flurry | lancing_flurry | preset on whirl: area (stone) | seam | req; seam: preset tags (javelin) | fight port duel: strikes |
| bastion_thrust | bastion_thrust | preset on sweep_quick: area (stone) | seam | usable while guarding: a reflex here; req; seam: preset tags (javelin, instant) | fight port duel: strikes |
| phalanx_thrust |  | cone kit skill | not ported | kit |  |
| claw |  | melee kit skill | not ported | kit |  |
| gloom_nip |  | melee kit skill | not ported | kit |  |
| scald_jet |  | nova kit skill | not ported | kit |  |
| stilt_spear |  | melee kit skill | not ported | kit |  |
| kettleback_burst |  | nova kit skill | not ported | kit |  |
| scalding_lunge |  | melee kit skill | not ported | kit |  |
| vapor_lash |  | melee kit skill | not ported | kit |  |
| bladder_vent |  | nova kit skill | not ported | kit |  |
| hopper_jab |  | melee kit skill | not ported | kit |  |
| mineral_slam |  | melee kit skill | not ported | kit |  |
| scalding_lash | scalding_lash | preset on sweep: area (ember) | ported (difference) | req; grow | fight port duel: strikes |
| tempo_stamp |  | melee kit skill | not ported | kit |  |
| tempo_vent_burst |  | nova kit skill | not ported | kit |  |
| crab_tick |  | melee kit skill | not ported | kit |  |
| crab_snap |  | melee kit skill | not ported | kit |  |
| wallow_reach |  | melee kit skill | not ported | kit |  |
| mantis_scythe |  | melee kit skill | not ported | kit |  |
| headsman_arc |  | melee kit skill | not ported | kit |  |
| penitent_lunge |  | melee kit skill | not ported | kit |  |
| cinder_bite |  | melee kit skill | not ported | kit |  |
| gnat_nip |  | melee kit skill | not ported | kit |  |
| rime_fang |  | melee kit skill | not ported | kit |  |
| entombing_lash |  | melee kit skill | not ported | kit |  |
| fang_strike |  | melee kit skill | not ported | kit |  |
| tithe_rake |  | melee kit skill | not ported | kit |  |
| tail_sting |  | melee kit skill | not ported | kit |  |
| sap_jet |  | cone kit skill | not ported | kit |  |
| engulf_slam |  | nova kit skill | not ported | kit |  |
| goad_jab |  | cone kit skill | not ported | kit |  |
| coil_lash |  | melee kit skill | not ported | kit |  |
| couched_lance |  | cone kit skill | not ported | kit |  |
| soul_toll |  | melee kit skill | not ported | kit |  |
| gale_lash | gale_lash | preset on sweep: area (stone) | ported (difference) | req; thr; grow | fight port duel: strikes |

### projectiles and bows (160: not ported 61, ported (difference) 50, seam 36, blocked 6, ported 7)

| Prototype | Ours | Mechanism | Status | Note | Proof |
|---|---|---|---|---|---|
| hive_poison_spit |  | projectile kit skill | not ported | kit |  |
| beast_spider_art |  | projectile kit skill | not ported | kit |  |
| beast_amphibian_art |  | projectile kit skill | not ported | kit |  |
| beast_insect_art |  | projectile kit skill | not ported | kit |  |
| court_vigil_bolt |  | projectile kit skill | not ported | kit |  |
| skeletal_fire_bolt |  | projectile kit skill | not ported | kit |  |
| skeletal_cold_bolt |  | projectile kit skill | not ported | kit |  |
| skeletal_lightning_bolt |  | projectile kit skill | not ported | kit |  |
| skeletal_chaos_bolt |  | projectile kit skill | not ported | kit |  |
| skeletal_lich_fireball |  | projectile kit skill | not ported | kit |  |
| skeletal_ice_spear |  | projectile kit skill | not ported | kit |  |
| skeletal_greater_ice_spear |  | projectile kit skill | not ported | kit |  |
| skeletal_ice_shard |  | projectile kit skill | not ported | kit |  |
| skeletal_chain_lightning |  | projectile kit skill | not ported | kit |  |
| skeletal_essence_drain |  | projectile kit skill | not ported | kit |  |
| rubble_flight |  | projectile kit skill | not ported | kit |  |
| rubble_flint |  | projectile kit skill | not ported | kit |  |
| rubble_burst |  | projectile kit skill | not ported | kit |  |
| rubble_conduit |  | projectile kit skill | not ported | kit |  |
| pact_lance |  | projectile kit skill | not ported | kit |  |
| thornfan_volley |  | projectile kit skill | not ported | kit |  |
| samara_shear |  | projectile kit skill | not ported | kit |  |
| satellite_rime_wake |  | projectile kit skill | not ported | kit |  |
| satellite_storm_wake |  | projectile kit skill | not ported | kit |  |
| aurora_pall |  | projectile kit skill | not ported | kit |  |
| flask_mana_lance |  | projectile kit skill | not ported | kit |  |
| borrowed_visage |  | projectile kit skill | not ported | kit |  |
| glass_lance | glass_lance | preset on missile: projectile (ember+stone) | ported (difference) | req; grow | fight port duel: projectiles |
| firebolt | firebolt | preset on missile: projectile (ember) | ported (difference) | req; tree; grow | fight port duel: projectiles |
| cinderwisp | cinderwisp | preset on missile: projectile (ember) | ported (difference) | a birth becomes a spawn of our pattern; sum:cinder_imp<cinder_sprite; req; grow | fight port duel: projectiles |
| unmaking_bolt | unmaking_bolt | preset on missile: projectile (void) | ported (difference) | req | fight port duel: projectiles |
| word_of_unmaking | word_of_unmaking | preset on missile: projectile (void) | ported (difference) | req | fight port duel: projectiles |
| frostbolt | frostbolt | preset on missile: projectile (tide) | ported (difference) | req | fight port duel: projectiles |
| spark | spark | preset on missile: projectile (storm) | ported (difference) | req; thr | fight port duel: projectiles |
| spark_bolt |  | projectile kit skill | not ported | kit |  |
| fulminate | fulminate | preset on missile: projectile (storm) | ported (difference) | req; thr; grow; inn | fight port duel: projectiles |
| pyroclast_bolt | pyroclast_bolt | preset on missile: projectile (ember) | ported (difference) | req; grow | fight port duel: projectiles |
| venom_bolt | venom_bolt | preset on missile: projectile (umbra) | ported (difference) | req | fight port duel: projectiles |
| blowdart | blowdart | preset on shot: projectile (stone) | ported (difference) | req; grow | fight port duel: projectiles |
| vine_lash | vine_lash | preset on missile: projectile (void) | ported (difference) | req; grow | fight port duel: projectiles |
| constrictor_coil |  | projectile kit skill | not ported | kit |  |
| dart_volley |  | projectile kit skill | not ported | kit |  |
| poison_nova | poison_nova | preset on missile: projectile (umbra) | ported (difference) | req; tree | fight port duel: projectiles |
| versicle | versicle | preset on missile: projectile (storm+ember) | seam | req; grow; seam: preset tags (song, instant) | fight port duel: projectiles |
| seraph_lance | seraph_lance | preset on missile: projectile (ember) | seam | req; grow; inn; seam: preset tags (javelin) | fight port duel: projectiles |
| ruin | ruin | preset on missile: projectile (void) | blocked | req; tree; grow; blocked: life bonds (row C3 bond) | fight port duel: projectiles |
| phantasm_bolt | phantasm_bolt | preset on missile: projectile (tide) | ported |  | fight port duel: projectiles |
| essence_drain | essence_drain | preset on missile: projectile (umbra) | seam | req; tree; seam: preset tags (duration) | fight port duel: projectiles |
| spectral_finger |  | projectile kit skill | not ported | kit |  |
| volatile_cinders | volatile_cinders | preset on missile: projectile (ember) | seam | plural targeting (several corpses) is one target here; req; grow; seam: preset tags (corpse) | fight port duel: projectiles |
| marrowhooks | marrowhooks | preset on missile: projectile (stone) | seam | plural targeting (several corpses) is one target here; req; grow; seam: preset tags (corpse) | fight port duel: projectiles |
| hammer_of_judgment | hammer_of_judgment | preset on missile: projectile (stone) | seam | req; tree; grow; seam: preset tags (duration) | fight port duel: projectiles |
| frozen_orb | frozen_orb | preset on missile: projectile (tide) | seam | req; grow; seam: preset tags (duration) | fight port duel: projectiles |
| spectral_helix | spectral_helix | preset on shot: projectile (stone+tide) | ported (difference) | req | fight port duel: projectiles |
| piercing_arrow | piercing_arrow | preset on shot: projectile (stone) | ported (difference) | req; tree | fight port duel: projectiles |
| fan_of_blades | fan_of_blades | preset on shot: projectile (stone) | ported (difference) | req; tree | fight port duel: projectiles |
| goad | goad | preset on shot: projectile (stone) | ported (difference) | req; tree; grow; inn | fight port duel: projectiles |
| null_lance | null_lance | preset on missile: projectile (void) | ported (difference) | req; grow; inn | fight port duel: projectiles |
| frost_pulse | frost_pulse | preset on missile: projectile (tide) | ported (difference) | req | fight port duel: projectiles |
| fire_siege | fire_siege | preset on missile: projectile (ember) | ported (difference) | req | fight port duel: projectiles |
| shockfront | shockfront | preset on shot: projectile (stone) | ported (difference) | req; tree | fight port duel: projectiles |
| ball_lightning | ball_lightning | preset on missile: projectile (storm) | seam | req; seam: preset tags (duration) | fight port duel: projectiles |
| chain_lightning | chain_lightning | preset on missile: projectile (storm) | ported (difference) | req; tree; inn | fight port duel: projectiles |
| chain_pull | chain_pull | preset on shot: projectile (stone) | ported (difference) | req; tree | fight port duel: projectiles |
| lava_glob | lava_glob | preset on missile_quick: projectile (ember) | ported |  | fight port duel: projectiles |
| powderkeg_arrow | powderkeg_arrow | preset on shot: projectile (stone+ember) | ported (difference) | req; inn | fight port duel: projectiles |
| orbital_blades | orbital_blades | preset on shot: projectile (stone) | seam | req; grow; seam: preset tags (duration) | fight port duel: projectiles |
| impale_spear |  | projectile kit skill | not ported | kit |  |
| pinning_spear | pinning_spear | preset on shot: projectile (stone) | blocked | req; tree; seam: preset tags (duration); blocked: planting where the flight ends (an embed: row B16 constructs, authored by hand) | fight port duel: projectiles |
| planted_banderilla | planted_banderilla | preset on shot: projectile (stone) | seam | req; tree; grow; seam: preset tags (duration) | fight port duel: projectiles |
| thrown_ace | thrown_ace | preset on shot: projectile (storm+stone) | ported (difference) | third:ember, tide; req; tree; grow | fight port duel: projectiles |
| stasis_lock | stasis_lock | preset on missile: projectile (void) | seam | status stasis is not ported; status temporal_drag is not ported; req; tree; grow; seam: preset tags (chrono) | fight port duel: projectiles |
| soul_volley | soul_volley | preset on missile: projectile (umbra) | seam | req; leech on the damage node, by hand; seam: preset tags (heal) | fight port duel: projectiles |
| splinter_volley | splinter_volley | preset on shot: projectile (stone) | ported (difference) | req; grow | fight port duel: projectiles |
| emetic_lob |  | projectile kit skill | not ported | kit |  |
| blinding_ichor | blinding_ichor | preset on missile: projectile (void) | ported (difference) | req; grow | fight port duel: projectiles |
| scatterhex | scatterhex | preset on missile: projectile (umbra) | ported (difference) | req; grow | fight port duel: projectiles |
| gut_hurl | gut_hurl | preset on missile: projectile (void+stone) | ported (difference) | req; grow | fight port duel: projectiles |
| web_shot | web_shot | preset on shot: projectile (stone) | seam | req; grow; seam: preset tags (duration) | fight port duel: projectiles |
| hurl_debris | hurl_debris | preset on missile: projectile (stone) | ported (difference) | req; grow | fight port duel: projectiles |
| barrage | barrage | preset on shot: projectile (stone) | ported (difference) | req | fight port duel: projectiles |
| cold_spot | cold_spot | preset on missile: projectile (tide) | ported (difference) | req | fight port duel: projectiles |
| hellfire_missile | hellfire_missile | preset on missile_quick: projectile (ember) | ported |  | fight port duel: projectiles |
| mirage_shot | mirage_shot | preset on shot: projectile (stone) | ported |  | fight port duel: projectiles |
| shadow_shuriken |  | projectile kit skill | not ported | kit |  |
| archon_lance | archon_lance | preset on missile: projectile (storm) | ported (difference) | req; grow; inn; pct-cost | fight port duel: projectiles |
| maelstrom_orb | maelstrom_orb | preset on missile: projectile (storm) | seam | req; seam: preset tags (duration) | fight port duel: projectiles |
| fireball | fireball | preset on missile: projectile (ember) | ported (difference) | req; thr | fight port duel: projectiles |
| flame_arrow | flame_arrow | preset on missile: projectile (ember) | ported (difference) | req; tree | fight port duel: projectiles |
| magma_glob | magma_glob | preset on missile_quick: projectile (ember) | ported |  | fight port duel: projectiles |
| cinder_swarm | cinder_swarm | preset on missile: projectile (ember) | ported (difference) | req | fight port duel: projectiles |
| ice_spear | ice_spear | preset on missile: projectile (tide) | ported (difference) | req | fight port duel: projectiles |
| glacial_shard | glacial_shard | preset on missile_quick: projectile (tide) | ported |  | fight port duel: projectiles |
| shrapnel_shard |  | projectile kit skill | not ported | kit |  |
| glimmer_pulse |  | projectile kit skill | not ported | kit |  |
| lure_lantern |  | preset on missile: projectile | not ported | req; seam: preset tags (duration); later: lure: false attraction (row C36, later) | fight port duel: projectiles |
| gourd_toss |  | projectile kit skill | not ported | kit |  |
| hurl_swarmpod |  | projectile kit skill | not ported | kit |  |
| bottled_swarm |  | preset on missile: projectile (stone) | not ported | req; grow; later: litePour: lightweight swarms (row C36, later); later: throngDirect: the throng fabric (row C38, later); later: the throng fabric (row C38) | fight port duel: projectiles |
| bolt_repeater | bolt_repeater | preset on shot: projectile (stone) | seam | req; thr; seam: preset tags (munition); seam: preset behaviors (uses) | fight port duel: projectiles |
| scattergun | scattergun | preset on shot: projectile (stone) | seam | req; seam: preset tags (munition); seam: preset behaviors (uses, convert, meta_action) | fight port duel: projectiles |
| arquebus | arquebus | preset on shot: projectile (stone) | seam | req; inn; seam: preset tags (munition); seam: preset behaviors (uses, convert) | fight port duel: projectiles |
| grenado | grenado | preset on shot: projectile (ember) | seam | req; seam: preset tags (munition); seam: preset behaviors (uses) | fight port duel: projectiles |
| bonespray | bonespray | preset on missile: projectile (stone) | ported (difference) | req; pct-cost | fight port duel: projectiles |
| sacrament_of_ruin | sacrament_of_ruin | preset on missile: projectile (void) | ported (difference) | req | fight port duel: projectiles |
| earthrender |  | preset on missile: projectile (stone) | not ported | req; seam: preset tags (duration, fissure); later: fissure trails (a fissure fabric this port lacks) | fight port duel: projectiles |
| nether_spirit | nether_spirit | preset on missile_quick: projectile (umbra) | seam | seam: preset tags (duration) | fight port duel: projectiles |
| wandering_spirits | wandering_spirits | preset on missile: projectile (umbra) | seam | req; grow; seam: preset tags (duration) | fight port duel: projectiles |
| creeping_frost | creeping_frost | preset on missile: projectile (tide) | seam | req; grow; seam: preset tags (duration) | fight port duel: projectiles |
| rift_shard | rift_shard | preset on missile_quick: projectile (tide) | ported |  | fight port duel: projectiles |
| impale_lance | impale_lance | preset on shot: projectile (stone) | blocked | req; seam: preset tags (duration); blocked: planting where the flight ends (an embed: row B16 constructs, authored by hand) | fight port duel: projectiles |
| arclight_rain | arclight_rain | preset on shot: projectile (storm+stone) | blocked | req; seam: preset tags (duration); blocked: planting where the flight ends (an embed: row B16 constructs, authored by hand) | fight port duel: projectiles |
| voltaic_orb | voltaic_orb | preset on missile: projectile (ember+storm) | seam | req; seam: preset tags (duration) | fight port duel: projectiles |
| beguile | beguile | preset on missile: projectile (void) | seam | req; tree; grow; seam: preset tags (duration) | fight port duel: projectiles |
| ushers_lull |  | projectile kit skill | not ported | kit |  |
| gaff_cast |  | projectile kit skill | not ported | kit |  |
| undertow_lash |  | projectile kit skill | not ported | kit |  |
| gyreblade | gyreblade | preset on shot: projectile (stone) | blocked | req; blocked: catching returned blades for charges: an on_return charge, authored by hand | fight port duel: projectiles |
| gyre_hurl | gyre_hurl | preset on shot_quick: projectile (stone) | seam | req; seam: preset behaviors (spend) | fight port duel: projectiles |
| caroms |  | preset on shot: projectile (stone) | not ported | req; seam: preset tags (duration); later: caroming anchors (row B25, later) | fight port duel: projectiles |
| hanging_volley |  | preset on shot: projectile (stone) | not ported | req; seam: preset tags (duration); later: caroming anchors (row B25, later) | fight port duel: projectiles |
| heartchaser | heartchaser | preset on shot: projectile (stone) | ported (difference) | selective pierce dropped; req | fight port duel: projectiles |
| living_barrage | living_barrage | preset on missile: projectile (stone) | seam | req; seam: preset tags (duration) | fight port duel: projectiles |
| skittering_bolt | skittering_bolt | preset on missile: projectile (storm) | ported (difference) | zigzag shards dropped; req | fight port duel: projectiles |
| frostcreep | frostcreep | preset on missile: projectile (tide) | seam | req; seam: preset tags (duration) | fight port duel: projectiles |
| rimeclaw | rimeclaw | preset on missile: projectile (tide) | ported (difference) | req | fight port duel: projectiles |
| splayshot | splayshot | preset on shot: projectile (stone) | ported (difference) | req | fight port duel: projectiles |
| galewisp | galewisp | preset on missile: projectile (stone) | seam | req; seam: preset tags (duration) | fight port duel: projectiles |
| wildwisp |  | preset on missile: projectile (stone) | not ported | req; seam: preset tags (duration); later: carom on hit (row B25, carom is later) | fight port duel: projectiles |
| invoke_lance |  | projectile kit skill | not ported | kit |  |
| offering_wisp |  | projectile kit skill | not ported | kit |  |
| soulflay | soulflay | preset on missile: projectile (umbra) | seam | req; inn; seam: preset tags (duration) | fight port duel: projectiles |
| gravewisp | gravewisp | preset on missile: projectile (umbra) | seam | req; seam: preset tags (duration) | fight port duel: projectiles |
| voltspear | voltspear | preset on shot: projectile (storm) | seam | req; seam: preset tags (javelin) | fight port duel: projectiles |
| blightspear | blightspear | preset on shot: projectile (umbra+stone) | seam | req; seam: preset tags (javelin, duration) | fight port duel: projectiles |
| mirespume | mirespume | preset on missile: projectile (umbra) | seam | req; grow; seam: preset tags (duration) | fight port duel: projectiles |
| marshlight | marshlight | preset on missile: projectile (void) | seam | req; grow; seam: preset tags (duration) | fight port duel: projectiles |
| ember_dart |  | projectile kit skill | not ported | kit |  |
| magma_lob | magma_lob | preset on missile: projectile (ember) | seam | req; grow; seam: preset tags (duration) | fight port duel: projectiles |
| void_hook | void_hook | preset on missile: projectile (void) | ported (difference) | req; grow | fight port duel: projectiles |
| tongue_reel | tongue_reel | preset on shot: projectile (stone) | ported (difference) | req; grow | fight port duel: projectiles |
| plague_reel |  | projectile kit skill | not ported | kit |  |
| doomsayers_word | doomsayers_word | preset on missile: projectile (umbra) | ported (difference) | req; grow | fight port duel: projectiles |
| whirlaxe | whirlaxe | preset on shot: projectile (stone) | blocked | req; seam: preset tags (duration); blocked: catching returned blades for charges: an on_return charge, authored by hand | fight port duel: projectiles |
| sinter_lance |  | projectile kit skill | not ported | kit |  |
| pressure_throw |  | projectile kit skill | not ported | kit |  |
| scald_undertow |  | projectile kit skill | not ported | kit |  |
| barb_spit |  | projectile kit skill | not ported | kit |  |
| pale_zap |  | projectile kit skill | not ported | kit |  |
| bone_arrow |  | projectile kit skill | not ported | kit |  |
| siren_song |  | projectile kit skill | not ported | kit |  |
| hellchain_volley |  | projectile kit skill | not ported | kit |  |
| static_jolt |  | projectile kit skill | not ported | kit |  |
| feather_volley | feather_volley | preset on shot: projectile (stone) | ported (difference) | req; thr; grow | fight port duel: projectiles |
| radiant_lance | radiant_lance | preset on shot: projectile (stone+storm) | seam | req; thr; grow; seam: preset tags (javelin) | fight port duel: projectiles |
| gloriole | gloriole | preset on missile: projectile (ember+storm) | seam | req; thr; grow; seam: preset tags (duration) | fight port duel: projectiles |
| aureate_lash | aureate_lash | preset on missile_quick: projectile (storm+ember) | ported (difference) | grow | fight port duel: projectiles |
| skyhook | skyhook | preset on missile: projectile (stone+tide) | ported (difference) | req; grow | fight port duel: projectiles |
| squall_dart | squall_dart | preset on missile: projectile (storm+stone) | ported (difference) | req; grow | fight port duel: projectiles |
| galewright_twister |  | projectile kit skill | not ported | kit |  |

### spells per Thread (164: not ported 61, ported (difference) 25, seam 72, blocked 3, ported 3)

| Prototype | Ours | Mechanism | Status | Note | Proof |
|---|---|---|---|---|---|
| hive_detonation |  | ground kit skill | not ported | kit |  |
| hive_royal_nova |  | ground kit skill | not ported | kit |  |
| beast_defiant_roar |  | nova kit skill | not ported | kit |  |
| beast_rally_pulse |  | nova kit skill | not ported | kit |  |
| beast_dread_pulse |  | target kit skill | not ported | kit |  |
| goad_device_call |  | nova kit skill | not ported | kit |  |
| court_wraith_hex |  | ground kit skill | not ported | kit |  |
| skeletal_ignite |  | target kit skill | not ported | kit |  |
| skeletal_winter_ring |  | nova kit skill | not ported | kit |  |
| skeletal_grave_thunder |  | ground kit skill | not ported | kit |  |
| skeletal_plague_ring |  | nova kit skill | not ported | kit |  |
| pact_mend |  | target kit skill | not ported | kit |  |
| nectar_mend |  | target kit skill | not ported | kit |  |
| magic_pack_footfall |  | ground kit skill | not ported | kit |  |
| magic_pack_scattershock |  | ground kit skill | not ported | kit |  |
| magic_pack_blast |  | ground kit skill | not ported | kit |  |
| magic_pack_ritual |  | ground kit skill | not ported | kit |  |
| magic_pack_hollow |  | ground kit skill | not ported | kit |  |
| magic_pack_arc |  | ground kit skill | not ported | kit |  |
| magic_pack_gravewheel |  | ground kit skill | not ported | kit |  |
| satellite_shelter_thorns |  | ground kit skill | not ported | kit |  |
| satellite_cinder_wake |  | ground kit skill | not ported | kit |  |
| satellite_iron_wake |  | ground kit skill | not ported | kit |  |
| creeper_barrow |  | ground kit skill | not ported | kit |  |
| flask_blood_bloom |  | nova kit skill | not ported | kit |  |
| flask_clean_company |  | nova kit skill | not ported | kit |  |
| mocking_refrain |  | nova kit skill | not ported | kit |  |
| ironbell_toll |  | nova kit skill | not ported | kit |  |
| hordefathers_reckoning |  | nova kit skill | not ported | kit |  |
| salt_burst | salt_burst | preset on burst: area (stone) | ported (difference) | req; grow | fight port duel: spells |
| whirl_of_grit | whirl_of_grit | preset on breath: area (stone) | ported (difference) | req; grow | fight port duel: spells |
| sirocco_ring | sirocco_ring | preset on burst: area (ember) | seam | req; grow; seam: preset tags (duration) | fight port duel: spells |
| solar_brand | solar_brand | preset on smite: damage, apply_status, apply_status, apply_status, area (ember) | seam | req; grow; seam: preset tags (targeted, duration) | fight port duel: spells |
| flame_wave | flame_wave | preset on breath: area (ember) | ported (difference) | req | fight port duel: spells |
| doom_chant | doom_chant | preset on lay: area (umbra) | seam | req; seam: preset tags (curse, duration) | fight port duel: spells |
| frost_nova | frost_nova | preset on burst: area (tide) | ported (difference) | req; tree | fight port duel: spells |
| pyre_nova | pyre_nova | preset on burst: area (ember) | ported (difference) | req; contagion (authored as an on-hit cast by hand); grow | fight port duel: spells |
| renew | renew | preset on smite_quick: apply_status | seam | req; grow; seam: preset tags (heal, buff, targeted, duration, instant) | fight port duel: spells |
| antiphon | antiphon | preset on burst: area (ember+storm) | seam | req; grow; seam: preset tags (song); seam: preset behaviors (equip_modifiers) | fight port duel: spells |
| hex_pulse | hex_pulse | preset on burst_quick: area (umbra) | seam | seam: preset tags (instant) | fight port duel: spells |
| judgement | judgement | preset on smite: damage, apply_status (ember) | seam | req; grow; seam: preset tags (targeted) | fight port duel: spells |
| lay_on_hands | lay_on_hands | preset on smite_quick: heal | seam | req; grow; seam: preset tags (heal, targeted, instant) | fight port duel: spells |
| blessing_of_might | blessing_of_might | preset on smite: apply_status | seam | req; grow; seam: preset tags (buff, targeted, duration) | fight port duel: spells |
| guardian_bond | guardian_bond | preset on smite: apply_status | seam | life bond (bondShare) is not ported; req; grow; seam: preset tags (buff, targeted, duration); seam: preset behaviors (equip_modifiers) | fight port duel: spells |
| choir_of_light | choir_of_light | preset on burst_quick: area | seam | req; grow; seam: preset tags (heal, duration) | fight port duel: spells |
| verdict_release | verdict_release | preset on burst_quick: area (ember) | seam | grow; seam: preset tags (instant); seam: preset behaviors (gates, spend) | fight port duel: spells |
| shock_nova | shock_nova | preset on burst: area (storm) | ported (difference) | req | fight port duel: spells |
| contagion | contagion | preset on lay: area (void) | seam | req; seam: preset tags (duration) | fight port duel: spells |
| despair | despair | preset on lay: area (umbra) | seam | req; tree; seam: preset tags (curse, duration) | fight port duel: spells |
| agony | agony | preset on lay: area (umbra) | seam | req; seam: preset tags (curse, duration) | fight port duel: spells |
| indecision | indecision | preset on lay: area (umbra) | seam | req; seam: preset tags (curse, duration) | fight port duel: spells |
| befuddlement | befuddlement | preset on lay: area (umbra) | seam | req; seam: preset tags (curse, duration) | fight port duel: spells |
| bewilder | bewilder | preset on lay: area (umbra) | seam | req; seam: preset tags (curse, duration) | fight port duel: spells |
| belligerence | belligerence | preset on burst: area | seam | req; seam: preset tags (buff, duration) | fight port duel: spells |
| furor | furor | preset on burst: area | seam | req; seam: preset tags (buff, duration) | fight port duel: spells |
| umbral_lance | umbral_lance | preset on breath: area (umbra) | ported (difference) | req | fight port duel: spells |
| corpse_explosion | corpse_explosion | preset on burst: area (ember) | seam | plural targeting (several corpses) is one target here; corpse life as damage or restoration: an expression over the consumed body, authored by hand; req; tree; grow; seam: preset tags (corpse) | fight port duel: spells |
| dark_pact | dark_pact | preset on burst: area (umbra) | ported (difference) | corpse life as damage or restoration: an expression over the consumed body, authored by hand; req; grow | fight port duel: spells |
| expunge | expunge | preset on smite: damage, field, apply_status, area (void) | seam | req; grow; seam: preset tags (duration) | fight port duel: spells |
| tame_beast | tame_beast | preset on smite_quick:  | blocked | req; tree; grow; seam: preset tags (duration, companion); seam: preset behaviors (convert, meta_action, equip_modifiers); later: tame: companions (row C35, later); blocked: concentration (row A13, open) | fight port duel: spells |
| immolation_blast | immolation_blast | preset on burst: area (ember) | ported (difference) | req | fight port duel: spells |
| challenging_shout | challenging_shout | preset on burst_quick: area | seam | usable while guarding: a reflex here; req; tree; grow; seam: preset tags (duration, instant) | fight port duel: spells |
| taunt_pulse | taunt_pulse | preset on burst_quick: area | seam | seam: preset tags (instant) | fight port duel: spells |
| aegis_ward | aegis_ward | preset on burst: area | seam | req; tree; grow; seam: preset tags (buff, duration) | fight port duel: spells |
| rallying_howl | rallying_howl | preset on burst: area | seam | req; tree; grow; seam: preset tags (buff, duration) | fight port duel: spells |
| static_shock | static_shock | preset on smite_quick: apply_status (storm) | seam | req; grow; a cannot_kill damage over target.life, by hand; seam: preset tags (targeted, instant) | fight port duel: spells |
| repulse_wave | repulse_wave | preset on burst_quick: area (storm) | ported |  | fight port duel: spells |
| flash_freeze | flash_freeze | preset on smite: damage, apply_status, area (tide) | seam | req; seam: preset tags (targeted) | fight port duel: spells |
| earthquake | earthquake | preset on slam: area (stone) | seam | ground pulses repeat the area where it stood (delay and steps folded into area.pulses); req; tree; seam: preset tags (pulse) | fight port duel: spells |
| ember_breath |  | cone kit skill | not ported | kit |  |
| rime_breath |  | cone kit skill | not ported | kit |  |
| fume_gout |  | cone kit skill | not ported | kit |  |
| expose_weakness | expose_weakness | preset on smite_quick: damage, apply_status (umbra) | seam | req; tree; grow; seam: preset tags (curse, targeted, duration, instant) | fight port duel: spells |
| word_of_doom | word_of_doom | preset on smite: damage, apply_status (umbra) | seam | req; grow; inn; seam: preset tags (curse, targeted, duration) | fight port duel: spells |
| magma_ward | magma_ward | preset on burst: area (ember) | seam | req; taps; seam: preset behaviors (spend) | fight port duel: spells |
| incite | incite | preset on burst: area (void) | seam | req; tree; grow; seam: preset tags (duration) | fight port duel: spells |
| shatterchord | shatterchord | preset on burst: area (storm+ember) | ported (difference) | third:tide; req; tree; grow | fight port duel: spells |
| mend | mend | preset on smite_quick: heal | seam | req; tree; grow; seam: preset tags (heal, targeted, instant) | fight port duel: spells |
| greater_mending | greater_mending | preset on smite: heal | seam | req; grow; seam: preset tags (heal, targeted) | fight port duel: spells |
| benediction | benediction | preset on burst_quick: area | seam | req; grow; seam: preset tags (heal, instant) | fight port duel: spells |
| communion | communion | preset on burst: area | seam | req; grow; seam: preset tags (heal) | fight port duel: spells |
| cleansing_light | cleansing_light | preset on smite_quick: remove_status, heal | seam | req; grow; seam: preset tags (heal, targeted, instant) | fight port duel: spells |
| mending_pulse | mending_pulse | preset on smite: heal | seam | the heal chain becomes a chain effect picking the most wounded ally; seam: preset tags (heal, targeted) | fight port duel: spells |
| soothing_touch |  | target kit skill | not ported | kit |  |
| gorge_burst |  | nova kit skill | not ported | kit |  |
| kindled_eruption |  | nova kit skill | not ported | kit |  |
| keening_shriek | keening_shriek | preset on breath: area (stone) | seam | req; seam: preset tags (duration) | fight port duel: spells |
| spore_burst | spore_burst | preset on burst: area (umbra) | ported (difference) | req; grow | fight port duel: spells |
| bile_spray | bile_spray | preset on breath: area (void) | ported (difference) | req; grow | fight port duel: spells |
| gaze_beam |  | cone kit skill | not ported | kit |  |
| stone_gaze | stone_gaze | preset on breath: area (stone) | seam | req; grow; seam: preset tags (duration) | fight port duel: spells |
| prismatic_ray | prismatic_ray | preset on breath: area (ember+tide) | ported (difference) | third:storm; req; grow | fight port duel: spells |
| petrifying_gaze |  | cone kit skill | not ported | kit |  |
| tear_burst |  | nova kit skill | not ported | kit |  |
| transfixing_gaze | transfixing_gaze | preset on smite_quick: damage (void) | blocked | status seen is not ported; req; grow; seam: preset tags (duration, targeted); blocked: concentration (row A13, open) | fight port duel: spells |
| syncope | syncope | preset on burst: area (void) | ported (difference) | req; grow | fight port duel: spells |
| witching_bell | witching_bell | preset on burst: area (void) | ported (difference) | req; grow | fight port duel: spells |
| turnwise_hex | turnwise_hex | preset on smite: damage (umbra) | seam | status widdershins is not ported; req; grow; seam: preset tags (duration, targeted) | fight port duel: spells |
| stridulate |  | nova kit skill | not ported | kit |  |
| sanguine_burst | sanguine_burst | preset on burst: area (stone) | ported (difference) | req; tree; grow; inn; pct-cost | fight port duel: spells |
| soul_harvest | soul_harvest | preset on burst: area (umbra+tide) | seam | req; taps; thr; seam: preset behaviors (spend) | fight port duel: spells |
| thunderclap | thunderclap | preset on burst_quick: area (storm) | seam | req; seam: preset tags (instant) | fight port duel: spells |
| overload | overload | preset on burst: area (storm) | ported (difference) | req; consume_status before the hit, by hand | fight port duel: spells |
| combustion_strike | combustion_strike | preset on smite: damage, apply_status, area (ember) | seam | req; seam: preset tags (targeted) | fight port duel: spells |
| ignite | ignite | preset on smite_quick: damage, apply_status (ember) | seam | req; tree; grow; seam: preset tags (targeted, instant) | fight port duel: spells |
| living_bomb | living_bomb | preset on smite: damage, apply_status (ember) | seam | req; inn; seam: preset tags (targeted, duration) | fight port duel: spells |
| backdraft | backdraft | preset on breath: area (ember) | ported (difference) | req | fight port duel: spells |
| absolute_zero | absolute_zero | preset on burst: area (tide) | ported (difference) | req; consume_status before the hit, by hand | fight port duel: spells |
| resonant_peal |  | nova kit skill | not ported | kit |  |
| discord_wail |  | nova kit skill | not ported | kit |  |
| pollen_puff |  | nova kit skill | not ported | kit |  |
| beguiling_glow |  | nova kit skill | not ported | kit |  |
| mimic_flash |  | nova kit skill | not ported | kit |  |
| prey_musk | prey_musk | preset on smite: apply_status | seam | req; grow; seam: preset tags (curse, duration) | fight port duel: spells |
| alarm_reek | alarm_reek | preset on burst: area | seam | req; grow; seam: preset tags (duration) | fight port duel: spells |
| kindle_wick |  | preset on lay: area | not ported | req; thr; grow; seam: preset tags (duration); later: kindle: lightwells (row C36, later) | fight port duel: spells |
| harrowing_wail | harrowing_wail | preset on breath: area (stone) | seam | req; tree; grow; seam: preset tags (duration) | fight port duel: spells |
| wax_flare |  | nova kit skill | not ported | kit |  |
| waxlight_pulse |  | nova kit skill | not ported | kit |  |
| galvanic_reserve | galvanic_reserve | preset on burst: area (storm) | seam | req; taps; thr; seam: preset behaviors (spend) | fight port duel: spells |
| siphon_blood | siphon_blood | preset on smite: damage (stone+void) | blocked | req; seam: preset tags (targeted); blocked: siphonOrb: resource orbs (row C23, open) | fight port duel: spells |
| acrid_splash |  | nova kit skill | not ported | kit |  |
| cindershell | cindershell | preset on burst: area (ember+stone) | seam | req; grow; inn; seam: preset behaviors (equip_modifiers) | fight port duel: spells |
| requiem | requiem | preset on burst: area (ember) | seam | req; grow; inn; seam: preset behaviors (spend, equip_modifiers) | fight port duel: spells |
| relic_burst | relic_burst | preset on burst_quick: area (stone) | ported |  | fight port duel: spells |
| relic_mend | relic_mend | preset on burst_quick: area | seam | seam: preset tags (heal) | fight port duel: spells |
| transfusion | transfusion | preset on smite: damage, move_status (void) | seam | req; seam: preset tags (targeted, duration) | fight port duel: spells |
| anathema | anathema | preset on smite: damage, area (void) | seam | req; consume_status before the hit, by hand; seam: preset tags (targeted) | fight port duel: spells |
| soul_glut | soul_glut | preset on burst: area (umbra) | ported (difference) | ward on each hit becomes a ward on cast; req | fight port duel: spells |
| fulgurweb | fulgurweb | preset on smite: damage, apply_status (storm) | seam | req; seam: preset tags (targeted, duration, curse) | fight port duel: spells |
| doombrand | doombrand | preset on smite: damage, apply_status (umbra) | seam | req; inn; seam: preset tags (targeted, duration, curse) | fight port duel: spells |
| snare_shock | snare_shock | preset on lay_quick: area (stone) | ported (difference) | inn | fight port duel: spells |
| single_out | single_out | preset on smite: apply_status, apply_status | seam | req; tree; grow; inn; seam: preset tags (targeted, duration) | fight port duel: spells |
| coda | coda | preset on burst: area (stone) | seam | req; tree; grow; seam: preset tags (song); seam: preset behaviors (spend) | fight port duel: spells |
| death_knell |  | nova kit skill | not ported | kit |  |
| bone_offering | bone_offering | preset on smite: apply_status | seam | plural targeting (several corpses) is one target here; req; grow; seam: preset tags (corpse, buff, duration) | fight port duel: spells |
| nerve_pulse | nerve_pulse | preset on burst: area (void) | seam | req; grow; seam: preset tags (instant) | fight port duel: spells |
| cask_blast |  | ground kit skill | not ported | kit |  |
| relic_pulse | relic_pulse | preset on burst_quick: area (stone) | seam | seam: preset tags (heal) | fight port duel: spells |
| bell_toll | bell_toll | preset on burst_quick: area (stone) | ported |  | fight port duel: spells |
| kettle_burst | kettle_burst | preset on lay: area (ember) | ported (difference) | status soaked is not ported; req; grow | fight port duel: spells |
| boil_over | boil_over | preset on smite: damage, rupture, area (ember) | seam | req; grow; seam: preset tags (duration) | fight port duel: spells |
| vent_veil |  | preset on lay: area (ember) | not ported | req; grow; seam: preset tags (duration); later: vent: fog banks (row C36, later) | fight port duel: spells |
| crag_peal |  | nova kit skill | not ported | kit |  |
| ash_smother |  | nova kit skill | not ported | kit |  |
| aureole | aureole | preset on burst: area (storm) | seam | req; grow; seam: preset behaviors (self_stack) | fight port duel: spells |
| trumpet_peal | trumpet_peal | preset on breath: area (storm) | ported (difference) | req; tree; grow | fight port duel: spells |
| static_discharge | static_discharge | preset on burst: area (storm) | seam | req; grow; seam: preset behaviors (self_stack) | fight port duel: spells |
| cloudcall |  | preset on lay: area | not ported | req; thr; grow; seam: preset tags (duration, buff); later: conjure: cloud bridges (row C36, later) | fight port duel: spells |
| gust_burst | gust_burst | preset on burst: area | ported (difference) | req; grow | fight port duel: spells |
| stormcradle |  | preset on lay: area (storm) | not ported | req; thr; grow; seam: preset tags (duration, buff); later: conjure: cloud bridges (row C36, later) | fight port duel: spells |
| balmcloud |  | preset on lay: area | not ported | req; grow; seam: preset tags (duration, buff, heal); later: conjure: cloud bridges (row C36, later) | fight port duel: spells |
| mistral_causeway |  | preset on lay: area | not ported | req; thr; grow; seam: preset tags (duration, buff); later: conjure: cloud bridges (row C36, later) | fight port duel: spells |
| low_ceiling |  | preset on lay: area | not ported | req; grow; seam: preset tags (duration); later: conjure: cloud bridges (row C36, later) | fight port duel: spells |
| hollow_star_collapse | hollow_star_collapse | preset on lay_quick: area (void) | seam | grow; seam: preset tags (ultimate) | fight port duel: spells |
| doom_bell | doom_bell | preset on burst: area (stone) | seam | req; ult; crowd empowerment (row C47, open); grow; seam: preset tags (ultimate) | fight port duel: spells |
| last_rites_toll | last_rites_toll | preset on burst_quick: area (stone) | seam | grow; seam: preset tags (ultimate) | fight port duel: spells |
| reapers_toll | reapers_toll | preset on burst: area (umbra) | ported (difference) | req; the gauge (a charge bank by hand); grow | fight port duel: spells |
| long_cold | long_cold | preset on burst: area (tide) | seam | req; ult; tree; grow; seam: preset tags (buff, ultimate); seam: preset behaviors (follow_up) | fight port duel: spells |
| long_cold_shatter | long_cold_shatter | preset on burst_quick: area (tide) | seam | grow; seam: preset tags (ultimate) | fight port duel: spells |
| litany_of_dawn_mend | litany_of_dawn_mend | preset on burst_quick: area | seam | seam: preset tags (heal, ultimate) | fight port duel: spells |
| cinderstep_trace |  | nova kit skill | not ported | kit |  |
| rimeglass_trace |  | nova kit skill | not ported | kit |  |
| lattice_discharge |  | nova kit skill | not ported | kit |  |

### channels and holds (63: ported (difference) 30, seam 27, blocked 2, not ported 4)

| Prototype | Ours | Mechanism | Status | Note | Proof |
|---|---|---|---|---|---|
| whirlwind | whirlwind | preset on whirl_stream: area (stone) | ported (difference) | req; tree | fight port duel: channels |
| undertow | undertow | preset on lay_stream: field (tide+stone) | seam | req; seam: preset tags (duration) | fight port duel: channels |
| aegis_of_dawn | aegis_of_dawn | preset on boon_stance:  | seam | req; grow; inn; seam: preset tags (heal, duration) | fight port duel: channels |
| iai_strike | iai_strike | preset on stride_beat: move (stone) | ported (difference) | req; tree; grow | fight port duel: channels |
| wild_strike | wild_strike | preset on sweep_stream: area (stone) | ported (difference) | req; aim transforms (an aim wrapper by hand); tree | fight port duel: channels |
| snipe | snipe | preset on shot_beat: projectile (stone) | ported (difference) | req | fight port duel: channels |
| frost_storm | frost_storm | preset on lay_stream: area (tide) | seam | req; seam: preset tags (duration) | fight port duel: channels |
| lightning_blast | lightning_blast | preset on lay_draw: area (storm) | ported (difference) | req | fight port duel: channels |
| inferno | inferno | preset on missile_stream: projectile (ember) | seam | req; grow; seam: preset tags (duration) | fight port duel: channels |
| infernal_ray | infernal_ray | preset on breath_stream: area (ember) | ported (difference) | req; tree | fight port duel: channels |
| sunpiercer | sunpiercer | preset on breath_draw: area (ember) | ported (difference) | arc taper dropped; req | fight port duel: channels |
| focusing_ray | focusing_ray | preset on breath_stream: area (storm) | ported (difference) | req | fight port duel: channels |
| serpent_ray | serpent_ray | preset on missile_stream: projectile (storm) | ported (difference) | req | fight port duel: channels |
| imperious_barrage | imperious_barrage | preset on missile_stream: projectile (void) | ported (difference) | req; aim transforms (an aim wrapper by hand) | fight port duel: channels |
| unstable_barrage | unstable_barrage | preset on hail_stream: scatter (ember) | seam | req; per-cast variance (row B31, by hand); seam: preset tags (duration) | fight port duel: channels |
| meteoric_bombardment | meteoric_bombardment | preset on hail_stream: scatter (ember) | seam | req; seam: preset tags (duration) | fight port duel: channels |
| perfect_strike | perfect_strike | preset on swing_beat: area (stone) | ported (difference) | req; tree | fight port duel: channels |
| timed_strike | timed_strike | preset on swing_beat: area (stone) | ported (difference) | req | fight port duel: channels |
| infinite_slashes | infinite_slashes | preset on swing_beat: area (stone) | ported (difference) | req | fight port duel: channels |
| shield_up | shield_up | preset on boon_stance:  | seam | req; tree; grow; seam: preset tags (duration) | fight port duel: channels |
| spiked_bulwark | spiked_bulwark | preset on boon_stance:  (stone) | seam | req; tree; grow; inn; seam: preset tags (duration) | fight port duel: channels |
| riposte | riposte | preset on cry_stance:  | seam | parry counter multiplier folded into the bash; req; tree; grow; seam: preset tags (duration) | fight port duel: channels |
| defiant_bulwark | defiant_bulwark | preset on boon_stance:  | seam | req; grow; seam: preset tags (duration) | fight port duel: channels |
| marching_bulwark | marching_bulwark | preset on boon_stance:  | seam | req; tree; grow; seam: preset tags (duration) | fight port duel: channels |
| runeward | runeward | preset on boon_stance:  | seam | req; grow; inn; seam: preset tags (duration) | fight port duel: channels |
| stone_communion | stone_communion | preset on boon_stance:  | blocked | req; grow; seam: preset tags (duration); blocked: conduits (row H7, open) | fight port duel: channels |
| sheathed_moon | sheathed_moon | preset on swing_draw: area (stone) | ported (difference) | req; grow | fight port duel: channels |
| lightning_bolt | lightning_bolt | preset on missile_beat: projectile (storm) | ported (difference) | req | fight port duel: channels |
| event_horizon | event_horizon | preset on lay_stream: area, field (void) | seam | req; seam: preset tags (duration) | fight port duel: channels |
| pestilent_nova | pestilent_nova | preset on missile_stream: projectile (void) | ported (difference) | req; aim transforms (an aim wrapper by hand) | fight port duel: channels |
| rolling_cannonade | rolling_cannonade | preset on missile_beat: projectile (ember) | ported (difference) | req | fight port duel: channels |
| gather_cinderkin |  | preset on boon_stream:  (ember) | not ported | req; grow; later: throngDirect: the throng fabric (row C38, later); later: the throng fabric (row C38) | fight port duel: channels |
| beckon_palewisps |  | preset on boon_stream:  (tide) | not ported | req; grow; later: throngDirect: the throng fabric (row C38, later); later: the throng fabric (row C38) | fight port duel: channels |
| raise_gnatveil |  | preset on boon_stream:  (stone) | not ported | req; tree; grow; later: throngDirect: the throng fabric (row C38, later); later: the throng fabric (row C38) | fight port duel: channels |
| loose_marrowgrubs |  | preset on boon_stream:  (stone) | not ported | req; grow; later: throngDirect: the throng fabric (row C38, later); later: the throng fabric (row C38) | fight port duel: channels |
| healing_stream | healing_stream | preset on smite_stream: heal | seam | req; grow; seam: preset tags (heal, targeted) | fight port duel: channels |
| lifedrain | lifedrain | preset on breath_stream: area (umbra) | ported (difference) | req; leech on the damage node, by hand | fight port duel: channels |
| arcane_missiles | arcane_missiles | preset on missile_stream: projectile (storm) | ported (difference) | req | fight port duel: channels |
| spirit_pyre | spirit_pyre | preset on call_stream: summon (ember) | seam | sum:remnant_shade<raging_spirit; req; tree; grow; seam: preset tags (duration) | fight port duel: channels |
| hurricane | hurricane | preset on burst_stream: area (storm) | ported (difference) | req | fight port duel: channels |
| flame_spear | flame_spear | preset on missile_beat: projectile (ember) | ported (difference) | req | fight port duel: channels |
| flame_blast | flame_blast | preset on lay_stream: area (ember) | seam | channel release authored by hand from {"dmgRamp":{"per":0.5,"max":4,"curve":"quadratic"},"aoeRamp":{"per":0.25,"max":1.6,"curve":"quadratic"},"pulses":false,"minHold":0.25}; req; seam: preset tags (duration) | fight port duel: channels |
| infernal_cannonade | infernal_cannonade | preset on missile_beat: projectile (ember) | ported (difference) | req | fight port duel: channels |
| volcano | volcano | preset on plant_draw: deploy (ember) | seam | req; seam: preset tags (duration) | fight port duel: channels |
| ice_shards | ice_shards | preset on missile_stream: projectile (tide) | ported (difference) | req | fight port duel: channels |
| ice_shield | ice_shield | preset on boon_stance:  (tide) | seam | req; tree; grow; seam: preset tags (duration) | fight port duel: channels |
| ice_blade | ice_blade | preset on swing_beat: area (tide) | ported (difference) | req; inn | fight port duel: channels |
| avalanche | avalanche | preset on breath_stream: area (tide) | ported (difference) | req | fight port duel: channels |
| reload_shells | reload_shells | preset on boon_stream: restore_uses | seam | seam: preset tags (reload, munition) | fight port duel: channels |
| flickerstep | flickerstep | preset on flicker_beat: move (stone) | ported (difference) | req; grow; inn | fight port duel: channels |
| immolation_rush | immolation_rush | preset on stride_draw: move (ember) | ported (difference) | req; inn | fight port duel: channels |
| tornado | tornado | preset on burst_stream: area (stone) | seam | req; inn; seam: preset tags (duration) | fight port duel: channels |
| sanguine_leech | sanguine_leech | preset on smite_stream: damage, apply_status (umbra) | seam | req; inn; leech on the damage node, by hand; seam: preset tags (targeted, duration) | fight port duel: channels |
| the_amalgam | the_amalgam | preset on boon_stream:  (void) | blocked | req; seam: preset tags (duration); blocked: the amalgam (row C48) | fight port duel: channels |
| arcswarm | arcswarm | preset on missile_stream: projectile (storm) | ported (difference) | release detonation of own projectiles: a detonate in the release, authored by hand; req | fight port duel: channels |
| long_exhale | long_exhale | preset on breath_draw: area (stone) | ported (difference) | req; tree; grow | fight port duel: channels |
| cerement | cerement | preset on burst_stream: area (void) | seam | req; seam: preset tags (duration) | fight port duel: channels |
| hailcrown | hailcrown | preset on hail_stream: scatter (tide) | seam | req; seam: preset tags (duration) | fight port duel: channels |
| surgewind | surgewind | preset on boon_stream: apply_status | seam | power-scaled buff: the modifiers do not scale with the use; channel release authored by hand from {"pulses":false}; req; grow; seam: preset tags (buff, duration) | fight port duel: channels |
| marrow_communion | marrow_communion | preset on burst_stream: area | seam | channel release authored by hand from {"pulses":false}; req; grow; seam: preset tags (heal, duration) | fight port duel: channels |
| kindled_ruin | kindled_ruin | preset on burst_stream: area (ember) | ported (difference) | channel release authored by hand from {"pulses":false,"requireFull":true,"dmgRamp":{"per":0.4,"max":1.6}}; req; grow | fight port duel: channels |
| glacier_crown | glacier_crown | preset on hail_stream: scatter (tide) | seam | channel release authored by hand from {"pulses":false,"minHold":0.6,"dmgRamp":{"per":0.28,"max":1.6}}; req; seam: preset tags (duration) | fight port duel: channels |
| head_of_steam | head_of_steam | preset on boon_stream: restore_uses (ember) | seam | req; grow; seam: preset tags (duration) | fight port duel: channels |

### auras and toggles (42: not ported 16, ported (difference) 1, seam 22, blocked 3)

| Prototype | Ours | Mechanism | Status | Note | Proof |
|---|---|---|---|---|---|
| sentinel_banner |  | aura kit skill | not ported | kit |  |
| sentinel_relief |  | aura kit skill | not ported | kit |  |
| court_stench |  | aura kit skill | not ported | kit |  |
| court_ember_halo |  | aura kit skill | not ported | kit |  |
| court_soul_veil |  | aura kit skill | not ported | kit |  |
| ossuary_command |  | aura kit skill | not ported | kit |  |
| ossuary_aegis |  | aura kit skill | not ported | kit |  |
| ossuary_mending |  | aura kit skill | not ported | kit |  |
| righteous_fire | righteous_fire | preset on mantle: aura (ember) | ported (difference) | the life drain the aura pays is its upkeep; upkeep as a fraction of maximum life: an expression by hand; req; grow | fight port duel: auras |
| vampiric_presence | vampiric_presence | preset on mantle: aura (void) | seam | siphon dropped; req; grow; seam: preset tags (duration) | fight port duel: auras |
| unholy_aura | unholy_aura | preset on mantle: aura (void) | blocked | req; grow; blocked: death spawns from an aura (row D26, authored by hand) | fight port duel: auras |
| devotion | devotion | preset on mantle: aura | seam | req; grow; seam: preset tags (buff) | fight port duel: auras |
| wellspring_stance | wellspring_stance | preset on mantle: aura | blocked | req; tree; grow; aura self modifiers become the preset modifiers (they scope by the weave): author on the domain by hand; seam: preset tags (buff, duration); blocked: conduits (row H7, open) | fight port duel: auras |
| preservation | preservation | preset on mantle: aura | seam | req; grow; seam: preset tags (buff, duration) | fight port duel: auras |
| discipline | discipline | preset on mantle_quick: aura | seam | req; grow; seam: preset tags (buff) | fight port duel: auras |
| mana_shield | mana_shield | preset on mantle_quick: aura | seam | req; seam: preset tags (buff) | fight port duel: auras |
| bristleback | bristleback | preset on mantle_quick: aura (stone) | seam | req; tree; grow; seam: preset tags (buff) | fight port duel: auras |
| soul_link | soul_link | preset on mantle_quick: aura | seam | req; grow; seam: preset tags (buff) | fight port duel: auras |
| ashen_vow | ashen_vow | preset on mantle_quick: aura (stone) | seam | upkeep as a fraction of maximum life: an expression by hand; req; tree; aura self modifiers become the preset modifiers (they scope by the weave): author on the domain by hand; seam: preset tags (buff) | fight port duel: auras |
| purity_of_elements | purity_of_elements | preset on mantle_quick: aura | seam | req; tree; grow; seam: preset tags (buff) | fight port duel: auras |
| purity_of_fire | purity_of_fire | preset on mantle_quick: aura (ember) | seam | req; grow; seam: preset tags (buff) | fight port duel: auras |
| purity_of_cold | purity_of_cold | preset on mantle_quick: aura (tide) | seam | req; grow; seam: preset tags (buff) | fight port duel: auras |
| purity_of_lightning | purity_of_lightning | preset on mantle_quick: aura (storm) | seam | req; grow; seam: preset tags (buff) | fight port duel: auras |
| determination | determination | preset on mantle_quick: aura | seam | req; grow; seam: preset tags (buff) | fight port duel: auras |
| rearguard_aegis |  | preset on mantle: aura | not ported | req; grow; seam: preset tags (buff, duration); later: aura.shellGuard (row B26 seals and ledgers, later) | fight port duel: auras |
| overclock |  | preset on mantle_quick: aura | not ported | req; thr; seam: preset tags (overdrive, buff); later: aura.overdrive (row B26 seals and ledgers, later) | fight port duel: auras |
| blood_mortgage |  | preset on mantle_quick: aura | not ported | req; thr; tree; seam: preset tags (overdrive, buff); later: aura.overdrive (row B26 seals and ledgers, later) | fight port duel: auras |
| berserk |  | preset on mantle_quick: aura (stone) | not ported | upkeep as a fraction of maximum life: an expression by hand; req; taps; thr; grow; aura self modifiers become the preset modifiers (they scope by the weave): author on the domain by hand; seam: preset tags (buff); later: aura.lockAboveLife (row B26 seals and ledgers, later) | fight port duel: auras |
| eye_of_the_storm | eye_of_the_storm | preset on mantle: aura (storm) | seam | req; seam: preset tags (buff, duration) | fight port duel: auras |
| static_field | static_field | preset on mantle_quick: aura (storm) | seam | req; seam: preset tags (buff) | fight port duel: auras |
| winters_mantle | winters_mantle | preset on mantle_quick: aura (tide) | seam | ally and enemy domains: the enemy domain is authored as a second aura by hand; req; seam: preset tags (buff) | fight port duel: auras |
| deathwatch | deathwatch | preset on mantle: aura (ember) | blocked | req; grow; seam: preset tags (buff); seam: preset behaviors (spend, equip_modifiers); blocked: charge-fed upkeep (row A17 charge lane, authored by hand) | fight port duel: auras |
| stormbind | stormbind | preset on mantle_quick: aura (storm) | seam | req; upkeep ramps by 0.14 a second: a form variant by hand; grow; aura self modifiers become the preset modifiers (they scope by the weave): author on the domain by hand; seam: preset tags (buff) | fight port duel: auras |
| emberbind | emberbind | preset on mantle_quick: aura (ember) | seam | req; upkeep ramps by 0.1 a second: a form variant by hand; grow; aura self modifiers become the preset modifiers (they scope by the weave): author on the domain by hand; seam: preset tags (buff) | fight port duel: auras |
| frostguard | frostguard | preset on mantle_quick: aura (tide) | seam | upkeep as a fraction of maximum mana: an expression by hand; req; grow; aura self modifiers become the preset modifiers (they scope by the weave): author on the domain by hand; seam: preset tags (buff) | fight port duel: auras |
| mortis_seal |  | preset on mantle_quick: aura (stone+void) | not ported | req; grow; seam: preset tags (buff, duration); seam: preset behaviors (toggle on_deactivate); later: aura.seal (row B26 seals and ledgers, later) | fight port duel: auras |
| seal_of_death |  | preset on mantle_quick: aura (stone+umbra) | not ported | upkeep as a fraction of maximum life: an expression by hand; req; upkeep ramps by 0.08 a second: a form variant by hand; grow; seam: preset tags (buff); seam: preset behaviors (toggle on_deactivate); later: aura.seal (row B26 seals and ledgers, later) | fight port duel: auras |
| arrears |  | preset on mantle_quick: aura | not ported | req; grow; seam: preset tags (buff, duration); seam: preset behaviors (meta_action); later: aura.ledger (row B26 seals and ledgers, later) | fight port duel: auras |
| grit_stance | grit_stance | preset on mantle_quick: aura | seam | req; grow; aura self modifiers become the preset modifiers (they scope by the weave): author on the domain by hand; seam: preset tags (buff, duration) | fight port duel: auras |
| reclamation |  | preset on mantle_quick: aura (storm) | not ported | req; grow; seam: preset tags (duration); later: aura.ledger (row B26 seals and ledgers, later) | fight port duel: auras |
| votive_ward | votive_ward | preset on mantle: aura | seam | req; grow; seam: preset tags (buff, duration, heal) | fight port duel: auras |
| tailwind | tailwind | preset on mantle: aura | seam | req; grow; seam: preset tags (buff, duration) | fight port duel: auras |

### summons and constructs (92: ported (difference) 14, not ported 31, seam 46, blocked 1)

| Prototype | Ours | Mechanism | Status | Note | Proof |
|---|---|---|---|---|---|
| summon_skeleton_mage | summon_skeleton_mage | preset on call: summon | ported (difference) | sum:remnant_archer<skeletal_pyromancer; sum:remnant_archer<skeletal_cryomancer; sum:remnant_archer<skeletal_stormcaller; sum:remnant_archer<skeletal_venomancer; summon.crewRules: the crew is our pattern's own kit; req; tree; grow | fight port duel: summons |
| summon_rubblekin |  | preset on pact: summon (stone) | not ported | sum:stone_warden<rubblekin; req; tree; grow; later: summon.strikeRelease (row B22, later); later: summon.escort (row B22, later) | fight port duel: summons |
| coppice_seedfall |  | summon kit skill | not ported | kit |  |
| heat_split | heat_split | preset on call: summon (ember) | ported (difference) | sum:shade_echo<heat_double; req; grow | fight port duel: summons |
| hellbore_mortar | hellbore_mortar | preset on call: summon (ember) | ported (difference) | sum:stone_warden<hellbore_engine; req; grow | fight port duel: summons |
| call_the_rift | call_the_rift | preset on call: summon (ember) | seam | sum:cinder_imp<ash_whelp; sum:cinder_imp<imp; req; grow; seam: preset behaviors (meta_action) | fight port duel: summons |
| summon_cherub | summon_cherub | preset on call: summon | seam | sum:sprig<cherub; req; grow; seam: preset tags (heal, duration) | fight port duel: summons |
| invoke_lampad | invoke_lampad | preset on call: summon | seam | sum:sprig<lampad_chorister; req; grow; seam: preset tags (song, duration) | fight port duel: summons |
| shamans_call | shamans_call | preset on call: summon | seam | sum:remnant_shade<zombie; plural targeting (several corpses) is one target here; req; grow; seam: preset tags (corpse) | fight port duel: summons |
| summon_plaguefather | summon_plaguefather | preset on call: summon (umbra) | seam | sum:stone_warden<plaguefather; req; grow; seam: preset tags (duration); seam: preset behaviors (meta_action) | fight port duel: summons |
| summon_skeleton | summon_skeleton | preset on call: summon | seam | sum:remnant_shade<skeleton_warrior; req; tree; grow; seam: preset behaviors (meta_action) | fight port duel: summons |
| summon_skeleton_archer | summon_skeleton_archer | preset on call: summon | ported (difference) | sum:remnant_archer<skeleton_archer; req; tree; grow | fight port duel: summons |
| summon_flame_sprite | summon_flame_sprite | preset on call: summon (ember) | ported (difference) | sum:cinder_imp<flame_sprite; req; grow | fight port duel: summons |
| raise_dead | raise_dead | preset on call: summon | seam | sum:remnant_shade<skeleton_warrior; sum:remnant_shade<zombie; req; tree; grow; seam: preset behaviors (meta_action) | fight port duel: summons |
| summon_sporeling | summon_sporeling | preset on call: summon | ported (difference) | sum:sprig<fungal_sporeling; req; grow | fight port duel: summons |
| summon_stone_golem | summon_stone_golem | preset on pact: summon | ported (difference) | sum:stone_warden<stone_golem; req; grow | fight port duel: summons |
| summon_bone_golem | summon_bone_golem | preset on pact: summon | ported (difference) | sum:stone_warden<bone_golem; req; tree; grow | fight port duel: summons |
| bind_familiar | bind_familiar | preset on pact: summon (void) | ported (difference) | sum:remnant_archer<arcane_familiar; req; tree; grow | fight port duel: summons |
| conjure_wisp | conjure_wisp | preset on call: summon (tide) | seam | sum:remnant_shade<spirit_wisp; req; grow; seam: preset tags (duration) | fight port duel: summons |
| temporal_pad |  | preset on plant: deploy | not ported | req; grow; seam: preset tags (duration); later: propelling pads (a launch pad fabric this port lacks) | fight port duel: summons |
| gate_shift | gate_shift | preset on plant: deploy | seam | req; grow; seam: preset tags (duration) | fight port duel: summons |
| raise_spectre |  | preset on call: summon | not ported | sum:remnant_shade<undefined; plural targeting (several corpses) is one target here; req; grow; seam: preset tags (corpse); later: summon.grimoire (row B22, later) | fight port duel: summons |
| revive | revive | preset on call: summon | seam | sum:remnant_shade<undefined; plural targeting (several corpses) is one target here; req; grow; seam: preset tags (corpse, duration) | fight port duel: summons |
| summon_fire_golem | summon_fire_golem | preset on pact: summon (ember) | ported (difference) | sum:stone_warden<fire_golem; req; grow | fight port duel: summons |
| summon_ice_golem | summon_ice_golem | preset on pact: summon (tide) | ported (difference) | sum:stone_warden<ice_golem; req; grow | fight port duel: summons |
| summon_blood_golem | summon_blood_golem | preset on pact: summon (stone) | ported (difference) | sum:stone_warden<blood_golem; req; grow | fight port duel: summons |
| orb_of_storms | orb_of_storms | preset on plant: deploy (storm) | seam | req; grow; seam: preset tags (duration) | fight port duel: summons |
| flame_totem | flame_totem | preset on plant: deploy (ember) | seam | req; grow; seam: preset tags (duration) | fight port duel: summons |
| ballista_sentry | ballista_sentry | preset on plant: deploy (stone) | seam | req; tree; grow; seam: preset tags (duration) | fight port duel: summons |
| storm_pylon | storm_pylon | preset on plant: deploy (storm) | seam | req; grow; seam: preset tags (duration) | fight port duel: summons |
| lodestone | lodestone | preset on plant: deploy | seam | req; thr; grow; seam: preset tags (duration) | fight port duel: summons |
| bone_prison | bone_prison | preset on plant: deploy (stone) | seam | req; seam: preset tags (duration) | fight port duel: summons |
| bone_cage | bone_cage | preset on plant: deploy (stone) | seam | req; seam: preset tags (duration) | fight port duel: summons |
| stone_rampart | stone_rampart | preset on plant: deploy (stone) | seam | req; tree; grow; seam: preset tags (duration) | fight port duel: summons |
| sanctuary | sanctuary | preset on plant: deploy | seam | req; grow; seam: preset tags (duration) | fight port duel: summons |
| repulsor_beacon | repulsor_beacon | preset on plant: deploy (storm) | seam | req; seam: preset tags (duration) | fight port duel: summons |
| summon_blade_wraith | summon_blade_wraith | preset on call: summon (umbra+stone) | ported (difference) | sum:remnant_shade<blade_wraith; req; grow | fight port duel: summons |
| summon_swarmlings | summon_swarmlings | preset on pact: summon | blocked | sum:sprig<swarmling; req; tree; grow; per-body handling is the pattern behavior; seam: preset tags (duration); seam: preset behaviors (meta_action); blocked: the hivecall contract | fight port duel: summons |
| cast_falcon | cast_falcon | preset on pact: summon | seam | sum:wolf<hunting_falcon; req; tree; grow; seam: preset tags (duration) | fight port duel: summons |
| tree_of_life | tree_of_life | preset on plant: deploy | seam | req; grow; seam: preset tags (heal, duration) | fight port duel: summons |
| font_of_renewal | font_of_renewal | preset on plant: deploy | seam | req; grow; seam: preset tags (heal, duration) | fight port duel: summons |
| summon_cleric | summon_cleric | preset on call: summon | seam | sum:sprig<skeletal_cleric; req; grow; seam: preset tags (heal) | fight port duel: summons |
| spirit_mender | spirit_mender | preset on call: summon | seam | sum:sprig<mender_sprite; req; grow; seam: preset tags (heal, duration) | fight port duel: summons |
| summon_bats | summon_bats | preset on call: summon | seam | sum:wolf<crimson_bat; req; grow; seam: preset behaviors (meta_action) | fight port duel: summons |
| disgorge_thralls |  | summon kit skill | not ported | kit |  |
| egg_hatch_spiders |  | summon kit skill | not ported | kit |  |
| egg_hatch_chitin |  | summon kit skill | not ported | kit |  |
| wake_the_scree |  | summon kit skill | not ported | kit |  |
| egg_hatch_maggots |  | summon kit skill | not ported | kit |  |
| bramble_hedge |  | preset on plant: deploy, apply_status (stone) | not ported | req; grow; seam: preset tags (duration); later: construct.breakable (rows B16/D24, later); later: construct.clearway (rows B16/D24, later) | fight port duel: summons |
| hell_rift | hell_rift | preset on plant: deploy (ember) | seam | req; grow; seam: preset tags (duration) | fight port duel: summons |
| mirage_archer |  | preset on plant: deploy | not ported | req; thr; grow; seam: preset tags (mirage, duration); later: construct.echo (rows B16/D24, later) | fight port duel: summons |
| shadow_clone |  | preset on plant: deploy | not ported | req; thr; tree; grow; seam: preset tags (clone, duration); later: construct.echo (rows B16/D24, later) | fight port duel: summons |
| summon_raging_spirit | summon_raging_spirit | preset on call: summon (ember) | seam | sum:remnant_shade<raging_spirit; req; tree; grow; seam: preset tags (duration) | fight port duel: summons |
| summon_wraith | summon_wraith | preset on call: summon (umbra) | ported (difference) | sum:remnant_shade<decay_wraith; req; tree; grow | fight port duel: summons |
| infernal_bombardment | infernal_bombardment | preset on call: summon (ember) | seam | sum:stone_warden<bombard_demon; req; grow; seam: preset behaviors (meta_action) | fight port duel: summons |
| flame_core | flame_core | preset on call: summon (ember) | seam | sum:cinder_imp<flame_core; req; seam: preset tags (duration) | fight port duel: summons |
| solar_orb | solar_orb | preset on plant: deploy (ember) | seam | req; seam: preset tags (duration) | fight port duel: summons |
| spew_dead |  | summon kit skill | not ported | kit |  |
| spew_brood |  | summon kit skill | not ported | kit |  |
| spew_flame |  | summon kit skill | not ported | kit |  |
| spew_rime |  | summon kit skill | not ported | kit |  |
| spew_shards |  | summon kit skill | not ported | kit |  |
| spew_spores |  | summon kit skill | not ported | kit |  |
| spew_grubs |  | summon kit skill | not ported | kit |  |
| spew_flesh |  | summon kit skill | not ported | kit |  |
| spew_formics |  | summon kit skill | not ported | kit |  |
| egg_hatch_formics |  | summon kit skill | not ported | kit |  |
| summon_scarecrow | summon_scarecrow | preset on call: summon | seam | sum:stone_warden<bound_scarecrow; req; grow; seam: preset behaviors (meta_action) | fight port duel: summons |
| summon_crows |  | summon kit skill | not ported | kit |  |
| spew_rats |  | summon kit skill | not ported | kit |  |
| warden_relic | warden_relic | preset on plant: deploy (stone) | seam | req; seam: preset tags (duration) | fight port duel: summons |
| mender_relic | mender_relic | preset on plant: deploy | seam | req; seam: preset tags (duration, heal) | fight port duel: summons |
| shambler_horde | shambler_horde | preset on call: summon | seam | sum:remnant_shade<grave_shambler; req; tree; grow; seam: preset tags (duration) | fight port duel: summons |
| stone_spires |  | preset on plant: deploy, apply_status (stone) | not ported | req; grow; seam: preset tags (duration); later: construct.breakable (rows B16/D24, later); later: construct.clearway (rows B16/D24, later) | fight port duel: summons |
| frost_wall |  | preset on plant: deploy, apply_status (tide) | not ported | req; grow; seam: preset tags (duration); later: construct.breakable (rows B16/D24, later); later: construct.clearway (rows B16/D24, later) | fight port duel: summons |
| wall_of_meat |  | preset on plant: deploy, apply_status (stone) | not ported | req; grow; seam: preset tags (duration); later: construct.breakable (rows B16/D24, later); later: construct.clearway (rows B16/D24, later) | fight port duel: summons |
| shardrift | shardrift | preset on plant: deploy (tide) | seam | req; seam: preset tags (duration) | fight port duel: summons |
| stormbrand_beacon | stormbrand_beacon | preset on plant: deploy (ember+storm) | seam | third:tide; req; seam: preset tags (duration) | fight port duel: summons |
| glacial_bulwark | glacial_bulwark | preset on plant: deploy, apply_status (tide) | seam | req; seam: preset tags (duration) | fight port duel: summons |
| battle_standard | battle_standard | preset on plant: deploy | seam | req; thr; tree; grow; seam: preset tags (duration) | fight port duel: summons |
| summon_harvester |  | preset on pact: summon (stone) | not ported | sum:stone_warden<harvester; req; grow; seam: preset behaviors (meta_action); later: summon.devour (row B22, later) | fight port duel: summons |
| call_harvester |  | preset on call: summon (stone) | not ported | sum:stone_warden<harvester; req; grow; seam: preset tags (duration); seam: preset behaviors (meta_action); later: summon.devour (row B22, later) | fight port duel: summons |
| reaper_swarm | reaper_swarm | preset on call: summon (stone) | seam | sum:remnant_shade<lesser_reaper; req; grow; seam: preset tags (duration) | fight port duel: summons |
| torpor_field | torpor_field | preset on plant: deploy | seam | req; tree; seam: preset tags (duration) | fight port duel: summons |
| brood_hatch |  | summon kit skill | not ported | kit |  |
| holy_relic | holy_relic | preset on plant: deploy (stone) | seam | req; seam: preset tags (duration, heal) | fight port duel: summons |
| tolling_bell | tolling_bell | preset on plant: deploy (stone) | seam | req; seam: preset tags (duration) | fight port duel: summons |
| brood_clutch_hatch |  | summon kit skill | not ported | kit |  |
| colonnade | colonnade | preset on plant: deploy (ember) | seam | req; thr; grow; seam: preset tags (duration) | fight port duel: summons |
| wisp_call | wisp_call | preset on call: summon | seam | sum:remnant_shade<cirrus_fingerling; req; grow; seam: preset behaviors (meta_action) | fight port duel: summons |
| grave_tide | grave_tide | preset on call: summon | seam | sum:remnant_shade<skeleton_warrior; sum:remnant_shade<zombie; req; ult; the gauge (a charge bank by hand); tree; grow; seam: preset tags (duration, ultimate) | fight port duel: summons |

### movement (43: not ported 20, ported (difference) 13, seam 7, blocked 2, ported 1)

| Prototype | Ours | Mechanism | Status | Note | Proof |
|---|---|---|---|---|---|
| beast_pursuit |  | leap kit skill | not ported | kit |  |
| beast_cat_art |  | leap kit skill | not ported | kit |  |
| beast_raptor_art |  | leap kit skill | not ported | kit |  |
| court_bone_lunge |  | dash kit skill | not ported | kit |  |
| court_wraith_lunge |  | dash kit skill | not ported | kit |  |
| dune_surge | dune_surge | preset on stride: move (stone) | ported (difference) | req; grow | fight port duel: movement |
| mirage_step | mirage_step | preset on flicker_quick: move, apply_status (ember) | ported (difference) | req; grow | fight port duel: movement |
| panther_pounce | panther_pounce | preset on vault_quick: move (stone) | ported (difference) | req; grow | fight port duel: movement |
| shield_charge | shield_charge | preset on stride_quick: move (stone) | ported (difference) | req; tree; grow | fight port duel: movement |
| dash | dash | preset on stride_quick: move | seam | req; tree; grow; seam: preset tags (instant) | fight port duel: movement |
| charge | charge | preset on stride_quick: move (stone) | ported (difference) | req; tree; grow | fight port duel: movement |
| gore_charge |  | dash kit skill | not ported | kit |  |
| caged_comet |  | preset on carom_quick:  (stone) | not ported | req; grow; later: carom delivery (row B25, later) | fight port duel: movement |
| warp | warp | preset on flicker_quick: move | seam | req; tree; grow; seam: preset tags (duration) | fight port duel: movement |
| teleport | teleport | preset on flicker_quick: move | seam | req; grow; seam: preset tags (instant) | fight port duel: movement |
| shadow_step | shadow_step | preset on flicker_quick: move | ported (difference) | req; tree; grow | fight port duel: movement |
| decoy |  | preset on stride_quick: move | not ported | req; tree; grow; seam: preset tags (duration); later: dash.decoyDuration (a fabric this port lacks) | fight port duel: movement |
| corpse_shift | corpse_shift | preset on flicker_quick: move | seam | req; grow; seam: preset tags (corpse) | fight port duel: movement |
| mark | mark | preset on waymark_quick: mark | ported (difference) | req; grow | fight port duel: movement |
| dash_strike | dash_strike | preset on stride_quick: move (stone) | ported (difference) | req; tree | fight port duel: movement |
| crushing_leap | crushing_leap | preset on vault_quick: move (stone) | ported (difference) | req; grow | fight port duel: movement |
| take_wing |  | leap kit skill | not ported | kit |  |
| surge | surge | preset on stride_quick: move (storm) | seam | req; seam: preset tags (instant) | fight port duel: movement |
| cape_feint |  | preset on stride_quick: move (stone) | not ported | req; tree; grow; later: dash.decoyDuration (a fabric this port lacks) | fight port duel: movement |
| locust_dive |  | leap kit skill | not ported | kit |  |
| saltant_slam |  | leap kit skill | not ported | kit |  |
| condor_stoop |  | leap kit skill | not ported | kit |  |
| phoenix_dive | phoenix_dive | preset on vault_quick: move (ember) | ported (difference) | req | fight port duel: movement |
| trailblaze |  | preset on stride_quick: move (ember) | not ported | req; seam: preset tags (duration, instant); later: dash.trailZone (a fabric this port lacks) | fight port duel: movement |
| shatterstep | shatterstep | preset on flicker_quick: move, field (tide) | blocked | req; seam: preset tags (instant); blocked: terrain: timed terrain (row C11, open) | fight port duel: movement |
| riftstep | riftstep | preset on flicker_quick: move (stone) | seam | req; thr; inn; seam: preset behaviors (uses) | fight port duel: movement |
| riftstorm | riftstorm | preset on flicker_quick: move (stone) | seam | req; taps; thr; grow; inn; seam: preset behaviors (spend) | fight port duel: movement |
| closing_fang | closing_fang | preset on stride_quick: move (stone) | ported (difference) | corridor damage scale folded into the hit; req; inn | fight port duel: movement |
| reaper_lunge | reaper_lunge | preset on stride_quick: move (stone) | ported |  | fight port duel: movement |
| skeletal_lunge |  | dash kit skill | not ported | kit |  |
| vent_ride |  | leap kit skill | not ported | kit |  |
| spout_hop |  | leap kit skill | not ported | kit |  |
| geyser_step | geyser_step | preset on vault: move (ember) | blocked | req; grow; blocked: leap.vent (fog banks, row C36) | fight port duel: movement |
| vent_hop |  | preset on stride_quick: move (ember) | not ported | req; grow; seam: preset tags (instant); later: dash.trailVent (a fabric this port lacks) | fight port duel: movement |
| skyfall | skyfall | preset on vault_quick: move (stone+storm) | ported (difference) | req; grow | fight port duel: movement |
| cloudstep |  | preset on stride_quick: move (stone) | not ported | req; tree; grow; later: dash.decoyDuration (a fabric this port lacks) | fight port duel: movement |
| squall_bite | squall_bite | preset on stride_quick: move (stone+tide) | ported (difference) | req; grow | fight port duel: movement |
| zephyr_step |  | preset on stride_quick: move | not ported | req; thr; grow; later: dash.trailConjure (a fabric this port lacks) | fight port duel: movement |

### traps and placements (165: not ported 80, seam 65, ported (difference) 13, ported 4, blocked 3)

| Prototype | Ours | Mechanism | Status | Note | Proof |
|---|---|---|---|---|---|
| hive_death_pool |  | ground kit skill | not ported | kit |  |
| hive_royal_cataclysm |  | ground kit skill | not ported | kit |  |
| skeletal_arrowfall |  | storm kit skill | not ported | kit |  |
| skeletal_cinder_rain |  | storm kit skill | not ported | kit |  |
| rhizarch_upheaval |  | ground kit skill | not ported | kit |  |
| pitcher_spill |  | ground kit skill | not ported | kit |  |
| sundew_glaze |  | ground kit skill | not ported | kit |  |
| arena_boss_bass |  | ground kit skill | not ported | kit |  |
| arena_boss_treble |  | ground kit skill | not ported | kit |  |
| arena_boss_requiem |  | ground kit skill | not ported | kit |  |
| arena_boss_furnace |  | ground kit skill | not ported | kit |  |
| arena_boss_ember |  | ground kit skill | not ported | kit |  |
| arena_boss_seep |  | ground kit skill | not ported | kit |  |
| arena_boss_pulse |  | ground kit skill | not ported | kit |  |
| primeval_left_hand |  | ground kit skill | not ported | kit |  |
| primeval_right_hand |  | ground kit skill | not ported | kit |  |
| primeval_crownfall |  | ground kit skill | not ported | kit |  |
| primeval_fault |  | ground kit skill | not ported | kit |  |
| primeval_venom_well |  | ground kit skill | not ported | kit |  |
| primeval_upheaval |  | ground kit skill | not ported | kit |  |
| titan_caldera |  | ground kit skill | not ported | kit |  |
| titan_whiteout |  | ground kit skill | not ported | kit |  |
| sovereign_rimeheart |  | ground kit skill | not ported | kit |  |
| sovereign_stormcrown |  | ground kit skill | not ported | kit |  |
| flask_volatile_bomb |  | ground kit skill | not ported | kit |  |
| flask_cold_wake |  | ground kit skill | not ported | kit |  |
| flask_fault |  | ground kit skill | not ported | kit |  |
| flask_venom_bed |  | ground kit skill | not ported | kit |  |
| ironbell_step |  | ground kit skill | not ported | kit |  |
| drowning_grasp | drowning_grasp | preset on lay: area, field (tide+stone) | seam | req; grow; seam: preset tags (duration) | fight port duel: placements |
| infernal_rift | infernal_rift | preset on lay: scatter (ember) | seam | req; seam: preset tags (duration) | fight port duel: placements |
| hate_eruption | hate_eruption | preset on lay: scatter (void) | seam | req; seam: preset tags (duration) | fight port duel: placements |
| null_verge | null_verge | preset on lay: field (void) | seam | req; seam: preset tags (duration) | fight port duel: placements |
| brimstone_volley | brimstone_volley | preset on hail: scatter (ember+stone) | seam | req; seam: preset tags (duration) | fight port duel: placements |
| rain_of_ash | rain_of_ash | preset on lay: field (ember) | seam | req; seam: preset tags (duration) | fight port duel: placements |
| hellshot_volley |  | storm kit skill | not ported | kit |  |
| hellbore_lob |  | storm kit skill | not ported | kit |  |
| vile_clutch |  | storm kit skill | not ported | kit |  |
| gravecast |  | storm kit skill | not ported | kit |  |
| whelp_toss |  | storm kit skill | not ported | kit |  |
| levinshot_volley |  | storm kit skill | not ported | kit |  |
| stormcall_strike |  | storm kit skill | not ported | kit |  |
| storm_call | storm_call | preset on lay: scatter (storm) | ported (difference) | req; tree | fight port duel: placements |
| starfall_shard | starfall_shard | preset on lay: scatter (stone+tide) | ported (difference) | req | fight port duel: placements |
| meteor |  | ground kit skill | not ported | kit |  |
| spore_bloom | spore_bloom | preset on lay: field (umbra) | seam | req; grow; seam: preset tags (duration) | fight port duel: placements |
| sparklattice | sparklattice | preset on hail_quick: scatter (storm) | seam | strikes armed during a channel fire on release: author with a channel release by hand; req; grow; seam: preset tags (duration) | fight port duel: placements |
| halo_of_light |  | preset on lay: field (ember) | not ported | req; grow; seam: preset tags (heal, duration); later: ground.retract (a placement motion this port lacks) | fight port duel: placements |
| toxic_cloud | toxic_cloud | preset on lay: field (void) | seam | req; seam: preset tags (duration) | fight port duel: placements |
| stone_shards |  | ground kit skill | not ported | kit |  |
| meteor_storm | meteor_storm | preset on hail: scatter (ember) | seam | req; seam: preset tags (duration) | fight port duel: placements |
| frost_trap | frost_trap | preset on trap: deploy (tide) | seam | req; grow; seam: preset tags (duration) | fight port duel: placements |
| fire_mine | fire_mine | preset on trap: deploy (ember) | seam | req; grow; seam: preset tags (mine, duration); seam: preset behaviors (meta_action) | fight port duel: placements |
| tempest | tempest | preset on lay: area, field (storm) | seam | req; seam: preset tags (duration) | fight port duel: placements |
| gale | gale | preset on lay: area, field (stone) | seam | req; seam: preset tags (duration) | fight port duel: placements |
| sunder | sunder | preset on slam: cascade (stone) | ported (difference) | req | fight port duel: placements |
| tolling_ruin | tolling_ruin | preset on slam: cascade (stone) | ported (difference) | req | fight port duel: placements |
| epicenter | epicenter | preset on lay: scatter (stone) | seam | ground pulses repeat the area where it stood (delay and steps folded into area.pulses); req; seam: preset tags (pulse) | fight port duel: placements |
| skipping_stone | skipping_stone | preset on slam: cascade (stone) | ported (difference) | req | fight port duel: placements |
| crumble | crumble | preset on lay: scatter (stone) | seam | ground pulses repeat the area where it stood (delay and steps folded into area.pulses); req; seam: preset tags (duration, pulse) | fight port duel: placements |
| carillon | carillon | preset on lay: scatter (stone) | seam | ground pulses repeat the area where it stood (delay and steps folded into area.pulses); req; seam: preset tags (duration, pulse) | fight port duel: placements |
| rising_knell | rising_knell | preset on lay: scatter (stone) | seam | ground pulses repeat the area where it stood (delay and steps folded into area.pulses); req; seam: preset tags (duration, pulse) | fight port duel: placements |
| fumarole | fumarole | preset on lay: area, field (ember) | seam | req; grow; seam: preset tags (duration) | fight port duel: placements |
| upheaval | upheaval | preset on slam: area, field (stone) | seam | req; seam: preset tags (duration) | fight port duel: placements |
| groundswell | groundswell | preset on slam: cascade (stone) | ported (difference) | req | fight port duel: placements |
| fume_vent |  | ground kit skill | not ported | kit |  |
| sap_trail |  | ground kit skill | not ported | kit |  |
| healing_rain | healing_rain | preset on lay: area, field | seam | req; grow; seam: preset tags (heal, duration) | fight port duel: placements |
| consecration | consecration | preset on lay: area, field (ember) | seam | req; tree; grow; seam: preset tags (heal, duration) | fight port duel: placements |
| volcanic_fissure | volcanic_fissure | preset on slam: area, field (stone+ember) | seam | req; seam: preset tags (duration) | fight port duel: placements |
| fissure_burst | fissure_burst | preset on lay_quick: scatter (ember) | ported |  | fight port duel: placements |
| eruption | eruption | preset on lay: area, field (ember) | seam | req; seam: preset tags (duration) | fight port duel: placements |
| thunderstorm | thunderstorm | preset on lay: area, field (storm) | seam | req; seam: preset tags (duration) | fight port duel: placements |
| thunder_bolt | thunder_bolt | preset on lay_quick: scatter (storm) | ported |  | fight port duel: placements |
| entangle | entangle | preset on lay: area, field (stone) | seam | req; seam: preset tags (duration) | fight port duel: placements |
| lash_tendril | lash_tendril | preset on lay_quick: scatter (stone+void) | ported |  | fight port duel: placements |
| root_grasp | root_grasp | preset on lay: scatter (stone) | seam | req; grow; seam: preset tags (duration) | fight port duel: placements |
| sporefall | sporefall | preset on hail: scatter (umbra) | seam | req; seam: preset tags (duration) | fight port duel: placements |
| stalactite_fall | stalactite_fall | preset on hail: scatter (stone+tide) | ported (difference) | req; grow | fight port duel: placements |
| digest | digest | preset on lay: area, field (void) | seam | req; grow; seam: preset tags (duration) | fight port duel: placements |
| lay_brood_egg |  | preset on trap: deploy | not ported | req; seam: preset tags (duration); later: construct.hatch (rows B16/D24, later) | fight port duel: placements |
| lay_chitin_clutch |  | preset on trap: deploy | not ported | req; seam: preset tags (duration); later: construct.hatch (rows B16/D24, later) | fight port duel: placements |
| lay_grub_clutch |  | preset on trap: deploy | not ported | req; seam: preset tags (duration); later: construct.hatch (rows B16/D24, later) | fight port duel: placements |
| strangler_seed |  | preset on trap: deploy (stone+void) | not ported | req; grow; seam: preset tags (duration); later: construct.hatch (rows B16/D24, later) | fight port duel: placements |
| vine_bloom |  | ground kit skill | not ported | kit |  |
| creeping_thicket | creeping_thicket | preset on lay: area, field (stone+void) | seam | req; seam: preset tags (duration) | fight port duel: placements |
| rune_of_power | rune_of_power | preset on lay: area, field | seam | req; tree; grow; seam: preset tags (buff, duration) | fight port duel: placements |
| toxic_domain | toxic_domain | preset on lay: area, field (umbra) | seam | req; seam: preset tags (duration, curse) | fight port duel: placements |
| soporific_veil | soporific_veil | preset on lay: field (umbra) | seam | exposure domain dropped; req; grow; seam: preset tags (duration, curse) | fight port duel: placements |
| thurible | thurible | preset on lay: field | seam | exposure domain dropped; req; grow; seam: preset tags (duration, buff) | fight port duel: placements |
| reap | reap | preset on slam: area, field (stone+umbra) | seam | req; tree; seam: preset tags (duration, sweep) | fight port duel: placements |
| pillar_of_flame |  | preset on lay: area, field (ember) | not ported | req; tree; seam: preset tags (duration); later: ground.fillFrom (a placement motion this port lacks); later: ground.fillTime (a placement motion this port lacks) | fight port duel: placements |
| flame_wall | flame_wall | preset on lay: area, field (ember) | seam | req; seam: preset tags (duration) | fight port duel: placements |
| icy_comet | icy_comet | preset on lay: scatter (tide) | blocked | req; seam: preset tags (duration); blocked: ground.leaveTerrain (row C11, open) | fight port duel: placements |
| call_of_the_deep | call_of_the_deep | preset on lay: area, field (tide) | seam | seam: preset tags (duration) | fight port duel: placements |
| glare_ice | glare_ice | preset on lay: scatter (tide) | blocked | seam: preset tags (duration); blocked: ground.leaveTerrain (row C11, open) | fight port duel: placements |
| cold_vortex | cold_vortex | preset on lay: area, field (tide) | seam | req; seam: preset tags (duration) | fight port duel: placements |
| creeping_ice | creeping_ice | preset on lay: area, field (tide) | seam | req; seam: preset tags (duration) | fight port duel: placements |
| lay_formic_clutch |  | preset on trap: deploy | not ported | req; seam: preset tags (duration); later: construct.hatch (rows B16/D24, later) | fight port duel: placements |
| silk_snare |  | ground kit skill | not ported | kit |  |
| honeydew_lure | honeydew_lure | preset on lay: area, field (void) | seam | req; grow; seam: preset tags (duration) | fight port duel: placements |
| gourd_bomb | gourd_bomb | preset on lay: scatter (ember) | seam | req; grow; seam: preset tags (duration) | fight port duel: placements |
| wax_drip |  | ground kit skill | not ported | kit |  |
| epidemic | epidemic | preset on lay: scatter (void) | seam | req; grow; seam: preset tags (duration) | fight port duel: placements |
| malediction | malediction | preset on lay: area, field (umbra) | blocked | req; seam: preset tags (curse, duration); blocked: linked hexes (row C48) | fight port duel: placements |
| wildfire_sweep |  | preset on lay: area, field (ember) | not ported | req; seam: preset tags (duration); later: ground.fillFrom (a placement motion this port lacks); later: ground.fillTime (a placement motion this port lacks) | fight port duel: placements |
| reavers_sweep |  | preset on slam: field (stone) | not ported | req; seam: preset tags (duration, sweep); later: ground.sweep (a placement motion this port lacks) | fight port duel: placements |
| scythe_arc |  | preset on slam: field (stone) | not ported | req; seam: preset tags (duration, sweep); later: ground.sweep (a placement motion this port lacks) | fight port duel: placements |
| follow_sweep |  | preset on slam_quick: field (stone) | not ported | seam: preset tags (duration, sweep); later: ground.sweep (a placement motion this port lacks) | fight port duel: placements |
| closing_shears |  | preset on slam: field (stone) | not ported | req; seam: preset tags (duration, sweep); later: ground.sweep (a placement motion this port lacks) | fight port duel: placements |
| sparkfield | sparkfield | preset on hail: scatter (storm) | ported (difference) | req | fight port duel: placements |
| squall_rune |  | preset on lay: area, field (storm) | not ported | req; seam: preset tags (buff, duration); later: ground.retract (a placement motion this port lacks) | fight port duel: placements |
| devouring_swarm | devouring_swarm | preset on lay: area, field (void) | seam | req; seam: preset tags (duration) | fight port duel: placements |
| blizzard_coil | blizzard_coil | preset on lay: area, field (tide) | seam | req; seam: preset tags (duration) | fight port duel: placements |
| fissure |  | preset on lay: scatter (stone+ember) | not ported | req; seam: preset tags (duration, fissure); later: ground.fissure (a placement motion this port lacks) | fight port duel: placements |
| netherfissure |  | preset on lay: area, field (ember+void) | not ported | req; seam: preset tags (duration, fissure); later: ground.fissure (a placement motion this port lacks) | fight port duel: placements |
| grasping_chasm |  | preset on lay: area, field (stone+void) | not ported | req; seam: preset tags (duration, fissure); later: ground.fissure (a placement motion this port lacks) | fight port duel: placements |
| chasm_tendril | chasm_tendril | preset on lay_quick: scatter (stone+void) | ported |  | fight port duel: placements |
| faultbreak |  | preset on slam: area, field (stone) | not ported | req; seam: preset tags (duration, fissure); later: ground.fissure (a placement motion this port lacks) | fight port duel: placements |
| restless_earth |  | preset on lay: area, field (stone) | not ported | req; grow; seam: preset tags (duration, fissure); later: ground.fissure (a placement motion this port lacks); later: ground.strobe (a placement motion this port lacks) | fight port duel: placements |
| fleshspur | fleshspur | preset on lay: scatter (stone+void) | seam | ground pulses repeat the area where it stood (delay and steps folded into area.pulses); req; seam: preset tags (pulse) | fight port duel: placements |
| thundermark |  | preset on lay: scatter (storm) | not ported | req; seam: preset tags (duration); later: ground.marker (a placement motion this port lacks) | fight port duel: placements |
| beacon_bolt | beacon_bolt | preset on lay_quick: scatter (ember+storm) | ported (difference) | third:tide | fight port duel: placements |
| glacial_rampart | glacial_rampart | preset on lay: area, field (tide) | seam | req; seam: preset tags (duration) | fight port duel: placements |
| cinderwhirl_trap | cinderwhirl_trap | preset on trap: deploy, apply_status (ember) | seam | req; seam: preset tags (duration) | fight port duel: placements |
| cinder_whirl | cinder_whirl | preset on lay_quick: area, field (ember) | seam | seam: preset tags (duration) | fight port duel: placements |
| aftershock_snare | aftershock_snare | preset on trap: deploy, apply_status (stone) | ported (difference) | req; tree | fight port duel: placements |
| caltrops | caltrops | preset on lay: area, field (stone) | seam | req; tree; grow; seam: preset tags (duration) | fight port duel: placements |
| war_chant | war_chant | preset on lay: field | seam | exposure domain dropped; req; taps; tree; grow; seam: preset tags (song, duration, buff) | fight port duel: placements |
| dissonance | dissonance | preset on lay: field (void) | seam | req; taps; tree; grow; seam: preset tags (song, duration) | fight port duel: placements |
| glacial_march | glacial_march | preset on lay: cascade (tide) | ported (difference) | req | fight port duel: placements |
| arrowfall | arrowfall | preset on barrage: scatter (stone) | ported (difference) | req | fight port duel: placements |
| invoke_conflagration |  | ground kit skill | not ported | kit |  |
| invoke_stormfront |  | storm kit skill | not ported | kit |  |
| invoke_glaciation |  | ground kit skill | not ported | kit |  |
| invoke_cataclysm |  | storm kit skill | not ported | kit |  |
| invoke_burst |  | ground kit skill | not ported | kit |  |
| oblation_of_flesh | oblation_of_flesh | preset on lay: area, field | seam | plural targeting (several corpses) is one target here; req; grow; seam: preset tags (corpse, duration) | fight port duel: placements |
| venom_seep |  | ground kit skill | not ported | kit |  |
| jelly_trail |  | ground kit skill | not ported | kit |  |
| maw_bloom | maw_bloom | preset on lay: area, field (void+stone) | seam | req; grow; seam: preset tags (duration) | fight port duel: placements |
| skyfall_volley | skyfall_volley | preset on barrage: scatter (stone) | seam | req; seam: preset tags (javelin) | fight port duel: placements |
| levinfall | levinfall | preset on hail: scatter (storm) | ported (difference) | req | fight port duel: placements |
| broodpod |  | preset on trap: deploy | not ported | req; seam: preset tags (duration); later: construct.hatch (rows B16/D24, later) | fight port duel: placements |
| nitrocask |  | preset on trap: deploy (ember) | not ported | req; seam: preset tags (duration); later: construct.hatch (rows B16/D24, later) | fight port duel: placements |
| scald_vent_storm |  | storm kit skill | not ported | kit |  |
| blowhole | blowhole | preset on hail: scatter (ember) | seam | req; grow; seam: preset behaviors (uses) | fight port duel: placements |
| cistern_boil |  | storm kit skill | not ported | kit |  |
| scald_rain_drop |  | ground kit skill | not ported | kit |  |
| cinder_fall |  | ground kit skill | not ported | kit |  |
| lay_brood_clutch |  | construct kit skill | not ported | kit |  |
| geysermaw_column |  | storm kit skill | not ported | kit |  |
| geysermaw_spray |  | storm kit skill | not ported | kit |  |
| hush_toll |  | ground kit skill | not ported | kit |  |
| grave_grasp |  | ground kit skill | not ported | kit |  |
| judgement_pillar | judgement_pillar | preset on lay: area, field (ember+storm) | seam | ground pulses repeat the area where it stood (delay and steps folded into area.pulses); req; grow; seam: preset tags (duration) | fight port duel: placements |
| starcall | starcall | preset on lay: area, field (ember+stone) | seam | ground pulses repeat the area where it stood (delay and steps folded into area.pulses); req; thr; grow; seam: preset tags (duration) | fight port duel: placements |
| gloria | gloria | preset on lay: field (ember+storm) | seam | req; grow; seam: preset tags (duration) | fight port duel: placements |
| updraft_burst | updraft_burst | preset on lay: area, field (tide+stone) | seam | ground pulses repeat the area where it stood (delay and steps folded into area.pulses); req; thr; grow; seam: preset tags (duration) | fight port duel: placements |
| downburst | downburst | preset on lay: scatter (tide+stone) | seam | req; grow; seam: preset tags (duration) | fight port duel: placements |
| hundred_partings | hundred_partings | preset on barrage: scatter (stone) | seam | req; time holds (question Q10); ult; grow; seam: preset tags (duration, chrono, ultimate) | fight port duel: placements |
| hollow_star | hollow_star | preset on lay: area, field (void) | seam | req; ult; grow; seam: preset tags (duration, ultimate); seam: preset behaviors (follow_up) | fight port duel: placements |
| stormcrown | stormcrown | preset on hail: scatter (storm) | seam | req; ult; grow; seam: preset tags (duration, ultimate) | fight port duel: placements |
| rain_of_knives | rain_of_knives | preset on barrage: scatter (stone) | seam | req; ult; the gauge (a charge bank by hand); tree; grow; seam: preset tags (ultimate) | fight port duel: placements |
| litany_of_dawn | litany_of_dawn | preset on hail: scatter (ember) | seam | req; ult; tree; grow; seam: preset tags (ultimate); seam: preset behaviors (follow_up) | fight port duel: placements |

### utility and defense (91: not ported 32, blocked 6, seam 51, ported (difference) 2)

| Prototype | Ours | Mechanism | Status | Note | Proof |
|---|---|---|---|---|---|
| companion_stance |  | preset on boon_quick:  | not ported | seam: preset tags (instant, companion); later: companionStance: companions (row C35, later) | fight port duel: utility |
| beast_rallying_whistle |  | self kit skill | not ported | kit |  |
| town_portal |  | self kit skill | not ported | kit |  |
| satellite_shelter_barrier |  | self kit skill | not ported | kit |  |
| relocate_workshop |  | self kit skill | not ported | kit |  |
| mimicry | mimicry | preset on boon_quick:  | blocked | seam: preset tags (mimic); seam: preset behaviors (meta_action); blocked: mimicry (row C38) | fight port duel: utility |
| mimic_attune |  | preset on boon_quick:  | not ported | seam: preset tags (mimic, instant); later: mimicSelect: mimicry (row C38, later) | fight port duel: utility |
| war_cry | war_cry | preset on boon: apply_status | seam | req; tree; grow; seam: preset tags (buff, duration) | fight port duel: utility |
| tideward_swell |  | self kit skill | not ported | kit |  |
| solar_litany | solar_litany | preset on boon: apply_status (ember) | seam | req; grow; seam: preset tags (buff, duration) | fight port duel: utility |
| vampiric_embrace | vampiric_embrace | preset on boon: apply_status | seam | req; grow; seam: preset tags (buff, duration) | fight port duel: utility |
| plague_benediction | plague_benediction | preset on boon: apply_status (umbra) | seam | req; grow; seam: preset tags (buff, duration) | fight port duel: utility |
| stealth | stealth | preset on boon_quick: charge, apply_status | seam | req; grow; seam: preset tags (buff, instant) | fight port duel: utility |
| cloak | cloak | preset on boon: apply_status | seam | req; tree; grow; seam: preset tags (buff, duration) | fight port duel: utility |
| invisibility | invisibility | preset on boon: apply_status | seam | req; tree; grow; seam: preset tags (buff, duration) | fight port duel: utility |
| companion_whistle |  | preset on boon_quick:  | not ported | seam: preset tags (instant, companion); later: whistleCompanion: companions (row C35, later) | fight port duel: utility |
| corpse_feast | corpse_feast | preset on boon: apply_status | seam | plural targeting (several corpses) is one target here; corpse life as damage or restoration: an expression over the consumed body, authored by hand; req; thr; grow; seam: preset tags (corpse, duration) | fight port duel: utility |
| gather_the_dead | gather_the_dead | preset on boon:  | blocked | req; thr; grow; seam: preset tags (corpse); blocked: dragCorpses: corpse piles (row C26, open) | fight port duel: utility |
| gorge_carrion |  | self kit skill | not ported | kit |  |
| detonate_mines | detonate_mines | preset on spark_quick: detonate | seam | req; grow; seam: preset tags (mine) | fight port duel: utility |
| quickstep | quickstep | preset on boon: apply_status | seam | req; tree; grow; seam: preset tags (buff, duration) | fight port duel: utility |
| stone_skin | stone_skin | preset on boon: apply_status | seam | req; tree; grow; seam: preset tags (buff, duration) | fight port duel: utility |
| quiet_step | quiet_step | preset on boon: apply_status | seam | req; thr; tree; grow; seam: preset tags (duration) | fight port duel: utility |
| power_surge | power_surge | preset on boon: apply_status, resource | seam | req; grow; seam: preset tags (buff, duration) | fight port duel: utility |
| venomous_aura | venomous_aura | preset on boon_quick:  (umbra) | seam | req; damage pools (a bank status by hand); seam: preset tags (duration) | fight port duel: utility |
| detonation | detonation | preset on boon:  (ember) | ported (difference) | req; damage pools (a bank status by hand) | fight port duel: utility |
| spear_recall | spear_recall | preset on cry_quick:  (stone) | blocked | req; tree; grow; seam: preset tags (instant); blocked: recallImpales: recalling impales (row C33, open) | fight port duel: utility |
| time_dilation | time_dilation | preset on boon_quick: cooldown | seam | req; tree; grow; seam: preset tags (buff, instant) | fight port duel: utility |
| iron_ward | iron_ward | preset on boon: apply_status (stone) | seam | req; grow; seam: preset tags (buff, duration) | fight port duel: utility |
| transgression | transgression | preset on boon_quick: guard | seam | usable while guarding: a reflex here; req; tree; grow; seam: preset tags (buff, instant) | fight port duel: utility |
| pain_hounds | pain_hounds | preset on boon: charge (ember) | seam | req; retaliation spawns (row C48, a proc by hand); grow; seam: preset tags (buff, duration) | fight port duel: utility |
| enrage_swarm | enrage_swarm | preset on boon_quick: apply_status | seam | seam: preset tags (buff, duration, instant) | fight port duel: utility |
| stack_the_deck | stack_the_deck | preset on boon: apply_status | seam | req; tree; grow; seam: preset tags (buff, duration) | fight port duel: utility |
| command_assault | command_assault | preset on boon_quick: command_minions | seam | req; tree; grow; seam: preset tags (instant); seam: preset behaviors (meta_action) | fight port duel: utility |
| command_recall | command_recall | preset on boon_quick: command_minions | seam | seam: preset tags (instant) | fight port duel: utility |
| snarled_orders |  | self kit skill | not ported | kit |  |
| time_stop | time_stop | preset on boon:  | seam | req; time holds (question Q10); grow; seam: preset tags (chrono, duration) | fight port duel: utility |
| wing_up |  | self kit skill | not ported | kit |  |
| alight |  | self kit skill | not ported | kit |  |
| convocation | convocation | preset on boon: recall_minions, apply_status | seam | req; grow; seam: preset tags (buff, duration) | fight port duel: utility |
| bloodlust | bloodlust | preset on boon_quick: charge (stone) | seam | req; taps; thr; grow; seam: preset tags (buff, instant); seam: preset behaviors (spend) | fight port duel: utility |
| flame_imbuement | flame_imbuement | preset on boon: apply_status (ember) | seam | req; grow; seam: preset tags (buff, duration) | fight port duel: utility |
| venom_ammunition | venom_ammunition | preset on boon: apply_status (umbra) | seam | req; grow; seam: preset tags (buff, duration) | fight port duel: utility |
| galvanize | galvanize | preset on boon: apply_status (storm) | seam | req; seam: preset tags (buff, duration) | fight port duel: utility |
| flame_wreath | flame_wreath | preset on boon: apply_status (ember) | seam | req; grow; seam: preset tags (buff, duration) | fight port duel: utility |
| cold_snap | cold_snap | preset on spark: detonate (tide) | ported (difference) | req | fight port duel: utility |
| moult | moult | preset on boon_quick: remove_status, apply_status | seam | req; grow; seam: preset tags (buff, instant, duration) | fight port duel: utility |
| vent_vermin |  | self kit skill | not ported | kit |  |
| vent_mites |  | self kit skill | not ported | kit |  |
| vent_ticks |  | self kit skill | not ported | kit |  |
| vent_souls |  | self kit skill | not ported | kit |  |
| reload_powder | reload_powder | preset on boon: restore_uses | seam | seam: preset tags (reload, munition) | fight port duel: utility |
| re_energize |  | self kit skill | not ported | kit |  |
| life_flask | life_flask | preset on boon_quick: apply_status, apply_status, apply_status | seam | taps; thr; tree; grow; seam: preset tags (instant, buff, duration, flask); seam: preset behaviors (gates, spend, equip_modifiers) | fight port duel: utility |
| mana_flask | mana_flask | preset on boon_quick: apply_status, apply_status | seam | taps; thr; tree; grow; seam: preset tags (instant, buff, duration, flask); seam: preset behaviors (gates, spend, equip_modifiers) | fight port duel: utility |
| catalyst_flask | catalyst_flask | preset on boon_quick: apply_status, apply_status, apply_status, apply_status | seam | per-charge restoration scales by charges_spent: author by hand; per-charge restoration scales by charges_spent: author by hand; taps; thr; tree; grow; seam: preset tags (instant, buff, duration, flask); seam: preset behaviors (spend, equip_modifiers) | fight port duel: utility |
| quicksilver_flask | quicksilver_flask | preset on boon_quick: apply_status, apply_status | seam | taps; thr; tree; grow; seam: preset tags (instant, buff, duration, flask); seam: preset behaviors (spend, equip_modifiers) | fight port duel: utility |
| stoneskin_flask | stoneskin_flask | preset on boon_quick: apply_status, apply_status | seam | taps; thr; tree; grow; seam: preset tags (instant, buff, duration, flask); seam: preset behaviors (spend, equip_modifiers) | fight port duel: utility |
| antidote_flask | antidote_flask | preset on boon_quick: remove_status, apply_status, apply_status | seam | taps; thr; tree; grow; seam: preset tags (instant, buff, duration, flask); seam: preset behaviors (spend, equip_modifiers) | fight port duel: utility |
| chaser_edge |  | self kit skill | not ported | kit |  |
| swig |  | self kit skill | not ported | kit |  |
| marshals_rift | marshals_rift | preset on boon_quick: recall_minions, command_minions | seam | req; seam: preset tags (instant) | fight port duel: utility |
| exhume | exhume | preset on boon: spawn (stone) | seam | sum:remnant_shade<skeleton_warrior; req; thr; seam: preset tags (corpse) | fight port duel: utility |
| command_detonate |  | self kit skill | not ported | kit |  |
| shatter_totem |  | self kit skill | not ported | kit |  |
| draw_corruption | draw_corruption | preset on boon_quick: move_status (void) | seam | req; grow; seam: preset tags (instant) | fight port duel: utility |
| stalk | stalk | preset on boon: apply_status | seam | req; tree; grow; seam: preset tags (buff, duration) | fight port duel: utility |
| relinquish |  | preset on boon:  | not ported | seam: preset tags (possession, instant); later: possessEnd: possession (row C38, later) | fight port duel: utility |
| form_of_the_dire_wolf |  | preset on boon:  | not ported | req; grow; seam: preset tags (possession); seam: preset behaviors (convert); later: shapeshift: shapeshifting (row C38, later) | fight port duel: utility |
| return_to_flesh |  | preset on boon:  | not ported | seam: preset tags (possession, instant); later: possessEnd: possession (row C38, later) | fight port duel: utility |
| heave | heave | preset on cry: damage (stone) | blocked | req; grow; seam: preset tags (throw); later: grabThrow: the grab fabric (row C37, later); blocked: a held-grab gate (row C37) | fight port duel: utility |
| mauler_toss |  | self kit skill | not ported | kit |  |
| yeti_hurl |  | self kit skill | not ported | kit |  |
| invocation | invocation | preset on boon:  | blocked | req; thr; tree; blocked: invocations (row C48) | fight port duel: utility |
| oblation_of_life | oblation_of_life | preset on boon_quick: apply_status (stone) | seam | req; grow; pct-cost; seam: preset tags (instant, buff, duration) | fight port duel: utility |
| oblation_of_mana | oblation_of_mana | preset on boon_quick: apply_status | seam | req; grow; pct-cost; seam: preset tags (instant, buff, duration) | fight port duel: utility |
| harvester_command | harvester_command | preset on boon_quick: minion_cast | seam | seam: preset tags (instant) | fight port duel: utility |
| war_horn | war_horn | preset on boon_quick: command_minions | seam | req; grow; seam: preset tags (instant) | fight port duel: utility |
| last_rite | last_rite | preset on boon_quick: consume_minion (ember) | seam | minions explode through consume_minion rather than a self-destruct charge; req; grow; seam: preset tags (instant) | fight port duel: utility |
| command_skeletal_strike |  | self kit skill | not ported | kit |  |
| symbiote_skin | symbiote_skin | preset on boon: apply_status | seam | req; grow; seam: preset tags (buff, duration) | fight port duel: utility |
| absolution | absolution | preset on boon_quick:  | blocked | seam: preset tags (instant); blocked: payLedger: toggle ledgers (row C29, open) | fight port duel: utility |
| steam_vent |  | self kit skill | not ported | kit |  |
| steam_veil |  | self kit skill | not ported | kit |  |
| bulwark_set |  | self kit skill | not ported | kit |  |
| cirrus_veil | cirrus_veil | preset on boon:  | seam | status cloudform is not ported; req; thr; grow; seam: preset tags (buff, duration) | fight port duel: utility |
| own_sky |  | preset on boon:  | not ported | req; thr; grow; seam: preset tags (buff, duration); later: conjure: cloud bridges (row C36, later) | fight port duel: utility |
| woken_hollow |  | preset on boon:  | not ported | req; ult; grow; seam: preset tags (possession, duration, ultimate); seam: preset behaviors (convert); later: shapeshift: shapeshifting (row C38, later) | fight port duel: utility |
| last_rites | last_rites | preset on boon_quick: heal, apply_status | seam | req; ult; grow; seam: preset tags (buff, instant, ultimate); seam: preset behaviors (gates, follow_up) | fight port duel: utility |
| hush_of_the_wake | hush_of_the_wake | preset on boon_quick: apply_status, heal, apply_status | seam | req; ult; grow; seam: preset tags (buff, instant, ultimate); seam: preset behaviors (spend) | fight port duel: utility |
| red_hour | red_hour | preset on boon_quick: apply_status | seam | gauge power stacks fixed to the full count; req; ult; the gauge (a charge bank by hand); tree; grow; seam: preset tags (buff, instant, ultimate) | fight port duel: utility |

## Supports (457: ported 245, ported (difference) 164, blocked 30, not ported 18)

| Prototype | Ours | Mechanism | Status | Note | Proof |
|---|---|---|---|---|---|
| crushing_impact | crushing_impact | glyph: modifiers | ported |  | weave report; glyph duel |
| battering_ram | battering_ram | glyph: modifiers | ported (difference) | drop:shoveAuthority, modifier impactDamage, modifier impactDamage | weave report; glyph duel |
| overmatch | overmatch | glyph: modifiers | ported |  | weave report; glyph duel |
| giantsbane | giantsbane | glyph: modifiers | ported |  | weave report; glyph duel |
| regicide | regicide | glyph: modifiers | ported |  | weave report; glyph duel |
| limbreaver | limbreaver | glyph: modifiers | ported (difference) | drop:limbreaver, modifier limbreaver | weave report; glyph duel |
| siegebreaker | siegebreaker | glyph: modifiers | ported |  | weave report; glyph duel |
| spentbane | spentbane | glyph: modifiers | ported |  | weave report; glyph duel |
| uprooter | uprooter | glyph: modifiers | ported |  | weave report; glyph duel |
| bondbreaker | bondbreaker | glyph: modifiers | ported (difference) | drop:bondbreaker, modifier bondbreaker | weave report; glyph duel |
| quailbane | quailbane | glyph: modifiers | ported |  | weave report; glyph duel |
| iron_grip | iron_grip | glyph: modifiers | ported (difference) | drop:gripPower, modifier gripPower | weave report; glyph duel |
| trebuchet_arm | trebuchet_arm | glyph: modifiers | ported (difference) | drop:shoveAuthority, modifier impactDamage, modifier impactDamage | weave report; glyph duel |
| wringing_grip | wringing_grip | glyph: modifiers | ported (difference) | drop:gripCrush, modifier gripCrush | weave report; glyph duel |
| exterminator | exterminator | glyph: modifiers | ported (difference) | drop:plyRend, modifier plyRend | weave report; glyph duel |
| serrated_edge | serrated_edge | glyph: modifiers | ported |  | weave report; glyph duel |
| envenomed_tips | envenomed_tips | glyph: modifiers | ported |  | weave report; glyph duel |
| fowlers_eye | fowlers_eye | glyph: modifiers | ported |  | weave report; glyph duel |
| ringing_report | ringing_report | glyph: modifiers | ported (difference) | drop:noiseOnHit, modifier noiseOnHit | weave report; glyph duel |
| smothering_spores | smothering_spores | glyph: modifiers | ported |  | weave report; glyph duel |
| loose_thread | loose_thread | glyph: modifiers | ported |  | weave report; glyph duel |
| entropic_bloom | entropic_bloom | glyph: modifiers | ported |  | weave report; glyph duel |
| grasping_tendrils | grasping_tendrils | glyph: modifiers | ported |  | weave report; glyph duel |
| parasitic_pact | parasitic_pact | glyph: modifiers | ported |  | weave report; glyph duel |
| held_note | held_note | glyph: modifiers | ported |  | weave report; glyph duel |
| countermelody | countermelody | glyph: modifiers | ported |  | weave report; glyph duel |
| rising_chorus | rising_chorus | glyph: hooks on_cast, modifiers | ported |  | weave report; glyph duel |
| lingering_moment | lingering_moment | glyph: modifiers | ported |  | weave report; glyph duel |
| borrowed_haste | borrowed_haste | glyph: modifiers | ported |  | weave report; glyph duel |
| commanding_presence | commanding_presence | glyph: modifiers | ported |  | weave report; glyph duel |
| packed_workshop | packed_workshop | glyph: meta, modifiers | ported |  | weave report; glyph duel |
| overwound_mechanism | overwound_mechanism | glyph: modifiers | ported |  | weave report; glyph duel |
| hair_trigger | hair_trigger | glyph: modifiers | ported |  | weave report; glyph duel |
| tinkers_arsenal | tinkers_arsenal | glyph: modifiers | ported |  | weave report; glyph duel |
| chaotic_discharge | chaotic_discharge | glyph: modifiers | ported (difference) | release order dropped | weave report; glyph duel |
| fortunes_favor | fortunes_favor | glyph: modifiers | ported (difference) | drop:luckyChance, modifier luckyChance | weave report; glyph duel |
| jinxing_touch | jinxing_touch | glyph: modifiers | ported |  | weave report; glyph duel |
| tempest_range | tempest_range | glyph: modifiers | ported (difference) | min and max fold into one added value; min and max fold into one added value | weave report; glyph duel |
| steady_current | steady_current | glyph: modifiers | ported (difference) | min and max fold into one added value; min and max fold into one added value | weave report; glyph duel |
| loaded_dice | loaded_dice | glyph: modifiers | ported (difference) | drop:damageSpread, modifier damageSpread | weave report; glyph duel |
| overload | overload | glyph: modifiers | ported |  | weave report; glyph duel |
| unstable_compression | unstable_compression | glyph: modifiers | ported (difference) | variance: rand() in the patched parameters by hand | weave report; glyph duel |
| parting_gift | parting_gift | glyph: hooks on_end, modifiers | ported |  | weave report; glyph duel |
| epidemic | epidemic | glyph: modifiers | blocked | blocked: contagion re-release needs cast { weave: host } (seam) | weave report; glyph duel |
| umbral_power | umbral_power | glyph: modifiers | ported |  | weave report; glyph duel |
| umbral_focus | umbral_focus | glyph: modifiers | ported |  | weave report; glyph duel |
| brimstone_tithe | brimstone_tithe | glyph: modifiers | ported |  | weave report; glyph duel |
| tormentors_glee | tormentors_glee | glyph: modifiers | ported |  | weave report; glyph duel |
| hellhunger | hellhunger | glyph: modifiers | ported |  | weave report; glyph duel |
| putrefaction | putrefaction | glyph: modifiers | ported |  | weave report; glyph duel |
| calcifying | calcifying | glyph: modifiers | ported |  | weave report; glyph duel |
| refraction | refraction | glyph: modifiers | ported |  | weave report; glyph duel |
| barbed_snare | barbed_snare | glyph: modifiers | ported |  | weave report; glyph duel |
| brood_tender | brood_tender | glyph: modifiers | ported |  | weave report; glyph duel |
| wound_worrier | wound_worrier | glyph: modifiers | ported |  | weave report; glyph duel |
| sanguine_feast | sanguine_feast | glyph: modifiers | ported |  | weave report; glyph duel |
| mending_echoes | mending_echoes | glyph: modifiers | ported (difference) | heal over time: a restoring status by hand | weave report; glyph duel |
| unyielding_stance | unyielding_stance | glyph: modifiers | ported |  | weave report; glyph duel |
| bulwark_of_thorns | bulwark_of_thorns | glyph: modifiers | ported (difference) | drop:channelThorns, modifier channelThorns | weave report; glyph duel |
| counterweight | counterweight | glyph: modifiers | ported (difference) | drop:guardParry, modifier guardParryPower | weave report; glyph duel |
| shieldwall_doctrine | shieldwall_doctrine | glyph: modifiers | ported |  | weave report; glyph duel |
| volcanic_heart |  | glyph: modifiers | not ported | later: fissures (a fabric this port lacks) | weave report; glyph duel |
| tectonic_echoes |  | glyph: modifiers | not ported | later: fissures (a fabric this port lacks) | weave report; glyph duel |
| seismic_waltz |  | glyph: modifiers | not ported | later: fissures (a fabric this port lacks) | weave report; glyph duel |
| restless_wound |  | glyph: modifiers | not ported | later: fissures (a fabric this port lacks) | weave report; glyph duel |
| orbital_fault |  | glyph: modifiers | not ported | later: fissures (a fabric this port lacks) | weave report; glyph duel |
| widening_fault |  | glyph: modifiers | not ported | later: fissures (a fabric this port lacks) | weave report; glyph duel |
| serpentine_fault |  | glyph: modifiers | not ported | later: fissures (a fabric this port lacks) | weave report; glyph duel |
| faultfinder |  | glyph: modifiers, tags | not ported | later: fissures (a fabric this port lacks) | weave report; glyph duel |
| cast_on_crit | cast_on_crit | glyph: trigger, modifiers | ported |  | weave report; glyph duel |
| cast_on_damage_taken | cast_on_damage_taken | glyph: trigger | ported (difference) | drop:triggerThreshold | weave report; glyph duel |
| cast_while_channeling | cast_while_channeling | glyph: trigger, modifiers | ported |  | weave report; glyph duel |
| cast_on_flawless | cast_on_flawless | glyph: trigger, modifiers | ported |  | weave report; glyph duel |
| cast_while_guarding | cast_while_guarding | glyph: trigger, modifiers | ported |  | weave report; glyph duel |
| cast_on_overcharge | cast_on_overcharge | glyph: trigger, modifiers | ported |  | weave report; glyph duel |
| cast_on_high_roll | cast_on_high_roll | glyph: modifiers | blocked | blocked: trigger kind highRoll (a high-roll trigger kind: seam) | weave report; glyph duel |
| culmination | culmination | glyph: trigger, modifiers | ported |  | weave report; glyph duel |
| culminant_frenzy | culminant_frenzy | glyph: modifiers | ported (difference) | charge tap on channelFinish becomes a proc by hand; drop:chargeCap | weave report; glyph duel |
| gathered_casting | gathered_casting | glyph: modifiers | blocked | drop:brimFill; blocked: gather converts (a channel brim by hand) | weave report; glyph duel |
| sequenced_invocation | sequenced_invocation | glyph: modifiers | ported |  | weave report; glyph duel |
| cast_on_ignition | cast_on_ignition | glyph: trigger | ported (difference) | drop:triggerPower | weave report; glyph duel |
| cast_on_poison | cast_on_poison | glyph: trigger | ported (difference) | drop:triggerPower | weave report; glyph duel |
| cast_on_bleed | cast_on_bleed | glyph: trigger | ported (difference) | drop:triggerPower | weave report; glyph duel |
| cast_on_block | cast_on_block | glyph: trigger, modifiers | ported |  | weave report; glyph duel |
| cast_on_kill | cast_on_kill | glyph: trigger, modifiers | ported |  | weave report; glyph duel |
| carrier_strain | carrier_strain | glyph: hooks on_hit, modifiers | ported |  | weave report; glyph duel |
| final_bloom | final_bloom | glyph: modifiers | ported (difference) | drop:dotPropagates, modifier dotPropagates | weave report; glyph duel |
| load_bearing_flaw | load_bearing_flaw | glyph: modifiers | blocked | blocked: breakable constructs with death bursts (row B16, by hand) | weave report; glyph duel |
| unmoored |  | glyph: modifiers | not ported | later: the mass fabric is not ported | weave report; glyph duel |
| skewering_blows | skewering_blows | glyph: modifiers | ported (difference) | drop:impalePower, modifier impalePower | weave report; glyph duel |
| curse_on_hit | curse_on_hit | glyph: modifiers | blocked | blocked: curse on hit needs cast { weave: host } at a scale plus a reservation (row D25 seam) | weave report; glyph duel |
| answering_steel | answering_steel | glyph: meta, modifiers | ported (difference) | charge tap on block becomes a proc by hand | weave report; glyph duel |
| reckless_rampart | reckless_rampart | glyph: modifiers | ported (difference) | drop:bashPower, modifier bashPower | weave report; glyph duel |
| answering_wall | answering_wall | glyph: modifiers | blocked | drop:bashPower, modifier bashFloor, modifier bashPower; blocked: a bash graft: guards are form-level (named difference) | weave report; glyph duel |
| hollow_answer | hollow_answer | glyph: modifiers | ported (difference) | drop:bashInvert, modifier bashPower, modifier bashPower | weave report; glyph duel |
| stalwart_rhythm | stalwart_rhythm | glyph: modifiers | ported |  | weave report; glyph duel |
| hiveborn | hiveborn | glyph: modifiers | blocked | blocked: corpse spawns (row D26, by hand) | weave report; glyph duel |
| ghostly_communion | ghostly_communion | glyph: modifiers | blocked | blocked: corpse spawns (row D26, by hand) | weave report; glyph duel |
| gift_of_the_choir | gift_of_the_choir | glyph: modifiers | blocked | blocked: random minion auras (row D26, by hand) | weave report; glyph duel |
| legion_doctrine | legion_doctrine | glyph: modifiers | ported |  | weave report; glyph duel |
| fresh_ranks | fresh_ranks | glyph: hooks on_birth | ported |  | weave report; glyph duel |
| resonance | resonance | glyph: modifiers | ported |  | weave report; glyph duel |
| blood_toll | blood_toll | glyph: hooks on_cast, modifiers | ported |  | weave report; glyph duel |
| dominating_blow | dominating_blow | glyph: hooks on_kill, modifiers | ported |  | weave report; glyph duel |
| maddening_miasma |  | glyph: modifiers | not ported | later: maddening (a lash-out fabric, by hand) | weave report; glyph duel |
| pulsing_hex | pulsing_hex | glyph: hooks on_tick, modifiers | ported |  | weave report; glyph duel |
| creeping_fumes | creeping_fumes | glyph: patches, modifiers | ported |  | weave report; glyph duel |
| overgrowth | overgrowth | glyph: patches, modifiers | ported |  | weave report; glyph duel |
| ebbing_ground | ebbing_ground | glyph: patches, modifiers | ported |  | weave report; glyph duel |
| blooming_ground | blooming_ground | glyph: patches, modifiers | ported |  | weave report; glyph duel |
| tidal_ground | tidal_ground | glyph: patches, modifiers | ported |  | weave report; glyph duel |
| stillwater_discipline | stillwater_discipline | glyph: modifiers | ported (difference) | drop:brimDecay, modifier brimPower, modifier brimPower | weave report; glyph duel |
| overbrim | overbrim | glyph: modifiers | ported (difference) | drop:brimPower, modifier brimFill, modifier brimPower | weave report; glyph duel |
| fleeting_devotion | fleeting_devotion | glyph: patches, modifiers | ported |  | weave report; glyph duel |
| blood_pact | blood_pact | glyph: modifiers | ported (difference) | life reservation is form-level: a form variant by hand | weave report; glyph duel |
| dominion_tithe | dominion_tithe | glyph: modifiers | ported (difference) | drop:reservedDamage, modifier reservedDamage | weave report; glyph duel |
| archons_wellspring | archons_wellspring | glyph: modifiers | ported (difference) | drop:maxManaDamage, modifier maxManaDamage | weave report; glyph duel |
| slow_brew | slow_brew | glyph: spend | ported (difference) | charge tap on second becomes a proc by hand | weave report; glyph duel |
| consecrated_echo | consecrated_echo | glyph: patches, modifiers | ported |  | weave report; glyph duel |
| echoing_might | echoing_might | glyph: modifiers | ported (difference) | drop:echoMight, modifier echoMight | weave report; glyph duel |
| hallowed_flames | hallowed_flames | glyph: modifiers | ported |  | weave report; glyph duel |
| warding_flesh | warding_flesh | glyph: modifiers | ported |  | weave report; glyph duel |
| monolith | monolith | glyph: modifiers | ported |  | weave report; glyph duel |
| measured_blows | measured_blows | glyph: modifiers | ported |  | weave report; glyph duel |
| point_blank | point_blank | glyph: modifiers | ported (difference) | drop:proximityDamage, modifier proximityDamage | weave report; glyph duel |
| pulsating_missiles | pulsating_missiles | glyph: modifiers | ported |  | weave report; glyph duel |
| withering_agony | withering_agony | glyph: modifiers | ported |  | weave report; glyph duel |
| creeping_doom | creeping_doom | glyph: modifiers | ported |  | weave report; glyph duel |
| lingering_doom | lingering_doom | glyph: modifiers | ported (difference) | drop:doomDot, modifier doomDot | weave report; glyph duel |
| wrathful_edge | wrathful_edge | glyph: modifiers | ported |  | weave report; glyph duel |
| grace_of_dawn | grace_of_dawn | glyph: modifiers | ported (difference) | charge tap on channelSecond becomes a proc by hand | weave report; glyph duel |
| broodclutch | broodclutch | glyph: modifiers | blocked | blocked: broods born of damage (row D26, by hand) | weave report; glyph duel |
| communion_of_flesh | communion_of_flesh | glyph: modifiers | ported |  | weave report; glyph duel |
| last_rites | last_rites | glyph: modifiers | ported (difference) | drop:minionDeathHeal, modifier minionExpiryIsDeath, modifier minionDeathHeal | weave report; glyph duel |
| stored_verdict | stored_verdict | glyph: meta, hooks on_cast | ported |  | weave report; glyph duel |
| summon_phantasm | summon_phantasm | glyph: modifiers | ported |  | weave report; glyph duel |
| teeming_vein | teeming_vein | glyph: modifiers, tags | ported (difference) | a chassis: axes authored by hand from rollBase | weave report; glyph duel |
| broodbearer |  | glyph: modifiers, tags | not ported | later: birth grafts (a clutch fabric this port lacks) | weave report; glyph duel |
| sainted_ash | sainted_ash | glyph: modifiers | ported |  | weave report; glyph duel |
| septic_bargain | septic_bargain | glyph: modifiers | ported (difference) | drop:hitToAffliction, modifier afflictionYield, modifier afflictionYield | weave report; glyph duel |
| opportunist | opportunist | glyph: modifiers | ported |  | weave report; glyph duel |
| nettles | nettles | glyph: modifiers | ported (difference) | drop:channelThorns, modifier channelThorns | weave report; glyph duel |
| eruption_cycle | eruption_cycle | glyph: modifiers | ported (difference) | drop:channelBurst, modifier channelBurst | weave report; glyph duel |
| channeled_tempest | channeled_tempest | glyph: modifiers | ported (difference) | drop:channelStorm, modifier channelStorm | weave report; glyph duel |
| patient_fury | patient_fury | glyph: modifiers | ported (difference) | drop:channelRamp, modifier channelRamp | weave report; glyph duel |
| spooling | spooling | glyph: modifiers | ported (difference) | drop:channelSpool, modifier channelSpool, modifier channelSpoolCap | weave report; glyph duel |
| mounting_frenzy | mounting_frenzy | glyph: modifiers | ported (difference) | drop:channelHitSpool, modifier channelHitSpool | weave report; glyph duel |
| overcharge | overcharge | glyph: modifiers, tags | blocked | blocked: an overcharge graft: cast modes are form-level (a form variant per host, named difference) | weave report; glyph duel |
| perfect_draw | perfect_draw | glyph: modifiers | blocked | blocked: a timing graft: cast modes are form-level (named difference) | weave report; glyph duel |
| wandering_mark | wandering_mark | glyph: modifiers | blocked | blocked: a timing graft: cast modes are form-level (named difference) | weave report; glyph duel |
| spark_discipline | spark_discipline | glyph: modifiers | ported (difference) | drop:sparkWindow, modifier sparkWindow, modifier sparkBonus | weave report; glyph duel |
| dive_bomb | dive_bomb | glyph: modifiers, tags | ported (difference) | drop:moveExplode, modifier moveExplode | weave report; glyph duel |
| fire_walker | fire_walker | glyph: modifiers, tags | ported (difference) | drop:moveTrail, modifier moveTrail | weave report; glyph duel |
| arcing | arcing | glyph: modifiers | ported |  | weave report; glyph duel |
| static_buildup | static_buildup | glyph: modifiers | ported |  | weave report; glyph duel |
| forked_focus | forked_focus | glyph: modifiers | ported (difference) | drop:multiTarget, modifier multiTarget | weave report; glyph duel |
| powderkeg | powderkeg | glyph: modifiers | ported (difference) | drop:igniteToBomb | weave report; glyph duel |
| nova_release | nova_release | glyph: modifiers | ported (difference) | drop:projNova, modifier projNova | weave report; glyph duel |
| elemental_conduction | elemental_conduction | glyph: modifiers | ported (difference) | drop:conduction | weave report; glyph duel |
| slow_burn | slow_burn | glyph: modifiers | ported |  | weave report; glyph duel |
| perfect_timing | perfect_timing | glyph: modifiers | ported (difference) | drop:guardParry, modifier guardParry, modifier guardParryPower | weave report; glyph duel |
| capacitor | capacitor | glyph: modifiers | ported (difference) | drop:auraEsRecharge, modifier auraEsRecharge | weave report; glyph duel |
| insulation | insulation | glyph: modifiers | ported (difference) | drop:auraEsDelay, modifier auraEsDelay | weave report; glyph duel |
| desperation | desperation | glyph: modifiers | ported |  | weave report; glyph duel |
| serene_power | serene_power | glyph: modifiers | ported |  | weave report; glyph duel |
| untouched_might | untouched_might | glyph: modifiers | ported |  | weave report; glyph duel |
| remnants | remnants | glyph: modifiers | ported (difference) | drop:remnantChance, modifier remnantChance | weave report; glyph duel |
| mirror_coating | mirror_coating | glyph: modifiers | ported (difference) | drop:domeDeflects | weave report; glyph duel |
| crimson_harvest | crimson_harvest | glyph: modifiers | ported (difference) | drop:orbOnHit_life, modifier orbOnHit_life | weave report; glyph duel |
| azure_harvest | azure_harvest | glyph: modifiers | ported (difference) | drop:orbOnHit_mana, modifier orbOnHit_mana | weave report; glyph duel |
| lambent_harvest | lambent_harvest | glyph: modifiers | ported (difference) | drop:orbOnHit_es, modifier orbOnHit_es | weave report; glyph duel |
| kindled_wake | kindled_wake | glyph: modifiers | ported (difference) | drop:orbOnHit_wakeflame, modifier orbOnKill_wakeflame, modifier orbOnKill_wakeflame | weave report; glyph duel |
| gutterglow | gutterglow | glyph: modifiers | ported |  | weave report; glyph duel |
| victors_tempo | victors_tempo | glyph: hooks on_kill | ported (difference) | drop:chargeCap | weave report; glyph duel |
| abundant_harvest | abundant_harvest | glyph: modifiers | blocked | drop:orbShedRate, modifier orbShedRate; blocked: resource orbs (row C23, open) | weave report; glyph duel |
| guardians_aegis | guardians_aegis | glyph: modifiers | ported (difference) | drop:guardAegis | weave report; glyph duel |
| muscle_memory | muscle_memory | glyph: modifiers | ported |  | weave report; glyph duel |
| libation | libation | glyph: modifiers | ported (difference) | drop:thirstless, modifier restorePower, modifier restorePower | weave report; glyph duel |
| acrid_draught | acrid_draught | glyph: follow_up, modifiers | ported (difference) | drop:restorePower | weave report; glyph duel |
| adrenal_decant | adrenal_decant | glyph: modifiers | blocked | drop:pourPct_surge, modifier pourPower_settle; blocked: mechanism pour:surge has no probe (flask lanes); blocked: mechanism pour:settle has no probe (flask lanes) | weave report; glyph duel |
| shared_draught | shared_draught | glyph: follow_up, modifiers | ported |  | weave report; glyph duel |
| chaser | chaser | glyph: follow_up, modifiers | ported |  | weave report; glyph duel |
| splitting | splitting | glyph: modifiers | ported |  | weave report; glyph duel |
| piercing | piercing | glyph: modifiers | ported |  | weave report; glyph duel |
| concentrated | concentrated | glyph: modifiers | ported |  | weave report; glyph duel |
| widening | widening | glyph: modifiers | ported |  | weave report; glyph duel |
| combustion | combustion | glyph: modifiers | ported |  | weave report; glyph duel |
| commanding | commanding | glyph: modifiers | ported |  | weave report; glyph duel |
| accelerated | accelerated | glyph: modifiers | ported |  | weave report; glyph duel |
| relentless | relentless | glyph: modifiers | ported |  | weave report; glyph duel |
| brutality | brutality | glyph: modifiers | ported |  | weave report; glyph duel |
| lingering | lingering | glyph: modifiers | ported |  | weave report; glyph duel |
| vampiric | vampiric | glyph: modifiers | ported |  | weave report; glyph duel |
| precision | precision | glyph: modifiers | ported |  | weave report; glyph duel |
| efficiency | efficiency | glyph: modifiers | ported |  | weave report; glyph duel |
| stunning | stunning | glyph: modifiers | ported |  | weave report; glyph duel |
| brutal_strikes | brutal_strikes | glyph: modifiers | ported |  | weave report; glyph duel |
| chaining | chaining | glyph: modifiers | ported |  | weave report; glyph duel |
| aftershocks | aftershocks | glyph: modifiers | ported |  | weave report; glyph duel |
| buried_charge | buried_charge | glyph: modifiers, tags | ported |  | weave report; glyph duel |
| unsettled_earth | unsettled_earth | glyph: modifiers | ported |  | weave report; glyph duel |
| carried_edge | carried_edge | glyph: patches, modifiers | ported |  | weave report; glyph duel |
| reapers_encore | reapers_encore | glyph: follow_up, modifiers | ported |  | weave report; glyph duel |
| accelerando | accelerando | glyph: modifiers | ported (difference) | cadence authored by hand | weave report; glyph duel |
| ritardando | ritardando | glyph: modifiers | ported (difference) | cadence authored by hand | weave report; glyph duel |
| square_sigil | square_sigil | glyph: modifiers | ported (difference) | drop:aoeShape | weave report; glyph duel |
| triangle_sigil | triangle_sigil | glyph: modifiers | ported (difference) | drop:aoeShape | weave report; glyph duel |
| titanic | titanic | glyph: modifiers | ported (difference) | drop:minionSize | weave report; glyph duel |
| legion_call | legion_call | glyph: modifiers | ported |  | weave report; glyph duel |
| cascading_call | cascading_call | glyph: modifiers | ported (difference) | drop:summonSequence | weave report; glyph duel |
| unstable_flesh | unstable_flesh | glyph: modifiers | ported (difference) | drop:minionExplodeLowLife, modifier minionExplodeLowLife | weave report; glyph duel |
| ephemeral | ephemeral | glyph: modifiers | ported |  | weave report; glyph duel |
| cloudburst | cloudburst | glyph: modifiers | ported (difference) | drop:stormImmediate | weave report; glyph duel |
| sustained_barrage | sustained_barrage | glyph: modifiers | ported |  | weave report; glyph duel |
| corpsefire | corpsefire | glyph: modifiers | ported |  | weave report; glyph duel |
| blood_price | blood_price | glyph: modifiers | ported (difference) | drop:costToLife | weave report; glyph duel |
| soul_tether | soul_tether | glyph: modifiers | ported (difference) | drop:minionRespawnTime, modifier minionRespawnTime | weave report; glyph duel |
| colossal | colossal | glyph: modifiers | ported (difference) | drop:projectileSize, modifier projectileSize | weave report; glyph duel |
| volley | volley | glyph: modifiers | ported (difference) | drop:projectileSize | weave report; glyph duel |
| swiftness | swiftness | glyph: modifiers | ported |  | weave report; glyph duel |
| seeker | seeker | glyph: modifiers | ported |  | weave report; glyph duel |
| unstable_flight | unstable_flight | glyph: modifiers | ported |  | weave report; glyph duel |
| clamor | clamor | glyph: modifiers | ported |  | weave report; glyph duel |
| quiet_hand | quiet_hand | glyph: modifiers | ported |  | weave report; glyph duel |
| beckoning | beckoning | glyph: modifiers | ported (difference) | drop:constructTaunt | weave report; glyph duel |
| sunbaked_edge | sunbaked_edge | glyph: modifiers | ported |  | weave report; glyph duel |
| noonglass | noonglass | glyph: modifiers | ported |  | weave report; glyph duel |
| scouring_grit | scouring_grit | glyph: modifiers | ported |  | weave report; glyph duel |
| crosswind | crosswind | glyph: modifiers | ported |  | weave report; glyph duel |
| updraft | updraft | glyph: modifiers | ported |  | weave report; glyph duel |
| slipstream | slipstream | glyph: self_stack, modifiers | ported |  | weave report; glyph duel |
| cloudborne | cloudborne | glyph: tags | ported (difference) | drop:cloudTrail, modifier cloudTrail | weave report; glyph duel |
| thunderhead | thunderhead | glyph: modifiers | ported (difference) | drop:cloudCharge | weave report; glyph duel |
| silver_lining | silver_lining | glyph: modifiers | ported (difference) | drop:cloudSalve | weave report; glyph duel |
| slow_weather | slow_weather | glyph: modifiers | ported |  | weave report; glyph duel |
| tethered_orbit | tethered_orbit | glyph: modifiers | ported |  | weave report; glyph duel |
| gyre | gyre | glyph: modifiers | ported |  | weave report; glyph duel |
| vortex | vortex | glyph: modifiers | ported |  | weave report; glyph duel |
| sidewinder | sidewinder | glyph: modifiers | ported |  | weave report; glyph duel |
| momentum | momentum | glyph: modifiers | ported (difference) | drop:projAccel, modifier projAccel | weave report; glyph duel |
| trueflight | trueflight | glyph: modifiers | ported |  | weave report; glyph duel |
| shrapnel_gem | shrapnel_gem | glyph: modifiers | ported (difference) | drop:projShrapnel, modifier projShrapnel | weave report; glyph duel |
| fulminate | fulminate | glyph: modifiers | ported (difference) | drop:projHitDetonate | weave report; glyph duel |
| lineage | lineage | glyph: modifiers | ported (difference) | drop:projInherit, modifier projInherit | weave report; glyph duel |
| cascade_of_knives | cascade_of_knives | glyph: modifiers | ported (difference) | drop:projReShatter | weave report; glyph duel |
| plus_fire | plus_fire | glyph: modifiers | ported |  | weave report; glyph duel |
| plus_cold | plus_cold | glyph: modifiers | ported |  | weave report; glyph duel |
| plus_lightning | plus_lightning | glyph: modifiers | ported |  | weave report; glyph duel |
| plus_projectile | plus_projectile | glyph: modifiers | ported |  | weave report; glyph duel |
| plus_minion | plus_minion | glyph: modifiers | ported |  | weave report; glyph duel |
| searing_heat | searing_heat | glyph: modifiers | ported |  | weave report; glyph duel |
| biting_cold | biting_cold | glyph: modifiers | ported |  | weave report; glyph duel |
| static_charge | static_charge | glyph: modifiers | ported |  | weave report; glyph duel |
| corrosion | corrosion | glyph: modifiers | ported |  | weave report; glyph duel |
| ruthless | ruthless | glyph: modifiers | ported |  | weave report; glyph duel |
| polyphony | polyphony | glyph: modifiers | ported |  | weave report; glyph duel |
| ostinato | ostinato | glyph: modifiers | ported |  | weave report; glyph duel |
| keen_study | keen_study | glyph: modifiers | ported (difference) | drop:mimicWitness, modifier mimicWitness | weave report; glyph duel |
| understudy | understudy | glyph: modifiers | ported (difference) | drop:mimicBank | weave report; glyph duel |
| iron_trance | iron_trance | glyph: modifiers | ported (difference) | drop:huskGuard, modifier huskGuard | weave report; glyph duel |
| long_communion | long_communion | glyph: modifiers | ported (difference) | drop:possessDuration, modifier possessDuration | weave report; glyph duel |
| vicious_brood | vicious_brood | glyph: modifiers | ported |  | weave report; glyph duel |
| hardy_brood | hardy_brood | glyph: modifiers | ported |  | weave report; glyph duel |
| patient_brood | patient_brood | glyph: modifiers | blocked | blocked: throng sources (row C38) | weave report; glyph duel |
| hidden_reserves | hidden_reserves | glyph: modifiers | blocked | blocked: throng sources (row C38) | weave report; glyph duel |
| teeming_warrens | teeming_warrens | glyph: modifiers | ported (difference) | drop:throngPockets, modifier throngYield, modifier throngYield | weave report; glyph duel |
| chitinous_brood | chitinous_brood | glyph: modifiers | ported (difference) | drop:minionPlies, modifier minionPlies | weave report; glyph duel |
| calcified_vigor | calcified_vigor | glyph: modifiers | ported (difference) | drop:minionLifePlyTrade | weave report; glyph duel |
| marrowbound_vigor | marrowbound_vigor | glyph: modifiers | ported (difference) | drop:minionLifePlyEcho | weave report; glyph duel |
| meat_shield | meat_shield | glyph: modifiers | ported (difference) | drop:minionGuard | weave report; glyph duel |
| bodyguard_doctrine | bodyguard_doctrine | glyph: modifiers | ported |  | weave report; glyph duel |
| alphas_bond | alphas_bond | glyph: modifiers | ported (difference) | drop:sympathy_bond_flask, modifier sympathy_bond_orb, modifier sympathy_bond_flask, modifier sympathy_bond_orb | weave report; glyph duel |
| pack_instinct | pack_instinct | glyph: modifiers | ported (difference) | levels cap at 1: rule glyphs.max_level is global; drop:sympathy_pack_charges | weave report; glyph duel |
| reciprocal_bond | reciprocal_bond | glyph: modifiers | ported (difference) | drop:sympathy_feral_reciprocity, modifier sympathy_feral_reciprocity | weave report; glyph duel |
| gentling_hand | gentling_hand | glyph: modifiers | blocked | levels cap at 1: rule glyphs.max_level is global; blocked: companion grafts (row C35) | weave report; glyph duel |
| beast_master | beast_master | glyph: modifiers | blocked | levels cap at 1: rule glyphs.max_level is global; blocked: companion grafts (row C35) | weave report; glyph duel |
| tripwire | tripwire | glyph: effects | ported |  | weave report; glyph duel |
| tripwire_web | tripwire_web | glyph: effects | ported (difference) | a network tether links each object to the caster here | weave report; glyph duel |
| transient_inferno | transient_inferno | glyph: effects | ported |  | weave report; glyph duel |
| lifeline | lifeline | glyph: effects | ported |  | weave report; glyph duel |
| witchfire_leash | witchfire_leash | glyph: effects | ported |  | weave report; glyph duel |
| taut_wire | taut_wire | glyph: modifiers | ported (difference) | drop:tetherWidth | weave report; glyph duel |
| charged_span | charged_span | glyph: effects | ported (difference) | a network tether links each object to the caster here | weave report; glyph duel |
| walking_meditation | walking_meditation | glyph: modifiers | ported |  | weave report; glyph duel |
| weathervane | weathervane | glyph: modifiers | ported (difference) | drop:channelTurnRate, modifier channelTurnRate | weave report; glyph duel |
| turbulence | turbulence | glyph: modifiers | ported (difference) | drop:knockBuffet | weave report; glyph duel |
| alternating_strikes | alternating_strikes | glyph: patches | ported |  | weave report; glyph duel |
| wild_abandon | wild_abandon | glyph: patches, modifiers | ported |  | weave report; glyph duel |
| measured_blade | measured_blade | glyph: modifiers | ported |  | weave report; glyph duel |
| fleetfoot | fleetfoot | glyph: modifiers | ported |  | weave report; glyph duel |
| spell_cascade | spell_cascade | glyph: patches, modifiers | ported |  | weave report; glyph duel |
| scattered_cascade | scattered_cascade | glyph: patches, modifiers | ported |  | weave report; glyph duel |
| seismic_march | seismic_march | glyph: patches, modifiers | ported |  | weave report; glyph duel |
| resounding_echo | resounding_echo | glyph: modifiers | ported |  | weave report; glyph duel |
| rattling_salvo | rattling_salvo | glyph: modifiers | ported (difference) | drop:fireSalvo | weave report; glyph duel |
| firing_line | firing_line | glyph: modifiers | ported (difference) | drop:fireVolley | weave report; glyph duel |
| close_order | close_order | glyph: modifiers | ported (difference) | drop:volleySpacing | weave report; glyph duel |
| choked_spread | choked_spread | glyph: modifiers | ported |  | weave report; glyph duel |
| cutthroat | cutthroat | glyph: modifiers | ported |  | weave report; glyph duel |
| displaced_conjuring | displaced_conjuring | glyph: modifiers | ported (difference) | drop:castAtCursor | weave report; glyph duel |
| puppet_strings | puppet_strings | glyph: modifiers | ported (difference) | drop:guidePower, modifier guidePower | weave report; glyph duel |
| detonating_passage | detonating_passage | glyph: hooks on_travel, patches | ported |  | weave report; glyph duel |
| scorched_wake | scorched_wake | glyph: hooks on_travel, patches | ported |  | weave report; glyph duel |
| sloughing_wake | sloughing_wake | glyph: hooks on_travel, patches | ported |  | weave report; glyph duel |
| time_fuse | time_fuse | glyph: patches, modifiers | ported |  | weave report; glyph duel |
| slow_match | slow_match | glyph: modifiers | ported |  | weave report; glyph duel |
| grafted_carapace |  | glyph: modifiers | not ported | later: shell guards (row B26, later) | weave report; glyph duel |
| suppuration | suppuration | glyph: modifiers | ported |  | weave report; glyph duel |
| heavy_caliber | heavy_caliber | glyph: modifiers | ported (difference) | drop:projectileSize | weave report; glyph duel |
| ignite_chance | ignite_chance | glyph: modifiers | ported |  | weave report; glyph duel |
| bleed_chance | bleed_chance | glyph: modifiers | ported |  | weave report; glyph duel |
| poison_chance | poison_chance | glyph: modifiers | ported |  | weave report; glyph duel |
| chill_chance | chill_chance | glyph: modifiers | ported |  | weave report; glyph duel |
| shock_chance | shock_chance | glyph: modifiers | ported |  | weave report; glyph duel |
| stun_chance | stun_chance | glyph: modifiers | ported |  | weave report; glyph duel |
| freeze_chance | freeze_chance | glyph: modifiers | ported |  | weave report; glyph duel |
| provocation | provocation | glyph: modifiers | ported |  | weave report; glyph duel |
| unnerving | unnerving | glyph: modifiers | ported |  | weave report; glyph duel |
| haunted_service | haunted_service | glyph: modifiers | ported |  | weave report; glyph duel |
| potency | potency | glyph: modifiers | ported |  | weave report; glyph duel |
| conflagrant | conflagrant | glyph: modifiers | ported |  | weave report; glyph duel |
| flameforged | flameforged | glyph: modifiers | ported |  | weave report; glyph duel |
| multistrike | multistrike | glyph: modifiers | ported (difference) | a chassis: axes authored by hand from rollBase | weave report; glyph duel |
| spell_echo | spell_echo | glyph: modifiers | ported |  | weave report; glyph duel |
| cascade | cascade | glyph: modifiers | ported |  | weave report; glyph duel |
| crescendo | crescendo | glyph: modifiers | ported |  | weave report; glyph duel |
| unleash | unleash | glyph: modifiers | ported (difference) | drop:unleashMax, modifier unleashMax | weave report; glyph duel |
| reverberation | reverberation | glyph: modifiers | ported (difference) | drop:meleeReverb, modifier meleeReverb | weave report; glyph duel |
| splash | splash | glyph: modifiers | ported (difference) | drop:splashRadius, modifier splashRadius | weave report; glyph duel |
| virulence | virulence | glyph: modifiers | ported (difference) | drop:dotPropagates | weave report; glyph duel |
| hex_blast | hex_blast | glyph: modifiers | ported (difference) | drop:hexBlast, modifier hexBlast | weave report; glyph duel |
| no_mans_land | no_mans_land | glyph: modifiers | ported (difference) | drop:lingerField, modifier lingerField | weave report; glyph duel |
| heavy_musk | heavy_musk | glyph: modifiers | ported |  | weave report; glyph duel |
| candied_scent | candied_scent | glyph: modifiers | ported |  | weave report; glyph duel |
| startling_reek | startling_reek | glyph: modifiers | ported |  | weave report; glyph duel |
| malfeasance | malfeasance | glyph: modifiers | ported (difference) | drop:curseRupture, modifier curseRupture | weave report; glyph duel |
| malpractice | malpractice | glyph: modifiers | ported (difference) | drop:dotRupture, modifier dotRupture | weave report; glyph duel |
| hedonism | hedonism | glyph: modifiers | ported (difference) | drop:hedonism | weave report; glyph duel |
| forking | forking | glyph: modifiers | ported |  | weave report; glyph duel |
| returning | returning | glyph: modifiers | ported |  | weave report; glyph duel |
| boomerang | boomerang | glyph: modifiers | ported |  | weave report; glyph duel |
| belligerence_gem | belligerence_gem | glyph: modifiers | ported (difference) | drop:minionDetectionRange, modifier minionDetectionRange | weave report; glyph duel |
| furor_gem | furor_gem | glyph: modifiers | ported |  | weave report; glyph duel |
| undying_loyalty | undying_loyalty | glyph: modifiers | ported (difference) | drop:minionUndying, modifier minionUndying | weave report; glyph duel |
| soulwalk | soulwalk | glyph: modifiers | ported (difference) | drop:targetMinionFallback | weave report; glyph duel |
| sacrificial_rites | sacrificial_rites | glyph: modifiers | ported (difference) | drop:sacrificeMinions | weave report; glyph duel |
| corpse_wagon | corpse_wagon | glyph: modifiers | ported (difference) | drop:corpseBatch, modifier corpseBatch | weave report; glyph duel |
| spirit_totem | spirit_totem | glyph: modifiers, tags | ported (difference) | drop:castAsTotem | weave report; glyph duel |
| pinpoint | pinpoint | glyph: modifiers | ported |  | weave report; glyph duel |
| alacrity | alacrity | glyph: modifiers | ported |  | weave report; glyph duel |
| phantasmal_echo |  | glyph: modifiers, tags | not ported | drop:mirageCount, modifier mirageDamage; later: echo riders (row D24, later) | weave report; glyph duel |
| ancestral_call |  | glyph: modifiers, tags | not ported | drop:mirageCount, modifier mirageDamage; later: echo riders (row D24, later) | weave report; glyph duel |
| vessel_of_shadow | vessel_of_shadow | glyph: effects, modifiers, tags | ported (difference) | sum:shade_echo<shadow_self | weave report; glyph duel |
| synchronicity | synchronicity | glyph: modifiers | ported |  | weave report; glyph duel |
| sweeping_blow | sweeping_blow | glyph: modifiers, tags | ported (difference) | drop:meleeSweep, modifier sweepRange | weave report; glyph duel |
| mana_feeder | mana_feeder | glyph: modifiers | ported (difference) | drop:addedManaCost, modifier costDamage_mana, modifier costDamage_mana | weave report; glyph duel |
| enduring_bond | enduring_bond | glyph: modifiers | ported (difference) | drop:minionDecayRate | weave report; glyph duel |
| vital_bond | vital_bond | glyph: modifiers | ported |  | weave report; glyph duel |
| bloodletters_rhythm | bloodletters_rhythm | glyph: modifiers | ported |  | weave report; glyph duel |
| exposure | exposure | glyph: modifiers | ported |  | weave report; glyph duel |
| bristling_riposte | bristling_riposte | glyph: modifiers | ported (difference) | drop:thornsToHit, modifier thornsToHit | weave report; glyph duel |
| intensive_care | intensive_care | glyph: modifiers | ported |  | weave report; glyph duel |
| mending_chain | mending_chain | glyph: modifiers | ported |  | weave report; glyph duel |
| overmend | overmend | glyph: modifiers | ported (difference) | drop:overheal, modifier overheal | weave report; glyph duel |
| fragmentation | fragmentation | glyph: modifiers | ported (difference) | drop:remnantDrop_ferocity, modifier remnantDrop_ferocity | weave report; glyph duel |
| bulwark_shards | bulwark_shards | glyph: modifiers | ported (difference) | drop:remnantDrop_bulwark, modifier remnantDrop_bulwark | weave report; glyph duel |
| rage_remnants | rage_remnants | glyph: modifiers | ported (difference) | drop:remnantDrop_rage, modifier remnantDrop_rage | weave report; glyph duel |
| remnant_conduit | remnant_conduit | glyph: modifiers | ported (difference) | drop:remnantOnCast, modifier remnantOnCast | weave report; glyph duel |
| metronome | metronome | glyph: modifiers | ported |  | weave report; glyph duel |
| colossus_stance | colossus_stance | glyph: modifiers | ported |  | weave report; glyph duel |
| transfusion_bond | transfusion_bond | glyph: modifiers | ported |  | weave report; glyph duel |
| controlled_burn | controlled_burn | glyph: modifiers | ported (difference) | drop:overdriveFlow, modifier overdriveRecovery, modifier overdriveFlow | weave report; glyph duel |
| anchored_focus | anchored_focus | glyph: modifiers | ported (difference) | drop:channelLockAim | weave report; glyph duel |
| reckless_breadth | reckless_breadth | glyph: modifiers | ported |  | weave report; glyph duel |
| communal_rites | communal_rites | glyph: modifiers | ported (difference) | drop:offeringShare, modifier offeringShare | weave report; glyph duel |
| deep_reserves | deep_reserves | glyph: uses, modifiers | ported |  | weave report; glyph duel |
| bandolier | bandolier | glyph: modifiers | ported |  | weave report; glyph duel |
| swift_hands | swift_hands | glyph: modifiers | ported |  | weave report; glyph duel |
| dead_mans_round | dead_mans_round | glyph: modifiers | ported (difference) | drop:finalRoundDamage, modifier finalRoundDamage | weave report; glyph duel |
| chambered_casting | chambered_casting | glyph: uses, modifiers, tags | ported |  | weave report; glyph duel |
| ricochet | ricochet | glyph: modifiers | ported |  | weave report; glyph duel |
| mirrored_bounds | mirrored_bounds | glyph: modifiers | ported (difference) | drop:projFrameBounce, modifier projFrameBounce | weave report; glyph duel |
| recurve | recurve | glyph: modifiers | ported (difference) | drop:projRecurve, modifier projRecurve | weave report; glyph duel |
| shredding_return | shredding_return | glyph: modifiers | ported (difference) | drop:returnShrapnel, modifier returnShrapnel | weave report; glyph duel |
| wraith_passage | wraith_passage | glyph: modifiers | ported |  | weave report; glyph duel |
| pulsing_ramparts | pulsing_ramparts | glyph: modifiers | ported (difference) | construct fx (a standing pulse or arrival burst) authored as on_birth and the device kit by hand | weave report; glyph duel |
| violent_genesis | violent_genesis | glyph: modifiers | ported (difference) | construct fx (a standing pulse or arrival burst) authored as on_birth and the device kit by hand | weave report; glyph duel |
| orbital_sweep | orbital_sweep | glyph: modifiers | ported (difference) | drop:channelAutoSpin, modifier channelAutoSpin | weave report; glyph duel |
| whirling_sigil | whirling_sigil | glyph: modifiers | ported (difference) | drop:aoeSpin, modifier aoeSpin | weave report; glyph duel |
| enduring_snares | enduring_snares | glyph: modifiers | ported (difference) | drop:embedIcd, modifier embedIcd | weave report; glyph duel |
| splintered_earth | splintered_earth | glyph: modifiers | ported (difference) | drop:fissureCount | weave report; glyph duel |
| branching_fissures | branching_fissures | glyph: modifiers | ported (difference) | drop:fissureBranches, modifier fissureBranches | weave report; glyph duel |
| suffusion | suffusion | glyph: modifiers | ported (difference) | drop:suffusion | weave report; glyph duel |
| deliberate_ruin | deliberate_ruin | glyph: modifiers | ported |  | weave report; glyph duel |
| grim_sentence | grim_sentence | glyph: modifiers | ported (difference) | drop:curseRupture, modifier curseRupture | weave report; glyph duel |
| command_gem | command_gem | glyph: meta, modifiers | ported |  | weave report; glyph duel |
| self_destruct | self_destruct | glyph: meta | ported (difference) | drop:minionExplodeDeath | weave report; glyph duel |
| shatterrite | shatterrite | glyph: meta, modifiers | ported |  | weave report; glyph duel |
| beckon_beyond | beckon_beyond | glyph: modifiers | ported (difference) | drop:summonAtCursor | weave report; glyph duel |
| violent_arrival | violent_arrival | glyph: modifiers | ported (difference) | drop:summonImpact, modifier summonImpact | weave report; glyph duel |
| pyre_legion | pyre_legion | glyph: modifiers | blocked | blocked: minion auras (row D26, by hand) | weave report; glyph duel |
| ritual_ground | ritual_ground | glyph: modifiers | ported (difference) | drop:channelPersist, modifier channelPersist | weave report; glyph duel |
| closing_instinct | closing_instinct | glyph: modifiers | ported (difference) | a targeting spec is form-level here: a form variant by hand | weave report; glyph duel |
| risen_offering |  | glyph: modifiers | not ported | later: turrets (row B16, later) | weave report; glyph duel |
| sundering_flight |  | glyph: modifiers | not ported | later: fissures (a fabric this port lacks) | weave report; glyph duel |
| skeletal_strike | skeletal_strike | glyph: meta, modifiers | ported |  | weave report; glyph duel |
| ravenous_pact |  | glyph: modifiers | not ported | later: devouring (row B22, later) | weave report; glyph duel |
| miasma | miasma | glyph: modifiers | blocked | blocked: curse fields need cast { weave: host } (row D25 seam) | weave report; glyph duel |
| miasmic_ground | miasmic_ground | glyph: modifiers | blocked | blocked: curse fields need cast { weave: host } (row D25 seam) | weave report; glyph duel |
| ravening | ravening | glyph: spend, modifiers | ported |  | weave report; glyph duel |
| embargo | embargo | glyph: spend, modifiers | ported |  | weave report; glyph duel |
| austerity | austerity | glyph: modifiers | ported |  | weave report; glyph duel |
| apotheosis | apotheosis | glyph: modifiers | ported |  | weave report; glyph duel |
| return_stroke |  | glyph: modifiers | not ported | later: pendulum placements (a placement motion this port lacks) | weave report; glyph duel |
| phalanx | phalanx | glyph: meta, modifiers | ported |  | weave report; glyph duel |
| guarded_casting | guarded_casting | glyph: modifiers | ported (difference) | guarded casting: a reflex gated on guarding | weave report; glyph duel |
| stoneblood_conduit | stoneblood_conduit | glyph: modifiers | blocked | drop:conduitEfficiency; blocked: conduits (row H7, open) | weave report; glyph duel |
| reckless_transfusion | reckless_transfusion | glyph: modifiers | blocked | drop:conduitRate; blocked: conduits (row H7, open) | weave report; glyph duel |
| bulwarks_tithe | bulwarks_tithe | glyph: modifiers | blocked | drop:conduitEfficiency; blocked: conduits (row H7, open) | weave report; glyph duel |
| sanguine_feed | sanguine_feed | glyph: modifiers | blocked | drop:conduitEfficiency; blocked: conduits (row H7, open) | weave report; glyph duel |
| crystal_cistern | crystal_cistern | glyph: modifiers | blocked | drop:conduitRate; blocked: conduits (row H7, open) | weave report; glyph duel |
| thoughtburn | thoughtburn | glyph: modifiers | blocked | drop:conduitEfficiency; blocked: conduits (row H7, open) | weave report; glyph duel |
| overflow_reservoir | overflow_reservoir | glyph: modifiers | blocked | drop:conduitEfficiency; blocked: conduits (row H7, open) | weave report; glyph duel |
| stillmind_conduit | stillmind_conduit | glyph: modifiers | blocked | drop:conduitEfficiency; blocked: conduits (row H7, open) | weave report; glyph duel |
| concussive_blows | concussive_blows | glyph: modifiers | ported (difference) | drop:sunderDuration | weave report; glyph duel |
| flowbreaker | flowbreaker | glyph: modifiers | ported (difference) | drop:insightPen, modifier insightPen | weave report; glyph duel |
| voidrend | voidrend | glyph: modifiers | ported (difference) | drop:esShred, modifier esShred | weave report; glyph duel |
| building_rhythm | building_rhythm | glyph: self_stack, modifiers | ported |  | weave report; glyph duel |
| lightbrand | lightbrand | glyph: modifiers | ported |  | weave report; glyph duel |
| terminal_velocity | terminal_velocity | glyph: modifiers | ported |  | weave report; glyph duel |
| parting_judgement | parting_judgement | glyph: hooks on_end, modifiers | ported |  | weave report; glyph duel |
| rarefy | rarefy | glyph: modifiers | ported |  | weave report; glyph duel |
| pallid_touch | pallid_touch | glyph: modifiers | ported |  | weave report; glyph duel |
| sickening | sickening | glyph: modifiers | ported |  | weave report; glyph duel |
| unblinking | unblinking | glyph: modifiers | ported |  | weave report; glyph duel |
| maddering | maddering | glyph: modifiers | ported |  | weave report; glyph duel |
| addling | addling | glyph: modifiers | ported |  | weave report; glyph duel |
| aureate_writ | aureate_writ | glyph: modifiers | ported |  | weave report; glyph duel |
| sanctal_cautery | sanctal_cautery | glyph: modifiers | ported |  | weave report; glyph duel |
| boiling_point | boiling_point | glyph: modifiers | ported (difference) | drop:applyWet_scalded, modifier applyWet_scalded | weave report; glyph duel |
| pressure_seal | pressure_seal | glyph: uses, modifiers | ported |  | weave report; glyph duel |
| afterspray | afterspray | glyph: tags | ported (difference) | drop:departSplash, modifier departSplash | weave report; glyph duel |
| vaporize | vaporize | glyph: modifiers | ported |  | weave report; glyph duel |
| mineral_tuning | mineral_tuning | glyph: modifiers | ported (difference) | drop:tuneFavor, modifier tuneFavor | weave report; glyph duel |
| executioners_edge | executioners_edge | glyph: modifiers | ported |  | weave report; glyph duel |
| shatterglass | shatterglass | glyph: modifiers | ported |  | weave report; glyph duel |
| opportunists_blade | opportunists_blade | glyph: modifiers | ported |  | weave report; glyph duel |
| assassins_angle | assassins_angle | glyph: modifiers | ported |  | weave report; glyph duel |
| frostbite_grip | frostbite_grip | glyph: modifiers | ported |  | weave report; glyph duel |
| hunters_momentum | hunters_momentum | glyph: modifiers | ported |  | weave report; glyph duel |
| steady_nerve | steady_nerve | glyph: modifiers | ported |  | weave report; glyph duel |
| deaths_door | deaths_door | glyph: modifiers | ported |  | weave report; glyph duel |
| crowd_favor | crowd_favor | glyph: modifiers | ported |  | weave report; glyph duel |
| at_the_brink | at_the_brink | glyph: modifiers | ported |  | weave report; glyph duel |
| fevered_hands | fevered_hands | glyph: modifiers | ported |  | weave report; glyph duel |
| answered_dodge | answered_dodge | glyph: modifiers | ported |  | weave report; glyph duel |
| desperate_measures | desperate_measures | glyph: modifiers | ported |  | weave report; glyph duel |

## Statuses (152: ported (difference) 38, ported 54, not ported 59, blocked 1)

| Prototype | Ours | Mechanism | Status | Note | Proof |
|---|---|---|---|---|---|
| burn | burning | tick | ported (difference) | roster:burning | status duel |
| flamewreathed | flamewreathed | tick | ported |  | status duel |
| starfire | starfire | tick | ported |  | status duel |
| stonelashed | stonelashed | tick, modifiers | ported |  | status duel |
| moonlit |  |  | not ported | fabric |  |
| companion_recovery |  |  | not ported | fabric |  |
| poison | poison | tick | ported (difference) | roster:poison | status duel |
| bleed | bleeding | tick | ported (difference) | roster:bleeding | status duel |
| impaled_physical | impaled | bank, consume_on, on_bearer_struck | ported (difference) | roster:impaled; discharges on any hit, not only its own type | status duel |
| impaled_fire | impaled | bank, consume_on, on_bearer_struck | ported (difference) | roster:impaled; discharges on any hit, not only its own type | status duel |
| impaled_cold | impaled | bank, consume_on, on_bearer_struck | ported (difference) | roster:impaled; discharges on any hit, not only its own type | status duel |
| impaled_lightning | impaled | bank, consume_on, on_bearer_struck | ported (difference) | roster:impaled; discharges on any hit, not only its own type | status duel |
| impaled_chaos | impaled | bank, consume_on, on_bearer_struck | ported (difference) | roster:impaled; discharges on any hit, not only its own type | status duel |
| impaled | impaled | bank, consume_on, on_bearer_struck | ported (difference) | roster:impaled | status duel |
| hemorrhage | hemorrhage | tick, pop | ported (difference) | roster:hemorrhage | status duel |
| fogveiled |  |  | not ported | fabric |  |
| mistfed |  |  | not ported | fabric |  |
| canopied |  |  | not ported | fabric |  |
| sunscorched | sunscorched | buildup, modifiers | ported |  | status duel |
| heatstroke | heatstroke | modifiers | ported |  | status duel |
| faintness | faintness | buildup, modifiers | ported |  | status duel |
| swoon | swoon | modifiers | ported |  | status duel |
| queasy | queasy | buildup, modifiers | ported |  | status duel |
| retching | retching | modifiers | ported |  | status duel |
| beheld |  |  | not ported | fabric |  |
| seen |  |  | not ported | fabric |  |
| blind | blinded | modifiers | ported (difference) | roster:blinded; drop:detectionRange | status duel |
| chill | chilled | buildup, modifiers | ported (difference) | roster:chilled; drop:insightSap | status duel |
| frozen | frozen | flags stunned, modifiers | ported (difference) | roster:frozen; drop:insightSap | status duel |
| shock | shocked | modifiers | ported (difference) | roster:shocked | status duel |
| stun | stunned | flags stunned | ported (difference) | roster:stunned; drop:insightSap | status duel |
| unhorsed |  |  | not ported | fabric |  |
| winded_gasp |  |  | not ported | fabric |  |
| guttered |  |  | not ported | fabric |  |
| sap_starved |  |  | not ported | fabric |  |
| wilted |  |  | not ported | fabric |  |
| sporemired |  |  | not ported | fabric |  |
| entranced | entranced | flags stunned | ported (difference) | drop:insightSap | status duel |
| stasis |  |  | not ported | fabric |  |
| temporal_drag |  |  | not ported | fabric |  |
| petrifying | petrifying | buildup, modifiers | ported |  | status duel |
| petrified | petrified | flags stunned, modifiers | not ported | drop:insightSap; later: personal time scales (row E14, later) | status duel |
| harrowing | harrowing | buildup, modifiers | ported |  | status duel |
| horrified | horrified | flags panicked, modifiers | ported |  | status duel |
| silence | silenced | forbids | ported (difference) | roster:silenced | status duel |
| disarm | disarmed | forbids | ported (difference) | roster:disarmed | status duel |
| rooted | rooted | forbids, modifiers | ported (difference) | roster:rooted | status duel |
| sundered | sundered | modifiers | ported |  | status duel |
| weaken | weakened | modifiers | ported (difference) | roster:weakened | status duel |
| vulnerable | vulnerable | modifiers | ported (difference) | roster:vulnerable | status duel |
| brittle | brittle | modifiers | ported (difference) | roster:brittle | status duel |
| melting | melting | modifiers | ported |  | status duel |
| waxlight | waxlight | status | ported (difference) | drop:detectability | status duel |
| doom | doom | bank | ported (difference) | roster:doom | status duel |
| scorch | scorch | tick | ported |  | status duel |
| wither_agony | wither_agony | tick | ported |  | status duel |
| unravelling | unraveling | tick, buildup, modifiers | ported (difference) | roster:unraveling | status duel |
| unmade | unmade | modifiers | ported |  | status duel |
| jinxed | jinxed | status | ported (difference) | drop:unluckyChance | status duel |
| maddened | maddened | tick | ported (difference) | drop:detectionRange | status duel |
| reeling | reeling | status | ported (difference) | drop:insightRegenPct | status duel |
| voided | voided | modifiers | ported |  | status duel |
| taunted | taunted | status | ported (difference) | roster:taunted | status duel |
| prey_marked | prey_marked | flags prey, modifiers | ported |  | status duel |
| bolted | bolted | flags panicked, modifiers | ported |  | status duel |
| exposed | exposed | status | blocked | roster:exposed; blocked: weak spots (row E15, open) | status duel |
| sear | seared | status | ported (difference) | drop:healTaken | status duel |
| decay | decay | tick | ported |  | status duel |
| torment | torment | tick, modifiers | ported |  | status duel |
| contagion | contagion | tick | ported |  | status duel |
| despair | despair | modifiers | ported |  | status duel |
| agony | agony | modifiers | ported |  | status duel |
| indecision | indecision | modifiers | ported |  | status duel |
| befuddlement | befuddled | modifiers | ported |  | status duel |
| bewilder | bewildered | status | ported (difference) | drop:aiAimJitter, modifier aiAimLead | status duel |
| disoriented | disoriented | buildup | ported |  | status duel |
| widdershins |  |  | not ported | fabric |  |
| addled | addled | status | not ported | roster:addled; later: scrambled casts (row A21, later) | status duel |
| hedonism | hedonism | modifiers | ported |  | status duel |
| belligerence | belligerence | status | ported (difference) | drop:detectionRange | status duel |
| furor | furor | modifiers | ported |  | status duel |
| rally | rallied | modifiers | ported |  | status duel |
| verdant_communion | verdant_communion | modifiers | ported |  | status duel |
| stone_communion | stone_communion | modifiers | ported |  | status duel |
| attuned_physical | attuned_stone | modifiers | ported |  | status duel |
| attuned_fire | attuned_ember | modifiers | ported |  | status duel |
| attuned_cold | attuned_tide | modifiers | ported |  | status duel |
| attuned_lightning | attuned_storm | modifiers | ported |  | status duel |
| attuned_chaos | attuned_void | modifiers | ported |  | status duel |
| kindled | kindled | status | ported |  | status duel |
| consecrated | consecrated | modifiers | ported |  | status duel |
| phasing | phasing | modifiers | ported |  | status duel |
| caroming |  |  | not ported | fabric |  |
| windswept |  |  | not ported | fabric |  |
| winded | winded | modifiers | ported |  | status duel |
| cloudform |  |  | not ported | fabric |  |
| aloft |  |  | not ported | fabric |  |
| cloudhaven |  |  | not ported | fabric |  |
| windlane |  |  | not ported | fabric |  |
| stormlaced |  |  | not ported | fabric |  |
| silverlined |  |  | not ported | fabric |  |
| smothered | smothered | modifiers | ported (difference) | drop:detectionRange | status duel |
| gloomveiled | gloomveiled | status | ported (difference) | drop:detectionRange, modifier detectability | status duel |
| hearthglow | hearthglow | status | ported (difference) | drop:windchillWard | status duel |
| transfixed | transfixed | modifiers | not ported | later: scrambled casts (row A21, later) | status duel |
| emboldened | emboldened | modifiers | ported |  | status duel |
| quickborn | quickborn | modifiers | ported |  | status duel |
| blooming | blooming | status | ported |  | status duel |
| infected_miasma |  |  | not ported | fabric |  |
| infected_adrenal |  |  | not ported | fabric |  |
| infected_mutant |  |  | not ported | fabric |  |
| wisp_ridden_pale |  |  | not ported | fabric |  |
| wisp_ridden_flame |  |  | not ported | fabric |  |
| wisp_ridden_grave |  |  | not ported | fabric |  |
| wisp_ridden_gloam |  |  | not ported | fabric |  |
| harried |  |  | not ported | fabric |  |
| seized |  |  | not ported | fabric |  |
| swallowed |  |  | not ported | fabric |  |
| burrowed |  |  | not ported | fabric |  |
| mu_veiled |  |  | not ported | fabric |  |
| mu_faint |  |  | not ported | fabric |  |
| mu_sworn |  |  | not ported | fabric |  |
| vent_submerged |  |  | not ported | fabric |  |
| vent_sinking |  |  | not ported | fabric |  |
| living_bomb | living_bomb | status | ported |  | status duel |
| fulgur_brand | fulgur_brand | tick | ported |  | status duel |
| doombrand | doombrand | status | ported |  | status duel |
| powder_charge | powder_charge | status | ported |  | status duel |
| warded | warded_shell | modifiers | ported (difference) | roster:warded_shell; bound to an absorption pool: bound_to names the pool status by hand | status duel |
| mired | mired | modifiers | ported |  | status duel |
| sodden |  |  | not ported | fabric |  |
| wading |  |  | not ported | fabric |  |
| crestborne |  |  | not ported | fabric |  |
| soulchill | soulchill | modifiers | ported (difference) | drop:insightSap | status duel |
| swimming |  |  | not ported | fabric |  |
| bogged |  |  | not ported | fabric |  |
| bog_rot | bog_rot | tick | ported |  | status duel |
| brine_burn | brine_burn | tick | ported |  | status duel |
| sulphur_sting | sulphur_sting | tick | ported |  | status duel |
| scalded | scalding | tick, bank | ported (difference) | roster:scalding | status duel |
| soaked |  |  | not ported | fabric |  |
| lamprey_flop |  |  | not ported | fabric |  |
| basking_torpor |  |  | not ported | fabric |  |
| basking_fury |  |  | not ported | fabric |  |
| slippery |  |  | not ported | fabric |  |
| seabed |  |  | not ported | fabric |  |
| concealed |  |  | not ported | fabric |  |
| ensnared | ensnared | modifiers | ported |  | status duel |
| caulfed |  |  | not ported | fabric |  |
| caulmired |  |  | not ported | fabric |  |
| blightfed |  |  | not ported | fabric |  |
| blightmired |  |  | not ported | fabric |  |

## Procs (147: ported 96, not ported 16, ported (difference) 29, seam 6)

| Prototype | Ours | Mechanism | Status | Note | Proof |
|---|---|---|---|---|---|
| brutal_strike | brutal_strike | proc on hit: extraHit | ported |  | proc duel |
| parasite_sip | parasite_sip | proc on hit: heal | ported |  | proc duel |
| pathcutter_stride | pathcutter_stride | proc on surface: buff | not ported | later: trigger surface: a fabric or pool event this port lacks | proc duel |
| cave_in | cave_in | proc on surface: burst | not ported | later: trigger surface: a fabric or pool event this port lacks | proc duel |
| thunderstruck | thunderstruck | proc on hit: explosion | ported |  | proc duel |
| vaporize | vaporize | proc on hit: vent | not ported | once per cast: an icd of a tick by hand; later: vent (row C36, later) | proc duel |
| corpsefire | corpsefire | proc on kill: explosion | ported (difference) | minion carry: minion hits raise the minion's own triggers here | proc duel |
| gutterglow | gutterglow | proc on kill: kindle | not ported | minion carry: minion hits raise the minion's own triggers here; later: kindle (row C36, later) | proc duel |
| short_circuit | short_circuit | proc on hit: explosion | ported |  | proc duel |
| overload_arc | overload_arc | proc on hit: arc | ported |  | proc duel |
| displacement_field | displacement_field | proc on hit: displace | ported |  | proc duel |
| magnetic_pull | magnetic_pull | proc on hit: displace | ported |  | proc duel |
| collision_crush | collision_crush | proc on collision: collisionDamage | ported |  | proc duel |
| bloodletters_rhythm | bloodletters_rhythm | proc on status_applied: gainCharge | ported (difference) | status filter: the proc reads the event's status through a seam (ProcDef.status) or by hand | proc duel |
| forge_weapon | forge_weapon | proc on hit: summon | ported (difference) | sum:remnant_shade<blade_wraith | proc duel |
| tempo | tempo | proc on hit: buff | ported |  | proc duel |
| adrenal_rush | adrenal_rush | proc on status_applied: buff | ported (difference) | status filter: the proc reads the event's status through a seam (ProcDef.status) or by hand | proc duel |
| bulwark_nova | bulwark_nova | proc on block: burst | ported |  | proc duel |
| guarded_heart | guarded_heart | proc on block: heal | ported |  | proc duel |
| second_wind | second_wind | proc on evade: heal | ported |  | proc duel |
| quickening | quickening | proc on evade: buff | ported |  | proc duel |
| answering_gale | answering_gale | proc on evade: cast | ported |  | proc duel |
| capacitor_burst | capacitor_burst | proc on broken: burst | ported |  | proc duel |
| phase_surge | phase_surge | proc on broken: buff | ported |  | proc duel |
| breakers_momentum | breakers_momentum | proc on break: buff | ported |  | proc duel |
| last_stand | last_stand | proc on broken: buff | ported |  | proc duel |
| shatterplate | shatterplate | proc on broken: burst | ported |  | proc duel |
| unbroken_wrath | unbroken_wrath | proc on broken: buff | ported |  | proc duel |
| tempered_rearm | tempered_rearm | proc on poiseRearmed: buff | not ported | later: trigger poiseRearmed: a fabric or pool event this port lacks | proc duel |
| shedding_plates | shedding_plates | proc on poiseBracket: fortify | not ported | later: trigger poiseBracket: a fabric or pool event this port lacks; later: endurance (a pool this port lacks) | proc duel |
| crest_discharge | crest_discharge | proc on esFilled: burst | not ported | later: trigger esFilled: a fabric or pool event this port lacks | proc duel |
| mind_like_water | mind_like_water | proc on evade: restore | ported |  | proc duel |
| radiant_reprisal | radiant_reprisal | proc on hit: delayedBurst | ported (difference) | once per cast: an icd of a tick by hand; skill gate (sanctified_strike): tags of the named weaves by hand | proc duel |
| radiant_cascade | radiant_cascade | proc on hit: delayedBurst | ported (difference) | skill gate (sanctified_strike): tags of the named weaves by hand | proc duel |
| executioners_rhythm | executioners_rhythm | proc on kill: cooldown | ported |  | proc duel |
| bastion_fortify | bastion_fortify | proc on block: fortify | not ported | later: endurance (a pool this port lacks) | proc duel |
| stalwart_rhythm | stalwart_rhythm | proc on block: cooldown | ported |  | proc duel |
| kindled_rage | kindled_rage | proc on charge_gained: gainCharge | ported (difference) | charge filter: the proc reads the event's charge through a seam (ProcDef.status) or by hand | proc duel |
| crimson_thirst | crimson_thirst | proc on charge_gained: gainCharge | ported (difference) | charge filter: the proc reads the event's charge through a seam (ProcDef.status) or by hand | proc duel |
| surging_frenzy | surging_frenzy | proc on charge_gained: gainCharge | ported (difference) | charge filter: the proc reads the event's charge through a seam (ProcDef.status) or by hand | proc duel |
| battle_chorus | battle_chorus | proc on status_applied: gainCharge | ported |  | proc duel |
| summon_phantasm | summon_phantasm | proc on hit: summon | ported (difference) | minion carry: minion hits raise the minion's own triggers here; sum:shade_echo<phantasm | proc duel |
| ruptured_veins | ruptured_veins | proc on hit: status | ported |  | proc duel |
| crimson_edge | crimson_edge | proc on crit: gainCharge | ported |  | proc duel |
| battle_insight | battle_insight | proc on crit: gainCharge | ported |  | proc duel |
| communion_tithe | communion_tithe | proc on hit: gainCharge | ported (difference) | minion carry: minion hits raise the minion's own triggers here | proc duel |
| sainted_ash | sainted_ash | proc on kill: delayedBurst | ported (difference) | minion carry: minion hits raise the minion's own triggers here | proc duel |
| votive_spark | votive_spark | proc on orbPickup: restore | not ported | orb filter: the proc reads the event's orb through a seam (ProcDef.status) or by hand; later: trigger orbPickup: a fabric or pool event this port lacks | proc duel |
| mi_gnoll_impale | mi_gnoll_impale | proc on hit: cast | ported (difference) | once per cast: an icd of a tick by hand | proc duel |
| mi_goblin_fan | mi_goblin_fan | proc on hit: cast | ported (difference) | once per cast: an icd of a tick by hand | proc duel |
| mi_bandit_caltrops | mi_bandit_caltrops | proc on hit: cast | ported (difference) | once per cast: an icd of a tick by hand | proc duel |
| hot_streak | hot_streak | proc on crit: buff | ported (difference) | once per cast: an icd of a tick by hand | proc duel |
| heating_up | heating_up | proc on crit: buff | ported (difference) | once per cast: an icd of a tick by hand | proc duel |
| heat_lost | heat_lost | proc on hit: removeBuff | ported (difference) | once per cast: an icd of a tick by hand | proc duel |
| overpower | overpower | proc on miss: buff | seam | seam: trigger kind miss (a trigger kind to register) | proc duel |
| shield_lesson | shield_lesson | proc on foiled: restore | seam | seam: trigger kind foiled (a trigger kind to register) | proc duel |
| reprisal | reprisal | proc on hurt: buff | ported |  | proc duel |
| deep_freeze | deep_freeze | proc on hit: status | ported |  | proc duel |
| desperate_ward | desperate_ward | proc on condition: ward | seam | seam: trigger kind condition (a trigger kind to register) | proc duel |
| adrenaline | adrenaline | proc on cast: buff | ported |  | proc duel |
| desperate_reserves | desperate_reserves | proc on cast: restore | ported |  | proc duel |
| slow_burn | slow_burn | proc on interval: gainCharge | ported |  | proc duel |
| necrotic_feast | necrotic_feast | proc on minion_died: buff | ported |  | proc duel |
| mending_ward | mending_ward | proc on heal: ward | ported |  | proc duel |
| phoenix | phoenix | proc on cheat_death: burst | ported |  | proc duel |
| unbound | unbound | proc on hurt: cleanse | ported |  | proc duel |
| emberbrand_rekindle | emberbrand_rekindle | proc on status_applied: cast | ported (difference) | status filter: the proc reads the event's status through a seam (ProcDef.status) or by hand | proc duel |
| stormcall_answer | stormcall_answer | proc on hit: cast | ported (difference) | once per cast: an icd of a tick by hand | proc duel |
| bloodletting | bloodletting | proc on kill: explosion | ported (difference) | once per cast: an icd of a tick by hand | proc duel |
| fleetfeather_refusal | fleetfeather_refusal | proc on evade: buff | ported |  | proc duel |
| misers_whisper | misers_whisper | proc on condition: cooldown | seam | seam: trigger kind condition (a trigger kind to register) | proc duel |
| cindervigil_answer | cindervigil_answer | proc on cast: cast | ported |  | proc duel |
| rimewake_release | rimewake_release | proc on cast: cast | ported |  | proc duel |
| mourning_bell_toll | mourning_bell_toll | proc on minion_died: ward | ported |  | proc duel |
| faultline_return | faultline_return | proc on collision: cooldown | ported |  | proc duel |
| lattice_discharge | lattice_discharge | proc on hurt: cast | ported (difference) | received-type gate dropped | proc duel |
| galewright_twister | galewright_twister | proc on interval: cast | ported |  | proc duel |
| cinder_answer | cinder_answer | proc on hurt: ward | ported (difference) | received-type gate dropped | proc duel |
| cinder_conductor | cinder_conductor | proc on hit: restore | ported (difference) | minion carry: minion hits raise the minion's own triggers here | proc duel |
| breach_bell | breach_bell | proc on break: cast | ported |  | proc duel |
| unspent_reply | unspent_reply | proc on block: buff | ported |  | proc duel |
| relic_guard_refrain | relic_guard_refrain | proc on block: restore | ported |  | proc duel |
| relic_wayfarer | relic_wayfarer | proc on cast: ward | ported |  | proc duel |
| relic_widows_wick | relic_widows_wick | proc on minion_died: restore | ported |  | proc duel |
| route_breach_spell | route_breach_spell | proc on break: buff | ported |  | proc duel |
| route_collision_mana | route_collision_mana | proc on collision: restore | ported |  | proc duel |
| route_evade_clock | route_evade_clock | proc on evade: cooldown | ported |  | proc duel |
| route_varied_ward | route_varied_ward | proc on cast: ward | ported |  | proc duel |
| route_filled_spell | route_filled_spell | proc on esFilled: buff | not ported | later: trigger esFilled: a fabric or pool event this port lacks | proc duel |
| route_empty_footing | route_empty_footing | proc on cast: restore | ported |  | proc duel |
| route_funeral_clock | route_funeral_clock | proc on minion_died: cooldown | ported |  | proc duel |
| route_summon_ward | route_summon_ward | proc on cast: ward | ported |  | proc duel |
| route_back_mana | route_back_mana | proc on hit: restore | ported |  | proc duel |
| route_move_needle | route_move_needle | proc on cast: buff | ported |  | proc duel |
| route_device_clock | route_device_clock | proc on cast: cooldown | ported |  | proc duel |
| route_evade_device | route_evade_device | proc on evade: buff | ported |  | proc duel |
| route_block_clock | route_block_clock | proc on block: cooldown | ported |  | proc duel |
| route_rearmed_ward | route_rearmed_ward | proc on poiseRearmed: ward | not ported | later: trigger poiseRearmed: a fabric or pool event this port lacks | proc duel |
| route_heal_clock | route_heal_clock | proc on heal: cooldown | ported |  | proc duel |
| route_block_mana | route_block_mana | proc on block: restore | ported |  | proc duel |
| route_held_clock | route_held_clock | proc on hit: cooldown | ported |  | proc duel |
| route_shield_poise | route_shield_poise | proc on broken: restore | ported |  | proc duel |
| native_undertow_fist | native_undertow_fist | proc on hit: displace | ported |  | proc duel |
| native_breach_reserve | native_breach_reserve | proc on break: restore | ported |  | proc duel |
| native_repeated_frenzy | native_repeated_frenzy | proc on cast: gainCharge | ported |  | proc duel |
| native_varied_mana | native_varied_mana | proc on cast: restore | ported |  | proc duel |
| native_critical_reservoir | native_critical_reservoir | proc on crit: restore | ported |  | proc duel |
| native_broken_veil | native_broken_veil | proc on broken: cleanse | ported |  | proc duel |
| native_parting_blessing | native_parting_blessing | proc on minion_died: delayedBurst | ported |  | proc duel |
| native_summoners_breath | native_summoners_breath | proc on cast: restore | ported |  | proc duel |
| native_vanishing_guard | native_vanishing_guard | proc on evade: ward | ported |  | proc duel |
| native_venom_breath | native_venom_breath | proc on status_applied: restore | ported (difference) | status filter: the proc reads the event's status through a seam (ProcDef.status) or by hand | proc duel |
| native_workshop_fury | native_workshop_fury | proc on cast: gainCharge | ported |  | proc duel |
| native_field_patch | native_field_patch | proc on cast: heal | ported |  | proc duel |
| native_halfway_shelter | native_halfway_shelter | proc on poiseBracket: ward | not ported | bracket filter: the proc reads the event's bracket through a seam (ProcDef.status) or by hand; later: trigger poiseBracket: a fabric or pool event this port lacks | proc duel |
| native_barbed_reply | native_barbed_reply | proc on block: buff | ported |  | proc duel |
| native_verse_reservoir | native_verse_reservoir | proc on charge_gained: restore | ported (difference) | charge filter: the proc reads the event's charge through a seam (ProcDef.status) or by hand | proc duel |
| native_song_of_return | native_song_of_return | proc on cast: delayedBurst | ported |  | proc duel |
| native_thawing_reserve | native_thawing_reserve | proc on hit: restore | ported |  | proc duel |
| native_wounded_clock | native_wounded_clock | proc on condition: cooldown | seam | seam: trigger kind condition (a trigger kind to register) | proc duel |
| weave_empty_stride | weave_empty_stride | proc on cast: restore | ported |  | proc duel |
| weave_still_mercy | weave_still_mercy | proc on condition: buff | seam | seam: trigger kind condition (a trigger kind to register) | proc duel |
| weave_airborne_shelter | weave_airborne_shelter | proc on hit: ward | ported |  | proc duel |
| weave_wall_fury | weave_wall_fury | proc on collision: gainCharge | ported |  | proc duel |
| weave_rain_collector | weave_rain_collector | proc on hit: restore | ported |  | proc duel |
| weave_rack_memory | weave_rack_memory | proc on cast: buff | ported |  | proc duel |
| weave_red_glass | weave_red_glass | proc on orbPickup: ward | not ported | orb filter: the proc reads the event's orb through a seam (ProcDef.status) or by hand; later: trigger orbPickup: a fabric or pool event this port lacks | proc duel |
| weave_blood_glass | weave_blood_glass | proc on status_applied: restore | ported (difference) | status filter: the proc reads the event's status through a seam (ProcDef.status) or by hand | proc duel |
| weave_thermal_memory | weave_thermal_memory | proc on hurt: buff | ported (difference) | received-type gate dropped | proc duel |
| weave_unbroken_return | weave_unbroken_return | proc on esRechargeStart: cleanse | not ported | later: trigger esRechargeStart: a fabric or pool event this port lacks | proc duel |
| weave_block_reserve | weave_block_reserve | proc on block: fortify | not ported | later: endurance (a pool this port lacks) | proc duel |
| weave_warcry_verse | weave_warcry_verse | proc on cast: buff | ported |  | proc duel |
| weave_funeral_clock | weave_funeral_clock | proc on cast: cooldown | ported |  | proc duel |
| weave_delegated_breath | weave_delegated_breath | proc on hit: restore | ported (difference) | minion carry: minion hits raise the minion's own triggers here | proc duel |
| weave_trap_hammer | weave_trap_hammer | proc on cast: buff | ported |  | proc duel |
| weave_spell_hunger | weave_spell_hunger | proc on cast: buff | ported |  | proc duel |
| weave_missed_jackpot | weave_missed_jackpot | proc on hit: buff | ported |  | proc duel |
| weave_break_for_breath | weave_break_for_breath | proc on surface: heal | not ported | later: trigger surface: a fabric or pool event this port lacks | proc duel |
| spec_fire_relay | spec_fire_relay | proc on cast: buff | ported |  | proc duel |
| spec_cold_relay | spec_cold_relay | proc on cast: buff | ported |  | proc duel |
| spec_lightning_relay | spec_lightning_relay | proc on cast: buff | ported |  | proc duel |
| spec_blade_to_spell | spec_blade_to_spell | proc on cast: buff | ported |  | proc duel |
| spec_spell_to_blade | spec_spell_to_blade | proc on cast: buff | ported |  | proc duel |
| spec_quick_preparation | spec_quick_preparation | proc on cast: buff | ported |  | proc duel |
| spec_deliberate_release | spec_deliberate_release | proc on cast: buff | ported |  | proc duel |
| spec_advance | spec_advance | proc on cast: buff | ported |  | proc duel |
| spec_return | spec_return | proc on cast: buff | ported |  | proc duel |

## Charges (23: ported 12, ported (difference) 5, not ported 6)

| Prototype | Ours | Mechanism | Status | Note | Proof |
|---|---|---|---|---|---|
| fury | fury | charge kind | ported |  | charge duel |
| static | static | charge kind | ported |  | charge duel |
| stealth | stealth | charge kind | ported |  | charge duel |
| wisp | wisp | charge kind | ported (difference) | regeneration of 2.5 per 10s: no accrual clock on a charge (seam or an interval proc) | charge duel |
| verse | verse | charge kind | ported |  | charge duel |
| riposte | riposte | charge kind | ported |  | charge duel |
| magma | magma | charge kind | ported |  | charge duel |
| rage | rage | charge kind | ported (difference) | per-charge modifiers are granted by whoever uses the charge, with gauge charge.<id> | charge duel |
| bloodlust | bloodlust | charge kind | ported (difference) | per-charge modifiers are granted by whoever uses the charge, with gauge charge.<id>; mods only while draining: a drain flag by hand | charge duel |
| soul | soul | charge kind | ported |  | charge duel |
| gyre | gyre | charge kind | ported |  | charge duel |
| storm | storm | charge kind | ported |  | charge duel |
| frenzy | frenzy | charge kind | ported (difference) | per-charge modifiers are granted by whoever uses the charge, with gauge charge.<id> | charge duel |
| brew | brew | charge kind | ported |  | charge duel |
| verdict | verdict | charge kind | ported |  | charge duel |
| communion | communion | charge kind | ported (difference) | per-charge modifiers are granted by whoever uses the charge, with gauge charge.<id> | charge duel |
| flask_life | flask_life | charge kind | not ported | flask founts: the flask family is not ported | charge duel |
| flask_mana | flask_mana | charge kind | not ported | flask founts: the flask family is not ported | charge duel |
| flask_catalyst | flask_catalyst | charge kind | not ported | flask founts: the flask family is not ported | charge duel |
| flask_quicksilver | flask_quicksilver | charge kind | not ported | flask founts: the flask family is not ported | charge duel |
| flask_stoneskin | flask_stoneskin | charge kind | not ported | flask founts: the flask family is not ported | charge duel |
| flask_antidote | flask_antidote | charge kind | not ported | flask founts: the flask family is not ported | charge duel |
| wakeflame | wakeflame | charge kind | ported |  | charge duel |

## Passive notables, keystones and choice nodes (668: to author 663, blocked 5)

| Prototype | Family | Kind | Ours | Mechanism | Status | Note | Proof |
|---|---|---|---|---|---|---|---|
| spec_fire_pressure | spec_*_pressure | notable |  | modifiers | to author |  | passives lint |
| spec_fire_fire_relay | spec_*_*_relay | notable |  | proc, modifiers | to author |  | passives lint |
| spec_fire_devotion | spec_*_devotion | keystone |  | modifiers | to author |  | passives lint |
| spec_cold_pressure | spec_*_pressure | notable |  | modifiers | to author |  | passives lint |
| spec_cold_cold_relay | spec_*_*_relay | notable |  | proc, modifiers | to author |  | passives lint |
| spec_cold_devotion | spec_*_devotion | keystone |  | modifiers | to author |  | passives lint |
| spec_lightning_pressure | spec_*_pressure | notable |  | modifiers | to author |  | passives lint |
| spec_lightning_lightning_relay | spec_*_*_relay | notable |  | proc, modifiers | to author |  | passives lint |
| spec_lightning_devotion | spec_*_devotion | keystone |  | modifiers | to author |  | passives lint |
| spec_melee_crowding | spec_*_crowding | notable |  | modifiers | to author |  | passives lint |
| spec_melee_blade_to_spell | spec_*_blade_to_spell | notable |  | proc, modifiers | to author |  | passives lint |
| spec_melee_short_blade | spec_*_short_blade | keystone |  | modifiers | to author |  | passives lint |
| spec_spell_spell_to_blade | spec_*_spell_to_blade | notable |  | proc, modifiers | to author |  | passives lint |
| spec_spell_reserve | spec_*_reserve | notable |  | modifiers | to author |  | passives lint |
| spec_spell_syntax | spec_*_syntax | notable |  | modifiers | to author |  | passives lint |
| spec_ranged_interval | spec_ranged_interval | notable |  | modifiers | to author |  | passives lint |
| spec_ranged_distant | spec_ranged_distant | notable |  | modifiers | to author |  | passives lint |
| spec_ranged_heavy_shaft | spec_ranged_heavy_shaft | keystone |  | modifiers | to author |  | passives lint |
| spec_instant_quick_preparation | spec_instant_quick_preparation | notable |  | proc, modifiers | to author |  | passives lint |
| spec_instant_quick_recovery | spec_instant_quick_recovery | notable |  | modifiers | to author |  | passives lint |
| spec_instant_quick_shelter | spec_instant_quick_shelter | notable |  | modifiers | to author | drop:absorbPower | passives lint |
| spec_timed_deliberate_release | spec_timed_deliberate_release | notable |  | proc, modifiers | to author |  | passives lint |
| spec_timed_still_recital | spec_timed_still_recital | notable |  | modifiers | to author |  | passives lint |
| spec_timed_long_recital | spec_timed_long_recital | keystone |  | modifiers | to author |  | passives lint |
| spec_channel_ramp | spec_channel_ramp | notable |  | node | to author | drop:channelRamp | passives lint |
| spec_channel_barbs | spec_channel_barbs | notable |  | modifiers | to author | drop:channelThorns | passives lint |
| spec_channel_narrow_current | spec_channel_narrow_current | keystone |  | modifiers | to author |  | passives lint |
| spec_swarm_wingbeat | spec_swarm_wingbeat | notable |  | modifiers | to author |  | passives lint |
| spec_swarm_cohort | spec_swarm_cohort | notable |  | modifiers | to author |  | passives lint |
| spec_swarm_formation | spec_swarm_formation | notable |  | modifiers | to author |  | passives lint |
| spec_skeleton_marrow | spec_skeleton_marrow | notable |  | modifiers | to author |  | passives lint |
| spec_skeleton_cohort | spec_skeleton_cohort | notable |  | modifiers | to author |  | passives lint |
| spec_skeleton_formation | spec_skeleton_formation | notable |  | modifiers | to author |  | passives lint |
| spec_archer_quiver | spec_archer_quiver | notable |  | modifiers | to author |  | passives lint |
| spec_archer_veteran | spec_archer_veteran | notable |  | modifiers | to author |  | passives lint |
| spec_archer_formation | spec_archer_formation | notable |  | modifiers | to author |  | passives lint |
| spec_mage_academy | spec_mage_academy | notable |  | modifiers | to author |  | passives lint |
| spec_mage_cohort | spec_mage_cohort | notable |  | modifiers | to author |  | passives lint |
| spec_mage_formation | spec_mage_formation | notable |  | modifiers | to author |  | passives lint |
| spec_golem_renewal | spec_golem_renewal | notable |  | modifiers | to author |  | passives lint |
| spec_golem_cohort | spec_golem_cohort | notable |  | modifiers | to author |  | passives lint |
| spec_golem_formation | spec_golem_formation | notable |  | modifiers | to author |  | passives lint |
| spec_stone_granite | spec_stone_granite | notable |  | modifiers | to author |  | passives lint |
| spec_stone_veteran | spec_stone_veteran | notable |  | modifiers | to author |  | passives lint |
| spec_stone_formation | spec_stone_formation | notable |  | modifiers | to author |  | passives lint |
| spec_bone_marrow_wall | spec_bone_marrow_wall | notable |  | modifiers | to author |  | passives lint |
| spec_bone_veteran | spec_bone_veteran | notable |  | modifiers | to author |  | passives lint |
| spec_bone_formation | spec_bone_formation | notable |  | modifiers | to author |  | passives lint |
| spec_flame_furnace | spec_flame_furnace | notable |  | modifiers | to author |  | passives lint |
| spec_flame_veteran | spec_flame_veteran | notable |  | modifiers | to author |  | passives lint |
| spec_flame_formation | spec_flame_formation | notable |  | modifiers | to author |  | passives lint |
| spec_ice_glacier | spec_ice_glacier | notable |  | modifiers | to author |  | passives lint |
| spec_ice_veteran | spec_ice_veteran | notable |  | modifiers | to author |  | passives lint |
| spec_ice_formation | spec_ice_formation | notable |  | modifiers | to author |  | passives lint |
| spec_blood_pulse | spec_blood_pulse | notable |  | modifiers | to author |  | passives lint |
| spec_blood_veteran | spec_blood_veteran | notable |  | modifiers | to author |  | passives lint |
| spec_blood_formation | spec_blood_formation | notable |  | modifiers | to author |  | passives lint |
| spec_close_pressure | spec_close_pressure | notable |  | modifiers | to author |  | passives lint |
| spec_close_breadth | spec_close_breadth | notable |  | modifiers | to author |  | passives lint |
| spec_close_focus | spec_close_focus | keystone |  | modifiers | to author |  | passives lint |
| spec_middle_pressure | spec_middle_pressure | notable |  | modifiers | to author |  | passives lint |
| spec_middle_breadth | spec_middle_breadth | notable |  | modifiers | to author |  | passives lint |
| spec_middle_focus | spec_middle_focus | keystone |  | modifiers | to author |  | passives lint |
| spec_far_pressure | spec_far_pressure | notable |  | modifiers | to author |  | passives lint |
| spec_far_breadth | spec_far_breadth | notable |  | modifiers | to author |  | passives lint |
| spec_far_focus | spec_far_focus | keystone |  | modifiers | to author |  | passives lint |
| spec_bulwark_broken | spec_bulwark_broken | notable |  | modifiers | to author |  | passives lint |
| spec_bulwark_standing | spec_bulwark_standing | notable |  | modifiers | to author |  | passives lint |
| spec_bulwark_mending | spec_bulwark_mending | notable |  | modifiers | to author |  | passives lint |
| spec_veil_whole | spec_veil_whole | notable |  | modifiers | to author |  | passives lint |
| spec_veil_thread | spec_veil_thread | notable |  | modifiers | to author | drop:healTaken | passives lint |
| spec_veil_fragile | spec_veil_fragile | keystone |  | modifiers | to author | drop:wardDecay | passives lint |
| spec_rhythm_advance | spec_rhythm_advance | notable |  | proc, modifiers | to author |  | passives lint |
| spec_rhythm_return | spec_rhythm_return | notable |  | proc, modifiers | to author |  | passives lint |
| spec_rhythm_cadence | spec_rhythm_cadence | notable |  | modifiers | to author |  | passives lint |
| weave_wayfarer_empty_stride | weave_wayfarer_empty_stride | notable |  | proc, modifiers | to author |  | passives lint |
| weave_wayfarer_pilgrim_blow | weave_wayfarer_pilgrim_blow | notable |  | node | to author | drop:strideReach, modifier critMulti, modifier poiseOnHit | passives lint |
| weave_wayfarer_passing_shelter | weave_wayfarer_passing_shelter | notable |  | modifiers | to author | drop:insightEfficiency, modifier healTaken | passives lint |
| weave_wayfarer_running_reserves | weave_wayfarer_running_reserves | notable |  | modifiers | to author |  | passives lint |
| weave_rooted_still_mercy | weave_rooted_still_mercy | notable |  | proc, modifiers | to author |  | passives lint |
| weave_rooted_rooted_read | weave_rooted_rooted_read | notable |  | modifiers | to author | drop:insightInversion | passives lint |
| weave_rooted_three_beat_siege | weave_rooted_three_beat_siege | notable |  | modifiers | to author |  | passives lint |
| weave_rooted_rooted_channel | weave_rooted_rooted_channel | notable |  | modifiers | to author | drop:channelTurnRate, modifier channelThorns | passives lint |
| weave_skirmisher_airborne_shelter | weave_skirmisher_airborne_shelter | notable |  | proc, modifiers | to author |  | passives lint |
| weave_skirmisher_ambush_surgeon | weave_skirmisher_ambush_surgeon | notable |  | modifiers | to author |  | passives lint |
| weave_skirmisher_catch_the_cast | weave_skirmisher_catch_the_cast | notable |  | modifiers | to author |  | passives lint |
| weave_skirmisher_cowards_cut | weave_skirmisher_cowards_cut | notable |  | modifiers | to author |  | passives lint |
| weave_grappler_wall_fury | weave_grappler_wall_fury | notable |  | proc, modifiers | to author |  | passives lint |
| weave_grappler_loose_foundations | weave_grappler_loose_foundations | notable |  | modifiers | to author | drop:shoveAuthority | passives lint |
| weave_grappler_heavy_hands | weave_grappler_heavy_hands | notable |  | modifiers | to author | drop:gripPower, modifier weight | passives lint |
| weave_grappler_borrowed_projectile | weave_grappler_borrowed_projectile | notable |  | modifiers | to author | drop:impactDamage | passives lint |
| weave_marksman_rain_collector | weave_marksman_rain_collector | notable |  | proc, modifiers | to author |  | passives lint |
| weave_marksman_bank_shot | weave_marksman_bank_shot | notable |  | modifiers | to author |  | passives lint |
| weave_marksman_slow_hunter | weave_marksman_slow_hunter | notable |  | modifiers | to author | drop:projectileSize | passives lint |
| weave_marksman_thread_the_crowd | weave_marksman_thread_the_crowd | notable |  | modifiers | to author |  | passives lint |
| weave_munitions_rack_memory | weave_munitions_rack_memory | notable |  | proc, modifiers | to author |  | passives lint |
| weave_munitions_last_word | weave_munitions_last_word | notable |  | modifiers | to author | drop:finalRoundDamage | passives lint |
| weave_munitions_surplus_round | weave_munitions_surplus_round | notable |  | modifiers | to author |  | passives lint |
| weave_munitions_rack_under_fire | weave_munitions_rack_under_* | notable |  | modifiers | to author |  | passives lint |
| weave_alchemist_red_glass | weave_alchemist_red_glass | notable |  | proc, modifiers | to author |  | passives lint |
| weave_alchemist_emergency_sip | weave_alchemist_emergency_sip | notable |  | modifiers | to author | drop:restorePower | passives lint |
| weave_alchemist_prepared_draught | weave_alchemist_prepared_draught | notable |  | node | to author | drop:pourPrime, modifier restorePower | passives lint |
| weave_alchemist_surge_bargain | weave_alchemist_surge_bargain | notable |  | node | to author | drop:pourPower_surge, modifier pourPower_settle | passives lint |
| weave_affliction_blood_glass | weave_affliction_blood_glass | notable |  | proc, modifiers | to author |  | passives lint |
| weave_affliction_precise_infection | weave_affliction_precise_infection | notable |  | modifiers | to author |  | passives lint |
| weave_affliction_carrier_blow | weave_affliction_carrier_blow | notable |  | node | to author | drop:hitToAffliction, modifier afflictionYield | passives lint |
| weave_affliction_half_burn | weave_affliction_half_burn | notable |  | modifiers | to author | drop:igniteToBomb | passives lint |
| weave_martyr_thermal_memory | weave_martyr_thermal_memory | notable |  | proc, modifiers | to author |  | passives lint |
| weave_martyr_scar_tissue | weave_martyr_scar_tissue | notable |  | modifiers | to author | drop:healTaken | passives lint |
| weave_martyr_slow_repair | weave_martyr_slow_repair | notable |  | node | to author | drop:recuperate, modifier recuperateTime | passives lint |
| weave_martyr_empty_shell | weave_martyr_empty_shell | notable |  | modifiers | to author |  | passives lint |
| weave_shieldweaver_unbroken_return | weave_shieldweaver_unbroken_return | notable |  | proc, modifiers | to author |  | passives lint |
| weave_shieldweaver_glass_footing | weave_shieldweaver_glass_footing | notable |  | node | to author | drop:esToPoise, modifier esForgone | passives lint |
| weave_shieldweaver_full_lattice | weave_shieldweaver_full_lattice | notable |  | modifiers | to author |  | passives lint |
| weave_shieldweaver_ward_distiller | weave_shieldweaver_ward_distiller | notable |  | modifiers | blocked | seam: conduits (row H7, open) | passives lint |
| weave_guard_medic_block_reserve | weave_guard_medic_block_reserve | notable |  | proc, modifiers | to author | drop:endurance | passives lint |
| weave_guard_medic_shelter_pulse | weave_guard_medic_shelter_pulse | notable |  | modifiers | to author | drop:guardMend | passives lint |
| weave_guard_medic_mercy_after_war | weave_guard_medic_mercy_after_war | notable |  | modifiers | to author | drop:absorbPower | passives lint |
| weave_guard_medic_overflowing_care | weave_guard_medic_overflowing_care | notable |  | modifiers | to author | drop:overheal | passives lint |
| weave_chorus_warcry_verse | weave_chorus_warcry_verse | notable |  | proc, modifiers | to author |  | passives lint |
| weave_chorus_held_authority | weave_chorus_held_authority | notable |  | modifiers | to author | drop:reservedDamage | passives lint |
| weave_chorus_third_voice | weave_chorus_third_voice | notable |  | modifiers | to author |  | passives lint |
| weave_chorus_quiet_dominion | weave_chorus_quiet_dominion | notable |  | modifiers | blocked | seam: conduits (row H7, open) | passives lint |
| weave_undertaker_funeral_clock | weave_undertaker_funeral_clock | notable |  | proc, modifiers | to author |  | passives lint |
| weave_undertaker_grave_procession | weave_undertaker_grave_procession | notable |  | modifiers | to author | drop:corpseBatch | passives lint |
| weave_undertaker_wakeflame_scribe | weave_undertaker_wakeflame_scribe | notable |  | modifiers | to author |  | passives lint |
| weave_undertaker_mourning_pace | weave_undertaker_mourning_pace | notable |  | modifiers | to author |  | passives lint |
| weave_commander_delegated_breath | weave_commander_delegated_breath | notable |  | proc, modifiers | to author |  | passives lint |
| weave_commander_close_formation | weave_commander_close_formation | notable |  | modifiers | to author | drop:minionGuard | passives lint |
| weave_commander_companions_courage | weave_commander_companions_courage | notable |  | node | to author | drop:endurance, modifier endurance | passives lint |
| weave_commander_keeper_echo | weave_commander_keeper_echo | notable |  | modifiers | to author |  | passives lint |
| weave_mechanist_trap_hammer | weave_mechanist_trap_hammer | notable |  | proc, modifiers | to author |  | passives lint |
| weave_mechanist_taut_geometry | weave_mechanist_taut_geometry | notable |  | modifiers | to author |  | passives lint |
| weave_mechanist_planted_workshop | weave_mechanist_planted_workshop | notable |  | modifiers | to author |  | passives lint |
| weave_mechanist_siege_saboteur | weave_mechanist_siege_saboteur | notable |  | modifiers | to author | drop:limbreaver | passives lint |
| weave_spellweaver_spell_hunger | weave_spellweaver_spell_hunger | notable |  | proc, modifiers | to author |  | passives lint |
| weave_spellweaver_mana_in_the_blade | weave_spellweaver_mana_in_the_blade | notable |  | modifiers | to author |  | passives lint |
| weave_spellweaver_three_arts | weave_spellweaver_three_arts | notable |  | modifiers | to author |  | passives lint |
| weave_spellweaver_borrowed_faces | weave_spellweaver_borrowed_faces | notable |  | node | to author | drop:mimicBank, modifier possessDuration | passives lint |
| weave_fortune_missed_jackpot | weave_fortune_missed_jackpot | notable |  | proc, modifiers | to author |  | passives lint |
| weave_fortune_wide_dice | weave_fortune_wide_dice | notable |  | node | to author | drop:damageSpread, modifier luckyChance | passives lint |
| weave_fortune_one_more_echo | weave_fortune_one_more_echo | notable |  | modifiers | to author | drop:procDepth | passives lint |
| weave_fortune_fury_dividend | weave_fortune_fury_dividend | notable |  | modifiers | to author |  | passives lint |
| weave_explorer_break_for_breath | weave_explorer_break_for_breath | notable |  | proc, modifiers | to author |  | passives lint |
| weave_explorer_lantern_pilgrim | weave_explorer_lantern_pilgrim | notable |  | modifiers | to author | drop:survivalEase_light, modifier survivalEase_soul | passives lint |
| weave_explorer_trample_weeds | weave_explorer_trample_weeds | notable |  | node | to author | drop:trample, modifier plyRend | passives lint |
| weave_explorer_rainbound | weave_explorer_rainbound | notable |  | modifiers | to author |  | passives lint |
| route_str_accent_breach_spell | route_str_accent_breach_spell | notable |  | proc, modifiers | to author |  | passives lint |
| route_str_accent_collision_mana | route_str_accent_collision_mana | notable |  | proc, modifiers | to author |  | passives lint |
| route_str_accent_weight_guard | route_str_accent_weight_guard | notable |  | node | to author | drop:guardStrength | passives lint |
| route_str_school_rebuttal | route_str_school_rebuttal | notable |  | proc, modifiers | to author |  | passives lint |
| route_str_school_graft_crushing_impact | route_str_school_graft_crushing_impact | notable |  | proc, modifiers | to author |  | passives lint |
| route_str_school_graft_battering_ram | route_str_school_graft_battering_ram | notable |  | proc, modifiers | to author |  | passives lint |
| route_str_school_graft_iron_grip | route_str_school_graft_iron_grip | notable |  | modifiers | to author | drop:weight | passives lint |
| route_str_school_graft_trebuchet_arm | route_str_school_graft_trebuchet_arm | notable |  | modifiers | to author | drop:addedPhysical | passives lint |
| route_str_school_graft_wringing_grip | route_str_school_graft_wringing_grip | notable |  | modifiers | to author |  | passives lint |
| route_str_school_graft_siegebreaker | route_str_school_graft_siegebreaker | notable |  | modifiers | to author |  | passives lint |
| route_prw_school_persistence | route_prw_school_persistence | notable |  | proc, modifiers | to author |  | passives lint |
| route_prw_accent_evade_clock | route_prw_accent_evade_clock | notable |  | proc, modifiers | to author |  | passives lint |
| route_prw_accent_varied_ward | route_prw_accent_varied_ward | notable |  | proc, modifiers | to author |  | passives lint |
| route_prw_school_graft_serrated_edge | route_prw_school_graft_serrated_edge | notable |  | proc, modifiers | to author |  | passives lint |
| route_prw_school_graft_answering_steel | route_prw_school_graft_answering_steel | notable |  | proc, modifiers | to author |  | passives lint |
| route_prw_school_graft_cast_on_crit | route_prw_school_graft_cast_on_crit | notable |  | modifiers | to author |  | passives lint |
| route_prw_school_graft_cast_on_kill | route_prw_school_graft_cast_on_kill | notable |  | modifiers | to author |  | passives lint |
| route_prw_school_graft_culmination | route_prw_school_graft_culmination | notable |  | modifiers | to author |  | passives lint |
| route_prw_school_graft_gathered_casting | route_prw_school_graft_gathered_casting | notable |  | modifiers | to author |  | passives lint |
| route_int_technique_conduit | route_int_technique_conduit | notable |  | modifiers | blocked | seam: conduits (row H7, open) | passives lint |
| route_int_school_reserve | route_int_school_reserve | notable |  | proc, modifiers | to author |  | passives lint |
| route_int_school_battery | route_int_school_battery | notable |  | modifiers | to author |  | passives lint |
| route_int_accent_filled_spell | route_int_accent_filled_spell | notable |  | proc, modifiers | to author |  | passives lint |
| route_int_accent_empty_footing | route_int_accent_empty_footing | notable |  | proc, modifiers | to author |  | passives lint |
| route_int_school_hot_streak | route_int_school_hot_streak | notable |  | proc, modifiers | to author |  | passives lint |
| route_int_accent_mana_heal | route_int_accent_mana_heal | notable |  | modifiers | to author |  | passives lint |
| route_int_school_graft_cast_while_channeling | route_int_school_graft_cast_while_channeling | notable |  | proc, modifiers | to author |  | passives lint |
| route_int_school_graft_cast_on_overcharge | route_int_school_graft_cast_on_overcharge | notable |  | proc, modifiers | to author |  | passives lint |
| route_int_school_graft_sequenced_invocation | route_int_school_graft_sequenced_invocation | notable |  | modifiers | to author | drop:channelTurnRate | passives lint |
| route_int_school_graft_refraction | route_int_school_graft_refraction | notable |  | modifiers | to author |  | passives lint |
| route_int_school_graft_unstable_compression | route_int_school_graft_unstable_compression | notable |  | modifiers | to author |  | passives lint |
| route_int_school_graft_entropic_bloom | route_int_school_graft_entropic_bloom | notable |  | node | to author | drop:channelThorns, modifier healTaken | passives lint |
| route_wis_technique_funeral | route_wis_technique_funeral | notable |  | proc, modifiers | to author |  | passives lint |
| route_wis_accent_funeral_clock | route_wis_accent_funeral_clock | notable |  | proc, modifiers | to author |  | passives lint |
| route_wis_accent_summon_ward | route_wis_accent_summon_ward | notable |  | proc, modifiers | to author |  | passives lint |
| route_wis_school_graft_brood_tender | route_wis_school_graft_brood_tender | notable |  | proc, modifiers | to author |  | passives lint |
| route_wis_school_graft_ghostly_communion | route_wis_school_graft_ghostly_communion | notable |  | proc, modifiers | to author |  | passives lint |
| route_wis_school_graft_gift_of_the_choir | route_wis_school_graft_gift_of_the_choir | notable |  | modifiers | to author |  | passives lint |
| route_wis_school_graft_legion_doctrine | route_wis_school_graft_legion_doctrine | notable |  | modifiers | to author |  | passives lint |
| route_wis_school_graft_hiveborn | route_wis_school_graft_hiveborn | notable |  | modifiers | to author |  | passives lint |
| route_wis_school_graft_parasitic_pact | route_wis_school_graft_parasitic_pact | notable |  | modifiers | to author |  | passives lint |
| route_fin_accent_back_mana | route_fin_accent_back_mana | notable |  | proc, modifiers | to author |  | passives lint |
| route_fin_accent_move_needle | route_fin_accent_move_needle | notable |  | proc, modifiers | to author |  | passives lint |
| route_fin_school_graft_envenomed_tips | route_fin_school_graft_envenomed_tips | notable |  | proc, modifiers | to author |  | passives lint |
| route_fin_school_graft_fowlers_eye | route_fin_school_graft_fowlers_eye | notable |  | proc, modifiers | to author |  | passives lint |
| route_fin_school_graft_quailbane | route_fin_school_graft_quailbane | notable |  | modifiers | to author |  | passives lint |
| route_fin_school_graft_overmatch | route_fin_school_graft_overmatch | notable |  | modifiers | to author | drop:healTaken | passives lint |
| route_fin_school_graft_regicide | route_fin_school_graft_regicide | notable |  | modifiers | to author |  | passives lint |
| route_fin_school_graft_limbreaver | route_fin_school_graft_limbreaver | notable |  | modifiers | to author |  | passives lint |
| route_dex_accent_device_clock | route_dex_accent_device_clock | notable |  | proc, modifiers | to author |  | passives lint |
| route_dex_accent_evade_device | route_dex_accent_evade_device | notable |  | proc, modifiers | to author |  | passives lint |
| route_dex_school_graft_packed_workshop | route_dex_school_graft_packed_workshop | notable |  | proc, modifiers | to author |  | passives lint |
| route_dex_school_graft_overwound_mechanism | route_dex_school_graft_overwound_mechanism | notable |  | proc, modifiers | to author |  | passives lint |
| route_dex_school_graft_hair_trigger | route_dex_school_graft_hair_trigger | notable |  | modifiers | to author |  | passives lint |
| route_dex_school_graft_tinkers_arsenal | route_dex_school_graft_tinkers_arsenal | notable |  | modifiers | to author |  | passives lint |
| route_dex_school_graft_barbed_snare | route_dex_school_graft_barbed_snare | notable |  | modifiers | to author |  | passives lint |
| route_dex_school_graft_parting_gift | route_dex_school_graft_parting_gift | notable |  | modifiers | to author |  | passives lint |
| route_for_technique_thorns | route_for_technique_thorns | notable |  | modifiers | to author |  | passives lint |
| route_for_accent_block_clock | route_for_accent_block_clock | notable |  | proc, modifiers | to author |  | passives lint |
| route_for_accent_rearmed_ward | route_for_accent_rearmed_ward | notable |  | proc, modifiers | to author |  | passives lint |
| route_for_school_wall | route_for_school_wall | notable |  | modifiers | blocked | seam: conduits (row H7, open) | passives lint |
| route_for_school_graft_unyielding_stance | route_for_school_graft_unyielding_stance | notable |  | proc, modifiers | to author |  | passives lint |
| route_for_school_graft_bulwark_of_thorns | route_for_school_graft_bulwark_of_thorns | notable |  | proc, modifiers | to author |  | passives lint |
| route_for_school_graft_counterweight | route_for_school_graft_counterweight | notable |  | modifiers | to author | drop:guardAegis | passives lint |
| route_for_school_graft_shieldwall_doctrine | route_for_school_graft_shieldwall_doctrine | notable |  | modifiers | to author |  | passives lint |
| route_for_school_graft_answering_wall | route_for_school_graft_answering_wall | notable |  | modifiers | to author |  | passives lint |
| route_for_school_graft_stalwart_rhythm | route_for_school_graft_stalwart_rhythm | notable |  | node | to author | drop:endurance, modifier enduranceRegenPct | passives lint |
| route_wil_accent_held_clock | route_wil_accent_held_clock | notable |  | proc, modifiers | to author |  | passives lint |
| route_wil_accent_shield_poise | route_wil_accent_shield_poise | notable |  | proc, modifiers | to author |  | passives lint |
| route_wil_school_fury | route_wil_school_fury | notable |  | proc, modifiers | to author |  | passives lint |
| route_wil_school_graft_lingering_moment | route_wil_school_graft_lingering_moment | notable |  | proc, modifiers | to author |  | passives lint |
| route_wil_school_graft_borrowed_haste | route_wil_school_graft_borrowed_haste | notable |  | proc, modifiers | to author |  | passives lint |
| route_wil_school_graft_smothering_spores | route_wil_school_graft_smothering_spores | notable |  | modifiers | to author |  | passives lint |
| route_wil_school_graft_loose_thread | route_wil_school_graft_loose_thread | notable |  | modifiers | to author |  | passives lint |
| route_wil_school_graft_putrefaction | route_wil_school_graft_putrefaction | notable |  | modifiers | to author |  | passives lint |
| route_wil_school_graft_epidemic | route_wil_school_graft_epidemic | notable |  | modifiers | to author |  | passives lint |
| route_cha_technique_ward | route_cha_technique_ward | notable |  | proc, modifiers | to author |  | passives lint |
| route_cha_technique_conduit | route_cha_technique_conduit | notable |  | modifiers | blocked | seam: conduits (row H7, open) | passives lint |
| route_cha_accent_heal_clock | route_cha_accent_heal_clock | notable |  | proc, modifiers | to author |  | passives lint |
| route_cha_accent_block_mana | route_cha_accent_block_mana | notable |  | proc, modifiers | to author |  | passives lint |
| route_cha_school_graft_held_note | route_cha_school_graft_held_note | notable |  | proc, modifiers | to author |  | passives lint |
| route_cha_school_graft_countermelody | route_cha_school_graft_countermelody | notable |  | proc, modifiers | to author |  | passives lint |
| route_cha_school_graft_rising_chorus | route_cha_school_graft_rising_chorus | notable |  | modifiers | to author |  | passives lint |
| route_cha_school_graft_commanding_presence | route_cha_school_graft_commanding_presence | notable |  | modifiers | to author | drop:wardDecay | passives lint |
| route_cha_school_graft_mending_echoes | route_cha_school_graft_mending_echoes | notable |  | modifiers | to author |  | passives lint |
| route_cha_school_graft_sanguine_feast | route_cha_school_graft_sanguine_feast | notable |  | modifiers | to author |  | passives lint |
| cross_str_mastery | cross_str_mastery | choice |  | choice | to author |  | passives lint |
| cross_prw_mastery | cross_prw_mastery | choice |  | choice | to author |  | passives lint |
| cross_int_mastery | cross_int_mastery | choice |  | choice | to author |  | passives lint |
| cross_wis_mastery | cross_wis_mastery | choice |  | choice | to author |  | passives lint |
| cross_fin_mastery | cross_fin_mastery | choice |  | choice | to author |  | passives lint |
| cross_dex_mastery | cross_dex_mastery | choice |  | choice | to author |  | passives lint |
| cross_for_mastery | cross_for_mastery | choice |  | choice | to author |  | passives lint |
| cross_wil_mastery | cross_wil_mastery | choice |  | choice | to author |  | passives lint |
| cross_cha_mastery | cross_cha_mastery | choice |  | choice | to author |  | passives lint |
| war_n1 | war_n1 | notable |  | modifiers | to author |  | passives lint |
| war_n2 | war_n2 | notable |  | modifiers | to author |  | passives lint |
| war_key | war_key | keystone |  | modifiers | to author |  | passives lint |
| brz_n1 | brz_n1 | notable |  | modifiers | to author |  | passives lint |
| brz_n2 | brz_n2 | notable |  | modifiers | to author |  | passives lint |
| brz_key | brz_key | keystone |  | modifiers | to author |  | passives lint |
| sor_n1 | sor_n1 | notable |  | modifiers | to author |  | passives lint |
| sor_n2 | sor_n2 | notable |  | modifiers | to author |  | passives lint |
| sor_key | sor_key | keystone |  | modifiers | to author |  | passives lint |
| sum_n1 | sum_n1 | notable |  | modifiers | to author |  | passives lint |
| sum_n2 | sum_n2 | notable |  | modifiers | to author |  | passives lint |
| sum_key | sum_key | keystone |  | modifiers | to author |  | passives lint |
| swb_n1 | swb_n1 | notable |  | modifiers | to author |  | passives lint |
| swb_n2 | swb_n2 | notable |  | modifiers | to author |  | passives lint |
| swb_key | swb_key | keystone |  | modifiers | to author |  | passives lint |
| ironturn | ironturn | keystone |  | node | to author | drop:evasionForgone, modifier evasionToArmor | passives lint |
| rng_n1 | rng_n1 | notable |  | modifiers | to author |  | passives lint |
| rng_n2 | rng_n2 | notable |  | modifiers | to author |  | passives lint |
| rng_key | rng_key | keystone |  | modifiers | to author |  | passives lint |
| sum_x1 | sum_x1 | keystone |  | node | to author | drop:minionExplodeDeath | passives lint |
| sum_x2 | sum_x2 | keystone |  | modifiers | to author | drop:minionSize | passives lint |
| sor_x1 | sor_x1 | notable |  | proc, modifiers | to author |  | passives lint |
| cl_gam_shrapnel | cl_gam_shrapnel | notable |  | modifiers | to author | drop:procRider_static_shrapnel | passives lint |
| cl_gam_short | cl_gam_short | notable |  | proc, modifiers | to author |  | passives lint |
| cl_gam_key | cl_gam_key | keystone |  | node | to author | drop:damageSpread, modifier luckyChance, modifier highRollWindow | passives lint |
| sor_es2 | sor_es2 | notable |  | modifiers | to author | drop:manaShield | passives lint |
| sor_es3 | sor_es3 | notable |  | modifiers | to author | drop:esToMana | passives lint |
| sum_es1 | sum_es1 | keystone |  | modifiers | to author | drop:lifeRegenToEs | passives lint |
| cl_phys_c | cl_phys_c | notable |  | modifiers | to author |  | passives lint |
| cl_armor_c | cl_armor_c | notable |  | modifiers | to author |  | passives lint |
| cl_block_c | cl_block_c | notable |  | modifiers | to author |  | passives lint |
| cl_spell_c | cl_spell_c | notable |  | modifiers | to author |  | passives lint |
| cl_fireres_c | cl_fireres_c | notable |  | modifiers | to author |  | passives lint |
| cl_minion_c | cl_minion_c | notable |  | modifiers | to author |  | passives lint |
| cl_sum_wagon | cl_sum_wagon | notable |  | modifiers | to author | drop:corpseBatch | passives lint |
| cl_coldres_c | cl_coldres_c | notable |  | modifiers | to author |  | passives lint |
| cl_lightres_c | cl_lightres_c | notable |  | modifiers | to author |  | passives lint |
| cl_acc_c | cl_acc_c | notable |  | modifiers | to author |  | passives lint |
| cl_melee_c | cl_melee_c | notable |  | modifiers | to author |  | passives lint |
| cl_lreg_c | cl_lreg_c | notable |  | modifiers | to author |  | passives lint |
| cl_es_c | cl_es_c | notable |  | modifiers | to author |  | passives lint |
| cl_mreg_c | cl_mreg_c | notable |  | modifiers | to author |  | passives lint |
| cl_allres_c | cl_allres_c | notable |  | modifiers | to author |  | passives lint |
| cl_chaosres_c | cl_chaosres_c | notable |  | modifiers | to author |  | passives lint |
| cl_eva_c | cl_eva_c | notable |  | modifiers | to author |  | passives lint |
| cl_proj_c | cl_proj_c | notable |  | modifiers | to author |  | passives lint |
| cl_attr_c | cl_attr_c | notable |  | modifiers | to author |  | passives lint |
| rng_pc2 | rng_pc2 | notable |  | modifiers | to author | drop:projectileCountChance | passives lint |
| war_kb2 | war_kb2 | notable |  | modifiers | to author |  | passives lint |
| mass_n1 | mass_n1 | notable |  | node | to author | drop:weight, modifier weight, modifier shoveAuthority, modifier impactDamage | passives lint |
| grip_n1 | grip_n1 | notable |  | node | to author | drop:gripPower, modifier combo_grapplers_rhythm | passives lint |
| mass_n2 | mass_n2 | notable |  | node | to author | drop:trample, modifier impactDamage | passives lint |
| sor_df2 | sor_df2 | notable |  | modifiers | to author |  | passives lint |
| sor_df3 | sor_df3 | notable |  | modifiers | to author |  | passives lint |
| brz_pd1 | brz_pd1 | notable |  | proc, modifiers | to author |  | passives lint |
| brz_pd2 | brz_pd2 | notable |  | proc, modifiers | to author |  | passives lint |
| node_12 | node_12 | notable |  | modifiers | to author |  | passives lint |
| node_37 | node_37 | notable |  | modifiers | to author |  | passives lint |
| node_54 | node_54 | notable |  | modifiers | to author |  | passives lint |
| cha_key_root | cha_key_root | keystone |  | node | to author | drop:insightInversion | passives lint |
| settling_stone | settling_stone | notable |  | modifiers | to author | drop:insightStillTaper | passives lint |
| wil_x1 | wil_x1 | keystone |  | modifiers | to author | drop:esDotResist | passives lint |
| sanguine_lattice | sanguine_lattice | notable |  | modifiers | to author |  | passives lint |
| vicious_cycle | vicious_cycle | notable |  | modifiers | to author |  | passives lint |
| fevered_blood | fevered_blood | notable |  | modifiers | to author |  | passives lint |
| riposte_doctrine | riposte_doctrine | notable |  | proc, modifiers | to author |  | passives lint |
| break_the_line | break_the_line | notable |  | proc, modifiers | to author |  | passives lint |
| eva_pr1 | eva_pr1 | notable |  | proc, modifiers | to author | drop:lifeOnEvade | passives lint |
| stored_lightning | stored_* | notable |  | proc, modifiers | to author |  | passives lint |
| forged_rhythm | forged_rhythm | notable |  | modifiers | to author | drop:poiseRearmAt | passives lint |
| rising_crest | rising_crest | notable |  | modifiers | to author | drop:poiseOvercharge | passives lint |
| bonewright_oath | bonewright_oath | keystone |  | node | to author | drop:esForgone, modifier esToPoise | passives lint |
| shatterplate_doctrine | shatterplate_doctrine | notable |  | proc, modifiers | to author |  | passives lint |
| broken_fury | broken_fury | notable |  | proc, modifiers | to author |  | passives lint |
| twice_quenched | twice_quenched | notable |  | proc, modifiers | to author |  | passives lint |
| battle_cadence | battle_cadence | notable |  | modifiers | to author | drop:combo_drumbeat | passives lint |
| prismatic_cycle | prismatic_cycle | notable |  | modifiers | to author | drop:combo_elemental_round | passives lint |
| unbroken_stream | unbroken_stream | notable |  | modifiers | to author | drop:esRechargeSteadfast | passives lint |
| tidal_mind | tidal_mind | keystone |  | modifiers | to author | drop:esRechargeSteadfast | passives lint |
| venom_focus | venom_focus | notable |  | modifiers | to author |  | passives lint |
| furious_momentum | furious_momentum | notable |  | modifiers | to author |  | passives lint |
| iron_sinews | iron_sinews | notable |  | modifiers | to author |  | passives lint |
| blood_arcana | blood_arcana | notable |  | modifiers | to author |  | passives lint |
| enduring_bulwark | enduring_bulwark | notable |  | proc, modifiers | to author | drop:endurance | passives lint |
| radiant_reprisal_n | radiant_reprisal_n | notable |  | proc, modifiers | to author |  | passives lint |
| radiant_cascade_n | radiant_cascade_n | notable |  | proc, modifiers | to author |  | passives lint |
| executioners_rhythm_n | executioners_rhythm_n | notable |  | proc, modifiers | to author |  | passives lint |
| reapers_due | reapers_due | keystone |  | modifiers | to author |  | passives lint |
| cl_fount_n | cl_fount_n | notable |  | node | to author | drop:restorePctMax, modifier restorePower | passives lint |
| cl_wake_cortege | cl_wake_cortege | notable |  | modifiers | to author |  | passives lint |
| cl_wake_tallow | cl_wake_tallow | notable |  | modifiers | to author |  | passives lint |
| cl_wake_bright | cl_wake_bright | notable |  | modifiers | to author | drop:orbRefund_wakeflame | passives lint |
| cl_wake_hours | cl_wake_hours | keystone |  | modifiers | to author |  | passives lint |
| chain_reaction | chain_reaction | keystone |  | node | to author | drop:procDepth | passives lint |
| gamblers_touch | gamblers_touch | notable |  | node | to author | drop:luck | passives lint |
| kindled_rage_n | kindled_rage_n | notable |  | proc, modifiers | to author |  | passives lint |
| crimson_thirst_n | crimson_thirst_n | notable |  | proc, modifiers | to author |  | passives lint |
| surging_frenzy_n | surging_frenzy_n | notable |  | proc, modifiers | to author |  | passives lint |
| battle_chorus_n | battle_chorus_n | notable |  | proc, modifiers | to author |  | passives lint |
| perpetual_motion | perpetual_motion | keystone |  | node | to author | drop:procDepth | passives lint |
| transfusion | transfusion | notable |  | node | to author | drop:vampiricShare | passives lint |
| fellowship_of_the_wake | fellowship_of_the_wake | notable |  | node | to author | drop:sympathy_menders_ripple | passives lint |
| oathbound_insight | oathbound_insight | notable |  | proc, modifiers | to author |  | passives lint |
| shepherds_rites | shepherds_rites | notable |  | node | to author | drop:minionDeathHeal, modifier summonMend | passives lint |
| silencing_brand | silencing_brand | notable |  | modifiers | to author |  | passives lint |
| stalwart_recovery | stalwart_recovery | notable |  | node | to author | drop:recuperate | passives lint |
| exsanguinate | exsanguinate | notable |  | modifiers | to author |  | passives lint |
| malignant_precision | malignant_precision | notable |  | modifiers | to author |  | passives lint |
| kindling_doctrine | kindling_doctrine | notable |  | modifiers | to author |  | passives lint |
| cho_calling | cho_calling | choice |  | choice | to author |  | passives lint |
| cho_temper | cho_temper | choice |  | choice | to author |  | passives lint |
| cho_bulwark1 | cho_bulwark1 | choice |  | choice | to author |  | passives lint |
| cho_bulwark2 | cho_bulwark2 | choice |  | choice | to author |  | passives lint |
| cho_war1 | cho_war1 | choice |  | choice | to author |  | passives lint |
| cho_war2 | cho_war2 | choice |  | choice | to author |  | passives lint |
| cho_arcane1 | cho_arcane1 | choice |  | choice | to author |  | passives lint |
| cho_arcane2 | cho_arcane2 | choice |  | choice | to author |  | passives lint |
| cho_wake_litany | cho_wake_litany | choice |  | choice | to author |  | passives lint |
| cho_wake_paean | cho_wake_paean | choice |  | choice | to author |  | passives lint |
| dev_hunt_n1 | dev_hunt_n1 | notable |  | modifiers | to author |  | passives lint |
| dev_hunt_cho | dev_hunt_cho | choice |  | choice | to author |  | passives lint |
| pan_major | pan_major | choice |  | choice | to author |  | passives lint |
| pan_minor | pan_minor | choice |  | choice | to author |  | passives lint |
| cl_crag_avalanche | cl_crag_avalanche | notable |  | node | to author | drop:trample, modifier impactDamage | passives lint |
| cl_crag_landslide | cl_crag_landslide | keystone |  | modifiers | to author | drop:weight, modifier weight, modifier impactDamage | passives lint |
| cl_crag_cavein | cl_crag_cavein | notable |  | proc, modifiers | to author |  | passives lint |
| cl_crag_wall | cl_crag_wall | notable |  | modifiers | to author |  | passives lint |
| cl_crag_bell | cl_crag_bell | keystone |  | modifiers | to author | drop:evasionForgone | passives lint |
| cl_crag_siege | cl_crag_siege | notable |  | modifiers | to author |  | passives lint |
| cl_crag_mountain | cl_crag_mountain | keystone |  | modifiers | to author |  | passives lint |
| cl_crag_bowl | cl_crag_bowl | notable |  | modifiers | to author | drop:impactDamage | passives lint |
| cho_post_crag | cho_post_crag | choice |  | choice | to author |  | passives lint |
| cl_cad_tempo | cl_cad_tempo | notable |  | proc, modifiers | to author |  | passives lint |
| cl_cad_crimson | cl_cad_crimson | notable |  | proc, modifiers | to author |  | passives lint |
| cl_cad_rage_g | cl_cad_rage_g | notable |  | modifiers | to author |  | passives lint |
| cl_cad_fury2 | cl_cad_fury2 | notable |  | proc, modifiers | to author |  | passives lint |
| cl_cad_fury_g | cl_cad_fury_g | keystone |  | modifiers | to author |  | passives lint |
| cl_cad_blood | cl_cad_blood | notable |  | proc, modifiers | to author |  | passives lint |
| cl_cad_adrenal | cl_cad_adrenal | notable |  | proc, modifiers | to author |  | passives lint |
| cl_cad_drum | cl_cad_drum | notable |  | node | to author | drop:combo_twin_measures | passives lint |
| cl_cad_exec | cl_cad_exec | notable |  | node | to author | drop:proximityDamage | passives lint |
| cl_cad_final | cl_cad_final | keystone |  | modifiers | to author |  | passives lint |
| cho_post_cadence | cho_post_cadence | choice |  | choice | to author |  | passives lint |
| cl_court_insightful | cl_court_insightful | notable |  | modifiers | to author | drop:insightRegenPct | passives lint |
| cl_court_pen | cl_court_pen | notable |  | node | to author | drop:insightPen | passives lint |
| cl_court_mimic | cl_court_mimic | notable |  | node | to author | drop:mimicWitness, modifier mimicBank | passives lint |
| cl_court_still | cl_court_still | keystone |  | modifiers | to author | drop:insightEfficiency | passives lint |
| cl_court_ease | cl_court_ease | notable |  | node | to author | drop:mercEase, modifier mercHireDiscount | passives lint |
| cl_court_captain | cl_court_captain | keystone |  | node | to author | drop:mercRetinue, modifier mercHireDiscount | passives lint |
| cl_court_mend | cl_court_mend | notable |  | modifiers | to author | drop:overheal | passives lint |
| cl_court_ripple | cl_court_ripple | notable |  | node | to author | drop:guardMend, modifier mercVigor | passives lint |
| cl_court_bounty | cl_court_bounty | keystone |  | modifiers | to author | drop:sympathy_provokers_bounty, modifier luck | passives lint |
| cho_oath_court | cho_oath_court | choice |  | choice | to author |  | passives lint |
| cl_court_presence | cl_court_presence | notable |  | modifiers | to author | drop:commandDiscipline, modifier wardDecay | passives lint |
| cl_mer_reload | cl_mer_reload | notable |  | modifiers | to author |  | passives lint |
| cl_mer_final | cl_mer_final | keystone |  | modifiers | to author | drop:finalRoundDamage | passives lint |
| cl_mer_ricochet | cl_mer_ricochet | notable |  | modifiers | to author |  | passives lint |
| cl_mer_return | cl_mer_return | keystone |  | modifiers | to author |  | passives lint |
| cl_mer_seeker | cl_mer_seeker | notable |  | modifiers | to author |  | passives lint |
| cl_mer_mark | cl_mer_mark | notable |  | modifiers | to author |  | passives lint |
| cl_mer_twin | cl_mer_twin | notable |  | node | to author | drop:multiTarget | passives lint |
| cl_mer_snipe | cl_mer_snipe | keystone |  | modifiers | to author |  | passives lint |
| cho_post_meridian | cho_post_meridian | choice |  | choice | to author |  | passives lint |
| cl_gal_ambush | cl_gal_ambush | notable |  | modifiers | to author |  | passives lint |
| cl_gal_ghost | cl_gal_ghost | keystone |  | modifiers | to author | drop:detectability | passives lint |
| cl_gal_fester | cl_gal_fester | notable |  | modifiers | to author |  | passives lint |
| cl_gal_septic | cl_gal_septic | keystone |  | node | to author | drop:hitToAffliction, modifier afflictionYield | passives lint |
| cl_gal_spread | cl_gal_spread | notable |  | node | to author | drop:dotPropagates | passives lint |
| cl_gal_flay | cl_gal_flay | notable |  | modifiers | to author |  | passives lint |
| cl_gal_vanish | cl_gal_vanish | keystone |  | modifiers | to author |  | passives lint |
| cho_oath_gallery | cho_oath_gallery | choice |  | choice | to author |  | passives lint |
| cl_gal_env | cl_gal_env | notable |  | proc, modifiers | to author |  | passives lint |
| cho_wild_gallery | cho_wild_gallery | choice |  | choice | to author |  | passives lint |
| cl_pri_pen | cl_pri_pen | notable |  | modifiers | to author |  | passives lint |
| cl_pri_round | cl_pri_round | notable |  | node | to author | drop:conduction | passives lint |
| cl_pri_suffuse | cl_pri_suffuse | notable |  | node | to author | drop:suffusion | passives lint |
| cl_pri_bomb | cl_pri_bomb | keystone |  | node | to author | drop:igniteToBomb | passives lint |
| cl_pri_page | cl_pri_page | keystone |  | modifiers | to author |  | passives lint |
| cl_pri_archmage | cl_pri_archmage | keystone |  | modifiers | to author | drop:maxManaDamage | passives lint |
| cl_pri_rote1 | cl_pri_rote1 | notable |  | modifiers | to author |  | passives lint |
| cl_pri_rote2 | cl_pri_rote2 | notable |  | modifiers | to author |  | passives lint |
| cl_pri_orbit2 | cl_pri_orbit2 | notable |  | modifiers | to author |  | passives lint |
| cho_triad_prism | cho_triad_prism | choice |  | choice | to author |  | passives lint |
| cl_sky_shear | cl_sky_shear | notable |  | modifiers | to author | drop:plyRend | passives lint |
| cl_sky_quanta | cl_sky_quanta | notable |  | node | to author | drop:minionPlies | passives lint |
| cl_sky_trade | cl_sky_trade | keystone |  | node | to author | drop:minionLifePlyTrade | passives lint |
| cl_sky_pocket | cl_sky_pocket | notable |  | node | to author | drop:throngPockets, modifier throngYield | passives lint |
| cl_sky_brood | cl_sky_brood | keystone |  | modifiers | to author | drop:minionSize | passives lint |
| cl_sky_tender | cl_sky_tender | notable |  | proc, modifiers | to author |  | passives lint |
| cl_sky_link | cl_sky_link | notable |  | node | to author | drop:minionShare | passives lint |
| cl_sky_offer | cl_sky_offer | notable |  | node | to author | drop:offeringShare | passives lint |
| cl_sky_husk | cl_sky_husk | keystone |  | node | to author | drop:possessDuration, modifier possessPower, modifier huskGuard | passives lint |
| cho_triad_sky | cho_triad_sky | choice |  | choice | to author |  | passives lint |
| cl_sky_undying | cl_sky_undying | notable |  | node | to author | drop:minionUndying | passives lint |
| cl_sky_carrion | cl_sky_carrion | notable |  | node | to author | drop:minionDeathHealFlat | passives lint |
| cho_wild_sky | cho_wild_sky | choice |  | choice | to author |  | passives lint |
| cl_sky_evergreen | cl_sky_evergreen | notable |  | node | to author | drop:minionRegenRate | passives lint |
| cl_cho_steady | cl_cho_steady | notable |  | modifiers | to author | drop:esRechargeSteadfast | passives lint |
| cl_cho_water | cl_cho_water | notable |  | proc, modifiers | to author |  | passives lint |
| cl_cho_crest | cl_cho_crest | notable |  | proc, modifiers | to author |  | passives lint |
| cl_cho_shred | cl_cho_shred | notable |  | node | to author | drop:esShred | passives lint |
| cl_cho_glimmer | cl_cho_glimmer | notable |  | node | to author | drop:combo_glimmer_chorus | passives lint |
| cl_cho_debt | cl_cho_debt | keystone |  | node | to author | drop:overdriveCap | passives lint |
| cl_cho_choir | cl_cho_choir | keystone |  | modifiers | to author |  | passives lint |
| cho_post_choir | cho_post_choir | choice |  | choice | to author |  | passives lint |
| cl_bed_endure2 | cl_bed_endure2 | notable |  | node | to author | drop:endurance, modifier enduranceRegenPct | passives lint |
| cl_bed_shed | cl_bed_shed | notable |  | proc, modifiers | to author |  | passives lint |
| cl_bed_stagger | cl_bed_stagger | keystone |  | node | to author | drop:staggerFrac | passives lint |
| cl_bed_value | cl_bed_value | notable |  | modifiers | to author |  | passives lint |
| cl_bed_stalwart | cl_bed_stalwart | notable |  | proc, modifiers | to author |  | passives lint |
| cl_bed_parry | cl_bed_parry | keystone |  | modifiers | to author | drop:guardParry | passives lint |
| cl_bed_reflect | cl_bed_reflect | notable |  | modifiers | to author |  | passives lint |
| cl_bed_spikes | cl_bed_spikes | keystone |  | modifiers | to author | drop:thornsToHit | passives lint |
| cho_oath_bedrock | cho_oath_bedrock | choice |  | choice | to author |  | passives lint |
| cho_triad_bedrock | cho_triad_bedrock | choice |  | choice | to author |  | passives lint |
| cl_thresh_harvest | cl_thresh_harvest | notable |  | node | to author | drop:impactDamage, modifier shoveAuthority | passives lint |
| cl_thresh_crush | cl_thresh_crush | notable |  | proc, modifiers | to author |  | passives lint |
| cl_thresh_wake | cl_thresh_wake | keystone |  | modifiers | to author | drop:trample | passives lint |
| cl_thresh_python | cl_thresh_python | notable |  | node | to author | drop:gripPower, modifier shoveAuthority | passives lint |
| cl_thresh_toss | cl_thresh_toss | keystone |  | modifiers | to author | drop:shoveAuthority, modifier impactDamage | passives lint |
| cl_thresh_ledger | cl_thresh_ledger | notable |  | node | to author | drop:recuperate | passives lint |
| cl_thresh_last | cl_thresh_last | notable |  | proc, modifiers | to author |  | passives lint |
| cl_watch_still | cl_watch_still | notable |  | modifiers | to author |  | passives lint |
| cl_watch_lucky | cl_watch_lucky | notable |  | node | to author | drop:luckyChance | passives lint |
| cl_watch_far | cl_watch_far | keystone |  | modifiers | to author |  | passives lint |
| cl_watch_owl | cl_watch_owl | notable |  | modifiers | to author |  | passives lint |
| cl_watch_kite | cl_watch_kite | keystone |  | modifiers | to author |  | passives lint |
| cl_twi_cull | cl_twi_cull | notable |  | modifiers | to author |  | passives lint |
| cl_twi_vamp | cl_twi_vamp | notable |  | node | to author | drop:vampiricShare | passives lint |
| cl_twi_bond | cl_twi_bond | keystone |  | node | to author | drop:bondShare, modifier healTaken | passives lint |
| cl_twi_grief | cl_twi_grief | notable |  | modifiers | to author |  | passives lint |
| cl_twi_line | cl_twi_line | keystone |  | modifiers | to author |  | passives lint |
| cl_twi_hunger | cl_twi_hunger | notable |  | modifiers | to author |  | passives lint |
| cl_eng_jinx | cl_eng_jinx | notable |  | modifiers | to author |  | passives lint |
| cl_eng_double | cl_eng_double | keystone |  | node | to author | drop:luckyChance, modifier unluckyChance | passives lint |
| cl_eng_rider | cl_eng_rider | notable |  | node | to author | drop:luck | passives lint |
| cl_eng_perp | cl_eng_perp | keystone |  | modifiers | to author | drop:procDepth, modifier luck | passives lint |
| cl_scr_burn | cl_scr_burn | notable |  | modifiers | to author |  | passives lint |
| cl_scr_doom | cl_scr_doom | notable |  | node | to author | drop:doomDot | passives lint |
| cl_scr_silence | cl_scr_silence | notable |  | modifiers | to author |  | passives lint |
| cl_scr_ash | cl_scr_ash | keystone |  | modifiers | to author | drop:wardDecay | passives lint |
| cho_post_scriptorium | cho_post_scriptorium | choice |  | choice | to author |  | passives lint |
| cl_chrono_n | cl_chrono_n | notable |  | modifiers | to author |  | passives lint |
| cl_chrono_k | cl_chrono_k | keystone |  | modifiers | to author |  | passives lint |
| cl_chrono_mob | cl_chrono_mob | notable |  | modifiers | to author |  | passives lint |
| cl_fount_r2 | cl_fount_r2 | notable |  | node | to author | drop:restorePctMax, modifier thirstless | passives lint |
| cl_fount_r3 | cl_fount_r3 | notable |  | modifiers | to author |  | passives lint |
| cl_vit_n | cl_vit_n | notable |  | modifiers | to author |  | passives lint |
| cl_chand_3 | cl_chand_3 | notable |  | modifiers | to author |  | passives lint |
| cl_chand_4 | cl_chand_4 | notable |  | node | to author | drop:orbRefund_wakeflame | passives lint |
| cl_gyre_n | cl_gyre_n | notable |  | modifiers | to author |  | passives lint |
| cl_std_n | cl_std_n | notable |  | node | to author | drop:durationAuraCap | passives lint |
| cl_iron_n | cl_iron_n | notable |  | modifiers | to author |  | passives lint |
| cl_tithe_n | cl_tithe_n | notable |  | node | to author | drop:orbShedRate, modifier remnantChance | passives lint |
| dev_bul_n1 | dev_bul_n1 | notable |  | modifiers | to author | drop:lifeOnBlock, modifier guardAegis | passives lint |
| dev_bul_n2 | dev_bul_n2 | notable |  | modifiers | to author | drop:endurance | passives lint |
| cl_verse_n | cl_verse_n | notable |  | proc, modifiers | to author |  | passives lint |
| cl_rip_n | cl_rip_n | notable |  | modifiers | to author |  | passives lint |
| cl_soul_n | cl_soul_n | notable |  | modifiers | to author |  | passives lint |
| cl_gyre_5 | cl_gyre_5 | notable |  | modifiers | to author |  | passives lint |
| cl_stat_n | cl_stat_n | notable |  | modifiers | to author |  | passives lint |
| cl_watch_n2 | cl_watch_n2 | notable |  | node | to author | drop:luckyChance | passives lint |
| cl_twi_n3 | cl_twi_n3 | notable |  | modifiers | to author |  | passives lint |
| cl_opp_exec | cl_opp_exec | notable |  | modifiers | to author |  | passives lint |
| cl_opp_open | cl_opp_open | notable |  | modifiers | to author |  | passives lint |
| cl_opp_rush | cl_opp_rush | notable |  | modifiers | to author |  | passives lint |
| cl_opp_steady | cl_opp_steady | notable |  | modifiers | to author |  | passives lint |
| cl_opp_wager | cl_opp_wager | keystone |  | modifiers | to author |  | passives lint |
| cl_opp_reprisal | cl_opp_reprisal | notable |  | proc, modifiers | to author |  | passives lint |
| cl_opp_gasp | cl_opp_gasp | keystone |  | proc, modifiers | to author | drop:lastGaspLife | passives lint |
| voc_warbringer_n1 | voc_warbringer_n1 | notable |  | modifiers | to author |  | passives lint |
| voc_warbringer_n2 | voc_warbringer_n2 | notable |  | proc, modifiers | to author |  | passives lint |
| voc_warbringer_n3 | voc_warbringer_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_warbringer_n4 | voc_warbringer_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_warbringer_k1 | voc_warbringer_k1 | keystone |  | modifiers | to author |  | passives lint |
| voc_gravebinder_n1 | voc_gravebinder_n1 | notable |  | modifiers | to author |  | passives lint |
| voc_gravebinder_n2 | voc_gravebinder_n2 | notable |  | node | to author | drop:minionUndying | passives lint |
| voc_gravebinder_n3 | voc_gravebinder_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_gravebinder_n4 | voc_gravebinder_n4 | notable |  | proc, modifiers | to author |  | passives lint |
| voc_gravebinder_k1 | voc_gravebinder_k1 | keystone |  | modifiers | to author |  | passives lint |
| voc_wildstalker_n1 | voc_wildstalker_n1 | notable |  | node | to author | drop:projectileCountChance | passives lint |
| voc_wildstalker_n2 | voc_wildstalker_n2 | notable |  | modifiers | to author |  | passives lint |
| voc_wildstalker_n3 | voc_wildstalker_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_wildstalker_n4 | voc_wildstalker_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_wildstalker_k1 | voc_wildstalker_k1 | keystone |  | modifiers | to author |  | passives lint |
| voc_archmage_n1 | voc_archmage_n1 | notable |  | modifiers | to author |  | passives lint |
| voc_archmage_n2 | voc_archmage_n2 | notable |  | proc, modifiers | to author |  | passives lint |
| voc_archmage_n3 | voc_archmage_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_archmage_n4 | voc_archmage_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_archmage_k1 | voc_archmage_k1 | keystone |  | modifiers | to author |  | passives lint |
| voc_spellblade_n1 | voc_spellblade_n1 | notable |  | node | to author | drop:combo_spellblade_weave | passives lint |
| voc_spellblade_n2 | voc_spellblade_n2 | notable |  | modifiers | to author |  | passives lint |
| voc_spellblade_n3 | voc_spellblade_n3 | notable |  | modifiers | to author | drop:comboWindow | passives lint |
| voc_spellblade_n4 | voc_spellblade_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_spellblade_k1 | voc_spellblade_k1 | keystone |  | modifiers | to author | drop:comboWindow | passives lint |
| voc_hierophant_n1 | voc_hierophant_n1 | notable |  | proc, modifiers | to author |  | passives lint |
| voc_hierophant_n2 | voc_hierophant_n2 | notable |  | proc, modifiers | to author |  | passives lint |
| voc_hierophant_n3 | voc_hierophant_n3 | notable |  | modifiers | to author | drop:offeringShare | passives lint |
| voc_hierophant_n4 | voc_hierophant_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_hierophant_k1 | voc_hierophant_k1 | keystone |  | modifiers | to author | drop:recuperate | passives lint |
| voc_greenwarden_n1 | voc_greenwarden_n1 | notable |  | node | to author | drop:attune_verdant_communion | passives lint |
| voc_greenwarden_n2 | voc_greenwarden_n2 | notable |  | node | to author | drop:terraform_sapling_ring | passives lint |
| voc_greenwarden_n3 | voc_greenwarden_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_greenwarden_n4 | voc_greenwarden_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_greenwarden_k1 | voc_greenwarden_k1 | keystone |  | modifiers | to author | drop:terraform_sapling_ring | passives lint |
| voc_greenwarden_n5 | voc_greenwarden_n5 | notable |  | node | to author | drop:terraformFx_sapling_ring | passives lint |
| voc_plaguelord_n1 | voc_plaguelord_n1 | notable |  | modifiers | to author |  | passives lint |
| voc_plaguelord_n2 | voc_plaguelord_n2 | notable |  | modifiers | to author |  | passives lint |
| voc_plaguelord_n3 | voc_plaguelord_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_plaguelord_n4 | voc_plaguelord_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_plaguelord_k1 | voc_plaguelord_k1 | keystone |  | modifiers | to author |  | passives lint |
| voc_shadowdancer_n1 | voc_shadowdancer_n1 | notable |  | modifiers | to author |  | passives lint |
| voc_shadowdancer_n2 | voc_shadowdancer_n2 | notable |  | proc, modifiers | to author |  | passives lint |
| voc_shadowdancer_n3 | voc_shadowdancer_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_shadowdancer_n4 | voc_shadowdancer_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_shadowdancer_k1 | voc_shadowdancer_k1 | keystone |  | modifiers | to author |  | passives lint |
| voc_bloodreaver_n1 | voc_bloodreaver_n1 | notable |  | modifiers | to author |  | passives lint |
| voc_bloodreaver_n2 | voc_bloodreaver_n2 | notable |  | proc, modifiers | to author |  | passives lint |
| voc_bloodreaver_n3 | voc_bloodreaver_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_bloodreaver_n4 | voc_bloodreaver_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_bloodreaver_k1 | voc_bloodreaver_k1 | keystone |  | modifiers | to author |  | passives lint |
| voc_stormweaver_n1 | voc_stormweaver_n1 | notable |  | modifiers | to author |  | passives lint |
| voc_stormweaver_n2 | voc_stormweaver_n2 | notable |  | proc, modifiers | to author |  | passives lint |
| voc_stormweaver_n3 | voc_stormweaver_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_stormweaver_n4 | voc_stormweaver_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_stormweaver_k1 | voc_stormweaver_k1 | keystone |  | modifiers | to author |  | passives lint |
| voc_bastion_n1 | voc_bastion_n1 | notable |  | proc, modifiers | to author |  | passives lint |
| voc_bastion_n2 | voc_bastion_n2 | notable |  | proc, modifiers | to author |  | passives lint |
| voc_bastion_n3 | voc_bastion_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_bastion_n4 | voc_bastion_n4 | notable |  | proc, modifiers | to author |  | passives lint |
| voc_bastion_k1 | voc_bastion_k1 | keystone |  | modifiers | to author |  | passives lint |
| voc_corsair_n1 | voc_corsair_n1 | notable |  | proc, modifiers | to author |  | passives lint |
| voc_corsair_n2 | voc_corsair_n2 | notable |  | modifiers | to author |  | passives lint |
| voc_corsair_n3 | voc_corsair_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_corsair_n4 | voc_corsair_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_corsair_k1 | voc_corsair_k1 | keystone |  | modifiers | to author |  | passives lint |
| voc_ashborn_n1 | voc_ashborn_n1 | notable |  | modifiers | to author |  | passives lint |
| voc_ashborn_n2 | voc_ashborn_n2 | notable |  | modifiers | to author |  | passives lint |
| voc_ashborn_n3 | voc_ashborn_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_ashborn_n4 | voc_ashborn_n4 | notable |  | node | to author | drop:dotPropagates | passives lint |
| voc_ashborn_k1 | voc_ashborn_k1 | keystone |  | modifiers | to author |  | passives lint |
| voc_exsanguinator_n1 | voc_exsanguinator_n1 | notable |  | modifiers | to author |  | passives lint |
| voc_exsanguinator_n2 | voc_exsanguinator_n2 | notable |  | proc, modifiers | to author |  | passives lint |
| voc_exsanguinator_n3 | voc_exsanguinator_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_exsanguinator_n4 | voc_exsanguinator_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_exsanguinator_k1 | voc_exsanguinator_k1 | keystone |  | modifiers | to author |  | passives lint |
| voc_stonewrought_n1 | voc_stonewrought_n1 | notable |  | node | to author | drop:attune_stone_communion | passives lint |
| voc_stonewrought_n2 | voc_stonewrought_n2 | notable |  | node | to author | drop:terraform_stone_cairns | passives lint |
| voc_stonewrought_n3 | voc_stonewrought_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_stonewrought_n4 | voc_stonewrought_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_stonewrought_k1 | voc_stonewrought_k1 | keystone |  | modifiers | to author | drop:terraform_stone_cairns | passives lint |
| voc_packwarden_n1 | voc_packwarden_n1 | notable |  | modifiers | to author |  | passives lint |
| voc_packwarden_n2 | voc_packwarden_n2 | notable |  | modifiers | to author |  | passives lint |
| voc_packwarden_n3 | voc_packwarden_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_packwarden_n4 | voc_packwarden_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_packwarden_k1 | voc_packwarden_k1 | keystone |  | modifiers | to author | drop:minionUndying | passives lint |
| voc_groundbreaker_n1 | voc_groundbreaker_n1 | notable |  | modifiers | to author |  | passives lint |
| voc_groundbreaker_n2 | voc_groundbreaker_n2 | notable |  | modifiers | to author |  | passives lint |
| voc_groundbreaker_n3 | voc_groundbreaker_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_groundbreaker_n4 | voc_groundbreaker_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_groundbreaker_k1 | voc_groundbreaker_k1 | keystone |  | modifiers | to author |  | passives lint |
| voc_spearhead_n1 | voc_spearhead_n1 | notable |  | node | to author | drop:shoveAuthority | passives lint |
| voc_spearhead_n2 | voc_spearhead_n2 | notable |  | node | to author | drop:impactDamage | passives lint |
| voc_spearhead_n3 | voc_spearhead_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_spearhead_n4 | voc_spearhead_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_spearhead_k1 | voc_spearhead_k1 | keystone |  | modifiers | to author |  | passives lint |
| voc_swordsaint_n1 | voc_swordsaint_n1 | notable |  | modifiers | to author |  | passives lint |
| voc_swordsaint_n2 | voc_swordsaint_n2 | notable |  | modifiers | to author | drop:comboWindow | passives lint |
| voc_swordsaint_n3 | voc_swordsaint_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_swordsaint_n4 | voc_swordsaint_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_swordsaint_k1 | voc_swordsaint_k1 | keystone |  | modifiers | to author |  | passives lint |
| voc_pitfighter_n1 | voc_pitfighter_n1 | notable |  | node | to author | drop:gripPower | passives lint |
| voc_pitfighter_n2 | voc_pitfighter_n2 | notable |  | node | to author | drop:wriggle | passives lint |
| voc_pitfighter_n3 | voc_pitfighter_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_pitfighter_n4 | voc_pitfighter_n4 | notable |  | node | to author | drop:combo_grapplers_rhythm | passives lint |
| voc_pitfighter_k1 | voc_pitfighter_k1 | keystone |  | modifiers | to author | drop:gripPower | passives lint |
| voc_thornwall_n1 | voc_thornwall_n1 | notable |  | modifiers | to author |  | passives lint |
| voc_thornwall_n2 | voc_thornwall_n2 | notable |  | modifiers | to author |  | passives lint |
| voc_thornwall_n3 | voc_thornwall_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_thornwall_n4 | voc_thornwall_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_thornwall_k1 | voc_thornwall_k1 | keystone |  | modifiers | to author |  | passives lint |
| voc_impaler_n1 | voc_impaler_n1 | notable |  | modifiers | to author |  | passives lint |
| voc_impaler_n2 | voc_impaler_n2 | notable |  | modifiers | to author |  | passives lint |
| voc_impaler_n3 | voc_impaler_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_impaler_n4 | voc_impaler_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_impaler_k1 | voc_impaler_k1 | keystone |  | modifiers | to author |  | passives lint |
| voc_enginewright_n1 | voc_enginewright_n1 | notable |  | modifiers | to author |  | passives lint |
| voc_enginewright_n2 | voc_enginewright_n2 | notable |  | modifiers | to author |  | passives lint |
| voc_enginewright_n3 | voc_enginewright_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_enginewright_n4 | voc_enginewright_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_enginewright_k1 | voc_enginewright_k1 | keystone |  | modifiers | to author |  | passives lint |
| voc_bannerlord_n1 | voc_bannerlord_n1 | notable |  | modifiers | to author |  | passives lint |
| voc_bannerlord_n2 | voc_bannerlord_n2 | notable |  | modifiers | to author |  | passives lint |
| voc_bannerlord_n3 | voc_bannerlord_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_bannerlord_n4 | voc_bannerlord_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_bannerlord_k1 | voc_bannerlord_k1 | keystone |  | modifiers | to author |  | passives lint |
| voc_warchanter_n1 | voc_warchanter_n1 | notable |  | node | to author | drop:combo_drumbeat | passives lint |
| voc_warchanter_n2 | voc_warchanter_n2 | notable |  | modifiers | to author |  | passives lint |
| voc_warchanter_n3 | voc_warchanter_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_warchanter_n4 | voc_warchanter_n4 | notable |  | modifiers | to author | drop:comboWindow | passives lint |
| voc_warchanter_k1 | voc_warchanter_k1 | keystone |  | modifiers | to author |  | passives lint |
| voc_veilweaver_n1 | voc_veilweaver_n1 | notable |  | modifiers | to author |  | passives lint |
| voc_veilweaver_n2 | voc_veilweaver_n2 | notable |  | modifiers | to author |  | passives lint |
| voc_veilweaver_n3 | voc_veilweaver_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_veilweaver_n4 | voc_veilweaver_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_veilweaver_k1 | voc_veilweaver_k1 | keystone |  | modifiers | to author |  | passives lint |
| voc_chronarch_n1 | voc_chronarch_n1 | notable |  | modifiers | to author |  | passives lint |
| voc_chronarch_n2 | voc_chronarch_n2 | notable |  | modifiers | to author |  | passives lint |
| voc_chronarch_n3 | voc_chronarch_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_chronarch_n4 | voc_chronarch_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_chronarch_k1 | voc_chronarch_k1 | keystone |  | modifiers | to author |  | passives lint |
| voc_stillmind_n1 | voc_stillmind_n1 | notable |  | modifiers | to author |  | passives lint |
| voc_stillmind_n2 | voc_stillmind_n2 | notable |  | modifiers | to author |  | passives lint |
| voc_stillmind_n3 | voc_stillmind_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_stillmind_n4 | voc_stillmind_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_stillmind_k1 | voc_stillmind_k1 | keystone |  | modifiers | to author |  | passives lint |
| voc_swarmlord_n1 | voc_swarmlord_n1 | notable |  | modifiers | to author |  | passives lint |
| voc_swarmlord_n2 | voc_swarmlord_n2 | notable |  | node | to author | drop:minionPlies | passives lint |
| voc_swarmlord_n3 | voc_swarmlord_n3 | notable |  | modifiers | to author |  | passives lint |
| voc_swarmlord_n4 | voc_swarmlord_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_swarmlord_k1 | voc_swarmlord_k1 | keystone |  | modifiers | to author | drop:minionUndying | passives lint |
| voc_harborwarden_n1 | voc_harborwarden_n1 | notable |  | node | to author | drop:mercEase | passives lint |
| voc_harborwarden_n2 | voc_harborwarden_n2 | notable |  | node | to author | drop:mercVigor | passives lint |
| voc_harborwarden_n3 | voc_harborwarden_n3 | notable |  | modifiers | to author | drop:mercHireDiscount | passives lint |
| voc_harborwarden_n4 | voc_harborwarden_n4 | notable |  | modifiers | to author |  | passives lint |
| voc_harborwarden_k1 | voc_harborwarden_k1 | keystone |  | node | to author | drop:mercRetinue | passives lint |

## Item bases (79: ported 57, not ported 14, ported (difference) 8)

| Prototype | Ours | Mechanism | Status | Note | Proof |
|---|---|---|---|---|---|
| helmet_armor | helmet_armor | base: helmet armor | ported |  | item roll test |
| helmet_evasion | helmet_evasion | base: helmet evasion | ported |  | item roll test |
| helmet_es | helmet_es | base: helmet energyShield | ported |  | item roll test |
| helmet_armor_evasion | helmet_armor_evasion | base: helmet armor+evasion | ported |  | item roll test |
| helmet_armor_es | helmet_armor_es | base: helmet armor+energyShield | ported |  | item roll test |
| helmet_evasion_es | helmet_evasion_es | base: helmet evasion+energyShield | ported |  | item roll test |
| helmet_armor_poise | helmet_armor_poise | base: helmet armor+poise | ported |  | item roll test |
| helmet_evasion_insight | helmet_evasion_insight | base: helmet evasion+insight | ported |  | item roll test |
| chest_armor | chest_armor | base: chest armor | ported |  | item roll test |
| chest_evasion | chest_evasion | base: chest evasion | ported |  | item roll test |
| chest_es | chest_es | base: chest energyShield | ported |  | item roll test |
| chest_armor_evasion | chest_armor_evasion | base: chest armor+evasion | ported |  | item roll test |
| chest_armor_es | chest_armor_es | base: chest armor+energyShield | ported |  | item roll test |
| chest_evasion_es | chest_evasion_es | base: chest evasion+energyShield | ported |  | item roll test |
| chest_armor_poise | chest_armor_poise | base: chest armor+poise | ported |  | item roll test |
| chest_evasion_insight | chest_evasion_insight | base: chest evasion+insight | ported |  | item roll test |
| gloves_armor | gloves_armor | base: gloves armor | ported |  | item roll test |
| gloves_evasion | gloves_evasion | base: gloves evasion | ported |  | item roll test |
| gloves_es | gloves_es | base: gloves energyShield | ported |  | item roll test |
| gloves_armor_evasion | gloves_armor_evasion | base: gloves armor+evasion | ported |  | item roll test |
| gloves_armor_es | gloves_armor_es | base: gloves armor+energyShield | ported |  | item roll test |
| gloves_evasion_es | gloves_evasion_es | base: gloves evasion+energyShield | ported |  | item roll test |
| gloves_armor_poise | gloves_armor_poise | base: gloves armor+poise | ported |  | item roll test |
| gloves_evasion_insight | gloves_evasion_insight | base: gloves evasion+insight | ported |  | item roll test |
| boots_armor | boots_armor | base: boots armor | ported |  | item roll test |
| boots_evasion | boots_evasion | base: boots evasion | ported |  | item roll test |
| boots_es | boots_es | base: boots energyShield | ported |  | item roll test |
| boots_armor_evasion | boots_armor_evasion | base: boots armor+evasion | ported |  | item roll test |
| boots_armor_es | boots_armor_es | base: boots armor+energyShield | ported |  | item roll test |
| boots_evasion_es | boots_evasion_es | base: boots evasion+energyShield | ported |  | item roll test |
| boots_armor_poise | boots_armor_poise | base: boots armor+poise | ported |  | item roll test |
| boots_evasion_insight | boots_evasion_insight | base: boots evasion+insight | ported |  | item roll test |
| legs_armor |  | base: legs armor | not ported | no legs slot here: dropped | item roll test |
| legs_evasion |  | base: legs evasion | not ported | no legs slot here: dropped | item roll test |
| legs_es |  | base: legs energyShield | not ported | no legs slot here: dropped | item roll test |
| legs_armor_evasion |  | base: legs armor+evasion | not ported | no legs slot here: dropped | item roll test |
| legs_armor_es |  | base: legs armor+energyShield | not ported | no legs slot here: dropped | item roll test |
| legs_evasion_es |  | base: legs evasion+energyShield | not ported | no legs slot here: dropped | item roll test |
| legs_armor_poise |  | base: legs armor+poise | not ported | no legs slot here: dropped | item roll test |
| legs_evasion_insight |  | base: legs evasion+insight | not ported | no legs slot here: dropped | item roll test |
| belt_armor | belt_armor | base: belt armor | ported |  | item roll test |
| belt_poise | belt_poise | base: belt poise | ported |  | item roll test |
| belt_endurance | belt_endurance | base: belt endurance | ported (difference) | endurance is not a pool here: the share is dropped | item roll test |
| ring_coral | ring_coral | base: ring | ported |  | item roll test |
| ring_lapis | ring_lapis | base: ring | ported |  | item roll test |
| ring_ruby | ring_ruby | base: ring | ported |  | item roll test |
| ring_sapphire | ring_sapphire | base: ring | ported |  | item roll test |
| ring_topaz | ring_topaz | base: ring | ported |  | item roll test |
| ring_onyx | ring_onyx | base: ring | ported |  | item roll test |
| ring_iron | ring_iron | base: ring | ported |  | item roll test |
| boots_mi_goblin | boots_mi_goblin | base: boots evasion | ported |  | item roll test |
| amulet_mi_goblin | amulet_mi_goblin | base: amulet | ported |  | item roll test |
| belt_mi_bandit | belt_mi_bandit | base: belt endurance | ported (difference) | endurance is not a pool here: the share is dropped | item roll test |
| gloves_mi_bandit | gloves_mi_bandit | base: gloves evasion | ported |  | item roll test |
| chest_mi_undead | chest_mi_undead | base: chest energyShield | ported |  | item roll test |
| helmet_mi_undead | helmet_mi_undead | base: helmet armor+energyShield | ported |  | item roll test |
| legs_mi_gnoll |  | base: legs evasion | not ported | no legs slot here: dropped | item roll test |
| ring_mi_gnoll | ring_mi_gnoll | base: ring | ported |  | item roll test |
| amulet_mi_abyssal | amulet_mi_abyssal | base: amulet | ported |  | item roll test |
| gloves_mi_abyssal | gloves_mi_abyssal | base: gloves energyShield | ported |  | item roll test |
| amulet_amber | amulet_amber | base: amulet | ported |  | item roll test |
| amulet_jade | amulet_jade | base: amulet | ported |  | item roll test |
| amulet_opal | amulet_opal | base: amulet | ported |  | item roll test |
| amulet_bone | amulet_bone | base: amulet | ported |  | item roll test |
| amulet_pearl | amulet_pearl | base: amulet | ported |  | item roll test |
| amulet_carnelian | amulet_carnelian | base: amulet | ported |  | item roll test |
| amulet_agate | amulet_agate | base: amulet | ported |  | item roll test |
| ring_moonstone | ring_moonstone | base: ring | ported |  | item roll test |
| skill_gem | weave_codex | weave items | ported (difference) | codices and stones carry the weave item model | item tests |
| support_gem | glyph_stone | weave items | ported (difference) | codices and stones carry the weave item model | item tests |
| rough_memory |  | weave items | not ported | memories, writs and quest tokens belong to other fabrics (row I9) | item tests |
| smith_writ |  | weave items | not ported | memories, writs and quest tokens belong to other fabrics (row I9) | item tests |
| quest_hammer |  | weave items | not ported | memories, writs and quest tokens belong to other fabrics (row I9) | item tests |
| quest_trophy |  | weave items | not ported | memories, writs and quest tokens belong to other fabrics (row I9) | item tests |
| preformed_memory |  | weave items | not ported | memories, writs and quest tokens belong to other fabrics (row I9) | item tests |
| relic_charm | relic_charm | base: relic | ported (difference) | explicit affix pool: list the families by hand | item roll test |
| relic_talisman | relic_talisman | base: relic | ported (difference) | explicit affix pool: list the families by hand | item roll test |
| relic_idol | relic_idol | base: relic | ported (difference) | explicit affix pool: list the families by hand | item roll test |
| relic_effigy | relic_effigy | base: relic | ported (difference) | explicit affix pool: list the families by hand | item roll test |

## Affix families (730: blocked 74, ported 199, ported (difference) 457)

| Prototype | Ours | Mechanism | Status | Note | Proof |
|---|---|---|---|---|---|
| creeper_barrow |  | affix prefix: creeperCount_barrow | blocked | seam: no line maps | affix roll test |
| satellite_rime_wake |  | affix prefix: satelliteCount_rime_wake | blocked | seam: no line maps | affix roll test |
| guardian_graveglass |  | affix prefix: guardianCount_graveglass | blocked | seam: no line maps | affix roll test |
| satellite_storm_wake |  | affix prefix: satelliteCount_storm_wake | blocked | seam: no line maps | affix roll test |
| aurora_pall |  | affix prefix: auroraCapacity_pall | blocked | seam: no line maps | affix roll test |
| satellite_cinder_wake |  | affix prefix: satelliteCount_cinder_wake | blocked | seam: no line maps | affix roll test |
| satellite_iron_wake |  | affix prefix: satelliteCount_iron_wake | blocked | seam: no line maps | affix roll test |
| relic_life | relic_life | affix prefix: life | ported |  | affix roll test |
| relic_mana | relic_mana | affix prefix: mana | ported |  | affix roll test |
| relic_es | relic_es | affix prefix: energyShield | ported |  | affix roll test |
| relic_armor | relic_armor | affix prefix: armor | ported |  | affix roll test |
| relic_evasion | relic_evasion | affix prefix: evasion | ported |  | affix roll test |
| relic_damage | relic_damage | affix prefix: damage | ported |  | affix roll test |
| relic_dmg_melee | relic_dmg_melee | affix prefix: damage | ported |  | affix roll test |
| relic_dmg_spell | relic_dmg_spell | affix prefix: damage | ported |  | affix roll test |
| relic_dmg_projectile | relic_dmg_projectile | affix prefix: damage | ported |  | affix roll test |
| relic_dmg_minion | relic_dmg_minion | affix prefix: damage | ported |  | affix roll test |
| relic_dmg_aoe | relic_dmg_aoe | affix prefix: damage | ported |  | affix roll test |
| relic_dmg_physical | relic_dmg_physical | affix prefix: damage | ported |  | affix roll test |
| relic_dmg_fire | relic_dmg_fire | affix prefix: damage | ported |  | affix roll test |
| relic_dmg_cold | relic_dmg_cold | affix prefix: damage | ported |  | affix roll test |
| relic_dmg_lightning | relic_dmg_lightning | affix prefix: damage | ported |  | affix roll test |
| relic_dmg_chaos | relic_dmg_chaos | affix prefix: damage | ported |  | affix roll test |
| relic_dmg_channel | relic_dmg_channel | affix prefix: damage | ported |  | affix roll test |
| relic_dmg_construct | relic_dmg_construct | affix prefix: damage | ported |  | affix roll test |
| relic_dmg_conjure | relic_dmg_conjure | affix prefix: damage | ported |  | affix roll test |
| relic_dmg_javelin | relic_dmg_javelin | affix prefix: damage | ported |  | affix roll test |
| relic_minion_damage | relic_minion_damage | affix prefix: minionDamage | ported |  | affix roll test |
| relic_minion_life | relic_minion_life | affix prefix: minionLife | ported |  | affix roll test |
| relic_area | relic_area | affix prefix: aoeRadius | ported |  | affix roll test |
| relic_projectile_speed | relic_projectile_speed | affix prefix: projectileSpeed | ported |  | affix roll test |
| relic_duration | relic_duration | affix prefix: effectDuration | ported |  | affix roll test |
| relic_planted | relic_planted | affix prefix: damage | ported |  | affix roll test |
| relic_reprisal | relic_reprisal | affix prefix: damage | ported |  | affix roll test |
| relic_execution | relic_execution | affix prefix: damage | ported |  | affix roll test |
| relic_opportunist | relic_opportunist | affix prefix: damage | ported |  | affix roll test |
| relic_variety | relic_variety | affix prefix: damage | ported |  | affix roll test |
| relic_repetition | relic_repetition | affix prefix: damage | ported |  | affix roll test |
| life | life | affix prefix: life | ported |  | affix roll test |
| mana | mana | affix prefix: mana | ported |  | affix roll test |
| armor_flat | armor_flat | affix prefix: armor | ported |  | affix roll test |
| armor_pct | armor_pct | affix prefix: armor | ported |  | affix roll test |
| armor_pct_global | armor_pct_global | affix prefix: armor | ported |  | affix roll test |
| evasion_flat | evasion_flat | affix prefix: evasion | ported |  | affix roll test |
| evasion_pct | evasion_pct | affix prefix: evasion | ported |  | affix roll test |
| evasion_pct_global | evasion_pct_global | affix prefix: evasion | ported |  | affix roll test |
| es_flat | es_flat | affix prefix: energyShield | ported |  | affix roll test |
| es_pct | es_pct | affix prefix: energyShield | ported |  | affix roll test |
| es_pct_global | es_pct_global | affix prefix: energyShield | ported |  | affix roll test |
| es_flat_global | es_flat_global | affix prefix: energyShield | ported |  | affix roll test |
| poise_flat | poise_flat | affix prefix: poise | ported |  | affix roll test |
| poise_pct | poise_pct | affix prefix: poise | ported |  | affix roll test |
| poise_pct_global | poise_pct_global | affix prefix: poise | ported |  | affix roll test |
| endurance_flat |  | affix prefix: endurance | blocked | seam: no line maps | affix roll test |
| endurance_pct |  | affix prefix: endurance | blocked | seam: no line maps | affix roll test |
| endurance_pct_global |  | affix prefix: endurance | blocked | seam: no line maps | affix roll test |
| insight_flat | insight_flat | affix prefix: insight | ported |  | affix roll test |
| insight_pct | insight_pct | affix prefix: insight | ported |  | affix roll test |
| insight_pct_global | insight_pct_global | affix prefix: insight | ported |  | affix roll test |
| thorns | thorns | affix prefix: thorns | ported |  | affix roll test |
| minion_damage | minion_damage | affix prefix: minionDamage | ported |  | affix roll test |
| minion_life | minion_life | affix prefix: minionLife | ported |  | affix roll test |
| minion_haste | minion_haste | affix prefix: minionHaste | ported |  | affix roll test |
| minion_regen_rate |  | affix prefix: minionRegenRate | blocked | seam: no line maps | affix roll test |
| trample_mass |  | affix suffix: trample | blocked | seam: no line maps | affix roll test |
| ply_rend |  | affix suffix: plyRend | blocked | seam: no line maps | affix roll test |
| corpse_batch |  | affix suffix: corpseBatch | blocked | seam: no line maps | affix roll test |
| mi_goblin_scavenger | mi_goblin_scavenger | affix prefix: lifeOnKill, manaOnKill | ported |  | affix roll test |
| mi_bandit_extortion | mi_bandit_extortion | affix prefix: lifeLeech, manaLeech | ported |  | affix roll test |
| mi_undead_gravecaller | mi_undead_gravecaller | affix prefix: minionDamage, minionRegen | ported |  | affix roll test |
| mi_gnoll_packlaw | mi_gnoll_packlaw | affix prefix: apply_bleed, moveSpeed | ported |  | affix roll test |
| mi_abyssal_horologe | mi_abyssal_horologe | affix prefix: castSpeed, cooldownRecovery | ported |  | affix roll test |
| royal_jelly | royal_jelly | affix prefix: life, lifeRegen | ported |  | affix roll test |
| chitin_plate | chitin_plate | affix prefix: armor, poise | ported |  | affix roll test |
| drowned_regalia | drowned_regalia | affix prefix: energyShield, mana | ported |  | affix roll test |
| barnacle_crust | barnacle_crust | affix prefix: armor, life | ported |  | affix roll test |
| oxdrawn |  | affix prefix: shoveAuthority, impactDamage | blocked | seam: no line maps | affix roll test |
| fleecebound | fleecebound | affix prefix: armor, coldRes | ported |  | affix roll test |
| deepvein | deepvein | affix prefix: life, lifeRegen | ported |  | affix roll test |
| hadal_cold | hadal_cold | affix prefix: addedCold, coldRes | ported |  | affix roll test |
| voidbound | voidbound | affix prefix: overmatch | ported |  | affix roll test |
| proc_concussive | proc_concussive | affix prefix: proc_brutal_strike | ported |  | affix roll test |
| proc_stormlit | proc_stormlit | affix prefix: proc_thunderstruck | ported |  | affix roll test |
| proc_pathcutter | proc_pathcutter | affix suffix: proc_pathcutter_stride | ported |  | affix roll test |
| proc_cave_in | proc_cave_in | affix suffix: proc_cave_in | ported |  | affix roll test |
| hybrid_life_mana | hybrid_life_mana | affix prefix: life, mana | ported |  | affix roll test |
| hybrid_armor_evasion | hybrid_armor_evasion | affix prefix: armor, evasion | ported |  | affix roll test |
| added_physical | added_physical | affix prefix: addedPhysical | ported |  | affix roll test |
| added_fire | added_fire | affix prefix: addedFire | ported |  | affix roll test |
| added_cold | added_cold | affix prefix: addedCold | ported |  | affix roll test |
| added_lightning | added_lightning | affix prefix: addedLightning | ported |  | affix roll test |
| added_chaos | added_chaos | affix prefix: addedChaos | ported |  | affix roll test |
| dmg_melee | dmg_melee | affix prefix: damage | ported |  | affix roll test |
| dmg_spell | dmg_spell | affix prefix: damage | ported |  | affix roll test |
| dmg_projectile | dmg_projectile | affix prefix: damage | ported |  | affix roll test |
| dmg_minion | dmg_minion | affix prefix: damage | ported |  | affix roll test |
| dmg_aoe | dmg_aoe | affix prefix: damage | ported |  | affix roll test |
| dmg_physical | dmg_physical | affix prefix: damage | ported |  | affix roll test |
| dmg_fire | dmg_fire | affix prefix: damage | ported |  | affix roll test |
| dmg_cold | dmg_cold | affix prefix: damage | ported |  | affix roll test |
| dmg_lightning | dmg_lightning | affix prefix: damage | ported |  | affix roll test |
| dmg_chaos | dmg_chaos | affix prefix: damage | ported |  | affix roll test |
| dmg_channel | dmg_channel | affix prefix: damage | ported |  | affix roll test |
| dmg_construct | dmg_construct | affix prefix: damage | ported |  | affix roll test |
| dmg_conjure | dmg_conjure | affix prefix: damage | ported |  | affix roll test |
| dmg_javelin | dmg_javelin | affix prefix: damage | ported |  | affix roll test |
| attack_speed | attack_speed | affix suffix: attackSpeed | ported |  | affix roll test |
| cast_speed | cast_speed | affix suffix: castSpeed | ported |  | affix roll test |
| move_speed | move_speed | affix suffix: moveSpeed | ported |  | affix roll test |
| crit_chance | crit_chance | affix suffix: critChance | ported |  | affix roll test |
| crit_multi | crit_multi | affix suffix: critMulti | ported |  | affix roll test |
| accuracy | accuracy | affix suffix: accuracy | ported |  | affix roll test |
| armor_pen | armor_pen | affix suffix: armorPen | ported |  | affix roll test |
| life_regen | life_regen | affix suffix: lifeRegen | ported |  | affix roll test |
| regen_rate | regen_rate | affix suffix: lifeRegen, lifeRegenPct | ported |  | affix roll test |
| mana_regen | mana_regen | affix suffix: manaRegen | ported |  | affix roll test |
| life_leech | life_leech | affix suffix: lifeLeech | ported |  | affix roll test |
| mana_leech | mana_leech | affix suffix: manaLeech | ported |  | affix roll test |
| life_on_hit | life_on_hit | affix suffix: lifeOnHit | ported |  | affix roll test |
| life_on_kill | life_on_kill | affix suffix: lifeOnKill | ported |  | affix roll test |
| mana_on_kill | mana_on_kill | affix suffix: manaOnKill | ported |  | affix roll test |
| es_leech | es_leech | affix suffix: esLeech | ported |  | affix roll test |
| es_on_hit | es_on_hit | affix suffix: esOnHit | ported |  | affix roll test |
| es_on_kill | es_on_kill | affix suffix: esOnKill | ported |  | affix roll test |
| life_on_evade |  | affix suffix: lifeOnEvade | blocked | seam: no line maps | affix roll test |
| life_on_block |  | affix suffix: lifeOnBlock | blocked | seam: no line maps | affix roll test |
| poise_recovery | poise_recovery | affix suffix: poiseRegenPct | ported |  | affix roll test |
| poise_on_hit | poise_on_hit | affix suffix: poiseOnHit | ported |  | affix roll test |
| poise_overcharge |  | affix suffix: poiseOvercharge | blocked | seam: no line maps | affix roll test |
| guard_strength | guard_strength | affix suffix: guardStrength | ported |  | affix roll test |
| ailment_resist | ailment_resist | affix suffix: ailmentResist | ported |  | affix roll test |
| crit_avoid | crit_avoid | affix suffix: critAvoid | ported |  | affix roll test |
| affliction_recovery |  | affix suffix: afflictionExpiry | blocked | seam: no line maps | affix roll test |
| mass |  | affix suffix: weight | blocked | seam: no line maps | affix roll test |
| traction |  | affix suffix: traction | blocked | seam: no line maps | affix roll test |
| threat_loud | threat_loud | affix suffix: threatGen | ported |  | affix roll test |
| threat_quiet | threat_quiet | affix suffix: threatGen | ported |  | affix roll test |
| conduit_rate |  | affix suffix: conduitRate | blocked | seam: no line maps | affix roll test |
| conduit_efficiency |  | affix suffix: conduitEfficiency | blocked | seam: no line maps | affix roll test |
| es_recharge_rate | es_recharge_rate | affix suffix: esRechargeRate | ported |  | affix roll test |
| es_steadfast |  | affix suffix: esRechargeSteadfast | blocked | seam: no line maps | affix roll test |
| mana_efficiency | mana_efficiency | affix suffix: manaCost | ported |  | affix roll test |
| cooldown | cooldown | affix suffix: cooldownRecovery | ported |  | affix roll test |
| aoe | aoe | affix suffix: aoeRadius | ported |  | affix roll test |
| proj_speed | proj_speed | affix suffix: projectileSpeed | ported |  | affix roll test |
| duration | duration | affix suffix: effectDuration | ported |  | affix roll test |
| heal_power | heal_power | affix suffix: healPower | ported |  | affix roll test |
| restore_power |  | affix suffix: restorePower | blocked | seam: no line maps | affix roll test |
| restore_pct_max |  | affix suffix: restorePctMax | blocked | seam: no line maps | affix roll test |
| luck |  | affix suffix: luck | blocked | seam: no line maps | affix roll test |
| lucky_hits |  | affix suffix: luckyChance | blocked | seam: no line maps | affix roll test |
| skill_charges | skill_charges | affix suffix: skillCharges | ported |  | affix roll test |
| charge_rate | charge_rate | affix suffix: skillChargeRate | ported |  | affix roll test |
| block | block | affix suffix: blockChance | ported |  | affix roll test |
| crit_pct_local | crit_pct_local | affix suffix: critChance | ported |  | affix roll test |
| all_res | all_res | affix suffix: fireRes, coldRes, lightningRes, chaosRes | ported |  | affix roll test |
| proc_radiant_oath | proc_radiant_oath | affix suffix: proc_radiant_reprisal | ported |  | affix roll test |
| proc_cascading_light | proc_cascading_light | affix suffix: proc_radiant_cascade | ported |  | affix roll test |
| orb_shed_rate |  | affix suffix: orbShedRate | blocked | seam: no line maps | affix roll test |
| sympathy_fellowship |  | affix suffix: sympathy_shared_surge | blocked | seam: no line maps | affix roll test |
| swarm_tempo | swarm_tempo | affix suffix: attackSpeed, castSpeed | ported |  | affix roll test |
| tideworn | tideworn | affix suffix: moveSpeed, coldRes | ported |  | affix roll test |
| foldkept | foldkept | affix suffix: lifeRegen, manaRegen | ported |  | affix roll test |
| lampkeeper |  | affix suffix: survivalEase_light | blocked | seam: no line maps | affix roll test |
| crushing_deep |  | affix suffix: shoveAuthority, impactDamage | blocked | seam: no line maps | affix roll test |
| deep_echo |  | affix suffix: comboWindow | blocked | seam: no line maps | affix roll test |
| mi_gnoll_impalement | mi_gnoll_impalement | affix suffix: proc_mi_gnoll_impale | ported |  | affix roll test |
| mi_goblin_knifefan | mi_goblin_knifefan | affix suffix: proc_mi_goblin_fan | ported |  | affix roll test |
| mi_bandit_toll | mi_bandit_toll | affix suffix: proc_mi_bandit_caltrops | ported |  | affix roll test |
| mi_abyssal_teeming |  | affix suffix: wornThrong_abyssal_brood | blocked | seam: no line maps | affix roll test |
| vs_lowlife | vs_lowlife | affix suffix: damage | ported |  | affix roll test |
| vs_hardcc | vs_hardcc | affix suffix: damage | ported |  | affix roll test |
| vs_behind | vs_behind | affix suffix: critChance | ported |  | affix roll test |
| recent_kill_speed | recent_kill_speed | affix suffix: moveSpeed | ported |  | affix roll test |
| calm_regen | calm_regen | affix suffix: lifeRegenPct | ported |  | affix roll test |
| missing_life_damage | missing_life_damage | affix suffix: damage | ported |  | affix roll test |
| crowd_damage | crowd_damage | affix suffix: damage | ported |  | affix roll test |
| last_gasp | last_gasp | affix suffix: lastGasp | ported |  | affix roll test |
| proc_reprisal | proc_reprisal | affix suffix: proc_reprisal | ported |  | affix roll test |
| proc_desperate_ward | proc_desperate_ward | affix suffix: proc_desperate_ward | ported |  | affix roll test |
| proc_adrenaline | proc_adrenaline | affix suffix: proc_adrenaline | ported |  | affix roll test |
| proc_slow_burn | proc_slow_burn | affix suffix: proc_slow_burn | ported |  | affix roll test |
| proc_mending_ward | proc_mending_ward | affix suffix: proc_mending_ward | ported |  | affix roll test |
| proc_necrotic_feast | proc_necrotic_feast | affix suffix: proc_necrotic_feast | ported |  | affix roll test |
| attr_strength | attr_strength | affix suffix: strength | ported |  | affix roll test |
| attr_prowess | attr_prowess | affix suffix: prowess | ported |  | affix roll test |
| attr_fortitude | attr_fortitude | affix suffix: fortitude | ported |  | affix roll test |
| attr_dexterity | attr_dexterity | affix suffix: dexterity | ported |  | affix roll test |
| attr_finesse | attr_finesse | affix suffix: finesse | ported |  | affix roll test |
| attr_charisma | attr_charisma | affix suffix: charisma | ported |  | affix roll test |
| attr_intelligence | attr_intelligence | affix suffix: intelligence | ported |  | affix roll test |
| attr_wisdom | attr_wisdom | affix suffix: wisdom | ported |  | affix roll test |
| attr_willpower | attr_willpower | affix suffix: willpower | ported |  | affix roll test |
| attr_vitality | attr_vitality | affix suffix: vitality | ported |  | affix roll test |
| res_fire | res_fire | affix suffix: fireRes | ported |  | affix roll test |
| res_cold | res_cold | affix suffix: coldRes | ported |  | affix roll test |
| res_lightning | res_lightning | affix suffix: lightningRes | ported |  | affix roll test |
| res_chaos | res_chaos | affix suffix: chaosRes | ported |  | affix roll test |
| pen_fire | pen_fire | affix suffix: firePen | ported |  | affix roll test |
| pen_cold | pen_cold | affix suffix: coldPen | ported |  | affix roll test |
| pen_lightning | pen_lightning | affix suffix: lightningPen | ported |  | affix roll test |
| pen_chaos | pen_chaos | affix suffix: chaosPen | ported |  | affix roll test |
| apply_burn | apply_burn | affix suffix: apply_burn | ported |  | affix roll test |
| apply_bleed | apply_bleed | affix suffix: apply_bleed | ported |  | affix roll test |
| apply_poison | apply_poison | affix suffix: apply_poison | ported |  | affix roll test |
| apply_chill | apply_chill | affix suffix: apply_chill | ported |  | affix roll test |
| apply_shock | apply_shock | affix suffix: apply_shock | ported |  | affix roll test |
| fount_cap_wisp | fount_cap_wisp | affix suffix: chargeCap_wisp | ported |  | affix roll test |
| fount_cap_flask_life | fount_cap_flask_life | affix suffix: chargeCap_flask_life | ported |  | affix roll test |
| fount_cap_flask_mana | fount_cap_flask_mana | affix suffix: chargeCap_flask_mana | ported |  | affix roll test |
| fount_cap_flask_catalyst | fount_cap_flask_catalyst | affix suffix: chargeCap_flask_catalyst | ported |  | affix roll test |
| fount_cap_flask_quicksilver | fount_cap_flask_quicksilver | affix suffix: chargeCap_flask_quicksilver | ported |  | affix roll test |
| fount_cap_flask_stoneskin | fount_cap_flask_stoneskin | affix suffix: chargeCap_flask_stoneskin | ported |  | affix roll test |
| fount_cap_flask_antidote | fount_cap_flask_antidote | affix suffix: chargeCap_flask_antidote | ported |  | affix roll test |
| class_skills_warrior |  | affix suffix: classSkill_warrior | blocked | seam: no line maps | affix roll test |
| class_skills_magician |  | affix suffix: classSkill_magician | blocked | seam: no line maps | affix roll test |
| class_skills_rogue |  | affix suffix: classSkill_rogue | blocked | seam: no line maps | affix roll test |
| class_skills_berserker |  | affix suffix: classSkill_berserker | blocked | seam: no line maps | affix roll test |
| class_skills_sorcerer |  | affix suffix: classSkill_sorcerer | blocked | seam: no line maps | affix roll test |
| class_skills_ranger |  | affix suffix: classSkill_ranger | blocked | seam: no line maps | affix roll test |
| class_skills_guardian |  | affix suffix: classSkill_guardian | blocked | seam: no line maps | affix roll test |
| class_skills_summoner |  | affix suffix: classSkill_summoner | blocked | seam: no line maps | affix roll test |
| class_skills_swashbuckler |  | affix suffix: classSkill_swashbuckler | blocked | seam: no line maps | affix roll test |
| class_skills_juggernaut |  | affix suffix: classSkill_juggernaut | blocked | seam: no line maps | affix roll test |
| class_skills_pyromancer |  | affix suffix: classSkill_pyromancer | blocked | seam: no line maps | affix roll test |
| class_skills_assassin |  | affix suffix: classSkill_assassin | blocked | seam: no line maps | affix roll test |
| class_skills_necromancer |  | affix suffix: classSkill_necromancer | blocked | seam: no line maps | affix roll test |
| class_skills_cleric |  | affix suffix: classSkill_cleric | blocked | seam: no line maps | affix roll test |
| class_skills_tamer |  | affix suffix: classSkill_tamer | blocked | seam: no line maps | affix roll test |
| class_skills_breaker |  | affix suffix: classSkill_breaker | blocked | seam: no line maps | affix roll test |
| class_skills_vanguard |  | affix suffix: classSkill_vanguard | blocked | seam: no line maps | affix roll test |
| class_skills_blademaster |  | affix suffix: classSkill_blademaster | blocked | seam: no line maps | affix roll test |
| class_skills_brawler |  | affix suffix: classSkill_brawler | blocked | seam: no line maps | affix roll test |
| class_skills_sentinel |  | affix suffix: classSkill_sentinel | blocked | seam: no line maps | affix roll test |
| class_skills_lancer |  | affix suffix: classSkill_lancer | blocked | seam: no line maps | affix roll test |
| class_skills_trapper |  | affix suffix: classSkill_trapper | blocked | seam: no line maps | affix roll test |
| class_skills_warlord |  | affix suffix: classSkill_warlord | blocked | seam: no line maps | affix roll test |
| class_skills_skald |  | affix suffix: classSkill_skald | blocked | seam: no line maps | affix roll test |
| class_skills_beguiler |  | affix suffix: classSkill_beguiler | blocked | seam: no line maps | affix roll test |
| class_skills_chronomancer |  | affix suffix: classSkill_chronomancer | blocked | seam: no line maps | affix roll test |
| class_skills_ascetic |  | affix suffix: classSkill_ascetic | blocked | seam: no line maps | affix roll test |
| class_skills_hivecaller |  | affix suffix: classSkill_hivecaller | blocked | seam: no line maps | affix roll test |
| class_skills_wallwright |  | affix suffix: classSkill_wallwright | blocked | seam: no line maps | affix roll test |
| class_skills_matador |  | affix suffix: classSkill_matador | blocked | seam: no line maps | affix roll test |
| class_skills_flagellant |  | affix suffix: classSkill_flagellant | blocked | seam: no line maps | affix roll test |
| class_skills_falconer |  | affix suffix: classSkill_falconer | blocked | seam: no line maps | affix roll test |
| class_skills_sharper |  | affix suffix: classSkill_sharper | blocked | seam: no line maps | affix roll test |
| class_skills_firebrand |  | affix suffix: classSkill_firebrand | blocked | seam: no line maps | affix roll test |
| class_skills_runeweaver |  | affix suffix: classSkill_runeweaver | blocked | seam: no line maps | affix roll test |
| class_skills_resonator |  | affix suffix: classSkill_resonator | blocked | seam: no line maps | affix roll test |
| class_skills_spellblade |  | affix suffix: classSkill_spellblade | blocked | seam: no line maps | affix roll test |
| class_skills_cryomancer |  | affix suffix: classSkill_cryomancer | blocked | seam: no line maps | affix roll test |
| class_skills_apothecary |  | affix suffix: classSkill_apothecary | blocked | seam: no line maps | affix roll test |
| graft_crushing_impact | graft_crushing_impact | affix suffix: slotgraft_1_crushing_impact | ported (difference) | seat1 | affix roll test |
| graft_battering_ram | graft_battering_ram | affix suffix: slotgraft_1_battering_ram | ported (difference) | seat1 | affix roll test |
| graft_overmatch | graft_overmatch | affix suffix: slotgraft_1_overmatch | ported (difference) | seat1 | affix roll test |
| graft_giantsbane | graft_giantsbane | affix suffix: slotgraft_1_giantsbane | ported (difference) | seat1 | affix roll test |
| graft_regicide | graft_regicide | affix suffix: slotgraft_1_regicide | ported (difference) | seat1 | affix roll test |
| graft_limbreaver | graft_limbreaver | affix suffix: slotgraft_1_limbreaver | ported (difference) | seat1 | affix roll test |
| graft_siegebreaker | graft_siegebreaker | affix suffix: slotgraft_1_siegebreaker | ported (difference) | seat1 | affix roll test |
| graft_spentbane | graft_spentbane | affix suffix: slotgraft_1_spentbane | ported (difference) | seat1 | affix roll test |
| graft_uprooter | graft_uprooter | affix suffix: slotgraft_1_uprooter | ported (difference) | seat1 | affix roll test |
| graft_bondbreaker | graft_bondbreaker | affix suffix: slotgraft_1_bondbreaker | ported (difference) | seat1 | affix roll test |
| graft_quailbane | graft_quailbane | affix suffix: slotgraft_1_quailbane | ported (difference) | seat1 | affix roll test |
| graft_iron_grip | graft_iron_grip | affix suffix: slotgraft_1_iron_grip | ported (difference) | seat1 | affix roll test |
| graft_trebuchet_arm | graft_trebuchet_arm | affix suffix: slotgraft_1_trebuchet_arm | ported (difference) | seat1 | affix roll test |
| graft_wringing_grip | graft_wringing_grip | affix suffix: slotgraft_1_wringing_grip | ported (difference) | seat1 | affix roll test |
| graft_exterminator | graft_exterminator | affix suffix: slotgraft_1_exterminator | ported (difference) | seat1 | affix roll test |
| graft_serrated_edge | graft_serrated_edge | affix suffix: slotgraft_1_serrated_edge | ported (difference) | seat1 | affix roll test |
| graft_envenomed_tips | graft_envenomed_tips | affix suffix: slotgraft_1_envenomed_tips | ported (difference) | seat1 | affix roll test |
| graft_fowlers_eye | graft_fowlers_eye | affix suffix: slotgraft_1_fowlers_eye | ported (difference) | seat1 | affix roll test |
| graft_ringing_report | graft_ringing_report | affix suffix: slotgraft_1_ringing_report | ported (difference) | seat1 | affix roll test |
| graft_smothering_spores | graft_smothering_spores | affix suffix: slotgraft_1_smothering_spores | ported (difference) | seat1 | affix roll test |
| graft_loose_thread | graft_loose_thread | affix suffix: slotgraft_1_loose_thread | ported (difference) | seat1 | affix roll test |
| graft_entropic_bloom | graft_entropic_bloom | affix suffix: slotgraft_1_entropic_bloom | ported (difference) | seat1 | affix roll test |
| graft_grasping_tendrils | graft_grasping_tendrils | affix suffix: slotgraft_1_grasping_tendrils | ported (difference) | seat1 | affix roll test |
| graft_parasitic_pact | graft_parasitic_pact | affix suffix: slotgraft_1_parasitic_pact | ported (difference) | seat1 | affix roll test |
| graft_held_note | graft_held_note | affix suffix: slotgraft_1_held_note | ported (difference) | seat1 | affix roll test |
| graft_countermelody | graft_countermelody | affix suffix: slotgraft_1_countermelody | ported (difference) | seat1 | affix roll test |
| graft_rising_chorus | graft_rising_chorus | affix suffix: slotgraft_1_rising_chorus | ported (difference) | seat1 | affix roll test |
| graft_lingering_moment | graft_lingering_moment | affix suffix: slotgraft_1_lingering_moment | ported (difference) | seat1 | affix roll test |
| graft_borrowed_haste | graft_borrowed_haste | affix suffix: slotgraft_1_borrowed_haste | ported (difference) | seat1 | affix roll test |
| graft_commanding_presence | graft_commanding_presence | affix suffix: slotgraft_1_commanding_presence | ported (difference) | seat1 | affix roll test |
| graft_packed_workshop | graft_packed_workshop | affix suffix: slotgraft_1_packed_workshop | ported (difference) | seat1 | affix roll test |
| graft_overwound_mechanism | graft_overwound_mechanism | affix suffix: slotgraft_1_overwound_mechanism | ported (difference) | seat1 | affix roll test |
| graft_hair_trigger | graft_hair_trigger | affix suffix: slotgraft_1_hair_trigger | ported (difference) | seat1 | affix roll test |
| graft_tinkers_arsenal | graft_tinkers_arsenal | affix suffix: slotgraft_1_tinkers_arsenal | ported (difference) | seat1 | affix roll test |
| graft_chaotic_discharge | graft_chaotic_discharge | affix suffix: slotgraft_1_chaotic_discharge | ported (difference) | seat1 | affix roll test |
| graft_fortunes_favor | graft_fortunes_favor | affix suffix: slotgraft_1_fortunes_favor | ported (difference) | seat1 | affix roll test |
| graft_jinxing_touch | graft_jinxing_touch | affix suffix: slotgraft_1_jinxing_touch | ported (difference) | seat1 | affix roll test |
| graft_tempest_range | graft_tempest_range | affix suffix: slotgraft_1_tempest_range | ported (difference) | seat1 | affix roll test |
| graft_steady_current | graft_steady_current | affix suffix: slotgraft_1_steady_current | ported (difference) | seat1 | affix roll test |
| graft_loaded_dice | graft_loaded_dice | affix suffix: slotgraft_1_loaded_dice | ported (difference) | seat1 | affix roll test |
| graft_overload | graft_overload | affix suffix: slotgraft_1_overload | ported (difference) | seat1 | affix roll test |
| graft_unstable_compression | graft_unstable_compression | affix suffix: slotgraft_1_unstable_compression | ported (difference) | seat1 | affix roll test |
| graft_parting_gift | graft_parting_gift | affix suffix: slotgraft_1_parting_gift | ported (difference) | seat1 | affix roll test |
| graft_epidemic | graft_epidemic | affix suffix: slotgraft_1_epidemic | ported (difference) | seat1 | affix roll test |
| graft_umbral_power | graft_umbral_power | affix suffix: slotgraft_1_umbral_power | ported (difference) | seat1 | affix roll test |
| graft_umbral_focus | graft_umbral_focus | affix suffix: slotgraft_1_umbral_focus | ported (difference) | seat1 | affix roll test |
| graft_brimstone_tithe | graft_brimstone_tithe | affix suffix: slotgraft_1_brimstone_tithe | ported (difference) | seat1 | affix roll test |
| graft_tormentors_glee | graft_tormentors_glee | affix suffix: slotgraft_1_tormentors_glee | ported (difference) | seat1 | affix roll test |
| graft_hellhunger | graft_hellhunger | affix suffix: slotgraft_1_hellhunger | ported (difference) | seat1 | affix roll test |
| graft_putrefaction | graft_putrefaction | affix suffix: slotgraft_1_putrefaction | ported (difference) | seat1 | affix roll test |
| graft_calcifying | graft_calcifying | affix suffix: slotgraft_1_calcifying | ported (difference) | seat1 | affix roll test |
| graft_refraction | graft_refraction | affix suffix: slotgraft_1_refraction | ported (difference) | seat1 | affix roll test |
| graft_barbed_snare | graft_barbed_snare | affix suffix: slotgraft_1_barbed_snare | ported (difference) | seat1 | affix roll test |
| graft_brood_tender | graft_brood_tender | affix suffix: slotgraft_1_brood_tender | ported (difference) | seat1 | affix roll test |
| graft_wound_worrier | graft_wound_worrier | affix suffix: slotgraft_1_wound_worrier | ported (difference) | seat1 | affix roll test |
| graft_sanguine_feast | graft_sanguine_feast | affix suffix: slotgraft_1_sanguine_feast | ported (difference) | seat1 | affix roll test |
| graft_mending_echoes | graft_mending_echoes | affix suffix: slotgraft_1_mending_echoes | ported (difference) | seat1 | affix roll test |
| graft_unyielding_stance | graft_unyielding_stance | affix suffix: slotgraft_1_unyielding_stance | ported (difference) | seat1 | affix roll test |
| graft_bulwark_of_thorns | graft_bulwark_of_thorns | affix suffix: slotgraft_1_bulwark_of_thorns | ported (difference) | seat1 | affix roll test |
| graft_counterweight | graft_counterweight | affix suffix: slotgraft_1_counterweight | ported (difference) | seat1 | affix roll test |
| graft_shieldwall_doctrine | graft_shieldwall_doctrine | affix suffix: slotgraft_1_shieldwall_doctrine | ported (difference) | seat1 | affix roll test |
| graft_volcanic_heart | graft_volcanic_heart | affix suffix: slotgraft_1_volcanic_heart | ported (difference) | seat1 | affix roll test |
| graft_tectonic_echoes | graft_tectonic_echoes | affix suffix: slotgraft_1_tectonic_echoes | ported (difference) | seat1 | affix roll test |
| graft_seismic_waltz | graft_seismic_waltz | affix suffix: slotgraft_1_seismic_waltz | ported (difference) | seat1 | affix roll test |
| graft_restless_wound | graft_restless_wound | affix suffix: slotgraft_1_restless_wound | ported (difference) | seat1 | affix roll test |
| graft_orbital_fault | graft_orbital_fault | affix suffix: slotgraft_1_orbital_fault | ported (difference) | seat1 | affix roll test |
| graft_widening_fault | graft_widening_fault | affix suffix: slotgraft_1_widening_fault | ported (difference) | seat1 | affix roll test |
| graft_serpentine_fault | graft_serpentine_fault | affix suffix: slotgraft_1_serpentine_fault | ported (difference) | seat1 | affix roll test |
| graft_faultfinder | graft_faultfinder | affix suffix: slotgraft_1_faultfinder | ported (difference) | seat1 | affix roll test |
| graft_cast_on_crit | graft_cast_on_crit | affix suffix: slotgraft_1_cast_on_crit | ported (difference) | seat1 | affix roll test |
| graft_cast_on_damage_taken | graft_cast_on_damage_taken | affix suffix: slotgraft_1_cast_on_damage_taken | ported (difference) | seat1 | affix roll test |
| graft_cast_while_channeling | graft_cast_while_channeling | affix suffix: slotgraft_1_cast_while_channeling | ported (difference) | seat1 | affix roll test |
| graft_cast_on_flawless | graft_cast_on_flawless | affix suffix: slotgraft_1_cast_on_flawless | ported (difference) | seat1 | affix roll test |
| graft_cast_while_guarding | graft_cast_while_guarding | affix suffix: slotgraft_1_cast_while_guarding | ported (difference) | seat1 | affix roll test |
| graft_cast_on_overcharge | graft_cast_on_overcharge | affix suffix: slotgraft_1_cast_on_overcharge | ported (difference) | seat1 | affix roll test |
| graft_cast_on_high_roll | graft_cast_on_high_roll | affix suffix: slotgraft_1_cast_on_high_roll | ported (difference) | seat1 | affix roll test |
| graft_culmination | graft_culmination | affix suffix: slotgraft_1_culmination | ported (difference) | seat1 | affix roll test |
| graft_culminant_frenzy | graft_culminant_frenzy | affix suffix: slotgraft_1_culminant_frenzy | ported (difference) | seat1 | affix roll test |
| graft_gathered_casting | graft_gathered_casting | affix suffix: slotgraft_1_gathered_casting | ported (difference) | seat1 | affix roll test |
| graft_sequenced_invocation | graft_sequenced_invocation | affix suffix: slotgraft_1_sequenced_invocation | ported (difference) | seat1 | affix roll test |
| graft_cast_on_ignition | graft_cast_on_ignition | affix suffix: slotgraft_1_cast_on_ignition | ported (difference) | seat1 | affix roll test |
| graft_cast_on_poison | graft_cast_on_poison | affix suffix: slotgraft_1_cast_on_poison | ported (difference) | seat1 | affix roll test |
| graft_cast_on_bleed | graft_cast_on_bleed | affix suffix: slotgraft_1_cast_on_bleed | ported (difference) | seat1 | affix roll test |
| graft_cast_on_block | graft_cast_on_block | affix suffix: slotgraft_1_cast_on_block | ported (difference) | seat1 | affix roll test |
| graft_cast_on_kill | graft_cast_on_kill | affix suffix: slotgraft_1_cast_on_kill | ported (difference) | seat1 | affix roll test |
| graft_carrier_strain | graft_carrier_strain | affix suffix: slotgraft_1_carrier_strain | ported (difference) | seat1 | affix roll test |
| graft_final_bloom | graft_final_bloom | affix suffix: slotgraft_1_final_bloom | ported (difference) | seat1 | affix roll test |
| graft_load_bearing_flaw | graft_load_bearing_flaw | affix suffix: slotgraft_1_load_bearing_flaw | ported (difference) | seat1 | affix roll test |
| graft_unmoored | graft_unmoored | affix suffix: slotgraft_1_unmoored | ported (difference) | seat1 | affix roll test |
| graft_skewering_blows | graft_skewering_blows | affix suffix: slotgraft_1_skewering_blows | ported (difference) | seat1 | affix roll test |
| graft_curse_on_hit | graft_curse_on_hit | affix suffix: slotgraft_1_curse_on_hit | ported (difference) | seat1 | affix roll test |
| graft_answering_steel | graft_answering_steel | affix suffix: slotgraft_1_answering_steel | ported (difference) | seat1 | affix roll test |
| graft_reckless_rampart | graft_reckless_rampart | affix suffix: slotgraft_1_reckless_rampart | ported (difference) | seat1 | affix roll test |
| graft_answering_wall | graft_answering_wall | affix suffix: slotgraft_1_answering_wall | ported (difference) | seat1 | affix roll test |
| graft_hollow_answer | graft_hollow_answer | affix suffix: slotgraft_1_hollow_answer | ported (difference) | seat1 | affix roll test |
| graft_stalwart_rhythm | graft_stalwart_rhythm | affix suffix: slotgraft_1_stalwart_rhythm | ported (difference) | seat1 | affix roll test |
| graft_hiveborn | graft_hiveborn | affix suffix: slotgraft_1_hiveborn | ported (difference) | seat1 | affix roll test |
| graft_ghostly_communion | graft_ghostly_communion | affix suffix: slotgraft_1_ghostly_communion | ported (difference) | seat1 | affix roll test |
| graft_gift_of_the_choir | graft_gift_of_the_choir | affix suffix: slotgraft_1_gift_of_the_choir | ported (difference) | seat1 | affix roll test |
| graft_legion_doctrine | graft_legion_doctrine | affix suffix: slotgraft_1_legion_doctrine | ported (difference) | seat1 | affix roll test |
| graft_fresh_ranks | graft_fresh_ranks | affix suffix: slotgraft_1_fresh_ranks | ported (difference) | seat1 | affix roll test |
| graft_resonance | graft_resonance | affix suffix: slotgraft_1_resonance | ported (difference) | seat1 | affix roll test |
| graft_blood_toll | graft_blood_toll | affix suffix: slotgraft_1_blood_toll | ported (difference) | seat1 | affix roll test |
| graft_dominating_blow | graft_dominating_blow | affix suffix: slotgraft_1_dominating_blow | ported (difference) | seat1 | affix roll test |
| graft_maddening_miasma | graft_maddening_miasma | affix suffix: slotgraft_1_maddening_miasma | ported (difference) | seat1 | affix roll test |
| graft_pulsing_hex | graft_pulsing_hex | affix suffix: slotgraft_1_pulsing_hex | ported (difference) | seat1 | affix roll test |
| graft_creeping_fumes | graft_creeping_fumes | affix suffix: slotgraft_1_creeping_fumes | ported (difference) | seat1 | affix roll test |
| graft_overgrowth | graft_overgrowth | affix suffix: slotgraft_1_overgrowth | ported (difference) | seat1 | affix roll test |
| graft_ebbing_ground | graft_ebbing_ground | affix suffix: slotgraft_1_ebbing_ground | ported (difference) | seat1 | affix roll test |
| graft_blooming_ground | graft_blooming_ground | affix suffix: slotgraft_1_blooming_ground | ported (difference) | seat1 | affix roll test |
| graft_tidal_ground | graft_tidal_ground | affix suffix: slotgraft_1_tidal_ground | ported (difference) | seat1 | affix roll test |
| graft_stillwater_discipline | graft_stillwater_discipline | affix suffix: slotgraft_1_stillwater_discipline | ported (difference) | seat1 | affix roll test |
| graft_overbrim | graft_overbrim | affix suffix: slotgraft_1_overbrim | ported (difference) | seat1 | affix roll test |
| graft_fleeting_devotion | graft_fleeting_devotion | affix suffix: slotgraft_1_fleeting_devotion | ported (difference) | seat1 | affix roll test |
| graft_blood_pact | graft_blood_pact | affix suffix: slotgraft_1_blood_pact | ported (difference) | seat1 | affix roll test |
| graft_dominion_tithe | graft_dominion_tithe | affix suffix: slotgraft_1_dominion_tithe | ported (difference) | seat1 | affix roll test |
| graft_archons_wellspring | graft_archons_wellspring | affix suffix: slotgraft_1_archons_wellspring | ported (difference) | seat1 | affix roll test |
| graft_slow_brew | graft_slow_brew | affix suffix: slotgraft_1_slow_brew | ported (difference) | seat1 | affix roll test |
| graft_consecrated_echo | graft_consecrated_echo | affix suffix: slotgraft_1_consecrated_echo | ported (difference) | seat1 | affix roll test |
| graft_echoing_might | graft_echoing_might | affix suffix: slotgraft_1_echoing_might | ported (difference) | seat1 | affix roll test |
| graft_hallowed_flames | graft_hallowed_flames | affix suffix: slotgraft_1_hallowed_flames | ported (difference) | seat1 | affix roll test |
| graft_warding_flesh | graft_warding_flesh | affix suffix: slotgraft_1_warding_flesh | ported (difference) | seat1 | affix roll test |
| graft_monolith | graft_monolith | affix suffix: slotgraft_1_monolith | ported (difference) | seat1 | affix roll test |
| graft_measured_blows | graft_measured_blows | affix suffix: slotgraft_1_measured_blows | ported (difference) | seat1 | affix roll test |
| graft_point_blank | graft_point_blank | affix suffix: slotgraft_1_point_blank | ported (difference) | seat1 | affix roll test |
| graft_pulsating_missiles | graft_pulsating_missiles | affix suffix: slotgraft_1_pulsating_missiles | ported (difference) | seat1 | affix roll test |
| graft_withering_agony | graft_withering_agony | affix suffix: slotgraft_1_withering_agony | ported (difference) | seat1 | affix roll test |
| graft_creeping_doom | graft_creeping_doom | affix suffix: slotgraft_1_creeping_doom | ported (difference) | seat1 | affix roll test |
| graft_lingering_doom | graft_lingering_doom | affix suffix: slotgraft_1_lingering_doom | ported (difference) | seat1 | affix roll test |
| graft_wrathful_edge | graft_wrathful_edge | affix suffix: slotgraft_1_wrathful_edge | ported (difference) | seat1 | affix roll test |
| graft_grace_of_dawn | graft_grace_of_dawn | affix suffix: slotgraft_1_grace_of_dawn | ported (difference) | seat1 | affix roll test |
| graft_broodclutch | graft_broodclutch | affix suffix: slotgraft_1_broodclutch | ported (difference) | seat1 | affix roll test |
| graft_communion_of_flesh | graft_communion_of_flesh | affix suffix: slotgraft_1_communion_of_flesh | ported (difference) | seat1 | affix roll test |
| graft_last_rites | graft_last_rites | affix suffix: slotgraft_1_last_rites | ported (difference) | seat1 | affix roll test |
| graft_stored_verdict | graft_stored_verdict | affix suffix: slotgraft_1_stored_verdict | ported (difference) | seat1 | affix roll test |
| graft_summon_phantasm | graft_summon_phantasm | affix suffix: slotgraft_1_summon_phantasm | ported (difference) | seat1 | affix roll test |
| graft_teeming_vein | graft_teeming_vein | affix suffix: slotgraft_1_teeming_vein | ported (difference) | seat1 | affix roll test |
| graft_broodbearer | graft_broodbearer | affix suffix: slotgraft_1_broodbearer | ported (difference) | seat1 | affix roll test |
| graft_sainted_ash | graft_sainted_ash | affix suffix: slotgraft_1_sainted_ash | ported (difference) | seat1 | affix roll test |
| graft_septic_bargain | graft_septic_bargain | affix suffix: slotgraft_1_septic_bargain | ported (difference) | seat1 | affix roll test |
| graft_opportunist | graft_opportunist | affix suffix: slotgraft_1_opportunist | ported (difference) | seat1 | affix roll test |
| graft_nettles | graft_nettles | affix suffix: slotgraft_1_nettles | ported (difference) | seat1 | affix roll test |
| graft_eruption_cycle | graft_eruption_cycle | affix suffix: slotgraft_1_eruption_cycle | ported (difference) | seat1 | affix roll test |
| graft_channeled_tempest | graft_channeled_tempest | affix suffix: slotgraft_1_channeled_tempest | ported (difference) | seat1 | affix roll test |
| graft_patient_fury | graft_patient_fury | affix suffix: slotgraft_1_patient_fury | ported (difference) | seat1 | affix roll test |
| graft_spooling | graft_spooling | affix suffix: slotgraft_1_spooling | ported (difference) | seat1 | affix roll test |
| graft_mounting_frenzy | graft_mounting_frenzy | affix suffix: slotgraft_1_mounting_frenzy | ported (difference) | seat1 | affix roll test |
| graft_overcharge | graft_overcharge | affix suffix: slotgraft_1_overcharge | ported (difference) | seat1 | affix roll test |
| graft_perfect_draw | graft_perfect_draw | affix suffix: slotgraft_1_perfect_draw | ported (difference) | seat1 | affix roll test |
| graft_wandering_mark | graft_wandering_mark | affix suffix: slotgraft_1_wandering_mark | ported (difference) | seat1 | affix roll test |
| graft_spark_discipline | graft_spark_discipline | affix suffix: slotgraft_1_spark_discipline | ported (difference) | seat1 | affix roll test |
| graft_dive_bomb | graft_dive_bomb | affix suffix: slotgraft_1_dive_bomb | ported (difference) | seat1 | affix roll test |
| graft_fire_walker | graft_fire_walker | affix suffix: slotgraft_1_fire_walker | ported (difference) | seat1 | affix roll test |
| graft_arcing | graft_arcing | affix suffix: slotgraft_1_arcing | ported (difference) | seat1 | affix roll test |
| graft_static_buildup | graft_static_buildup | affix suffix: slotgraft_1_static_buildup | ported (difference) | seat1 | affix roll test |
| graft_forked_focus | graft_forked_focus | affix suffix: slotgraft_1_forked_focus | ported (difference) | seat1 | affix roll test |
| graft_powderkeg | graft_powderkeg | affix suffix: slotgraft_1_powderkeg | ported (difference) | seat1 | affix roll test |
| graft_nova_release | graft_nova_release | affix suffix: slotgraft_1_nova_release | ported (difference) | seat1 | affix roll test |
| graft_elemental_conduction | graft_elemental_conduction | affix suffix: slotgraft_1_elemental_conduction | ported (difference) | seat1 | affix roll test |
| graft_slow_burn | graft_slow_burn | affix suffix: slotgraft_1_slow_burn | ported (difference) | seat1 | affix roll test |
| graft_perfect_timing | graft_perfect_timing | affix suffix: slotgraft_1_perfect_timing | ported (difference) | seat1 | affix roll test |
| graft_capacitor | graft_capacitor | affix suffix: slotgraft_1_capacitor | ported (difference) | seat1 | affix roll test |
| graft_insulation | graft_insulation | affix suffix: slotgraft_1_insulation | ported (difference) | seat1 | affix roll test |
| graft_desperation | graft_desperation | affix suffix: slotgraft_1_desperation | ported (difference) | seat1 | affix roll test |
| graft_serene_power | graft_serene_power | affix suffix: slotgraft_1_serene_power | ported (difference) | seat1 | affix roll test |
| graft_untouched_might | graft_untouched_might | affix suffix: slotgraft_1_untouched_might | ported (difference) | seat1 | affix roll test |
| graft_remnants | graft_remnants | affix suffix: slotgraft_1_remnants | ported (difference) | seat1 | affix roll test |
| graft_mirror_coating | graft_mirror_coating | affix suffix: slotgraft_1_mirror_coating | ported (difference) | seat1 | affix roll test |
| graft_crimson_harvest | graft_crimson_harvest | affix suffix: slotgraft_1_crimson_harvest | ported (difference) | seat1 | affix roll test |
| graft_azure_harvest | graft_azure_harvest | affix suffix: slotgraft_1_azure_harvest | ported (difference) | seat1 | affix roll test |
| graft_lambent_harvest | graft_lambent_harvest | affix suffix: slotgraft_1_lambent_harvest | ported (difference) | seat1 | affix roll test |
| graft_kindled_wake | graft_kindled_wake | affix suffix: slotgraft_1_kindled_wake | ported (difference) | seat1 | affix roll test |
| graft_gutterglow | graft_gutterglow | affix suffix: slotgraft_1_gutterglow | ported (difference) | seat1 | affix roll test |
| graft_victors_tempo | graft_victors_tempo | affix suffix: slotgraft_1_victors_tempo | ported (difference) | seat1 | affix roll test |
| graft_abundant_harvest | graft_abundant_harvest | affix suffix: slotgraft_1_abundant_harvest | ported (difference) | seat1 | affix roll test |
| graft_guardians_aegis | graft_guardians_aegis | affix suffix: slotgraft_1_guardians_aegis | ported (difference) | seat1 | affix roll test |
| graft_muscle_memory | graft_muscle_memory | affix suffix: slotgraft_1_muscle_memory | ported (difference) | seat1 | affix roll test |
| graft_libation | graft_libation | affix suffix: slotgraft_1_libation | ported (difference) | seat1 | affix roll test |
| graft_acrid_draught | graft_acrid_draught | affix suffix: slotgraft_1_acrid_draught | ported (difference) | seat1 | affix roll test |
| graft_adrenal_decant | graft_adrenal_decant | affix suffix: slotgraft_1_adrenal_decant | ported (difference) | seat1 | affix roll test |
| graft_shared_draught | graft_shared_draught | affix suffix: slotgraft_1_shared_draught | ported (difference) | seat1 | affix roll test |
| graft_chaser | graft_chaser | affix suffix: slotgraft_1_chaser | ported (difference) | seat1 | affix roll test |
| graft_splitting | graft_splitting | affix suffix: slotgraft_1_splitting | ported (difference) | seat1 | affix roll test |
| graft_piercing | graft_piercing | affix suffix: slotgraft_1_piercing | ported (difference) | seat1 | affix roll test |
| graft_concentrated | graft_concentrated | affix suffix: slotgraft_1_concentrated | ported (difference) | seat1 | affix roll test |
| graft_widening | graft_widening | affix suffix: slotgraft_1_widening | ported (difference) | seat1 | affix roll test |
| graft_combustion | graft_combustion | affix suffix: slotgraft_1_combustion | ported (difference) | seat1 | affix roll test |
| graft_commanding | graft_commanding | affix suffix: slotgraft_1_commanding | ported (difference) | seat1 | affix roll test |
| graft_accelerated | graft_accelerated | affix suffix: slotgraft_1_accelerated | ported (difference) | seat1 | affix roll test |
| graft_relentless | graft_relentless | affix suffix: slotgraft_1_relentless | ported (difference) | seat1 | affix roll test |
| graft_brutality | graft_brutality | affix suffix: slotgraft_1_brutality | ported (difference) | seat1 | affix roll test |
| graft_lingering | graft_lingering | affix suffix: slotgraft_1_lingering | ported (difference) | seat1 | affix roll test |
| graft_vampiric | graft_vampiric | affix suffix: slotgraft_1_vampiric | ported (difference) | seat1 | affix roll test |
| graft_precision | graft_precision | affix suffix: slotgraft_1_precision | ported (difference) | seat1 | affix roll test |
| graft_efficiency | graft_efficiency | affix suffix: slotgraft_1_efficiency | ported (difference) | seat1 | affix roll test |
| graft_stunning | graft_stunning | affix suffix: slotgraft_1_stunning | ported (difference) | seat1 | affix roll test |
| graft_brutal_strikes | graft_brutal_strikes | affix suffix: slotgraft_1_brutal_strikes | ported (difference) | seat1 | affix roll test |
| graft_chaining | graft_chaining | affix suffix: slotgraft_1_chaining | ported (difference) | seat1 | affix roll test |
| graft_aftershocks | graft_aftershocks | affix suffix: slotgraft_1_aftershocks | ported (difference) | seat1 | affix roll test |
| graft_buried_charge | graft_buried_charge | affix suffix: slotgraft_1_buried_charge | ported (difference) | seat1 | affix roll test |
| graft_unsettled_earth | graft_unsettled_earth | affix suffix: slotgraft_1_unsettled_earth | ported (difference) | seat1 | affix roll test |
| graft_carried_edge | graft_carried_edge | affix suffix: slotgraft_1_carried_edge | ported (difference) | seat1 | affix roll test |
| graft_reapers_encore | graft_reapers_encore | affix suffix: slotgraft_1_reapers_encore | ported (difference) | seat1 | affix roll test |
| graft_accelerando | graft_accelerando | affix suffix: slotgraft_1_accelerando | ported (difference) | seat1 | affix roll test |
| graft_ritardando | graft_ritardando | affix suffix: slotgraft_1_ritardando | ported (difference) | seat1 | affix roll test |
| graft_square_sigil | graft_square_sigil | affix suffix: slotgraft_1_square_sigil | ported (difference) | seat1 | affix roll test |
| graft_triangle_sigil | graft_triangle_sigil | affix suffix: slotgraft_1_triangle_sigil | ported (difference) | seat1 | affix roll test |
| graft_titanic | graft_titanic | affix suffix: slotgraft_1_titanic | ported (difference) | seat1 | affix roll test |
| graft_legion_call | graft_legion_call | affix suffix: slotgraft_1_legion_call | ported (difference) | seat1 | affix roll test |
| graft_cascading_call | graft_cascading_call | affix suffix: slotgraft_1_cascading_call | ported (difference) | seat1 | affix roll test |
| graft_unstable_flesh | graft_unstable_flesh | affix suffix: slotgraft_1_unstable_flesh | ported (difference) | seat1 | affix roll test |
| graft_ephemeral | graft_ephemeral | affix suffix: slotgraft_1_ephemeral | ported (difference) | seat1 | affix roll test |
| graft_cloudburst | graft_cloudburst | affix suffix: slotgraft_1_cloudburst | ported (difference) | seat1 | affix roll test |
| graft_sustained_barrage | graft_sustained_barrage | affix suffix: slotgraft_1_sustained_barrage | ported (difference) | seat1 | affix roll test |
| graft_corpsefire | graft_corpsefire | affix suffix: slotgraft_1_corpsefire | ported (difference) | seat1 | affix roll test |
| graft_blood_price | graft_blood_price | affix suffix: slotgraft_1_blood_price | ported (difference) | seat1 | affix roll test |
| graft_soul_tether | graft_soul_tether | affix suffix: slotgraft_1_soul_tether | ported (difference) | seat1 | affix roll test |
| graft_colossal | graft_colossal | affix suffix: slotgraft_1_colossal | ported (difference) | seat1 | affix roll test |
| graft_volley | graft_volley | affix suffix: slotgraft_1_volley | ported (difference) | seat1 | affix roll test |
| graft_swiftness | graft_swiftness | affix suffix: slotgraft_1_swiftness | ported (difference) | seat1 | affix roll test |
| graft_seeker | graft_seeker | affix suffix: slotgraft_1_seeker | ported (difference) | seat1 | affix roll test |
| graft_unstable_flight | graft_unstable_flight | affix suffix: slotgraft_1_unstable_flight | ported (difference) | seat1 | affix roll test |
| graft_clamor | graft_clamor | affix suffix: slotgraft_1_clamor | ported (difference) | seat1 | affix roll test |
| graft_quiet_hand | graft_quiet_hand | affix suffix: slotgraft_1_quiet_hand | ported (difference) | seat1 | affix roll test |
| graft_beckoning | graft_beckoning | affix suffix: slotgraft_1_beckoning | ported (difference) | seat1 | affix roll test |
| graft_sunbaked_edge | graft_sunbaked_edge | affix suffix: slotgraft_1_sunbaked_edge | ported (difference) | seat1 | affix roll test |
| graft_noonglass | graft_noonglass | affix suffix: slotgraft_1_noonglass | ported (difference) | seat1 | affix roll test |
| graft_scouring_grit | graft_scouring_grit | affix suffix: slotgraft_1_scouring_grit | ported (difference) | seat1 | affix roll test |
| graft_crosswind | graft_crosswind | affix suffix: slotgraft_1_crosswind | ported (difference) | seat1 | affix roll test |
| graft_updraft | graft_updraft | affix suffix: slotgraft_1_updraft | ported (difference) | seat1 | affix roll test |
| graft_slipstream | graft_slipstream | affix suffix: slotgraft_1_slipstream | ported (difference) | seat1 | affix roll test |
| graft_cloudborne | graft_cloudborne | affix suffix: slotgraft_1_cloudborne | ported (difference) | seat1 | affix roll test |
| graft_thunderhead | graft_thunderhead | affix suffix: slotgraft_1_thunderhead | ported (difference) | seat1 | affix roll test |
| graft_silver_lining | graft_silver_lining | affix suffix: slotgraft_1_silver_lining | ported (difference) | seat1 | affix roll test |
| graft_slow_weather | graft_slow_weather | affix suffix: slotgraft_1_slow_weather | ported (difference) | seat1 | affix roll test |
| graft_tethered_orbit | graft_tethered_orbit | affix suffix: slotgraft_1_tethered_orbit | ported (difference) | seat1 | affix roll test |
| graft_gyre | graft_gyre | affix suffix: slotgraft_1_gyre | ported (difference) | seat1 | affix roll test |
| graft_vortex | graft_vortex | affix suffix: slotgraft_1_vortex | ported (difference) | seat1 | affix roll test |
| graft_sidewinder | graft_sidewinder | affix suffix: slotgraft_1_sidewinder | ported (difference) | seat1 | affix roll test |
| graft_momentum | graft_momentum | affix suffix: slotgraft_1_momentum | ported (difference) | seat1 | affix roll test |
| graft_trueflight | graft_trueflight | affix suffix: slotgraft_1_trueflight | ported (difference) | seat1 | affix roll test |
| graft_shrapnel_gem | graft_shrapnel_gem | affix suffix: slotgraft_1_shrapnel_gem | ported (difference) | seat1 | affix roll test |
| graft_fulminate | graft_fulminate | affix suffix: slotgraft_1_fulminate | ported (difference) | seat1 | affix roll test |
| graft_lineage | graft_lineage | affix suffix: slotgraft_1_lineage | ported (difference) | seat1 | affix roll test |
| graft_cascade_of_knives | graft_cascade_of_knives | affix suffix: slotgraft_1_cascade_of_knives | ported (difference) | seat1 | affix roll test |
| graft_plus_fire | graft_plus_fire | affix suffix: slotgraft_1_plus_fire | ported (difference) | seat1 | affix roll test |
| graft_plus_cold | graft_plus_cold | affix suffix: slotgraft_1_plus_cold | ported (difference) | seat1 | affix roll test |
| graft_plus_lightning | graft_plus_lightning | affix suffix: slotgraft_1_plus_lightning | ported (difference) | seat1 | affix roll test |
| graft_plus_projectile | graft_plus_projectile | affix suffix: slotgraft_1_plus_projectile | ported (difference) | seat1 | affix roll test |
| graft_plus_minion | graft_plus_minion | affix suffix: slotgraft_1_plus_minion | ported (difference) | seat1 | affix roll test |
| graft_searing_heat | graft_searing_heat | affix suffix: slotgraft_1_searing_heat | ported (difference) | seat1 | affix roll test |
| graft_biting_cold | graft_biting_cold | affix suffix: slotgraft_1_biting_cold | ported (difference) | seat1 | affix roll test |
| graft_static_charge | graft_static_charge | affix suffix: slotgraft_1_static_charge | ported (difference) | seat1 | affix roll test |
| graft_corrosion | graft_corrosion | affix suffix: slotgraft_1_corrosion | ported (difference) | seat1 | affix roll test |
| graft_ruthless | graft_ruthless | affix suffix: slotgraft_1_ruthless | ported (difference) | seat1 | affix roll test |
| graft_polyphony | graft_polyphony | affix suffix: slotgraft_1_polyphony | ported (difference) | seat1 | affix roll test |
| graft_ostinato | graft_ostinato | affix suffix: slotgraft_1_ostinato | ported (difference) | seat1 | affix roll test |
| graft_keen_study | graft_keen_study | affix suffix: slotgraft_1_keen_study | ported (difference) | seat1 | affix roll test |
| graft_understudy | graft_understudy | affix suffix: slotgraft_1_understudy | ported (difference) | seat1 | affix roll test |
| graft_iron_trance | graft_iron_trance | affix suffix: slotgraft_1_iron_trance | ported (difference) | seat1 | affix roll test |
| graft_long_communion | graft_long_communion | affix suffix: slotgraft_1_long_communion | ported (difference) | seat1 | affix roll test |
| graft_vicious_brood | graft_vicious_brood | affix suffix: slotgraft_1_vicious_brood | ported (difference) | seat1 | affix roll test |
| graft_hardy_brood | graft_hardy_brood | affix suffix: slotgraft_1_hardy_brood | ported (difference) | seat1 | affix roll test |
| graft_patient_brood | graft_patient_brood | affix suffix: slotgraft_1_patient_brood | ported (difference) | seat1 | affix roll test |
| graft_hidden_reserves | graft_hidden_reserves | affix suffix: slotgraft_1_hidden_reserves | ported (difference) | seat1 | affix roll test |
| graft_teeming_warrens | graft_teeming_warrens | affix suffix: slotgraft_1_teeming_warrens | ported (difference) | seat1 | affix roll test |
| graft_chitinous_brood | graft_chitinous_brood | affix suffix: slotgraft_1_chitinous_brood | ported (difference) | seat1 | affix roll test |
| graft_calcified_vigor | graft_calcified_vigor | affix suffix: slotgraft_1_calcified_vigor | ported (difference) | seat1 | affix roll test |
| graft_marrowbound_vigor | graft_marrowbound_vigor | affix suffix: slotgraft_1_marrowbound_vigor | ported (difference) | seat1 | affix roll test |
| graft_meat_shield | graft_meat_shield | affix suffix: slotgraft_1_meat_shield | ported (difference) | seat1 | affix roll test |
| graft_bodyguard_doctrine | graft_bodyguard_doctrine | affix suffix: slotgraft_1_bodyguard_doctrine | ported (difference) | seat1 | affix roll test |
| graft_alphas_bond | graft_alphas_bond | affix suffix: slotgraft_1_alphas_bond | ported (difference) | seat1 | affix roll test |
| graft_pack_instinct | graft_pack_instinct | affix suffix: slotgraft_1_pack_instinct | ported (difference) | seat1 | affix roll test |
| graft_reciprocal_bond | graft_reciprocal_bond | affix suffix: slotgraft_1_reciprocal_bond | ported (difference) | seat1 | affix roll test |
| graft_gentling_hand | graft_gentling_hand | affix suffix: slotgraft_1_gentling_hand | ported (difference) | seat1 | affix roll test |
| graft_beast_master | graft_beast_master | affix suffix: slotgraft_1_beast_master | ported (difference) | seat1 | affix roll test |
| graft_tripwire | graft_tripwire | affix suffix: slotgraft_1_tripwire | ported (difference) | seat1 | affix roll test |
| graft_tripwire_web | graft_tripwire_web | affix suffix: slotgraft_1_tripwire_web | ported (difference) | seat1 | affix roll test |
| graft_transient_inferno | graft_transient_inferno | affix suffix: slotgraft_1_transient_inferno | ported (difference) | seat1 | affix roll test |
| graft_lifeline | graft_lifeline | affix suffix: slotgraft_1_lifeline | ported (difference) | seat1 | affix roll test |
| graft_witchfire_leash | graft_witchfire_leash | affix suffix: slotgraft_1_witchfire_leash | ported (difference) | seat1 | affix roll test |
| graft_taut_wire | graft_taut_wire | affix suffix: slotgraft_1_taut_wire | ported (difference) | seat1 | affix roll test |
| graft_charged_span | graft_charged_span | affix suffix: slotgraft_1_charged_span | ported (difference) | seat1 | affix roll test |
| graft_walking_meditation | graft_walking_meditation | affix suffix: slotgraft_1_walking_meditation | ported (difference) | seat1 | affix roll test |
| graft_weathervane | graft_weathervane | affix suffix: slotgraft_1_weathervane | ported (difference) | seat1 | affix roll test |
| graft_turbulence | graft_turbulence | affix suffix: slotgraft_1_turbulence | ported (difference) | seat1 | affix roll test |
| graft_alternating_strikes | graft_alternating_strikes | affix suffix: slotgraft_1_alternating_strikes | ported (difference) | seat1 | affix roll test |
| graft_wild_abandon | graft_wild_abandon | affix suffix: slotgraft_1_wild_abandon | ported (difference) | seat1 | affix roll test |
| graft_measured_blade | graft_measured_blade | affix suffix: slotgraft_1_measured_blade | ported (difference) | seat1 | affix roll test |
| graft_fleetfoot | graft_fleetfoot | affix suffix: slotgraft_1_fleetfoot | ported (difference) | seat1 | affix roll test |
| graft_spell_cascade | graft_spell_cascade | affix suffix: slotgraft_1_spell_cascade | ported (difference) | seat1 | affix roll test |
| graft_scattered_cascade | graft_scattered_cascade | affix suffix: slotgraft_1_scattered_cascade | ported (difference) | seat1 | affix roll test |
| graft_seismic_march | graft_seismic_march | affix suffix: slotgraft_1_seismic_march | ported (difference) | seat1 | affix roll test |
| graft_resounding_echo | graft_resounding_echo | affix suffix: slotgraft_1_resounding_echo | ported (difference) | seat1 | affix roll test |
| graft_rattling_salvo | graft_rattling_salvo | affix suffix: slotgraft_1_rattling_salvo | ported (difference) | seat1 | affix roll test |
| graft_firing_line | graft_firing_line | affix suffix: slotgraft_1_firing_line | ported (difference) | seat1 | affix roll test |
| graft_close_order | graft_close_order | affix suffix: slotgraft_1_close_order | ported (difference) | seat1 | affix roll test |
| graft_choked_spread | graft_choked_spread | affix suffix: slotgraft_1_choked_spread | ported (difference) | seat1 | affix roll test |
| graft_cutthroat | graft_cutthroat | affix suffix: slotgraft_1_cutthroat | ported (difference) | seat1 | affix roll test |
| graft_displaced_conjuring | graft_displaced_conjuring | affix suffix: slotgraft_1_displaced_conjuring | ported (difference) | seat1 | affix roll test |
| graft_puppet_strings | graft_puppet_strings | affix suffix: slotgraft_1_puppet_strings | ported (difference) | seat1 | affix roll test |
| graft_detonating_passage | graft_detonating_passage | affix suffix: slotgraft_1_detonating_passage | ported (difference) | seat1 | affix roll test |
| graft_scorched_wake | graft_scorched_wake | affix suffix: slotgraft_1_scorched_wake | ported (difference) | seat1 | affix roll test |
| graft_sloughing_wake | graft_sloughing_wake | affix suffix: slotgraft_1_sloughing_wake | ported (difference) | seat1 | affix roll test |
| graft_time_fuse | graft_time_fuse | affix suffix: slotgraft_1_time_fuse | ported (difference) | seat1 | affix roll test |
| graft_slow_match | graft_slow_match | affix suffix: slotgraft_1_slow_match | ported (difference) | seat1 | affix roll test |
| graft_grafted_carapace | graft_grafted_carapace | affix suffix: slotgraft_1_grafted_carapace | ported (difference) | seat1 | affix roll test |
| graft_suppuration | graft_suppuration | affix suffix: slotgraft_1_suppuration | ported (difference) | seat1 | affix roll test |
| graft_heavy_caliber | graft_heavy_caliber | affix suffix: slotgraft_1_heavy_caliber | ported (difference) | seat1 | affix roll test |
| graft_ignite_chance | graft_ignite_chance | affix suffix: slotgraft_1_ignite_chance | ported (difference) | seat1 | affix roll test |
| graft_bleed_chance | graft_bleed_chance | affix suffix: slotgraft_1_bleed_chance | ported (difference) | seat1 | affix roll test |
| graft_poison_chance | graft_poison_chance | affix suffix: slotgraft_1_poison_chance | ported (difference) | seat1 | affix roll test |
| graft_chill_chance | graft_chill_chance | affix suffix: slotgraft_1_chill_chance | ported (difference) | seat1 | affix roll test |
| graft_shock_chance | graft_shock_chance | affix suffix: slotgraft_1_shock_chance | ported (difference) | seat1 | affix roll test |
| graft_stun_chance | graft_stun_chance | affix suffix: slotgraft_1_stun_chance | ported (difference) | seat1 | affix roll test |
| graft_freeze_chance | graft_freeze_chance | affix suffix: slotgraft_1_freeze_chance | ported (difference) | seat1 | affix roll test |
| graft_provocation | graft_provocation | affix suffix: slotgraft_1_provocation | ported (difference) | seat1 | affix roll test |
| graft_unnerving | graft_unnerving | affix suffix: slotgraft_1_unnerving | ported (difference) | seat1 | affix roll test |
| graft_haunted_service | graft_haunted_service | affix suffix: slotgraft_1_haunted_service | ported (difference) | seat1 | affix roll test |
| graft_potency | graft_potency | affix suffix: slotgraft_1_potency | ported (difference) | seat1 | affix roll test |
| graft_conflagrant | graft_conflagrant | affix suffix: slotgraft_1_conflagrant | ported (difference) | seat1 | affix roll test |
| graft_flameforged | graft_flameforged | affix suffix: slotgraft_1_flameforged | ported (difference) | seat1 | affix roll test |
| graft_multistrike | graft_multistrike | affix suffix: slotgraft_1_multistrike | ported (difference) | seat1 | affix roll test |
| graft_spell_echo | graft_spell_echo | affix suffix: slotgraft_1_spell_echo | ported (difference) | seat1 | affix roll test |
| graft_cascade | graft_cascade | affix suffix: slotgraft_1_cascade | ported (difference) | seat1 | affix roll test |
| graft_crescendo | graft_crescendo | affix suffix: slotgraft_1_crescendo | ported (difference) | seat1 | affix roll test |
| graft_unleash | graft_unleash | affix suffix: slotgraft_1_unleash | ported (difference) | seat1 | affix roll test |
| graft_reverberation | graft_reverberation | affix suffix: slotgraft_1_reverberation | ported (difference) | seat1 | affix roll test |
| graft_splash | graft_splash | affix suffix: slotgraft_1_splash | ported (difference) | seat1 | affix roll test |
| graft_virulence | graft_virulence | affix suffix: slotgraft_1_virulence | ported (difference) | seat1 | affix roll test |
| graft_hex_blast | graft_hex_blast | affix suffix: slotgraft_1_hex_blast | ported (difference) | seat1 | affix roll test |
| graft_no_mans_land | graft_no_mans_land | affix suffix: slotgraft_1_no_mans_land | ported (difference) | seat1 | affix roll test |
| graft_heavy_musk | graft_heavy_musk | affix suffix: slotgraft_1_heavy_musk | ported (difference) | seat1 | affix roll test |
| graft_candied_scent | graft_candied_scent | affix suffix: slotgraft_1_candied_scent | ported (difference) | seat1 | affix roll test |
| graft_startling_reek | graft_startling_reek | affix suffix: slotgraft_1_startling_reek | ported (difference) | seat1 | affix roll test |
| graft_malfeasance | graft_malfeasance | affix suffix: slotgraft_1_malfeasance | ported (difference) | seat1 | affix roll test |
| graft_malpractice | graft_malpractice | affix suffix: slotgraft_1_malpractice | ported (difference) | seat1 | affix roll test |
| graft_hedonism | graft_hedonism | affix suffix: slotgraft_1_hedonism | ported (difference) | seat1 | affix roll test |
| graft_forking | graft_forking | affix suffix: slotgraft_1_forking | ported (difference) | seat1 | affix roll test |
| graft_returning | graft_returning | affix suffix: slotgraft_1_returning | ported (difference) | seat1 | affix roll test |
| graft_boomerang | graft_boomerang | affix suffix: slotgraft_1_boomerang | ported (difference) | seat1 | affix roll test |
| graft_belligerence_gem | graft_belligerence_gem | affix suffix: slotgraft_1_belligerence_gem | ported (difference) | seat1 | affix roll test |
| graft_furor_gem | graft_furor_gem | affix suffix: slotgraft_1_furor_gem | ported (difference) | seat1 | affix roll test |
| graft_undying_loyalty | graft_undying_loyalty | affix suffix: slotgraft_1_undying_loyalty | ported (difference) | seat1 | affix roll test |
| graft_soulwalk | graft_soulwalk | affix suffix: slotgraft_1_soulwalk | ported (difference) | seat1 | affix roll test |
| graft_sacrificial_rites | graft_sacrificial_rites | affix suffix: slotgraft_1_sacrificial_rites | ported (difference) | seat1 | affix roll test |
| graft_corpse_wagon | graft_corpse_wagon | affix suffix: slotgraft_1_corpse_wagon | ported (difference) | seat1 | affix roll test |
| graft_spirit_totem | graft_spirit_totem | affix suffix: slotgraft_1_spirit_totem | ported (difference) | seat1 | affix roll test |
| graft_pinpoint | graft_pinpoint | affix suffix: slotgraft_1_pinpoint | ported (difference) | seat1 | affix roll test |
| graft_alacrity | graft_alacrity | affix suffix: slotgraft_1_alacrity | ported (difference) | seat1 | affix roll test |
| graft_phantasmal_echo | graft_phantasmal_echo | affix suffix: slotgraft_1_phantasmal_echo | ported (difference) | seat1 | affix roll test |
| graft_ancestral_call | graft_ancestral_call | affix suffix: slotgraft_1_ancestral_call | ported (difference) | seat1 | affix roll test |
| graft_vessel_of_shadow | graft_vessel_of_shadow | affix suffix: slotgraft_1_vessel_of_shadow | ported (difference) | seat1 | affix roll test |
| graft_synchronicity | graft_synchronicity | affix suffix: slotgraft_1_synchronicity | ported (difference) | seat1 | affix roll test |
| graft_sweeping_blow | graft_sweeping_blow | affix suffix: slotgraft_1_sweeping_blow | ported (difference) | seat1 | affix roll test |
| graft_mana_feeder | graft_mana_feeder | affix suffix: slotgraft_1_mana_feeder | ported (difference) | seat1 | affix roll test |
| graft_enduring_bond | graft_enduring_bond | affix suffix: slotgraft_1_enduring_bond | ported (difference) | seat1 | affix roll test |
| graft_vital_bond | graft_vital_bond | affix suffix: slotgraft_1_vital_bond | ported (difference) | seat1 | affix roll test |
| graft_bloodletters_rhythm | graft_bloodletters_rhythm | affix suffix: slotgraft_1_bloodletters_rhythm | ported (difference) | seat1 | affix roll test |
| graft_exposure | graft_exposure | affix suffix: slotgraft_1_exposure | ported (difference) | seat1 | affix roll test |
| graft_bristling_riposte | graft_bristling_riposte | affix suffix: slotgraft_1_bristling_riposte | ported (difference) | seat1 | affix roll test |
| graft_intensive_care | graft_intensive_care | affix suffix: slotgraft_1_intensive_care | ported (difference) | seat1 | affix roll test |
| graft_mending_chain | graft_mending_chain | affix suffix: slotgraft_1_mending_chain | ported (difference) | seat1 | affix roll test |
| graft_overmend | graft_overmend | affix suffix: slotgraft_1_overmend | ported (difference) | seat1 | affix roll test |
| graft_fragmentation | graft_fragmentation | affix suffix: slotgraft_1_fragmentation | ported (difference) | seat1 | affix roll test |
| graft_bulwark_shards | graft_bulwark_shards | affix suffix: slotgraft_1_bulwark_shards | ported (difference) | seat1 | affix roll test |
| graft_rage_remnants | graft_rage_remnants | affix suffix: slotgraft_1_rage_remnants | ported (difference) | seat1 | affix roll test |
| graft_remnant_conduit | graft_remnant_conduit | affix suffix: slotgraft_1_remnant_conduit | ported (difference) | seat1 | affix roll test |
| graft_metronome | graft_metronome | affix suffix: slotgraft_1_metronome | ported (difference) | seat1 | affix roll test |
| graft_colossus_stance | graft_colossus_stance | affix suffix: slotgraft_1_colossus_stance | ported (difference) | seat1 | affix roll test |
| graft_transfusion_bond | graft_transfusion_bond | affix suffix: slotgraft_1_transfusion_bond | ported (difference) | seat1 | affix roll test |
| graft_controlled_burn | graft_controlled_burn | affix suffix: slotgraft_1_controlled_burn | ported (difference) | seat1 | affix roll test |
| graft_anchored_focus | graft_anchored_focus | affix suffix: slotgraft_1_anchored_focus | ported (difference) | seat1 | affix roll test |
| graft_reckless_breadth | graft_reckless_breadth | affix suffix: slotgraft_1_reckless_breadth | ported (difference) | seat1 | affix roll test |
| graft_communal_rites | graft_communal_rites | affix suffix: slotgraft_1_communal_rites | ported (difference) | seat1 | affix roll test |
| graft_deep_reserves | graft_deep_reserves | affix suffix: slotgraft_1_deep_reserves | ported (difference) | seat1 | affix roll test |
| graft_bandolier | graft_bandolier | affix suffix: slotgraft_1_bandolier | ported (difference) | seat1 | affix roll test |
| graft_swift_hands | graft_swift_hands | affix suffix: slotgraft_1_swift_hands | ported (difference) | seat1 | affix roll test |
| graft_dead_mans_round | graft_dead_mans_round | affix suffix: slotgraft_1_dead_mans_round | ported (difference) | seat1 | affix roll test |
| graft_chambered_casting | graft_chambered_casting | affix suffix: slotgraft_1_chambered_casting | ported (difference) | seat1 | affix roll test |
| graft_ricochet | graft_ricochet | affix suffix: slotgraft_1_ricochet | ported (difference) | seat1 | affix roll test |
| graft_mirrored_bounds | graft_mirrored_bounds | affix suffix: slotgraft_1_mirrored_bounds | ported (difference) | seat1 | affix roll test |
| graft_recurve | graft_recurve | affix suffix: slotgraft_1_recurve | ported (difference) | seat1 | affix roll test |
| graft_shredding_return | graft_shredding_return | affix suffix: slotgraft_1_shredding_return | ported (difference) | seat1 | affix roll test |
| graft_wraith_passage | graft_wraith_passage | affix suffix: slotgraft_1_wraith_passage | ported (difference) | seat1 | affix roll test |
| graft_pulsing_ramparts | graft_pulsing_ramparts | affix suffix: slotgraft_1_pulsing_ramparts | ported (difference) | seat1 | affix roll test |
| graft_violent_genesis | graft_violent_genesis | affix suffix: slotgraft_1_violent_genesis | ported (difference) | seat1 | affix roll test |
| graft_orbital_sweep | graft_orbital_sweep | affix suffix: slotgraft_1_orbital_sweep | ported (difference) | seat1 | affix roll test |
| graft_whirling_sigil | graft_whirling_sigil | affix suffix: slotgraft_1_whirling_sigil | ported (difference) | seat1 | affix roll test |
| graft_enduring_snares | graft_enduring_snares | affix suffix: slotgraft_1_enduring_snares | ported (difference) | seat1 | affix roll test |
| graft_splintered_earth | graft_splintered_earth | affix suffix: slotgraft_1_splintered_earth | ported (difference) | seat1 | affix roll test |
| graft_branching_fissures | graft_branching_fissures | affix suffix: slotgraft_1_branching_fissures | ported (difference) | seat1 | affix roll test |
| graft_suffusion | graft_suffusion | affix suffix: slotgraft_1_suffusion | ported (difference) | seat1 | affix roll test |
| graft_deliberate_ruin | graft_deliberate_ruin | affix suffix: slotgraft_1_deliberate_ruin | ported (difference) | seat1 | affix roll test |
| graft_grim_sentence | graft_grim_sentence | affix suffix: slotgraft_1_grim_sentence | ported (difference) | seat1 | affix roll test |
| graft_command_gem | graft_command_gem | affix suffix: slotgraft_1_command_gem | ported (difference) | seat1 | affix roll test |
| graft_self_destruct | graft_self_destruct | affix suffix: slotgraft_1_self_destruct | ported (difference) | seat1 | affix roll test |
| graft_shatterrite | graft_shatterrite | affix suffix: slotgraft_1_shatterrite | ported (difference) | seat1 | affix roll test |
| graft_beckon_beyond | graft_beckon_beyond | affix suffix: slotgraft_1_beckon_beyond | ported (difference) | seat1 | affix roll test |
| graft_violent_arrival | graft_violent_arrival | affix suffix: slotgraft_1_violent_arrival | ported (difference) | seat1 | affix roll test |
| graft_pyre_legion | graft_pyre_legion | affix suffix: slotgraft_1_pyre_legion | ported (difference) | seat1 | affix roll test |
| graft_ritual_ground | graft_ritual_ground | affix suffix: slotgraft_1_ritual_ground | ported (difference) | seat1 | affix roll test |
| graft_closing_instinct | graft_closing_instinct | affix suffix: slotgraft_1_closing_instinct | ported (difference) | seat1 | affix roll test |
| graft_risen_offering | graft_risen_offering | affix suffix: slotgraft_1_risen_offering | ported (difference) | seat1 | affix roll test |
| graft_sundering_flight | graft_sundering_flight | affix suffix: slotgraft_1_sundering_flight | ported (difference) | seat1 | affix roll test |
| graft_skeletal_strike | graft_skeletal_strike | affix suffix: slotgraft_1_skeletal_strike | ported (difference) | seat1 | affix roll test |
| graft_ravenous_pact | graft_ravenous_pact | affix suffix: slotgraft_1_ravenous_pact | ported (difference) | seat1 | affix roll test |
| graft_miasma | graft_miasma | affix suffix: slotgraft_1_miasma | ported (difference) | seat1 | affix roll test |
| graft_miasmic_ground | graft_miasmic_ground | affix suffix: slotgraft_1_miasmic_ground | ported (difference) | seat1 | affix roll test |
| graft_ravening | graft_ravening | affix suffix: slotgraft_1_ravening | ported (difference) | seat1 | affix roll test |
| graft_embargo | graft_embargo | affix suffix: slotgraft_1_embargo | ported (difference) | seat1 | affix roll test |
| graft_austerity | graft_austerity | affix suffix: slotgraft_1_austerity | ported (difference) | seat1 | affix roll test |
| graft_apotheosis | graft_apotheosis | affix suffix: slotgraft_1_apotheosis | ported (difference) | seat1 | affix roll test |
| graft_return_stroke | graft_return_stroke | affix suffix: slotgraft_1_return_stroke | ported (difference) | seat1 | affix roll test |
| graft_phalanx | graft_phalanx | affix suffix: slotgraft_1_phalanx | ported (difference) | seat1 | affix roll test |
| graft_guarded_casting | graft_guarded_casting | affix suffix: slotgraft_1_guarded_casting | ported (difference) | seat1 | affix roll test |
| graft_stoneblood_conduit | graft_stoneblood_conduit | affix suffix: slotgraft_1_stoneblood_conduit | ported (difference) | seat1 | affix roll test |
| graft_reckless_transfusion | graft_reckless_transfusion | affix suffix: slotgraft_1_reckless_transfusion | ported (difference) | seat1 | affix roll test |
| graft_bulwarks_tithe | graft_bulwarks_tithe | affix suffix: slotgraft_1_bulwarks_tithe | ported (difference) | seat1 | affix roll test |
| graft_sanguine_feed | graft_sanguine_feed | affix suffix: slotgraft_1_sanguine_feed | ported (difference) | seat1 | affix roll test |
| graft_crystal_cistern | graft_crystal_cistern | affix suffix: slotgraft_1_crystal_cistern | ported (difference) | seat1 | affix roll test |
| graft_thoughtburn | graft_thoughtburn | affix suffix: slotgraft_1_thoughtburn | ported (difference) | seat1 | affix roll test |
| graft_overflow_reservoir | graft_overflow_reservoir | affix suffix: slotgraft_1_overflow_reservoir | ported (difference) | seat1 | affix roll test |
| graft_stillmind_conduit | graft_stillmind_conduit | affix suffix: slotgraft_1_stillmind_conduit | ported (difference) | seat1 | affix roll test |
| graft_concussive_blows | graft_concussive_blows | affix suffix: slotgraft_1_concussive_blows | ported (difference) | seat1 | affix roll test |
| graft_flowbreaker | graft_flowbreaker | affix suffix: slotgraft_1_flowbreaker | ported (difference) | seat1 | affix roll test |
| graft_voidrend | graft_voidrend | affix suffix: slotgraft_1_voidrend | ported (difference) | seat1 | affix roll test |
| graft_building_rhythm | graft_building_rhythm | affix suffix: slotgraft_1_building_rhythm | ported (difference) | seat1 | affix roll test |
| graft_lightbrand | graft_lightbrand | affix suffix: slotgraft_1_lightbrand | ported (difference) | seat1 | affix roll test |
| graft_terminal_velocity | graft_terminal_velocity | affix suffix: slotgraft_1_terminal_velocity | ported (difference) | seat1 | affix roll test |
| graft_parting_judgement | graft_parting_judgement | affix suffix: slotgraft_1_parting_judgement | ported (difference) | seat1 | affix roll test |
| graft_rarefy | graft_rarefy | affix suffix: slotgraft_1_rarefy | ported (difference) | seat1 | affix roll test |
| graft_pallid_touch | graft_pallid_touch | affix suffix: slotgraft_1_pallid_touch | ported (difference) | seat1 | affix roll test |
| graft_sickening | graft_sickening | affix suffix: slotgraft_1_sickening | ported (difference) | seat1 | affix roll test |
| graft_unblinking | graft_unblinking | affix suffix: slotgraft_1_unblinking | ported (difference) | seat1 | affix roll test |
| graft_maddering | graft_maddering | affix suffix: slotgraft_1_maddering | ported (difference) | seat1 | affix roll test |
| graft_addling | graft_addling | affix suffix: slotgraft_1_addling | ported (difference) | seat1 | affix roll test |
| graft_aureate_writ | graft_aureate_writ | affix suffix: slotgraft_1_aureate_writ | ported (difference) | seat1 | affix roll test |
| graft_sanctal_cautery | graft_sanctal_cautery | affix suffix: slotgraft_1_sanctal_cautery | ported (difference) | seat1 | affix roll test |
| graft_boiling_point | graft_boiling_point | affix suffix: slotgraft_1_boiling_point | ported (difference) | seat1 | affix roll test |
| graft_pressure_seal | graft_pressure_seal | affix suffix: slotgraft_1_pressure_seal | ported (difference) | seat1 | affix roll test |
| graft_afterspray | graft_afterspray | affix suffix: slotgraft_1_afterspray | ported (difference) | seat1 | affix roll test |
| graft_vaporize | graft_vaporize | affix suffix: slotgraft_1_vaporize | ported (difference) | seat1 | affix roll test |
| graft_mineral_tuning | graft_mineral_tuning | affix suffix: slotgraft_1_mineral_tuning | ported (difference) | seat1 | affix roll test |
| graft_executioners_edge | graft_executioners_edge | affix suffix: slotgraft_1_executioners_edge | ported (difference) | seat1 | affix roll test |
| graft_shatterglass | graft_shatterglass | affix suffix: slotgraft_1_shatterglass | ported (difference) | seat1 | affix roll test |
| graft_opportunists_blade | graft_opportunists_blade | affix suffix: slotgraft_1_opportunists_blade | ported (difference) | seat1 | affix roll test |
| graft_assassins_angle | graft_assassins_angle | affix suffix: slotgraft_1_assassins_angle | ported (difference) | seat1 | affix roll test |
| graft_frostbite_grip | graft_frostbite_grip | affix suffix: slotgraft_1_frostbite_grip | ported (difference) | seat1 | affix roll test |
| graft_hunters_momentum | graft_hunters_momentum | affix suffix: slotgraft_1_hunters_momentum | ported (difference) | seat1 | affix roll test |
| graft_steady_nerve | graft_steady_nerve | affix suffix: slotgraft_1_steady_nerve | ported (difference) | seat1 | affix roll test |
| graft_deaths_door | graft_deaths_door | affix suffix: slotgraft_1_deaths_door | ported (difference) | seat1 | affix roll test |
| graft_crowd_favor | graft_crowd_favor | affix suffix: slotgraft_1_crowd_favor | ported (difference) | seat1 | affix roll test |
| graft_at_the_brink | graft_at_the_brink | affix suffix: slotgraft_1_at_the_brink | ported (difference) | seat1 | affix roll test |
| graft_fevered_hands | graft_fevered_hands | affix suffix: slotgraft_1_fevered_hands | ported (difference) | seat1 | affix roll test |
| graft_answered_dodge | graft_answered_dodge | affix suffix: slotgraft_1_answered_dodge | ported (difference) | seat1 | affix roll test |
| graft_desperate_measures | graft_desperate_measures | affix suffix: slotgraft_1_desperate_measures | ported (difference) | seat1 | affix roll test |
| relic_attack_speed | relic_attack_speed | affix suffix: attackSpeed | ported |  | affix roll test |
| relic_cast_speed | relic_cast_speed | affix suffix: castSpeed | ported |  | affix roll test |
| relic_move | relic_move | affix suffix: moveSpeed | ported |  | affix roll test |
| relic_crit | relic_crit | affix suffix: critChance | ported |  | affix roll test |
| relic_crit_multi | relic_crit_multi | affix suffix: critMulti | ported |  | affix roll test |
| relic_regen | relic_regen | affix suffix: lifeRegen | ported |  | affix roll test |
| relic_mana_regen | relic_mana_regen | affix suffix: manaRegen | ported |  | affix roll test |
| relic_res_fire | relic_res_fire | affix suffix: fireRes | ported |  | affix roll test |
| relic_res_cold | relic_res_cold | affix suffix: coldRes | ported |  | affix roll test |
| relic_res_lightning | relic_res_lightning | affix suffix: lightningRes | ported |  | affix roll test |
| relic_res_chaos | relic_res_chaos | affix suffix: chaosRes | ported |  | affix roll test |
| relic_all_res | relic_all_res | affix suffix: fireRes, coldRes, lightningRes | ported |  | affix roll test |
| relic_cooldown | relic_cooldown | affix suffix: cooldownRecovery | ported |  | affix roll test |
| relic_leech | relic_leech | affix suffix: lifeLeech | ported |  | affix roll test |
| relic_luck |  | affix suffix: luck | blocked | seam: no line maps | affix roll test |
| relic_accuracy | relic_accuracy | affix suffix: accuracy | ported |  | affix roll test |
| relic_recovery | relic_recovery | affix suffix: lifeRegen | ported |  | affix roll test |
| relic_escape | relic_escape | affix suffix: moveSpeed | ported |  | affix roll test |
| relic_recharge | relic_recharge | affix suffix: castSpeed | ported |  | affix roll test |
| relic_pursuit | relic_pursuit | affix suffix: attackSpeed | ported |  | affix roll test |

## Uniques (59: ported 59)

| Prototype | Ours | Mechanism | Status | Note | Proof |
|---|---|---|---|---|---|
| wanderers_wake | wanderers_wake | unique on boots_evasion: strideReach, damage, addedPhysical, moveSpeed, evasion | ported |  | unique roll test |
| emberbrand | emberbrand | unique on ring_ruby: skillgrant_firebolt, proc_emberbrand_rekindle, addedFire, damageVs_burn, fireRes | ported |  | unique roll test |
| duelists_ledger | duelists_ledger | unique on chest_evasion: evasionToInsight, evasionToInsightForgo, evasion, insight | ported |  | unique roll test |
| slowmatch_coil | slowmatch_coil | unique on ring_ruby: fusePower, fuseDelay, damage, castSpeed | ported |  | unique roll test |
| pale_bargain | pale_bargain | unique on belt_endurance: lowLifeLine, damage, lifeRegen, life | ported |  | unique roll test |
| tuning_fork | tuning_fork | unique on amulet_opal: convert_physical_lightning, damage, lightningRes, castSpeed | ported |  | unique roll test |
| gravebloom | gravebloom | unique on helmet_es: minionBloom, minionBloomPower, minionLife, minionApply_poison | ported |  | unique roll test |
| bloodletters_girdle | bloodletters_girdle | unique on belt_poise: proc_bloodletting, procPower_bloodletting, apply_bleed, poise, life | ported |  | unique roll test |
| hollow_sovereign | hollow_sovereign | unique on chest_es: energyShield, damageTaken, esRechargeRate, mana, life | ported |  | unique roll test |
| stormcall | stormcall | unique on amulet_opal: proc_stormcall_answer, procPower_stormcall_answer, addedLightning, apply_shock, lightningRes | ported |  | unique roll test |
| aegis_of_the_drowned | aegis_of_the_drowned | unique on chest_armor: armor, thorns, thorns, lifeRegen | ported |  | unique roll test |
| fleetfeather | fleetfeather | unique on boots_armor_evasion: proc_fleetfeather_refusal, moveSpeed, attackSpeed, evasion | ported |  | unique roll test |
| misers_loop | misers_loop | unique on ring_lapis: proc_misers_whisper, damage, mana, manaRegen, cooldownRecovery | ported |  | unique roll test |
| titans_grasp | titans_grasp | unique on gloves_armor: damage, attackSpeed, addedPhysical, strength | ported |  | unique roll test |
| thousand_steps | thousand_steps | unique on boots_evasion: moveSpeed, evasion, damage, damage | ported |  | unique roll test |
| halo_ninth_choir | halo_ninth_choir | unique on helmet_es: energyShield, damage, castSpeed, apply_shock, lightningRes | ported |  | unique roll test |
| rote_hand | rote_hand | unique on gloves_evasion: castSpeed, mana, attackSpeed, evasion | ported | choice finger_1 draws from 3656 options (one per graft): a generated option set by hand; choice finger_2 draws from 3657 options (one per graft): a generated option set by hand; choice finger_3 draws from 3657 options (one per graft): a generated option set by hand; choice finger_4 draws from 3657 options (one per graft): a generated option set by hand | unique roll test |
| the_unmoved | the_unmoved | unique on chest_armor_evasion: evasionToArmor, evasionForgone, damage, armor, moveSpeed | ported |  | unique roll test |
| galewrights | galewrights | unique on gloves_armor_evasion: slotgraft_1_turbulence, slotgraft_2_battering_ram, shoveAuthority, impactDamage, proc_galewright_twister | ported |  | unique roll test |
| squallstep | squallstep | unique on legs_evasion_es: proc_answering_gale, evasion, apply_winded, moveSpeed | ported |  | unique roll test |
| borrowed_breath | borrowed_breath | unique on amulet_jade: survivalEase_breath, apply_winded, damageVs_winded, evasion, pocketGrant_borrowed_air | ported |  | unique roll test |
| gleaners_crown | gleaners_crown | unique on helmet_armor_es: throngPockets, throngYield, minionLife, minionDamage | ported |  | unique roll test |
| bellwether | bellwether | unique on amulet_agate: noiseOnHit, poise, thorns, damage | ported |  | unique roll test |
| standing_toast | standing_toast | unique on belt_endurance: reflex, thirstless, restorePower, restorePctMax | ported |  | unique roll test |
| shared_cup | shared_cup | unique on ring_coral: sympathy_bond_flask, sympathy_bond_orb, healPower, life, sympathy_cup_minion_flask, sympathy_cup_minion_orb | ported |  | unique roll test |
| roundelay | roundelay | unique on amulet_opal: combo_elemental_round, damage, castSpeed, mana | ported |  | unique roll test |
| grounded_lattice | grounded_lattice | unique on chest_armor_es: esToPoise, esForgone, poise, armor, relayStatus_grounding, proc_lattice_discharge | ported |  | unique roll test |
| cindervigil | cindervigil | unique on helmet_es: skillgrant_firebolt, skillgrant_pyroclast_bolt, proc_cindervigil_answer, extraAs_fire, damage, lifeOnKill, manaRegen | ported |  | unique roll test |
| cinder_conductor | cinder_conductor | unique on amulet_opal: companiongrant_summon_fire_golem, proc_cinder_conductor, procPower_cinder_conductor, minionDamage, manaRegen | ported |  | unique roll test |
| bell_of_the_breach | bell_of_the_breach | unique on belt_poise: skillgrant_war_cry, proc_breach_bell, poiseDamage, poise, moveSpeed | ported |  | unique roll test |
| unspent_reply | unspent_reply | unique on gloves_armor_es: proc_unspent_reply, blockChance, energyShield, manaCost | ported |  | unique roll test |
| rimewake | rimewake | unique on boots_evasion_es: skillgrant_frost_nova, proc_rimewake_release, damageVs_chill, moveSpeed, fireRes | ported |  | unique roll test |
| mourning_bell | mourning_bell | unique on amulet_bone: skillgrant_summon_skeleton, proc_mourning_bell_toll, procPower_mourning_bell_toll, life, minionDamage | ported |  | unique roll test |
| faultline_grips | faultline_grips | unique on gloves_armor: knockback, proc_faultline_return, shoveAuthority, impactDamage, attackSpeed | ported |  | unique roll test |
| cinderstep | cinderstep | unique on boots_armor_es: trailGrant_cinderstep, moveSpeed, damageVs_burn, coldRes | ported |  | unique roll test |
| pale_watch | pale_watch | unique on chest_es: wornThrong_pale_watch, minionLife, minionDamage, damage | ported |  | unique roll test |
| lineage_knot | lineage_knot | unique on amulet_opal: life, minionMoveSpeed, mana | ported |  | unique roll test |
| common_pulse | common_pulse | unique on ring_coral: bequest_vital, vitality, healPower, wardGain | ported |  | unique roll test |
| winter_after_you | winter_after_you | unique on belt_poise: trailGrant_rimeglass, damageVs_chill, energyShield, fireRes | ported |  | unique roll test |
| storm_tithe | storm_tithe | unique on belt_endurance: takenAs_physical_lightning, lightningRes, damage, coldRes | ported |  | unique roll test |
| coal_remembered | coal_remembered | unique on ring_ruby: takenAs_cold_fire, proc_cinder_answer, fireRes, mana | ported |  | unique roll test |
| hermits_bead | hermits_bead | unique on relic_charm: seatPower_solitude, damage, life | ported |  | unique roll test |
| sunderstone | sunderstone | unique on relic_charm: accuracy | ported |  | unique roll test |
| lodestone | lodestone | unique on relic_talisman: seatPower_communion, damage, lifeRegen, critChance | ported |  | unique roll test |
| unquarried_idol | unquarried_idol | unique on relic_idol: companiongrant_summon_stone_golem, summonReservation_summon_stone_golem, minionLife, minionDamage, manaRegen | ported |  | unique roll test |
| tally_idol | tally_idol | unique on relic_idol: damage, life, mana | ported |  | unique roll test |
| reliquary_crown | reliquary_crown | unique on relic_effigy: seatPower_outward, life, fireRes, coldRes, lightningRes, chaosRes | ported |  | unique roll test |
| idol_of_many_names | idol_of_many_names | unique on relic_idol: minionLife, mana | ported |  | unique roll test |
| architects_keystone | architects_keystone | unique on relic_effigy: effectDuration, manaRegen | ported |  | unique roll test |
| thorn_testament | thorn_testament | unique on relic_talisman: thornsToHit, thorns, armor | ported |  | unique roll test |
| pilgrim_spindle | pilgrim_spindle | unique on relic_talisman: channelMobility, damage, mana | ported |  | unique roll test |
| venom_ledger | venom_ledger | unique on relic_charm: ailmentStacks, statusMagnitude, chaosRes | ported |  | unique roll test |
| cracked_censer | cracked_censer | unique on relic_idol: proc_relic_guard_refrain, procPower_relic_guard_refrain, armor | ported |  | unique roll test |
| wayfarers_knot | wayfarers_knot | unique on relic_charm: proc_relic_wayfarer, procPower_relic_wayfarer, moveSpeed | ported |  | unique roll test |
| widows_wick | widows_wick | unique on relic_charm: proc_relic_widows_wick, procPower_relic_widows_wick, minionDamage | ported |  | unique roll test |
| wellspring_seal | wellspring_seal | unique on relic_talisman: overheal, healPower, life | ported |  | unique roll test |
| last_drop | last_drop | unique on relic_charm: pourPrime, restorePower, lifeRegen | ported |  | unique roll test |
| needle_eye | needle_eye | unique on relic_talisman: pierceCount, projectileSpeed, accuracy | ported |  | unique roll test |
| hollow_choir | hollow_choir | unique on relic_effigy: durationAuraCap, effectDuration, manaRegen | ported |  | unique roll test |

