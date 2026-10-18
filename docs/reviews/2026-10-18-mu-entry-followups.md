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

On this follow-up source: `dotnet build HollowWake.sln` zero warnings/errors; .NET tests 814 (643 Core, 119 Client, 52 Server); content zero errors/warnings; web 192 tests and production build; `protocol-ts --check` current; load 200 ticks; fight Hob/Wolf ends t=10.60 with Wolf 68/96; live feature probe passes. Launcher build, 33 unit tests, hidden offline smoke and six log-observation regressions pass.

`node scripts/test-background.mjs --suite all --no-build` passes browser 30/30 and native 36/36 with two explicit rendering skips. `node experiments/mu-selection/check.mjs` passes 36 checks, including the new background-login course and existing Options/modal/reply lifecycle checks. Its first run hit the existing neutral-input gate after Options closed; the test now waits for neutral animation frames before pressing a fresh movement key. Production movement behavior is unchanged. Reports: [entry-followups evidence](../../experiments/mu-selection/evidence/entry-followups/).

The focus course models browser focus without activating an OS window; it does not claim Windows foreground-lock integration testing. The log observer's six regressions run locally; the full graphical archive/launcher course and native pixels still require an isolated graphical session. No claim that these checks ran in CI. The standard native smoke isolates physical input; the standalone quick-play native smoke was deliberately not run here because its isolation flag remains ineffective.

## Handoff and cleanup

Claude review requested for the four small follow-ups. Preserve explicit Wake, late ErrorMsg correlation and stale roster/capacity limits. No merge without review. The merged #35 worktree was archived and its local branch deleted; this branch started from main at `6aa0117`. Root checkout and live preview were untouched.

Acknowledged by: pending Claude review.
