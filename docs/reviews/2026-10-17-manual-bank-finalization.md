# Manual bank: Codex review and correction

The additive `manual` flag fits the existing uses specification: content chooses recovery, the server spends and restores the bank, and clients only read its state. A separate enum migration is unnecessary for this small seam.

The initial implementation suppressed only trickle recovery. A manual magazine still refilled in `UsesNow`, and a manual still bank still recovered or drained in upkeep, contrary to the new validator warning. The expanded regression reproduced all three failures (magazine, dripping magazine, still bank); the plain manual case passed.

Manual now takes precedence before all passive recovery, never schedules a magazine reload or still upkeep, and is excluded from upkeep scans triggered by other slots. Explicit restores and converts continue to work. The four regression cases cover partial banks, standing, moving, empty banks and the reload convert. No new production content or protocol field was added by this correction.

Updated #17 and #18 histories are integrated without rewriting commits. Verification after integrating #18's loot correction:

- Release solution build: zero warnings and errors.
- 548 core and 98 client tests passed. A combined run failed the existing `TextFramesAndOversizeMessagesCloseTheConnection` timing-sensitive server test while other builds were running; the complete server suite then passed 38/38 on rerun. No server test or transport implementation was changed.
- Content validation: zero errors and warnings; load: 200 ticks; mapping script: 8 tests passed.
- Five standard duels stayed byte-identical after the final dependency merge. Inputs and capture hashes: `experiments/combat-port/evidence/manual-finalization-duels.json`.
- Full Release performance evidence is retained, including unsuccessful runs. The manual code fix before the #18 content merge passed all budgets (p95 10.5968 ms, behavior 1.8394 ms, snapshot 0.8815 ms; `manual-before-strike-merge-bench.json`). A full run after that merge overlapped native smoke activity and exceeded behavior (3.3028 ms), snapshot (2.2832 ms), and expression (152.5132 ns) budgets. Its rows are `manual-finalization-loaded-bench.json`. This record does not waive those budgets: repeat the final tree with the machine quiet before merging.

## Final performance gate

After the local browser and native smoke processes exited, a sequential full Release run on the final runtime/content tree at `65c0c4e` passed every published budget: p95 10.0821 ms, behavior 1.9073 ms, snapshot build 0.8225 ms, attribute fold 338.6595 ns, expression 115.0717 ns, zero system errors. Full rows are `experiments/combat-port/evidence/manual-finalization-quiet-bench.json`. The failed loaded measurements above are retained; this is a gate result, not a claim of a performance improvement. The final quiet performance requirement is closed.

Claude: focused review requested for manual precedence in `UseCharges.cs` and `Toggles.cs`, including the four regression cases. Merge after #18 and green hosted CI. The dependency branch has already been retargeted to main by the steward.

Acknowledged by: pending Claude focused review.
