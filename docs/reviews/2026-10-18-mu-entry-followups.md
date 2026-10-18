# Mu entry follow-ups after #35

## Claim, 2026-10-18

Following [Claude's approval and follow-up findings](https://github.com/arianna-arpg/hollow-wake-mmo/pull/35#issuecomment-6253231758). #35 merged as `6aa0117`; this branch starts from current main and owns the browser Mu focus gate, launcher acceptance observation, their focused test coverage, and the content/modding/Codespaces/entry contracts. No server, protocol, tutorial or gameplay behavior changes are planned. The owner confirmed Play waits in Mu for explicit Wake.

Known limitations stay explicit: ErrorMsg has no creation request id, so a late refusal can clear a newer pending request; ignoring a stale correlated list also ignores its roster/capacity update until another list arrives. Native rendered inspection and graphical installed-archive launcher acceptance need an isolated graphical session. The standard background native smoke isolates input; `--isolated-input` in the separate quick-play smoke is not yet honored, so that course is not used alongside the owner's desktop here.

## Changes and evidence

- Browser Mu uses a visibility/suspension gate for DOM focus and keeps window activity as an additional input gate. Remembered focus still respects open cards and Options. `verify-browser.mjs` now models `document.hasFocus() == false` during login, asserts remembered focus and no wisp movement, then activates the page and enters with keyboard only.
- Launcher acceptance uses a bounded log observer: wait for ENTRY, then observe three full seconds, rejecting CROSSING or READY in any read, including the final read. Six deterministic tests cover continued observation, both later markers, the final boundary, late ENTRY and missing ENTRY. Failure still saves the latest client log.
- The content contract and modding guide document `validateCollection`, callback signatures, after-definition ordering, absent/removed kinds, diagnostic identity, strict errors, partial catalogs and construction-helper exceptions. Codespaces now promises automatic login into Mu and explicit Wake.
- The entry contract names both unfixed reply-order consequences, including the last-slot A / refused B stale roster case. No protocol fields, server policy or reconciliation behavior changed.

## Verification

On follow-up source `be3fd9ecfa9f103c818fcf121e79ce8c88a5277c`: `dotnet build HollowWake.sln` zero warnings/errors; .NET tests 814 (643 Core, 119 Client, 52 Server); content zero errors/warnings; web 192 tests and production build; `protocol-ts --check` current; load 200 ticks; fight Hob/Wolf ends t=10.60 with Wolf 68/96; live feature probe passes. Launcher build, 33 unit tests, hidden offline smoke and six log-observation regressions pass.

`node scripts/test-background.mjs --suite all --no-build` passes browser 30/30 and native 36/36 with two explicit rendering skips. `node experiments/mu-selection/check.mjs` passes 36 checks, including the new background-login course and existing Options/modal/reply lifecycle checks. Its first run hit the existing neutral-input gate after Options closed; the test now waits for neutral animation frames before pressing a fresh movement key. Production movement behavior is unchanged. Reports: [entry-followups evidence](../../experiments/mu-selection/evidence/entry-followups/).

The focus course models browser focus without activating an OS window; it does not claim Windows foreground-lock integration testing. The log observer's six regressions run locally; the full graphical archive/launcher course and native pixels still require an isolated graphical session. No claim that these checks ran in CI. The standard native smoke isolates physical input; the standalone quick-play native smoke was deliberately not run here because its isolation flag remains ineffective.

## Handoff and cleanup

Claude review requested for the four small follow-ups. Preserve explicit Wake, late ErrorMsg correlation and stale roster/capacity limits. No merge without review. The merged #35 worktree was archived and its local branch deleted; this branch started from main at `6aa0117`. Root checkout and live preview were untouched.

## Steward review and main integration

[Claude approved `be3fd9e`](https://github.com/arianna-arpg/hollow-wake-mmo/pull/51#issuecomment-6253662916), independently checking the solution, content, protocol, client/web smokes, live probe and unchanged standard duel hashes. [The subsequent integration request](https://github.com/arianna-arpg/hollow-wake-mmo/pull/51#issuecomment-6253671762) asks to merge main forward and explicitly carries those gates for the documentation conflict.

Main at `82e3a48bb6052801ced7faa64a43fbda718cea06` includes #49 and #53. It is merged forward here; the only conflict was in `docs/modding.md`. Both the collection-validator explanation and the AI movement-plan cache migration paragraph are retained after the registration list. Incoming main code was merged without manual changes. The remaining edits in this integration are this review acknowledgment and evidence clarification.

Local builds, tests and smokes remain paused for the coordinated quiet benchmark window. The verification above belongs to `be3fd9e`, not a new local run on the integrated head. Whitespace and conflict-marker checks are the only local integration checks; remote CI for the published merge head is reported on #51.

The steward's nonblocking findings remain open:

- README's remembered-character wording still says Play resumes through Crossing; actual Play behavior waits in Mu for explicit Wake.
- The background-focus regression is a manual experiment, not a vitest CI regression. It shadows `document.hasFocus` while Playwright's actual page stays focused. It proves prepared DOM focus and paused wisp movement under that stub, then keyboard Wake after the stub becomes true. It does not prove OS activation/restoration, controller behavior or unfocused native DOM Enter gating.
- `experiments/mu-selection/check.mjs` now waits two animation frames after closing Options before pressing a fresh movement key. This avoids a real unresolved input race: a key pressed before the next Mu frame can be cleared by `releaseAll()`, and rejected repeats can leave movement stopped until the key is pressed again. This test adjustment does not fix production input.
- The three-second launcher observer checks every read, but its markers depend on event-handler ordering. A selection bypassing `EnterRequested` might emit only READY after terrain work takes longer than the window. The full graphical launcher course was not run. The Mu course has 36 checks because the standalone native quick-play check was omitted; graphical and physical-input limitations above still apply.

Experiment and smoke evidence is manual, contains local absolute paths and is not proof of those courses running in CI. Late uncorrelated ErrorMsg and stale correlated-list roster/capacity consequences remain as documented above.

Acknowledged by: Claude, [approval of `be3fd9e`](https://github.com/arianna-arpg/hollow-wake-mmo/pull/51#issuecomment-6253662916) and [main-forward instruction](https://github.com/arianna-arpg/hollow-wake-mmo/pull/51#issuecomment-6253671762).
