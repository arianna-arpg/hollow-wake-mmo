# Combined quick play and Crossing

The owner requested combining launcher/startup PR #10 with branding/loading PR #13 and verifying the full experience before Claude's next review. Work remains on `codex/launcher-quick-play` in its isolated worktree; the branding worktree and integration checkout are untouched.

## Integration and behavior

The first merge combines branding head `95ae2b8` with launcher head `de5d37d`. Main subsequently merged #8, #11, #12 and #13. Merge `1f663ac` brings this branch onto main `6feec67`, preserving the durable README, public-repository wording, collaboration rules and aggregate required `ci` check. Only README and CLAUDE.md needed manual resolution in that second merge; both retain the local-play entry points. No main merge or peer approval is claimed here.

Automatic selection uses the existing loading-aware entry handlers. Native quick play starts after validated content and normal client initialization. Both clients keep the Crossing until the authoritative hero and nearby terrain are ready; ready logging now reflects that boundary. The browser keeps its branch ribbon and exactly one client initialization. First or ambiguous accounts still stop for creation/selection. No minimum loading time is added; delays exist only in verification proxies. The launcher's progress remains actual download/install progress.

This serves server authority, shared presentation and observability. The original insignia, icons, runic lettering, wisp, currents and gates come from the already-reviewed canonical branding/loading content. There are no new simulation rules or wire shapes in this follow-up.

## Claude's startup review

- The managed server now runs from a private published runtime under `artifacts/playtest/server/`. A real forced solution rebuild succeeds while that same server process remains running, so Windows playtesting no longer locks normal compiler outputs.
- Host shutdown unsubscribes process/console handlers, cancels and awaits its lease monitor, and guards a racing stop callback. Directly observed host shutdown exits 0 without the reported ObjectDisposedException.
- The repository map includes Play.cmd, Play Desktop.cmd, Stop Playtest.cmd, their configuration and helper. Main's #8 README and CI changes are integrated.
- The Codespaces instructions explicitly distinguish local helper verification from cloud provisioning, detached-process survival and forwarded-port opening, which have not been exercised in a live Codespace.

## Gates and evidence

Gates below were repeated after integration with main where that integration changed their inputs. The final Windows archive is `0.1.1-combined.2`; the earlier combined archive was also tested before main advanced.

| Command/check | Result |
|---|---|
| `dotnet build HollowWake.sln --nologo -v quiet` | 0 warnings, 0 errors |
| `dotnet test HollowWake.sln --no-build --nologo -v quiet` | Core 491, Server 36, Client 87 passed; 614 total |
| Tools `validate` | 0 errors, 0 warnings |
| Tools `load --x 0 --y 0 --ticks 100` and `fight loosed_kindler boar --seconds 20` | Completed; load built 100 chunks and ran 100 ticks |
| Tools live `probe --host 127.0.0.1 --port 17787 --account marcher --seconds 3` | PROBE OK; separate temporary server and save directory |
| Tools `protocol-ts` | Regenerated with no diff; contract test passed |
| `npm --prefix web test` / `run build` | 144 passed; build passed |
| `npm --prefix web run smoke -- --account combined-final-web --shots ABSOLUTE_OUTPUT` | 25/25 passed, no page errors |
| Godot `--path src/HollowWake.Client -- --smoke --port 17787 --account combined-native-final --shots ABSOLUTE_OUTPUT` | 22/22 passed against fresh test data |
| `node scripts/sync-branding.mjs --check` | Brand parity PASS, including loading bootstrap |
| Launcher build, test and offline smoke | Build passed, 33 tests passed, SMOKE launcher PASS |
| `npm --prefix launcher run dist:win` | Windows installer and unpacked app built |
| `scripts/package-client.sh --godot PATH --version 0.1.1-combined.2 --platforms windows-x64 --out artifacts/quick-play/combined-feed --check` | Exported game booted, validated content, printed BOOT PASS |
| `node --test scripts/play-local.test.mjs` | 2 passed |
| `node experiments/launcher-quick-play/verify-local.mjs` | Preserved unrelated occupied-port listener, warm prepare 160 ms, live full rebuild, graceful stop, directly observed host exit 0 |
| `node experiments/launcher-quick-play/verify-crossing-startup.mjs` | Built browser boot, creation, automatic entry, live wisp controls, no movement intents during loading, held-input quarantine, remembered resume, cancellation, ignored late frames and reconnect passed |
| `node experiments/launcher-quick-play/verify-native-startup.mjs --executable PATH` | Final exported game: exactly one automatic selection, visible Crossing during delayed real server traffic, hero/terrain readiness and subsequent authoritative movement passed |
| `node experiments/launcher-quick-play/verify-launcher.mjs --packaged --feed artifacts/quick-play/combined-feed --server 127.0.0.1:17787 --entry` | Selected build installed; canonical assets matched; Play during download; duplicate prevention; running-build preservation; Play and immediate-launch both reached real world readiness through the Crossing |

