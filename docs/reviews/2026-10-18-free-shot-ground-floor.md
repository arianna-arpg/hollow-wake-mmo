# Free-shot ground floor and returning pierce correction

Date: 2026-10-18. PR #36. Runtime **fda30afb81452285b5956797c63363cb7bd42b46**, following **01b2884c3ddc61c0b617c9062a7e36b575d94551**.

## Separate review disposition

Exact final 01b inspection confirmed all three requests in [Claude review 6255777391](https://github.com/arianna-arpg/hollow-wake-mmo/pull/36#issuecomment-6255777391) and the [owner decision 6255794640](https://github.com/arianna-arpg/hollow-wake-mmo/pull/36#issuecomment-6255794640) remained open. The independent 85ee039 review covered aim/placement composition only.

This correction owns the projectile aim registry, launch/state/integration, delivery rule/validation, contract and regressions. These overlap #20's composition work; the coordinator and forecasting peer received this exact scoped commit. No full #20/#50 or downstream #34 merge was made, and no automatic sibling propagation is authorized before review.

## Behavior

Default target aim without a retained or sampled body now aims toward the cursor ground point. The outward path cannot descend below the terrain-relative delivery.projectile_ground_floor. The shipped rule is 0.342 m, independently tested against the shortest authored damage-body top. The centered projectile slab overlaps that body. Clamping before the body sweep preserves planar range on near input and follows smooth ascending/descending ground. Walls still stop the flight. The aim registry carries this policy without body-family, preset or child-handler switches.

Explicit horizontal, point and gravity trajectories remain available. Retained body aim, frozen SampledTargetPoint, AimOffset and Direction retain their precedence. Flight-hook contexts with LaunchLift preserve their exact emission position instead of treating it as new cursor input. The hook contract now distinguishes elevated in-flight positions from on_land and ground-clamped endings, whose children start at ground height.

A returning non-gravity/non-orbit flight levels downward pitch after a successful pierce and uses the content floor for the remaining outward leg. The old segment after contact is discarded. It can reach its authored range and return after a low hit. Chains and return turns clear the floor before destination steering. Body sizes, clearance, hit volumes, payloads, bands and budgets are unchanged.

## Regressions

[Evidence and commands](../../experiments/entity-hitboxes/evidence/ground-floor/source.txt) accompany the logs.

- Initial new 96 cases on 01b: 62 fail, 34 pass. They use a humanoid player profile at 1.8 m, launch at 1.098 m, six short-body patterns at authored dimensions, flat/up/down slopes, Firebolt/Piercing Arrow before and beyond the cursor, near/far empty-ground range, and hovered Glaive against skirmisher/whelp. All six Glaive returns failed before.
- Final focused selection: 197/197, comprising 104 new cases, 54 existing launch cases and 39 aim/placement cases. Additional cases verify the floor against content anatomy, reject invalid rules and pin exact ground-contact hook positions for lobbed and straight flights.
- Orb-child origins are compared with the parent's independently observed current position at emission. Comparing with its original launch height became invalid once free-shot parents descended. Child contact, Source and CastId checks remain.
- All 882 actual-body casts remain. Three successful Glaive returns exposed stale test assertions against the final per-leg ledger, which a return intentionally clears. The tests now observe that projectile ledger at DamageDealt before a later turn can clear it; both real damage and projectile contact remain required.
- Empty-ground wrapper expectations now assert the requested cursor-ground slope/floor. Frozen and retained body tests remain intact.

## Changed duels and causal checks

The old baseline and failing check were retained. [comparison.json](../../experiments/entity-hitboxes/evidence/ground-floor/comparison.json) records results, changed fights and controlled variants. Four owned runtime files were temporarily varied, then restored byte for byte in finally before the final Release build.

| Variant | Standard result |
|---|---|
| Original 01b runtime files | All seven old hashes |
| Floor only, returning-pierce block removed | All seven old hashes |
| Returning-pierce correction only, free-ground fallback disabled | All seven corrected hashes |
| Complete correction | All seven corrected hashes |

Both changed standard pairings are caused by returning pierce. Skirmisher/Hob changes 0d9d4a3110181c7a to 1cf76df19ceeb296; Skirmisher/Boar changes 7dd259e21c8ac0e8 to cf41fad065ff7e16. Both retain 10/10 wins and median times 5.85 s / 7.6 s. The Windows baseline records only these intentional changes.

Retained seeded event logs show the result: Hob seed 0's Glaive previously despawned at 2.30 s; it now survives and hits again at 2.80 s. In Boar seed 1 its returning hit moves from 2.45 s to 2.40 s. These are requested gameplay-event logs, not performance traces.

Proving is no longer structurally identical. Its hash changes 4730bef23767a558 to 6673881dfab95df6 through Fletcher/Evoker's returning flights. The floor-only variant matches the old hash; return-only matches the new. Fletcher/Evoker retain 0.5 win rate and 7 s median. The same three inherited ceilings remain: Bulwark, Plaguer and Pitfighter each win 10/10 against 0.8. Proving still exits 1; bands were not changed.

## Final gates and limits

- Debug solution and Tools Release builds: zero warnings/errors.
- Core 2004, server 59, native 132, browser 269: **2464 tests pass**, no .NET failures/skips.
- Validate 0/0; load 200 ticks; all seven Windows hashes match the revised baseline.
- Census 499 exact keys; lint 777 exact keys unchanged (580 smells, 197 notes).
- Browser production build passes with its existing chunk-size warning.
- Private run run-b7Iv7S: browser 30/native 36 pass, cleanup errors empty. Isometric screenshot inspected and renders the world. Card mode retains the known blank-world/tiny-viewport issue fixed only downstream in #34.
- Native pixels remain untested. Usual shutdown diagnostics remain: 5 dummy textures, 2 shaped texts, 1 font, 1 CanvasItem and 26 ObjectDB instances. No benchmark or performance trace ran.

## Handoff

Claude rereview is requested on this exact runtime and record, specifically the three separate asks, the floor/return policy, causal duel evidence and emission boundary. The forecasting peer has the commit for independent frozen-aim/Direction and nested-emission review. PR #36 stays draft pending review disposition and exact-head CI.

The review's separate unshipped on-hit ring-child target-retention behavior is unchanged; no clearance is claimed for it.

Paid echo/captured-direction and targeted-sector behavior in the eventual #20/#50 union remain required. This correction does not establish combined-source acceptance, performance clearance, native rendering or downstream renderer acceptance. No broad sibling gate wave should start before review.

Acknowledged by: pending Claude rereview and independent scoped review.
