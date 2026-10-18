# Public appearance and world day finalization

Date: 2026-10-17. Reviewer: Codex. Request: #22, original head 784d9b1. Integrated #9, #10 and main through the projectile roster in an isolated worktree; the author checkout was not changed.

## Findings and corrections

The original public-equipment seam restored a Calling's kit after every item came off. An empty list disappeared at the protocol getter and browser delta cache, and both WornBy resolvers treated empty self equipment and empty public descriptors as missing. The server also omitted the descriptor for an initially naked player.

A new server now sends an authoritative empty list for a player with no recognized worn gear, on spawn and full refresh as well as delta. Nonplayers and old-server absence remain absent. Both clients retain an empty list, and an available self map wins even when empty. Appearance and self-equipment refresh keys distinguish missing from empty. A transition back to an absent descriptor uses a full state because an absent delta field means unchanged. No new wire field was needed.

The server snapshot regressions failed 3/3 before the correction. Four browser checks failed before it, including the production plan path. Existing full-state and delta viewers now see all gear removed, and a viewer joining afterward receives a naked spawn. Paired client tests retain old-server fallback and empty-self precedence over a populated public descriptor.

A second review finding concerned configurable day lengths: WorldState holds noon when day length is nonpositive, but both clients clamped it to one second and animated a day between reports. Clients now hold the reported fraction for a stopped day and preserve positive subsecond rates. Paired regressions cover zero, negative, fractional and ordinary day lengths. The existing server-owned world clock, entry synchronization, self-state cadence and legacy fallback are preserved.

Reviewed the snapshot fingerprint/capture/delta paths, public descriptor parsing and refresh, world-day rule construction, both entry/self-state wiring paths and both client day interpolators. The landscape rules group matches the calendar contract. Simulation reads no presentation metadata; classes/tags and server time remain authoritative.

## Verification

On integrated source 83aaad5, including the two correction commits 6b0ae3c and 31b00e0:

- Release solution build: 0 warnings, 0 errors. All tests pass: 563 core, 114 client, 43 server. Web: 187 tests pass; production typecheck/build passes.
- Content validation: 0 errors, 0 warnings. Generated protocol is current. Load: 100 chunks and 200 ticks. Kindler/boar 10-second trace SHA256 remains `8CE22EA7521959FEF67D57E344BB33CAC910085017D4DF15D70D626010E07B68`.
- Private local server, probe with deltas/interface: PROBE OK; world day was reported. The shared isolated runner passes browser 30/30 and native headless 26/26, including server-day checks, with no cleanup errors. Native rendering and physical controller support are explicitly outside this headless course; earlier graphical appearance evidence remains in #9.
- Default Release benchmark fits every documented budget: zone p95 11.130 ms; behavior 1.760 ms / 22.9 KB; snapshot build 0.8313 ms/player; full states 27.716 KB/player and deltas 8.550 KB/player. Full result is preserved with the evidence.

Evidence is under `experiments/appearance/evidence/wire-finalization-*`; complete local runs remain under `artifacts/pr22-background` in the review worktree. A later merge brings only #9's already verified playthrough logs.

## Handoff

The original architecture is accepted by this Codex review subject to the corrections above. Claude must review the Codex changes before merge, with hosted CI green. Review the absent/empty boundary, refresh keys, naked-viewer regression and stopped-day interpolation. #9/#23 remain prerequisite presentation/harness review gates; no review is waived by carrying their commits here.

Acknowledged by: pending Claude focused review.