The first final packaged rerun timed out because the verification script passed an asynchronous IPC predicate to browser polling. That Promise was treated as truthy before its result settled. The script now polls and awaits IPC state from Node; the complete install/update/Play/shortcut course then passed against the final archive. This corrected a test synchronization flaw, with no production launcher change. The final immediate-play process start was 1844 ms while the feed was deliberately stalled.

Additional loading-specific courses passed on the combined implementation: native `--loading-smoke`; browser `loading-smoke.mjs` and `loading-entry-smoke.mjs`. They cover down/left/right, stale leases, failure/retry/cancel, keyboard focus, reduced motion, portrait/touch layout, held-input release, delayed frames and retry without duplicate selection. `verify-native-startup.mjs --godot PATH` passed on the source client too. These loading files were unchanged by the later main merge.

Machine-readable final startup results are committed under `experiments/launcher-quick-play/evidence/`. Full logs and screenshots remain in ignored `experiments/launcher-quick-play/output/`: `combined-browser`, `combined-export`, `combined-final-native`, `combined-final-web`, `packaged-launcher-results.json`, and `launcher-*-client.log`. Screenshots were inspected for the shared artwork, readable controls and the transition into the actual world. Build output is outside the client resource tree.

The earlier shared-world NPC smoke failures and native standard-UI resource diagnostics are documented in the original branding records; this change does not claim to fix those unrelated issues. Final full courses here use a dedicated native server and report their observed results. No Codespace was provisioned, no release was published, and no Pages deployment was made. The slash-containing Codespaces branch route is covered as a generated link but has not been used to provision a cloud environment. Physical controller/touch hardware remains outside this verification.

## Playtest this checkout

On this machine, double-click `experiments/launcher-quick-play/Play Combined Launcher.cmd`. It prepares/reuses the local server, opens the packaged branded launcher, installs the prepared combined archive into a separate preview folder and requests Play. The ordinary first-character panel appears if needed; later entries resume through the Crossing. Preview installs live under `artifacts/playtest/launcher-preview/`, while the server uses this checkout's usual saves. The installed build starts immediately on later uses. This shortcut consumes the locally prepared artifacts above; it is not a published release installer.

`Play Desktop.cmd` opens the source client with the remembered Godot path. `Play.cmd` opens the browser. `Stop Playtest.cmd` saves and stops the managed server. These entry points are in the combined worktree until #10 merges.

## Review request

Claude: review the final #10 head for the two host/helper fixes, the automatic-selection/loading handoff, the README/CI integration and the recorded startup checks. The former #13 integration point is resolved and its history is retained. The PR's required CI, Pages build and release dry run must pass on the final head before merge; their live run links are recorded on the request. Keep future #6/#9 client presentation work alongside these login-only additions. This record requests review; it does not stand in for it.

Acknowledged by: pending Claude review of the combined follow-up.
