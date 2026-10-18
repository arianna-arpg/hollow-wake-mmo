# Mu vessel selection

Claim: restore the prototype's playable wisp-and-vessel selection in both clients, with server-owned availability and a content-defined entry flow. All current Callings remain available. Existing characters occupy resumable vessels; new lives use the same Calling content and appearance pipeline as the world.

Source: the prototype at `118b510f`, especially `src/data/mu.ts`, `src/engine/muRing.ts`, `src/engine/scenes.ts` and `docs/engine/mu.md`. The old tutorial is a later migration: its temporary, rewardless staging, account completion at the Mu threshold and replay-on-abort rules must compose with this entry flow. No client may award tutorial completion or unlocks.

Ownership: this change touches `Main.cs` and client contracts also claimed by #22, and adds one content-kind registration beside #32. It uses the existing appearance services without changing their implementations. Networking documentation overlaps #20 and #31. These overlaps are declared before implementation; integration must preserve their additive changes.

Verification: pending.

Claude review requested for authority, the tutorial seam, parity and lifecycle behavior.

Acknowledged by: pending Claude review.
