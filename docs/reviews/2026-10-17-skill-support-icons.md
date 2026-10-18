# Shared skill and support icon migration

Codex claims the prototype skill/support icon migration for inventory, carries, learned seats and hotbars in both clients. Work lives in `codex/skill-support-icons`, based on main, in its own managed worktree. The root checkout and coordinator files are outside this change.

The source is the illustrated catalog on the prototype's `codex/seamless-world-foundation` branch at `5bf91b865dbce821780a6c1611e9d628af0c50f1`, rather than the older initials on prototype main. The importer carries the authored paths, layer transforms, palette and support badge into shared content: 948 skill faces and 457 support faces. Presentation catalogs and rendering are generic across skills and supports; no gameplay dispatch or skill-specific renderer is introduced.

Ownership notice before edits: this requires an additive registration in `Content/Kinds/InterfaceKinds.cs`, also touched by PRs #9 and #20, and additive presentation identity in interface messages/snapshots (the manual-bank work may touch those files). No forecast, appearance, manual-bank or launcher behavior is changed. Both clients' item, seat and hotbar surfaces are owned by this request.

## Verification

- `dotnet build HollowWake.sln`: 0 warnings/errors, including the native client.
- `dotnet test HollowWake.sln`: 531 Core, 38 Server and 104 Client tests passed. New paired client tests cover imported-face coverage, item/seat identity, preset/form id collisions, converted and older-server faces, semantic fallbacks, support badges and XML escaping. C# also rejects malformed vectors and reference cycles; protocol samples round-trip all new fields.
- `hollowwake validate`: 0 errors, 0 warnings. `load --x 0 --y 0` and `fight wolf hob` completed; live TCP interface probe passed on the isolated server.
- Browser tests: 179 passed. Production build passed; it now reports the chunk-size advisory for the bundled vector catalog (about 1.71 MB raw / 375 KB gzip for the app chunk). No warning is suppressed.
- `node experiments/ability-icons/render.mjs`: seven production item/rack/hotbar comparisons have identical SVG layers, with small sockets, dormant paint and converted identity checked. `evidence/shared-artwork.png` and `render-report.json` record the result.
- Native staged inventory rendered in the existing `--mockup` harness. The native raster cache was also stressed at a temporary capacity of one; retained canvas resource references kept visible item icons intact after eviction. The exact content was restored to capacity 128. Screenshots were inspected. The mockup reports shutdown RID/ObjectDB leaks; its diagnostic output is preserved, not declared clean.
- Live native and web screenshots show matching rack/hotbar art. **The full smokes are not green:** native 20/22 (initial held movement and villager `too_far`), web 22/25 (initial movement/jump and quest-panel Escape timing). These are outside the icon changes and overlap the integration/harness issues being investigated elsewhere. Their complete logs are retained in `experiments/ability-icons/evidence`; these initial failures were retained for the later integration rerun below. No smoke assertion was removed or weakened and no coordinator harness file was edited.

Root checkout verification: clean at `5843798` after the coordinator's merges; this task only wrote its managed icon worktree. The initially observed untracked `.claude/` folder was left alone.

## Integration verification

Merged main through `32f194b` into this worktree at `a143826`, bringing the reviewed shared background harness and input synchronization. No harness or coordinator file was changed by this task.

- Solution build succeeds with 0 errors and three existing xUnit2031 analyzer warnings in main's `PortedProjectileTests.cs`; the complete log is retained.
- Solution tests: 582 Core, 38 Server and 117 Client pass (737 total). Browser tests: 187 pass. Production browser build passes, retaining the bundle-size advisory (app chunk about 2.01 MB raw / 436 KB gzip after the main integration).
- Content validation: 0 errors, 0 warnings. Headless zone load, wolf/hob duel and live TCP interface probe pass.
- `node scripts/test-background.mjs --suite all --no-build --out artifacts/ability-icons/integration`: browser 29/29 and native 25/25 pass, with no cleanup errors. The harness uses private ports/data and isolates input. Native headless rendering checks are explicitly skipped; the earlier graphical icon and capacity-one cache evidence remains the rendering evidence.
- Focused production icon check passes again for all seven inventory/rack/hotbar comparisons. The updated gallery and live browser weave-page screenshot were inspected. No icon implementation or smoke assertion changed for this rerun.

The integration logs, completed runner result and focused-render report are saved under `experiments/ability-icons/evidence/integration-*`. Earlier smoke failures and native mockup shutdown diagnostics remain available. The full client behavior gate is now satisfied; independent Claude review is still pending.

## Claude review request

Please review the generic catalog/resolver and presentation aliases, converted identity and old-server fallback, vector paint parity, and native texture ownership across cache eviction. Confirm the broad catalog can serve later skill/support ports with content changes alone. The additive protocol fields contain identities only, behind the existing interface flow, and do not require a new feature or client reset. Please check the completed integration evidence alongside the retained native graphical evidence.

Acknowledged by: pending Claude review.
