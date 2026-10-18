# Flight launch correction after Claude rereview

Date: 2026-10-18. PR #36. Parent: c07b6df017ac9c7f9a854db836d5689e70debe16.

Claude's rereview6254640832 identified two blockers despite the earlier green suite: empty-ground direction shots pitched into the cursor and nested flights added launch lift again. Ownership of launch/elevation code in DeliveryHandlers, ProjectileSystem and ProjectileAims was coordinated with #20; its contact/height forecast work stays separate. Main #54 Aim and #50 CastSource must be retained in subsequent complete-source unions.

The initial 35-case regression suite fails 33 cases on unchanged runtime. Firebolt aimed at ground 3 m away ends at 3.564 m of 16.250 m; Piercing Arrow ends at 3.250 m of 19.380 m. The explicit point/horizontal controls pass. Logs are retained under experiments/entity-hitboxes/artifacts/flight-correction. Expanded coverage adds delayed, salvo and aim-wrapped emissions. Salvo coverage exposed later shots restarting at the caster; those now preserve the emission anchor. Geometry, clearance reach, body dimensions and budgets are unchanged.

The content default is target elevation: retained or sampled body aim, otherwise horizontal. Explicit point remains available. LaunchLift records whether the current anchor needs the ground-placement lift; flight hooks provide zero. Context copies and same-origin cast/aim wrappers preserve it; new placement anchors reset it. Later salvo beats use the inherited From anchor. Source, Origin and CastId are preserved.

The original shape-sensitivity fixture has been retained. Its observer now records every hit target and tick, strengthening the assertions: bar and wave had equal first/last ticks and totals but different intermediate contact times. No equivalence exception, assertion or budget was relaxed. The prior failing output and exploratory fixture runs are retained.

An exact archived-source build of c07b6df reproduces all seven committed Windows hashes. The corrected Release runtime also reproduces those hashes, with no baseline rewrite. Full gameplay-readable duel comparison and final combined gates are being completed after the final #33 merge; this intermediate record does not certify that combination. No new timing sample has been taken.

Acknowledged by: pending Claude rereview after final combined gates.
