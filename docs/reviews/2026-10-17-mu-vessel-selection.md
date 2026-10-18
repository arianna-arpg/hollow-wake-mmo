# Mu vessel selection

Claim: restore the prototype's playable wisp-and-vessel selection in both clients, with server-owned availability and a content-defined entry flow. All current Callings remain available. Existing characters occupy resumable vessels; new lives use the same Calling content and appearance pipeline as the world.

Source: the prototype at `118b510f`, especially `src/data/mu.ts`, `src/engine/muRing.ts`, `src/engine/scenes.ts` and `docs/engine/mu.md`. The old tutorial is a later migration: its temporary, rewardless staging, account completion at the Mu threshold and replay-on-abort rules must compose with this entry flow. No client may award tutorial completion or unlocks.

Ownership: this change touches `Main.cs` and client contracts also claimed by #22, and adds one content-kind registration beside #32. It uses the existing appearance services without changing their implementations. Networking documentation overlaps #20 and #31. These overlaps are declared before implementation; integration must preserve their additive changes.

Implementation: both clients now use the same authored spatial selection, prototype ring reference, shared spirit recipe and existing body/gear builders. Remembered lives and available Calling vessels have explicit Wake cards; quick play focuses a remembered vessel instead of bypassing Mu. Options, modal focus, delayed creation replies, cancelled entry, disconnect and returning from the world use the existing shell and loading lifecycle. No simulation entity or account progress exists until the corresponding authoritative action.

The flow, stage capabilities, activity schemas and views have registration points. `entry_facts` and optional Calling requirements are server-owned tags; all shipped Callings have empty requirements. A synthetic preceding stage proves that create/select cannot bypass it, abort does not complete it, and server completion returns to the same Mu stage. The actual tutorial, transient staging instances and completion migration are not implemented. Their dependent systems and threshold contract are recorded in [entry.md](../design/entry.md).

Integration: merged main at `a810b0a`, preserving #22's public outfits and shared day and #30's test cleanup. Mu previews the Calling's kit, not a saved equipment snapshot. Root main was not edited; its pre-existing untracked `.claude/` directory was left alone.

Verification on `4b8143c` (the subsequent commit adds this record and evidence only):

- `dotnet build HollowWake.sln`: zero errors and warnings, including the native client.
- `dotnet test HollowWake.sln --no-build`: 749 passed (582 Core, 119 Client, 48 Server).
- `npm --prefix web test`: 192 passed; `npm --prefix web run build` passed; `protocol-ts` regenerated and the contract test passed.
- `hollowwake validate`: zero errors and warnings. `load --x 0 --y 0` ran 200 ticks; `fight hob wolf` finished at t=10.60 with Hob defeated and Wolf at 68/96. No simulation hot path changed.
- `node scripts/test-background.mjs --suite all --no-build --out experiments/mu-selection/artifacts`: browser 30/30 and native 31/31, including actual Mu cards, native Tab/directional focus and return from the world. Owned test servers stopped cleanly.
- `node experiments/mu-selection/check.mjs`: 28 passed, including touch drift/dwell, keyboard, controller, refusal, pending/cancelled creation, modal focus, portrait layout, reconnect, world return, live `probe` with `entry_flow,interface`, and the delayed startup/cancellation course. No browser exceptions.

[Evidence](../../experiments/mu-selection/evidence/) contains the reports, real probe and combat traces, and inspected wide/card/portrait browser captures. Native ran with the headless driver: behavior and card layout passed, but rendered native pixels and geometry/card visual performance still require an isolated graphical session. The harness explicitly records those two skipped rendering checks. No native pixel parity is claimed. Launcher delivery, exports and release code did not change.

Claude review requested for authority, the tutorial seam, parity and lifecycle behavior, and a native graphical inspection when an isolated session is available.

Acknowledged by: pending Claude review.

## Review follow-up, 2026-10-18

Resuming the changes requested in [Claude's review](https://github.com/arianna-arpg/hollow-wake-mmo/pull/35#issuecomment-6245774094). This follow-up owns the existing entry files, quick-play acceptance scripts, and collection validation in `ContentKinds.cs` / `ContentRegistry.cs`. It preserves main's additive content APIs and does not alter other PRs' effect, combat or entity implementations. The current PR routes Play to Mu for explicit Wake; the owner confirmed on 2026-10-18 that Play must open Mu and wait for explicit Wake. Background checks use private processes and data; the live launcher preview is not a test target.


Follow-up changes:

- Preserved legacy Calling content order and matched creation capacity to owned account ids, including missing sheets. Completion facts now match exactly; availability retains hierarchical tags. Entry stage registration is safe during concurrent validation/tests.
- Added generic content-kind collection validation so a Calling catalog cannot lose its default entry flow through removal, disabling or duplication. Partial tooling catalogs without Callings or flows remain supported.
- Older servers without entry state select the terminal persistent stage in both clients, so future tutorial content cannot strand them. Modern server stage authority is unchanged.
- Cancel A, create B, receive A leaves B's pending card and request intact; B alone may Wake on its successful reply. Non-creation errors remain visible. Native roster refresh and both clients' remembered focus respect Options and open cards.
- Browser Mu releases WebGL contexts and settings listeners on disposal; binding maps rebuild when settings change. Shared card/roster/progress dimensions and palette are content tokens; unused wisp/cancel requirements were removed.
- Updated browser/launcher acceptance from the removed picker to explicit Wake. Native startup defaults to headless with isolated settings and a bounded process. Graphical launcher acceptance requires an explicit isolated-session flag and only claims the Play-to-entry boundary. Documented in-world creation refusal and inactive-world-input behavior.

Verification on the follow-up source (2026-10-18): solution build zero errors/warnings; .NET 753 tests (582 Core, 119 Client, 52 Server); web 192 tests and production build; protocol regenerated without a diff; content zero errors/warnings; load 200 ticks; fight ends at t=10.60 with Wolf 68/96; live feature probe passes. Launcher build, 33 tests and hidden offline smoke pass. Background browser 30/30 and native 36/36 pass, including actual native legacy fallback, delayed creation replies, visible refusals and Options focus. Native retains two explicit rendering skips. Mu course passes 37 checks, including movement rebinding, context disposal, delayed startup and both quick-play acceptance courses.

Evidence is in [review-followup](../../experiments/mu-selection/evidence/review-followup/). Browser wide/card/portrait captures were inspected. Initial follow-up test attempts exposed two harness mistakes (a pre-world shortcut to Options and Escape rather than Done after rebinding); the final runs use the actual Options button and Done. The first direct Electron run preceded the pinned binary download; the standard hidden offline smoke completed after installation. No production workaround was added for these test setup issues.

The owner confirmed on 2026-10-18: **Open Mu and wait for Wake**, including remembered characters. The passing startup courses assert that decision.

Open before merge: Claude rereview; native rendered inspection and installed-archive launcher acceptance in an isolated graphical session. The follow-up preserves the owner-confirmed explicit-Wake behavior. The old tutorial and its constituent systems remain future work as documented in the entry contract. The owner's running launcher preview and root checkout were untouched.

Acknowledged by: pending Claude rereview.
