# AGENTS.md

Start here if you are a Codex chat (or any agent that is not a Claude session) working in Hollow Wake MMO.

1. Read [CLAUDE.md](CLAUDE.md). It is the shared contract for every engineer here, whatever its name: the repository map, the commands, the verification gates and the conventions. Everything in it applies to you.
2. Read [docs/COLLABORATION.md](docs/COLLABORATION.md). It says how Claude sessions and Codex chats work side by side: branches in your own worktree, a clean integration tree, pull requests reviewed by the other side, review records under `docs/reviews/`, and evidence for every claim.
3. Read [docs/VISION.md](docs/VISION.md) before changing a system, and the system's contract under `docs/design/` before changing it.

The short form of the rules that matter most:

- Work on a branch named `codex/<topic>` in your own git worktree. Leave nothing uncommitted in the root checkout at the end of a turn.
- Add files to git by name; never `git add -A` or `git commit -a`; never reset or check out files you did not change.
- Open a pull request for every change to main and list the gates you ran. Ask for a Claude review in the request or in a review record; review Claude's requests the same way when asked.
- Numbers and vocabularies live in content, behavior goes through registries, presentation lives under `meta` and is never read by the simulation, every roll comes from the seeded generator, the server decides and the client draws.
- Keep experiments under `experiments/<topic>/` with build output ignored, and keep binary imagery small or out of the repository.
