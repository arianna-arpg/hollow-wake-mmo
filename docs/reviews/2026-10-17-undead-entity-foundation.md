# Undead entity foundation

Claimed by Codex on 2026-10-17 from origin/main at `32f194b`, implemented on `codex/undead-entity-foundation` in its own managed worktree. Claude review requested; this record is not an approval.

## Scope and ownership

The request was a substantial first entity pass based on the local ARPG prototype, prioritizing undead that can serve both as summons and enemies. The result is sixteen concrete patterns, thirteen summon/raise/bind presets, an archer projectile, a content-authored shambler behavior, three roaming tables, three ruin-site definitions, and individual appearances in both clients. Provenance is pinned to prototype `118b510f8b5bfb781163c5bc13ea4908f698e75b` in [the translation ledger](../../experiments/undead-entities/provenance.json).

Overlap was declared before shared edits: PR #22 owns the appearance builders/contract and `Snapshots.cs`. This branch adds separate reusable anatomy files and additive family registrations; its snapshot change corrects effective owner allegiance using the existing field, with no wire shape change. PR #25 owns ability icons; these weaves use the existing generic weave presentation. No other branch was edited or merged.

This follows data over code, tag joins, registered behavior, the common attribute engine, seeded rolls and server authority. Source prototype code is not imported at runtime. No new Calling starter kit or progression unlock is assumed.

## Changes with evidence

- The common body patterns carry current combat kits, resources, level scaling, Remnant faction, hierarchical material/family/role/habitat classifiers and wild rewards. `UndeadContentTests.WildBodiesHaveClassifiersRewardsAndActuallyFight` exercises all sixteen through ordinary AI and real hits.
- Summons share those kits and appearances while respecting ownership, caps, ranged/melee behavior, reservations, respawn, weighted pools, decay and corpse consumption. The new summon tests exercise eight individual presets, the deterministic four-school pool, bone-golem contract/exclusivity, wraith decay and Raise Zombie. Existing minion command/inheritance tests continue to pass.
- Summon birth removes wild Drops, including experience. A native encounter's rewards must not be farmable by summoning and killing copies. The tests check wild rewards remain present and summoned rewards are absent.
- Snapshot faction and its fingerprint now use `Relations.FactionOf`, so a player's Remnant-pattern companion draws with the same allegiance combat uses. `UndeadSnapshotTests` verifies owner changes affect the fingerprint without rewriting the native faction. The generated protocol is unchanged.
- Roaming tables join marsh, deep forest and hollowed biomes. Ossuary, grave-court and charnel-yard sites share the existing ruin budget. POI inhabitants previously ignored `level`; they now check the effective site level before rolls. The low/high-level and seeded-replay test covers that correction. A site's faction restriction is a region eligibility filter, not resident allegiance, so these hostile sites remain eligible within settled regions.
- The generation golden update is intentional. [Before/after evidence](../../experiments/undead-entities/evidence/) shows zone `(1,0)` gaining an ossuary and changed site spacing. Its chunk hash changes; the other sampled chunk hashes and climate/region hash do not. Their zone-info hashes include neighboring sites and do change. The original failure is retained as `generation-before-golden-update.txt`; the final full test log passes.
- The paired anatomy families preserve skeletal rib gaps and skulls, school-specific features, golem bulk/bindings, abomination grafts, a legless crawler and the skeletal hound rig. The shared native/browser fixture pins all sixteen plans including equipment. [Isometric](../../experiments/undead-entities/evidence/undead-isometric.jpg), [third-person](../../experiments/undead-entities/evidence/undead-third-person.jpg) and [grayscale walk](../../experiments/undead-entities/evidence/undead-grayscale-walk.jpg) captures were visually inspected. The gallery uses actual client geometry; it is not a separate mockup.

## Verification

Evidence is under [experiments/undead-entities/evidence/gates](../../experiments/undead-entities/evidence/gates/).

| Gate | Result |
| --- | --- |
| `dotnet build HollowWake.sln` | Success, zero errors; the same three pre-existing xUnit2031 warnings in PortedProjectileTests as the saved baseline |
| `dotnet test HollowWake.sln --no-build` | 612 core, 112 client, 39 server tests passed |
| `hollowwake validate` | 0 errors, 0 warnings |
| `hollowwake load --x 1 --y 0` | 100 chunks, 131 spawners, 299 monsters; 200 ticks completed |
| `hollowwake fight skeleton_warrior wolf` | Actual cleave/bite hits and completed duel trace |
| `hollowwake protocol-ts` | Regenerated, no protocol diff |
| `npm --prefix web test` | 198 tests across 30 files passed |
| `npm --prefix web run build` | TypeScript and production bundle passed |
| `node --test scripts/port/port-prototype.test.mjs` | 10 passed |
| `node scripts/test-background.mjs --suite all --no-build` | Browser 29/29, native 25/25; private server/process cleanup passed |
| TCP `probe --deltas --interface` against that owned server | PROBE OK, movement plus delta/interface negotiation |
| `node experiments/undead-entities/render.mjs` | All sixteen real body plans rendered, zero browser errors |
| `dotnet run --project src/HollowWake.Tools -c Release -- bench` | All applicable budgets met; zero zone errors |

Release measurements: zone p95 11.727 ms against 25 ms; allocation 68.181 KB/tick against 2 MB; behavior 1.849 ms and 22.9 KB/tick against 3 ms and 1 MB. Chunk generation 0.819 ms and 14.325 KB against 4 ms/400 KB. Snapshots 0.749 ms/player against 1 ms; full 27.391 KB against 30 KB; deltas 8.525 KB against 10 KB.

## Limits and asks

Native smoke is headless and explicitly skips native rendering/theme pixels. Browser captures and native/browser geometry parity are supplied; no native GPU screenshot, physical-controller test, large undead-crowd rendering benchmark or human art approval is claimed. The default server benchmark is the repository workload, not a claim about unlimited summon armies.

These are coherent procedural foundation models. Dedicated prototype mage skills, court upgrades, healing clerics, corpse graft mechanics, perception/flanking AI and the wider living fauna roster remain deferred in the ledger. The current active substitutes are named there and in entity content. Wraith hover remains presentation only. Numerical balance and progression availability still require the wider combat/content pass.

Claude: review the body/summon boundary (especially reward stripping and owner allegiance), level gating and encounter density, kit/resource balance, sprite-to-geometry readability, and the small overlaps with PR #22. Confirm the paired family additions survive that branch's appearance-wire changes before integration. Merge only through the protected-main PR workflow after review and CI.

Acknowledged by: pending Claude review.
