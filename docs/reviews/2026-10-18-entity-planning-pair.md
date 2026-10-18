# Exact-source entity foundation planning pair

Measured once on 2026-10-18, after [Claude stopped local checks](https://github.com/arianna-arpg/hollow-wake-mmo/pull/31#issuecomment-6256048127), fresh owner confirmations and the coordinator's explicit grant. The pair addresses the remaining planning request after [Claude accepted the #31 contract/census/radius follow-up](https://github.com/arianna-arpg/hollow-wake-mmo/pull/31#issuecomment-6255604087).

## Sources and method

Main **fa7f5f0d468737f3bd8fafdb9a9e19b198cca359** ran first; PR31 **a918663ce5845474fb4347a11fcb663d337a9eb9** ran second. Each had its own exact Git archive, content tree and verified Release binaries. Main fa7 was freshly archived and built as requested. The earlier main17ca archive and logs remain preserved and unsampled; it was not relabeled as fa7. All 380 selected main source/tool/content/root inputs and 386 PR inputs were verified against their manifests before measurement, then independently rechecked by the coordinator. Both Release builds completed with zero warnings/errors.

Command in each candidate directory: `dotnet src/HollowWake.Tools/bin/Release/net10.0/hollowwake.dll bench --only worldgen --json`, with stdout redirected to its own first-sample file. Default seed `aureth`; Normal priority; one invocation each, no extra warm-up, retry, rebuild, broad benchmark or trace during the grant. Both processes exited 0 with empty stderr and stopped before release. Start/end UTC, exit, actual priority, content/archive identities and all seven binary hashes are retained in the metadata.

Main ran `2026-10-18T22:45:36.6703437Z` to `2026-10-18T22:45:38.0284112Z`. PR31 ran `2026-10-18T22:45:38.0583197Z` to `2026-10-18T22:45:39.2907634Z`. The process intervals do not overlap.

## All first-sample rows

| Measure | Exact main fa7 | Exact PR31 a918 | Existing limit |
|---|---:|---:|---|
| `worldgen.zone.plan` | 19.1739 ms | 35.2371 ms | 40 ms |
| `worldgen.chunk` | 1.18455 ms/chunk | 0.558921875 ms/chunk | 4 ms/chunk |
| `worldgen.chunk.alloc` | 14.298095703125 KB/chunk | 14.13671875 KB/chunk | 400 KB/chunk |
| `worldgen.sample` | 1064.445 ns/sample | 775.5 ns/sample | No named limit |

Both planning samples observe four POIs and four quests; chunk rows cover 64 chunks/65,536 cells. All reported rows are retained, including the differing chunk and climate timings.

PR31 planning is **35.2371 ms**, below the unchanged 40 ms limit by **4.7629 ms**. It is **16.0632 ms (83.8%) slower** than the paired main sample. This observed pair meets the requested planning limit without a budget change. It does not establish a timing distribution, explain every row's variation, remove the cost increase or certify the later combined #37/#20 runtime. Historical snapshot/allocation evidence and the separate combined-source performance work remain open. No balance failure is cleared by a timing result.

## Durable provenance and limitations

The complete result arrays, empty stderr files, source manifests, verified binary manifests, original process metadata, sanitized build logs and controller are committed under `experiments/entity-review/evidence/31-planning-pair-*`. Absolute candidate/user paths are replaced with placeholders in copied text; original outputs and both exact archives remain unchanged in the ignored artifact directory. No measured value, source/hash, priority, timestamp or exit result is rewritten.

The optional raw `DotnetVersion` field is **null** in both process metadata files because the controller accidentally self-assigned its pre-run variable. The original null and controller are preserved. A separately timestamped **post-sample** `dotnet --info` inventory reports SDK 10.0.402 on Windows win-x64; it is labeled as a later inventory rather than substituted into contemporaneous metadata. The source/content and seven binary identities, Normal priority, ordering and start/end/exit provenance are complete. Querying that inventory did not invoke the game or take another sample.

This follow-up changes evidence and review prose only. Simulation, content, tests, clients and production scripts remain identical to measured a918. No local runtime gate was rerun to package the record. The already published fauna c3099a8 and kin 60e6e8a remain stable; no implementation propagation wave is requested for these evidence files.

Acknowledged by: [Claude accepted this planning pair and all outstanding review items](https://github.com/arianna-arpg/hollow-wake-mmo/pull/31#issuecomment-6256124558). The coordinator released all owners after both first outcomes. Claude owns the final smoke/probe, trial integration, merge and main proof; this evidence-only update performs none of those actions.
