# Combat grammar: pass evidence

The combat grammar ([docs/design/combat-grammar.md](../../docs/design/combat-grammar.md)) is built in passes, each a vertical slice with a review record under `docs/reviews/`. This folder keeps the evidence those records cite: the dated inventories and reports a pass produced, the probes that produced them, and the traces they read. Reports here carry counts and line numbers that go stale; the contract page carries none.

## pass0

Pass zero inventoried the engine and the AI before anything was changed, from main at `14f7f3b`.

- `capability-map.md` and `capability-map.json`: every combat handler's parameters, probes, trigger kinds, hit stages and definition fields, read from the engine's registrations; each field classified as an axis, a node, a choice or a flag; what each flag should become; duplicated parameters; numbers no glyph can reach; the prototype's delivery mechanics against ours with the smallest carrier for each; composition smells with file and line; the invariants enforced and assumed.
- `ai-inventory.md`: every decision the AI makes, what it reads and writes and whether a client could send the same intent; the mechanics it cannot use or never uses, measured; a capability-aware chooser; positioning; observability; the split of work with Codex.
- `design-grammar.md`: the axes with prior art, the playstyle matrix and its coverage today, the exemplars worth porting, mechanics by counter, what the literature asks of an AI that plays mechanics, the measurement plan, the passes and the open design questions, with sources.
- `harness/duel.fsx`: the read-only F# probe the AI inventory used. It loads a built core from `harness/bin/` (a scratch copy of `dotnet build` output, not committed) and replays a seeded arena duel exactly as `hollowwake fight` does, recording refusals, releases, toggles, banks, states and positions, optionally swapping kits onto the bodies. Run it with `dotnet fsi duel.fsx A B seed seconds [kitA|-] [kitB|-] [timelineStep]` from this folder after copying the core's build output into `harness/bin/`.
- `traces/`: the duel traces the inventory cites (`h-*` the standard duels; `m-*` one mechanic each on a proving body).
