<!-- docs/COLLABORATION.md is the contract: a pull request says which gates ran, names every behavior change with its evidence, and which vision tenet it serves. Keep the headings; delete the hints. -->

## What and why

<!-- What changes, which tenet of docs/VISION.md it serves, and the roadmap or parity item it claims. -->

## Behavior changes and evidence

<!-- One line per change in what a player, an operator or the simulation does, with its evidence: a test name, a trace, a screenshot, a bench line. "None" for docs or tooling only. -->

-

## Gates

<!-- CLAUDE.md, Verification gates. Tick what ran, with counts where they exist; strike what the change does not touch. -->

- [ ] `dotnet build HollowWake.sln` is clean
- [ ] `dotnet test HollowWake.sln` passes (counts: )
- [ ] `hollowwake validate` reports 0 errors and no new warnings
- [ ] Generation, movement, combat or networking: `hollowwake load`, `hollowwake fight` and the loopback server tests pass; for networking, the server plus `hollowwake probe`
- [ ] Combat: `hollowwake tournament --standard --check-hashes experiments/combat-grammar/baselines/standard-duels.json` is identical on the baseline's named platform; record the platform and any intentionally changed pairings. Other platforms record differences and verify repeatability without replacing that baseline. The required Windows CI job enforces the committed baseline.
- [ ] Desktop client: `dotnet build src/HollowWake.Client` compiles and the Godot smoke play-through passes against a local server
- [ ] Web client or a message: `npm --prefix web test` and `npm --prefix web run build` pass, `web/src/net/protocol.ts` regenerated, the browser smoke passes
- [ ] Launcher, release workflows, `scripts/package-client.sh` or the export presets: launcher `npm run build`, `npm test` and `npm run smoke -- --offline` pass; the release dry run on this request is green
- [ ] Hot path: `bench` in Release within `docs/design/performance.md`
- [ ] The system's contract in `docs/design/` is updated, free of counts, rosters and version strings that go stale

## Compatibility

<!-- Wire: new fields and messages only, behind a hello feature when a client must opt in; the TypeScript module regenerated. Content: additive, or say what a layer has to change. "None" otherwise. -->

## Review

<!-- Who reviews: a Codex chat for a Claude branch, a Claude session for a Codex branch. The review is a comment on this request (one account serves both engineers, so GitHub's approval state is not used). Link the review record under docs/reviews/ when the change is large. When this merges, GitHub deletes the branch; remove the local branch and its worktree the same day (docs/COLLABORATION.md, Branch life). -->
