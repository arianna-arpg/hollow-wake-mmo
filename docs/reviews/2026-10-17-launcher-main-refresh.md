# Launcher integration refresh

The owner requested a combined launcher, Hollow Wake branding and playable Crossing startup. Claude approved launcher head ae933a9 in [the PR review](https://github.com/arianna-arpg/hollow-wake-mmo/pull/10#issuecomment-6243716929), requesting the remaining merge conflict be resolved before integration.

Merge a316048 brings reviewed main 91f234f into #10. The only manual conflict was CLAUDE.md's repository map; both the porting scripts description and the local Play entry points are retained. Main's cast presentation and combat content additions are preserved. The refresh makes no additional production behavior change and no wire or save-format change.

## Narrow verification ownership

This follow-up touches the startup experiment drivers and adds an opt-in physical-controller exclusion to web/scripts/smoke.mjs. The browser smoke drives keyboard and mouse; earlier runs had unexpected panels and cancelled entry despite no corresponding keyboard events. An isolated diagnostic run passed all 25 existing assertions. That supports controller interference as an explanation, but no captured physical-device trace proves it. The final reproducible run with --no-physical-gamepads also passed 25/25. No assertion is removed and no server response is mocked. Default smoke invocations still see controllers. The loading-specific course separately exercises explicit controller states.

This narrow test-harness edit is recorded because presentation request #9 also edits the smoke script. The production background-loading focus guard is separately owned in #9 (commit b57ee405454b396ab9a8ab0bf569aad6015c69fa). Inspection confirms the earlier native and browser loading confirmation handlers lacked a focus check; this refresh does not claim to fix them or general background gameplay controller input.

The startup drivers now accept --url (browser), --port (native) and --output, so checks can use a private server and captures while the owner's preview stays running. Tests used TCP 18787, WebSocket/HTTP 18788, fresh saves and a separately published runtime. The web build went to artifacts/quick-play/main-refresh/web, preserving the preview's web/dist. The owner's managed server PID 88584 and lease stayed unchanged. All test servers shut down through their own lease with exit 0. The root checkout was not edited; its unrelated .claude/ directory was left alone.

## Repeated gates

| Check | Result |
|---|---|
| dotnet build HollowWake.sln --nologo -v quiet | 0 warnings, 0 errors |
| dotnet test HollowWake.sln --no-build --nologo -v quiet | Core 531, Server 38, Client 99; 668 total passed |
| Tools validate | 0 errors, 0 warnings |
| Tools load --x 0 --y 0 --ticks 100; fight loosed_kindler boar --seconds 20 | Both completed; 100 chunks and 100 ticks in load |
| Tools probe --host 127.0.0.1 --port 18787 --seconds 3 | PROBE OK |
| Tools protocol-ts | Regenerated with no diff |
| npm --prefix web test; npm --prefix web run build -- --outDir ../artifacts/quick-play/main-refresh/web | 176 tests and build passed |
| Godot --path src/HollowWake.Client -- --smoke --port 18787 --account combined-native-final --shots OUTPUT | 22/22 passed in 42.2 s against fresh saves |
| node web/scripts/smoke.mjs --url http://localhost:18788/ --shots OUTPUT --account final-keyboard-smoke --no-physical-gamepads | 25/25 passed in 69.5 s; no page errors |
| node experiments/launcher-quick-play/verify-native-startup.mjs --godot PATH --port 18787 --output OUTPUT | Source quick play: exactly one selection, delayed real entry, visible Crossing, terrain ready and authoritative movement passed |
| node experiments/launcher-quick-play/verify-crossing-startup.mjs --url http://127.0.0.1:18788 --output OUTPUT | Built browser boot, creation, automatic entry, wisp steering, held-input quarantine, remembered resume, cancellation, ignored late frames and reconnect passed |
| Launcher build, test, smoke -- --offline | Build passed, 33 tests passed, SMOKE launcher PASS |
| node scripts/sync-branding.mjs --check; node --test scripts/play-local.test.mjs | Branding parity and 2 helper tests passed |
| node --check on the three edited verification scripts; git diff --check | Passed |

No hot path is added by this integration. Reviewed main's performance gates remain with its owning requests. The exported combined.2 archive, packaged launcher install/update/Play course and real playtest remain the approved ae933a9 evidence in [the original combined record](2026-10-17-combined-startup.md); those prepared artifacts were not overwritten or presented as freshly rebuilt from main. The final-head hosted CI, Pages build and release dry run are linked on #10 once complete; publication and deployment are not requested.

## Retained failures and limits

All captures are under ignored experiments/launcher-quick-play/output/. main-refresh-native initially missed vendor_state (21/22). main-refresh-native-rerun passed all 22 standard checks but its optional cast cue check missed an impact (22/23; observed start, release, evade, recovery, cancel and status_expired). The final standard run is main-refresh-native-final (22/22); no combat presentation fix is claimed here.

The unisolated browser runs remain in main-refresh-browser (18/23) and main-refresh-browser-rerun (15/19). The diagnostic run in main-refresh-browser-diagnostic lost its world session during loading with no keyboard event and later showed Options. An initial controller-isolated runner used the wrong server DLL filename and failed before connection; its command was corrected before either full isolated course. The diagnostic isolated course is main-refresh-browser-isolated (25/25); the committed option's final course is main-refresh-browser-final (25/25). This is keyboard/mouse smoke coverage, not proof of physical controller correctness. The delayed-entry quick-play runs did not use controller exclusion and passed. Loading and world screenshots were inspected.

Machine-readable results are in experiments/launcher-quick-play/evidence/main-refresh-results.json. Live Codespace creation, port forwarding and physical controller/touch devices remain unverified. Future #9 loading-focus and presentation work should land through its own reviewed request; this refresh preserves that ownership.

## Review handoff

Claude: the requested conflict resolution is complete. Please review the small verification-only follow-up and this evidence, then merge #10 once its required ci check is green. The original approval covers ae933a9, not an implied review of this follow-up. Preserve the active playtest runtime and preview artifacts until the owner is finished with them.

Acknowledged by: Claude approved ae933a9 at the link above; final integration follow-up pending.
