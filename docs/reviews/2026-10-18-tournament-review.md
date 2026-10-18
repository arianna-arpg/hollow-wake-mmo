# Codex review of the combat tournament

Reviewed PR #44 at `380ecee41d48ef9924978c34a197dc2b0857336d`: the event-based metrics, owner-chain credit, kit accounting, bands, trace baseline, CLI refusal paths, validator, tests and evaluation contract. Pairings and bands live in content; the harness observes the authoritative simulation and uses seeded fights. Kit coverage and cast spread are useful first measurements, with the contract correctly reserving engagement and decision entropy for later work.

## Finding and correction

The winner was fixed by the first `EntityDied` callback even though the simulation completes the entire terminal tick. A reproducible mirror with one lethal projectile per contestant, pairing index 7, killed both at 0.8 seconds, with one hit each and both life fractions zero, but reported side A as the winner. This makes win and draw rates depend on death-event order.

The runner now records both contestants' death events and decides the outcome after the tick completes. Mutual deaths are a draw; one surviving contestant wins; time-cap draws remain draws. `TournamentOutcomeTests.MutualProjectileKillsAreADraw` failed on the reviewed head and passes with the correction. No game simulation behavior changes.

The contract now states the terminal-tick rule and the limits of the trace hash. Its horizontal coordinates and rounded event values are a regression signal, not proof of identical full floating-point state across platforms. The baseline is unchanged.

## Verification

- Original head: Release build 0 warnings and 0 errors; 594 core, 114 client and 43 server tests passed; content validation 0 errors and 0 warnings.
- Corrected source: Release build 0 warnings and 0 errors; 595 core, 114 client and 43 server tests passed. The final named outcome regression also passes independently.
- All seven standard-duel hashes match the committed baseline before and after the correction, using `tournament --standard --check-hashes experiments/combat-grammar/baselines/standard-duels.json`.
- `git diff --check` passes. No client, wire or gameplay hot path changed, so their runtime gates were not repeated for this accounting correction.

Local raw evidence, including the failing regression, is in the review worktree's `artifacts/` folder. Claude review is requested for the focused correction; this record does not self-approve the Codex follow-up or mark the draft ready.

Acknowledged by: Claude, the combat session, in its comment on PR #44 of 2026-10-18 that answers this record and keeps the correction, and in docs/reviews/2026-10-18-combat-pass-zero.md.
