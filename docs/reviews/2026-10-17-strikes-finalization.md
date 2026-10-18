# Strike content: Codex review and corrections

Reviewed the reusable shells, authored strike graphs, charge/discharge statuses, behavior tests, item payload selection and ledger differences. The composition belongs in content: weapon dice flow through the existing effect graph and modifier engine, and banks and riders use existing services. This is a review of the shipped slice, not a claim of complete prototype parity.

Two follow-ups were needed before approval:

1. Every new empty shell inherited `drop_weight: 1`. The ordinary codex payload roller includes every positive-weight form, so a random reward could teach an empty graph. The eleven new shells now use the existing `drop_weight: 0` field. Named presets still compile and can be explicitly authored into rewards. A regression failed before the fix and passes after it. An initially broader assertion also exposed older empty forms outside this slice; this patch deliberately scopes its content change to the eleven new shells.
2. Several descriptions still promised prototype mechanics absent from the authored graph or deferred in the ledger (crowd empowerment, resource orbs, conscription, insight bypass and a variable storm interval). The affected descriptions now describe the current graph, including the fixed discharge interval and its fresh weapon roll, rather than promising those future mechanics. No such mechanic was removed by this correction. The ledger remains the record of deferred work.

The updated #17 history is integrated without rewriting commits. Before publication, the final source and content passed:

- Release solution build, zero warnings/errors; 544 core, 98 client and 38 server tests.
- Content validation, zero errors/warnings; load, 200 ticks.
- Five standard duels byte-identical to reviewed #17 (`9e6980e`), recorded in `experiments/combat-port/evidence/strikes-finalization-duels.json`.
- The proving duel `fight proving_pitfighter proving_mower --seed strikes --seconds 60` repeated identically; captured-file SHA-256 `B7207CB022C130B68DEFC44F5A9BA964D719DE4D8830A9C45AB89A883C770ACD` (checkout text normalization can alter byte hashes).
- Full Release benchmark: all published budgets pass, p95 11.747 ms, behavior 2.4424 ms, snapshot build 0.8393 ms, full/delta payload 27.391/8.525 KB, zero errors. Full rows: `experiments/combat-port/evidence/strikes-finalization-bench.json`.

Claude: focused review requested for the loot exclusion and description corrections. #19 remains stacked here and must be retargeted before this branch is deleted. This record does not approve later projectile content or the older empty-form loot issue outside this slice.

Acknowledged by: pending Claude focused review.
