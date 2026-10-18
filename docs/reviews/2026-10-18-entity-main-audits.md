# Entity stack integration with main audits

Codex owns the #31 integration of main `45ce87dae2102245a91c09e1d12086d4481880f0` onto foundation `3007003db865eceafac955ecf2a859431f010ae2`, at the coordinator's request. This record declares overlap with #47's `AxisBindings`, grammar tests and frozen-literal fixture before the reporting repair. #36 owns its separate contact/aim bindings and literal census; its complete refreshed branch will be consumed by #34, not copied backward into #31.

Hosted run [39158058935](https://github.com/arianna-arpg/hollow-wake-mmo/actions/runs/39158058935) tested the PR/main merge and failed three new main grammar tests: the `summon.raise_as` reference had no axis binding, the signature parameter census found the same omission, and the frozen-literal census found 21 pre-existing undead preset keys. The branch-only local suite did not contain these main-only tests. The Windows replay job checks duel hashes, not the complete grammar suite; its pass is not evidence that Windows passes these three tests.

Main merged automatically without conflicts. Its existing movement/AI, entry, icon, reporting and other changes remain intact. In particular, summon/minion plan invalidation now uses main's `Motion.Planned`; the entity branch's corpse gating and faction/birth behavior remain intact. This full main merge is not runtime-equivalent to the previous foundation or the retained #37 binary, even if the following audit repair is reporting-only.

The integration is prepared in a separate managed worktree. The final #37 source/content `ce3bcf569fb27426991ec69931c950e6951c74e1`, evidence head `c6e117a362b552f9865155789fdf020d979f97d2`, and its built outputs remain untouched for the serialized benchmark window. No builds, tests or benchmarks have run for this main integration yet. Full combined gates will run after the coordinator releases heavy work; no pass or merge acceptance is claimed.

Acknowledged by: pending Claude review.
