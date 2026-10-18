# Projectile snapshot motion and chained-pierce range

Date: 2026-10-18. PR #36. Runtime **b116725108a1b814bb875d408f3b17aee95bf412**, after publication **26a60dfd1868ef4dff7a441aef01f63b459f7099**.

## Scope

The coordinator's [source-only review and ownership declaration](https://github.com/arianna-arpg/hollow-wake-mmo/pull/36#issuecomment-6256434243) identifies two defects in the preceding floor correction. Both are handled in this one follow-up. Shared Snapshots ownership with #20 was declared before editing. The peer's local cases were reviewed for authorized correction; its blocked raw review payload is not republished here.

## Corrections

Snapshots now obtain a flight's observed velocity through a pure core query. For a terrain-floor flight it describes the next locally constrained step; the raw integration velocity remains unchanged, including descent needed when the ground slopes down. Capture and Fingerprint use the same query and existing velocity fields. No client or wire-shape change is required.

Terrain.TryHeightAt reads only already-loaded chunks, with the same bilinear height semantics across positive and negative boundaries. Observation cannot load or generate terrain. If supporting terrain is unavailable, the reported velocity holds the observed pose until the next authoritative update while simulation motion proceeds normally. Ordinary, gravity and returning flights retain their previous reporting. Extrapolation is still a local linear approximation, not a prediction of later turns or terrain bends.

Range accounting records the chain count before contact. A newly consumed chain owns a fresh leg counter; a flight already chasing that changes trajectory at a pierce must subtract the discarded segment from its current leg as well as its odometer. The prior Chasing boolean did not distinguish these cases. Source, aim, contact geometry, seeded rolls, payloads and return steering are unchanged.

## Verification

[Commands and evidence](../../experiments/entity-hitboxes/evidence/motion-observation/source.txt).

- Own pre-fix snapshot selection: 14 failures and 7 controls passing. Own range selection: two chained failures and one direct control passing. First evidence and corrected fixture diagnostics remain local under ignored artifacts; no peer raw payload was republished.
- Final focused selections pass core 200 and server 24. The new server cases cover first floor contact and later steps, both real projectile presets on flat/up/down slopes, unchanged raw pitch and authoritative motion, repeated observation purity, velocity deltas/fingerprints, missing terrain and negative/chunk boundaries. New core cases cover direct and chained pierces reaching the authored leg range.
- Full solution passes core 2007, server 79 and native 132 with no .NET skips. Browser tests pass 269. Total: **2487**.
- Debug solution and Tools Release builds have zero warnings/errors. Validate reports 0/0; starter load completes 200 ticks.
- Generated protocol remains current. The private network probe negotiated all six requested features and passed after verifying this run's server identity.
- All seven Windows replay hashes remain identical. No baseline was changed. The complete proving report is structurally identical to the preceding corrected report, hash 6673881dfab95df6; it still exits 1 for the same three inherited ceiling failures. All 777 lint finding keys remain unchanged.
- Content, web and script trees are unchanged, so the preceding production web build and literal census remain applicable at their recorded source. Fresh browser tests and private runtime smoke cover this follow-up.

Private run run-zUtxqe passes browser 30/30 and native 36/36 with no cleanup errors. The isometric screenshot was inspected and renders the world; card mode still shows the separately owned blank-world/tiny-viewport issue fixed downstream in #34. Native pixels remain skipped by headless mode. The retained native log has the same shutdown diagnostics: 5 dummy textures, 2 shaped texts, 1 font, 1 CanvasItem and 26 ObjectDB instances. No benchmark or performance trace ran.

## Handoff

The exact runtime was sent to the forecasting peer for independent rereview. Claude rereview remains requested on the combined source and evidence. PR #36 remains draft pending review disposition and exact-head CI. Its retarget to main changed metadata only; no newer main or sibling source was merged backward.

Paid echoes, captured Direction, targeted-sector behavior and physical CastSource in the final #20/#50 union remain required. No broad sibling propagation or timing is authorized by this handoff. Separate native rendering, downstream card rendering and coordinated performance acceptance remain open.

Acknowledged by: pending independent and Claude rereview.
