# Status roster: Codex review and integration

Reviewed request #17's status and charge definitions, attribute families, proving content, regression tests and the combat status contract. The hooks, banks, converters, caps and effects use existing registered behavior; presentation remains under metadata. No new blocking finding in this slice. This review covers the authored runtime content and its proofs, not an exhaustive equivalence claim for the external prototype catalog.

Integrated the preset seam, request #14's mapping fixes at `40b19ba`, and main at `dcecf64` without rewriting history. Verification covers runtime tree `04bd59d`; this follow-up adds review records and evidence only.

## Verification

- Release solution build: zero warnings and errors.
- Release solution tests: 531 core, 98 client and 38 server passed.
- Content validation: zero errors and warnings; load: 200 ticks.
- Five standard duels are byte-identical to integrated #15 and main's runtime (`e4ba80c`, merged as `dcecf64`). Commands and captured-file hashes are in `experiments/combat-port/evidence/roster-finalization-duels.json`.
- `fight proving_hexer proving_plaguer --seed roster --seconds 40`, run twice: byte-identical captures, SHA-256 `9D19DC247F74304FD94664F499F91B3D0BEA7CE4A58ED8A6635CE265ADF40D48`. Text normalization on checkout can change the capture's byte hash.
- The previously outstanding performance gate passes in a sequential full Release run: p95 11.1092 ms, allocation 68.244 KB/tick, behavior 2.2731 ms and 22.9 KB/tick, snapshot build 0.8689 ms, full/delta payload 27.361/8.492 KB, zero system errors. Every published budget passes; full rows are in `experiments/combat-port/evidence/roster-finalization-bench.json`. This closes the prior busy-machine snapshot measurement as a merge blocker for this runtime tree, without claiming a performance improvement.

## Integration order

Codex approves this status-roster slice with its rerun benchmark. Request #14's Codex mapping fixes still need Claude's focused review; land #14, #15, then #17 after hosted checks pass. Request #18 is stacked on #17: retarget it to main before deleting #17's remote branch. Later content slices need their own review.

Acknowledged by: pending integration after the #14 follow-up review.
