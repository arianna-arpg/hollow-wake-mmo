# Axes report: independent provenance review

Codex reviewed request #47 at `320d115b26b9d5c6686eca9c96529bccd6b49dbb` and integrated main at `6e7e24118a696f695bfa2ac67f4c7052d9d62866` before the final gates.

The review covered the report's registry and conditional bindings, patch replay against `EffectPatcher`, representative casting, targeting, delivery and status readers, the literal census, and the report tests. This report is observability code; the correction below does not change compilation or combat resolution.

## Finding and correction

A `mode: default` patch does not lock a parameter. A later explicit `mode: first` patch can therefore replace its value. The report detected the changed value but retained the first writer through `TryAdd`, incorrectly naming the default glyph as the source of the final value.

The report now attributes each observed value change to the patch that made it. `AxisProvenanceTests.ThePatchThatWinsOwnsTheFinalValue` reproduces replacement of speed 20 by speed 30 and checks the value, source kind, glyph, origin and detail. Its second case checks that an initial `mode: first` still locks speed 20 and retains that writer. The replacement case failed before the correction with the explicit glyph expected and the default glyph reported.

## Evidence

- Release solution build: no warnings or errors.
- Integrated solution tests: 663 core, 114 client and 43 server tests passed (820 total).
- Content validation: no errors or warnings.
- The complete axes literal census is byte-identical to the committed frozen fixture; SHA-256 `CFEAAD165C7D94FA0DC9331DADDF4AE274723DC19C8BEC478B42BCBF008E291B`.
- `git diff --check` passed.

Local evidence is under this review worktree's ignored `artifacts/`: `provenance-before.txt`, `build-integrated.txt`, `tests-integrated.txt`, `validate-integrated.txt` and `frozen-literals-fixed.txt`. The regression tests and review record are committed for reproducibility. No gameplay, hot-path, client or networking behavior changed in this correction, so their live and performance checks were not repeated.

Compiler-recorded provenance remains a separate architectural follow-up; this correction keeps the report's existing replay approach and fixes the demonstrated attribution error. Claude must review this Codex correction before merge.

Acknowledged by: Claude's steward review of request #47 at `1b07c1e` accepted this attribution correction. The separately requested census and shared-fallback corrections are tracked in the subsequent review follow-up.
