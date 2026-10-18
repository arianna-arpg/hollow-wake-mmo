# Preset seam: Codex review and integration

Reviewed the preset compiler, definition, validator, mechanism probes and regression tests from request #15, including additive form/preset/glyph composition and the first active glyph, then preset, then form precedence for singleton behaviors. No new blocking finding in that seam. Existing content retains its behavior; the new data joins the same compiler and registries without another runtime or a client decision.

Integrated request #14's mapping corrections at `40b19ba` and reviewed main at `dcecf64` (including request #6), without rewriting history. Verification below covers runtime tree `e879f87`; this follow-up adds only this record and captured evidence.

## Verification

- Release solution build: zero warnings and errors.
- Release solution tests: 517 core, 98 client and 38 server passed.
- Content validation: zero errors and warnings; load: 200 ticks.
- Mapping script: 8 tests passed after the dependency merge.
- Five standard duels are byte-identical across main's runtime (`e4ba80c`, merged as `dcecf64`), this request and the integrated status roster. Inputs and captured-file hashes are in `experiments/combat-port/evidence/preset-finalization-duels.json`.
- Full Release benchmark passes every published budget: p95 11.0741 ms, allocation 68.001 KB/tick, behavior 2.3093 ms and 22.9 KB/tick, snapshot build 0.9091 ms, full/delta payload 26.971/8.059 KB. Full rows are in `experiments/combat-port/evidence/preset-finalization-bench.json`.

Existing client code is inherited unchanged from reviewed main. This review does not certify every future preset or every row in the prototype ledger. The evidence compares the named local captures; line-ending normalization can change a text file's byte hash after checkout.

## Integration order

Codex approves the preset seam reviewed here. Request #14 still needs Claude's focused review of the two Codex mapping fixes before the stack lands. Land #14, #15, then #17; check hosted CI on each final pushed head. Preserve subsequent stacked requests when deleting their base branches.

Acknowledged by: pending Claude review of the #14 mapping follow-up.
