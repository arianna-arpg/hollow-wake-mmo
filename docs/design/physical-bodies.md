# Physical bodies

Physical profiles give creatures a simulation-owned body envelope and material mixture. The appearance translates that profile into a silhouette; it never supplies collision or mass data back to the simulation.

## Authoring

The registered `physical` pattern component holds `width` across the body, `depth` along its forward axis, `height` upward, `fill`, `heft`, and a `materials` list of `{ material, fraction }`. Horizontal axes rotate with facing. World-space coordinates remain X east, Y north, Z up; renderer conversion stays at the existing boundary. A world unit is treated as a meter for this mass model.

The envelope excludes decorative extremities such as banners and weapon tips. `fill` is the occupied fraction of that envelope, accounting for empty space between limbs. Material fractions are volume fractions of the occupied volume and must sum to one. `body_materials` defines effective density in kilograms per cubic world unit. Densities and fill are authored game estimates, not a mesh-volume integration or a claim of measured biological density. Equipment already represented in the authored mixture is not added again from inventory.

All dimensions, fill, heft and densities are finite and positive; fill cannot exceed one. The enclosing horizontal radius is half the larger horizontal dimension, and the pattern's radius and height must match the profile. Validation checks the resolved inheritance chain, so partial child overrides cannot bypass the envelope and mixture checks.

## Mass and modifiers

`Physicality.Mass` is the authoritative geometric query:

`width * depth * height * fill * heft * sum(material fraction * density)`

The query reads live Transform radius and height relative to the rest envelope. Horizontal scaling affects both width and depth; vertical scaling affects height. Uniform rarity or summon scaling therefore changes mass with the cube of scale. The conservative cylinder collision and current movement/path clearance remain unchanged; directional box collision is future work.

The geometric value supplies an external base to the ordinary `mass` attribute fold, including flat, increased, more, override, links into mass, scopes and bounds. A body without a profile uses its ordinary `mass` attribute, whose content base supplies neutral reference weight. `mass` is an externally based channel like delivered damage: callers needing actual body weight must use `Physicality.Mass`; a plain attribute lookup or link from mass does not derive geometry. It is hidden from the character sheet to avoid presenting a nominal channel base as measured body weight. A future expression bridge must preserve this single fold and its cache rules.

## Displacement

Knockback, pull and buffet share the move handler's shove path. Effective weight is mass divided by the reference mass, multiplied by remaining poise anchoring. The broken-poise flag removes that anchoring. A live pusher contributes a clamped power of its own effective weight; a missing source contributes neutral authority. The requested distance is multiplied by pusher authority and `shove.authority`, then divided by the victim's effective weight. Rule entries under `physicality` own the reference mass, floors, poise coefficient, exponent and authority limits, translated from the prototype's weight model.

The result enters the existing decaying impulse integrator once. Pulls still stop short of their anchor; immovable bodies still reject shoves. Dash, leap, teleport and direct velocity APIs keep their meanings. Poise changes on existing unprofiled bodies also affect anchoring, an intentional combat change. Source poise contributes to pushing authority as in the prototype.

This foundation does not yet implement momentum damage, body bowling, trample, mass-dependent fall damage, mounted rider aggregation, or inventory load. Ordinary walking still uses existing body avoidance. Those systems must consume this mass query rather than inventing another weight calculation.

## Verification

Physicality tests cover mixture volume, cubic resizing and rarity, modifier layers, neutral legacy bases, poise break, source authority, displacement distance, seeded buffet replay, pull clamping and invalid inherited profiles. Entity combat tests exercise the authored kits through AI. Native/browser fixtures pin the same reviewed geometry and equipment, with shared-scale and individual silhouette captures beside the content experiment.
