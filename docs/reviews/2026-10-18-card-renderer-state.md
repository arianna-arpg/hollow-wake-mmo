# Card renderer state correction

The browser's directional atlas baker changed the renderer's default viewport/scissor to the last small atlas cell. Restoring the render target alone reused that changed viewport, leaving the main scene absent while DOM labels and the HUD still appeared. Auto-card mode could therefore also blank combat screenshots. The preceding fauna and kin evidence retained this defect; it is not caused by the new creature dimensions.

Atlas cell rectangles now belong to the offscreen render target in physical pixels. Default canvas viewport/scissor remain untouched, independent of display pixel ratio. A finally block restores the caller's target, clear color/alpha and automatic clearing and disposes temporary body geometry; a failed bake also disposes its incomplete target. This is presentation only and adds no wire or simulation behavior.

Four renderer-boundary regressions cover the main canvas and an existing offscreen target, successful and interrupted baking, and exact physical cell extents at display scale two. They pass in the combined browser suite (290 tests). The private browser playthrough passes 31/31, including the new screenshot check. It samples the central world above the HUD and rejects a nearly uniform sky frame; this is a nonblank-world regression, not a general art-quality test.

Retained before/after PNGs and measurements are under `experiments/entity-review/evidence/37-card-*`. The bad frame has 97.50% dominant color; the repaired frame has 18.52%, and was visually inspected for terrain and bodies. Browser smoke stage metadata and raw unit output are retained. These checks ran on the #37 candidate after merge 6a63896 with its pending kin content corrections; this self-contained renderer commit is split for the other species owners to consume and rerun against their own heads. Native pixels are not claimed. Complete final-head entity gates are recorded separately.

Claude review requested for renderer-state isolation and the conservative smoke assertion. Ownership was declared in the entity correction record before editing shared presentation/driver files.

Acknowledged by: pending Claude rereview.
