# Gnoll/Beastkin world audit against the actual fauna base

Seed `aureth`. Actual complete fauna #34 base `c3099a8ea5c7a32fc0799c3a17cb167b2bd1231c` versus kin source `2d5b3306a5ab3f7915fb92542c7695ebd4b4879b`. Both executables use their own source/content and the same audit utility. Exact source files, four before/after CLI maps, full site/pattern/effective-level/quest reports and a unified map diff accompany this record as `37-base-*`.

This supersedes the earlier #37 world claims that compared two #37 checkpoints. The old maps in `experiments/gnoll-beastkin/evidence` are historical and do not establish this PR's effect against its base. `37-combined-*` separately describes the latest same-branch dependency integration; those unchanged placement counts must not be presented as unchanged placement versus #34.

## Actual content effect

The six new weighted Gnoll/Beastkin POI candidates change existing weighted selections and spacing outcomes. They do not change the terrain/climate algorithms or POI budgets. A controlled content copy removes only those six POI entries while retaining every kin pattern, faction, appearance and the corrected runtime. The resulting entire four-zone JSON equals the fauna-base report, including sites, per-pattern counts, explicit/effective levels and quests. The exact removed IDs and source are in `37-base-counterfactual.json`; its full output is retained. This isolates the candidate-pool change as the cause of the measured world difference.

| Reference zone | Changed report fields |
|---|---|
| 0_0 | Quests |
| 1_0 | Sites, Patterns, SpawnLevels |
| 0_-1 | None |
| 3_2 | None |

### Eastern zone (1,0)

| Site cell | Fauna base | Kin source |
|---|---|---|
| 291,217 | `poi.1_0.orc_muster.291.217`; site level 5; 2 bodies | `poi.1_0.ossuary.291.217`; site level 3; 8 bodies |
| 421,38 | `poi.1_0.ossuary.421.38`; site level 3; 7 bodies | `poi.1_0.orc_muster.421.38`; site level 5; 2 bodies |
| 431,198 | `poi.1_0.orc_chaincamp.431.198`; site level 8; 2 bodies | `poi.1_0.knot.431.198`; site level 3; 17 bodies |

Coordinates above are the site-ID cells; the CLI maps round displayed site centers. The new Knot replaces the Chainkeeper camp in this level-1 zone. Its 17 generated bodies comprise seven level-1 cairn stones and ten combat residents: six Cinder Imps, three Remnant Shades and one Tangle. Under the subsequently owner-approved Knot policy, those ten residents all use site level3. The earlier level4/6 statement is historical. The former level8 Orc Ashcaller and its camp are absent here; the other two site replacements and their complete inhabitant levels/counts are retained in the report. This is an intended consequence of adding candidates, not a claim that populations are unchanged.

### Quest comparison

Zone `0_0`: before `[{"Id": "gen.poi.0_0.hamlet.128.128.clear_knot.0", "Level": 1, "Targets": [{"Type": "reach", "Target": null, "Poi": "poi.0_0.knot.200.60"}, {"Type": "kill", "Target": "cinder_imp", "Poi": null}]}, {"Id": "gen.poi.0_0.hamlet.128.128.gather_floss.1", "Level": 1, "Targets": [{"Type": "collect", "Target": "thread_floss", "Poi": null}]}, {"Id": "gen.poi.0_0.hamlet.128.128.cull_beasts.2", "Level": 1, "Targets": [{"Type": "kill", "Target": "hob", "Poi": null}]}, {"Id": "gen.poi.0_0.hamlet.128.128.scout_ruin.3", "Level": 1, "Targets": [{"Type": "discover", "Target": null, "Poi": "poi.1_1.ruin.305.334"}]}]`; after `[{"Id": "gen.poi.0_0.hamlet.128.128.clear_knot.0", "Level": 1, "Targets": [{"Type": "reach", "Target": null, "Poi": "poi.0_0.knot.200.60"}, {"Type": "kill", "Target": "cinder_imp", "Poi": null}]}, {"Id": "gen.poi.0_0.hamlet.128.128.gather_floss.1", "Level": 1, "Targets": [{"Type": "collect", "Target": "thread_floss", "Poi": null}]}, {"Id": "gen.poi.0_0.hamlet.128.128.cull_beasts.2", "Level": 1, "Targets": [{"Type": "kill", "Target": "hob", "Poi": null}]}, {"Id": "gen.poi.0_0.hamlet.128.128.scout_ruin.3", "Level": 1, "Targets": [{"Type": "discover", "Target": null, "Poi": "poi.1_0.ossuary.291.217"}]}]`.

Both trees already include the restored anchored Clear Knot quest and the owner-selected site levels. That restoration is inherited foundation work, not a newly introduced kin feature.

### Source-matched generation goldens

| Zone | Base info / chunks | Kin info / chunks |
|---|---|---|
| 0,0 | `033B8E3528AB9828` / `B04153F9629D9BAE` | `7F54E6904EF8D669` / `B04153F9629D9BAE` |
| 1,0 | `767BE9395B9E34DB` / `0E386FE1D6659BC0` | `761ACC8E28128913` / `3D1C51508A579108` |
| 0,-1 | `8AD7370958FA7D7B` / `D785D8F084A1F919` | `EEBC1F594B876706` / `D785D8F084A1F919` |
| 3,2 | `BAFEA2C1CCA0DEC0` / `8711D19C4613D904` | `22F000E49CFDDCC1` / `8711D19C4613D904` |

Base overlay hash: `F0DCC2D8A5E8658F`.

Kin overlay hash: `2006751BBDE90CD2`.

Both full suites verify these constants. The climate golden is unchanged. The golden test comment points here and at the matching maps, rather than the original prototype-era captures.

### Remaining content boundaries

Gnolls and Beastkin are allied; Gnolls also ally with Goblinkin. Gnolls remain neutral to Orcs; Beastkin remain neutral to Goblinkin and Orcs, symmetrically through the existing resolver. Kin inhabitants use authored pattern levels unless a site explicitly opts into scaling; their minimum eligibility windows do not automatically scale them in high-level regions. The Knot policy is a deliberate separate opt-in. Current role kits reuse supported skills: Longshot/Trapper/Impaler share the ranged pair, and Gnoll Impaler/Beastkin Chaser share the spear pair. All kin currently share the inherited Goblinkin drop table at chance0.65; champion-specific loot is not claimed. Bullwarden shields and Trapper snares are visual; body-to-body movement avoidance, tactical pack/herd AI and anatomical hit volumes remain outside this pass. The inherited evaded-rend/status behavior reported by Claude remains a combat-pipeline concern, not a newly fixed mechanic here.

Acknowledged by: Claude identified the prior base-comparison defect in PR37 review6254572663; this corrected exact-base audit awaits rereview.
