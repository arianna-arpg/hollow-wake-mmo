# Collaboration

Two kinds of engineer work in this repository: Claude sessions (coordinator of the parity plan, integration, gates and docs) and Codex chats (independent engineers and peer reviewers). Neither can message the other directly. Everything between them goes through the repository and, when it cannot wait, through the owner. This page is the contract both follow; [CLAUDE.md](../CLAUDE.md) holds the conventions and gates, [AGENTS.md](../AGENTS.md) is where a Codex chat starts, and [VISION.md](VISION.md) is the test for every change.

## Where work happens

- **The integration tree is clean.** The checkout at the repository root is where main is integrated and verified. Nothing is left uncommitted there between turns, by anyone. Work in progress lives on a branch in its own git worktree: `git worktree add <folder> -b <branch>` from main, with branches named `claude/<topic>` or `codex/<topic>`, and folders outside the root checkout or under an ignored path.
- **Main moves through pull requests.** Every change reaches main as a pull request, reviewed by the other kind of engineer before it merges, except the coordinator's merges of package branches that were reviewed against their reports, and one-line fixes to docs. Main is protected: it takes pull requests only, each with the `ci` check green and zero required approvals (one GitHub account serves both engineers and cannot approve its own requests, so the review is a comment on the request), and never a force push or a deletion; the exceptions waive the other side's review, not the pull request. A pull request says which gates ran, names every behavior change with its evidence (trace, test, screenshot or bench line), and which vision tenet it serves; the template under `.github/` lays that out.
- **Never sweep the tree.** No `git add -A`, `git add .` or `git commit -a` in a shared tree; add paths by name. Never reset, check out or stash files you did not change. Never rewrite a branch someone else has fetched.
- **Ownership is per pull request.** A file belongs to the open pull request that touches it until that request merges. Before starting on a file another open request touches, say so in a review record or on that request. The package ownership of the parity plan's section 3 has expired now that the packages landed; the registries and message shapes it froze remain additive-only, and a shape change is called out in the request.

## Branch life

A branch is a place to work, not a place to keep work. Uncommitted or unmerged work is lost work: the other engineer cannot see it, it drifts from main, and it goes with the worktree when a machine or a session does.

- **Push at least daily.** Whatever state the branch is in, its commits are on the remote by the end of every working day.
- **Open a draft pull request early**, as soon as the branch has a shape, so the claim is visible and the gates run on every push. Mark it ready when the gates and the evidence are in.
- **A branch lives days, not weeks.** Slice the work so a branch merges within days; a long piece becomes a chain of small pull requests, not one branch kept open.
- **Delete it when it merges.** GitHub deletes the remote branch on merge; the engineer removes the local branch and its worktree the same day (`git worktree remove <folder>`, `git branch -d <branch>`). `scripts/branches.sh` shows what is entirely on main and can go, and what is not.
- **Stacked requests are retargeted before their base goes.** A request based on another branch is retargeted to main before that base merges, because deleting the base on merge closes the stacked request, and a closed request cannot be reopened or retargeted; or the stack is merged bottom-up and its base branches are deleted only after the top lands.
- **The weekly report names what lingers.** The `branches` workflow lists every branch with commits not on main, with its age, in its job summary and in one issue it keeps current (Branch report: work not on main). Each engineer answers for its own branches there: merge, split or delete, not leave.

## How we talk

- **Review records** live under `docs/reviews/`, one dated file per handoff or review, in the form Codex began: what was done, what was verified and how, what was not, and explicit asks of the other side. A record ends with an `Acknowledged by` line the other side fills in with its own record or pull request link, so no one claims coordination that did not happen.
- **Pull request threads** carry code review: concrete defects, compatibility risks, missing regressions, with file and line. A review that finds nothing says what it checked.
- **The owner relays** anything urgent, and decides product questions (a renamed Thread, a dropped feature, a budget argued down). An engineer proposes; the record shows the question open until the owner answers.
- **Claims need evidence.** "Tests pass" names the command and the counts. "Traces unchanged" names the recorded baseline. A number that changes a decision appears with its measurement.

## What every change respects

- The verification gates in CLAUDE.md, in full, before a pull request is opened: build, tests, `validate`, and the load, fight, loopback, probe, client and web checks when the change touches those areas; `bench` in Release within [performance.md](design/performance.md) when it touches a hot path.
- Data over code: numbers and vocabularies in content, behavior through registries, presentation keys under `meta` that the simulation never reads. One modifier engine. Tags as the join. Deterministic rolls from the seeded generator. Server authority: the client sends intents and draws state.
- Compatibility on the wire: new fields and messages only, behind a hello feature when a client must opt in, with the generated TypeScript module regenerated and its contract test green.
- Each system's contract in `docs/design/` is updated with the system, and stays free of counts and rosters that go stale.
- Experiments live under `experiments/<topic>/` with their build output ignored, and reference imagery stays small (a few hundred kilobytes a file) or outside the repository; evidence captures belong to the experiment, not to `docs/`.

## Reviews both ways

A Claude session asks Codex to review its pull requests as an independent engineer; a Codex chat asks Claude the same. A good review here checks, in this order: does the change serve the vision or quietly hard-code something content could express; is the simulation still deterministic and server-authoritative; are registries used instead of switches; do the tests pin the behavior that changed; is the contract document updated; did the gates and budgets run. Findings go on the request; a summary goes in a review record when the change is large.

## Backlog and claims

Open work is listed in [ROADMAP.md](ROADMAP.md) and in section 1.5 of the [parity analysis](design/parity/hollow-wake-gap.md). An engineer claims an item by naming it in a review record or a draft pull request before starting, so two do not build the same thing.
