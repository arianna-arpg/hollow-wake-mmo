# Physical bodies

Physical profiles give creatures a simulation-owned body envelope and material mixture. The appearance translates that profile into a silhouette; it never supplies collision or mass data back to the simulation.

## Authoring

The registered `physical` pattern component holds `width` across the body, `depth` along its forward axis, `height` upward, `fill`, `heft`, and a `materials` list of `{ material, fraction }`. Horizontal axes rotate with facing. World-space coordinates remain X east, Y north, Z up; renderer conversion stays at the existing boundary. A world unit is treated as a meter for this mass model.

The envelope excludes decorative extremities such as banners and weapon tips. `fill` is the occupied fraction of that envelope, accounting for empty space between limbs. Material fractions are volume fractions of the occupied volume and must sum to one. `body_materials` defines effective density in kilograms per cubic world unit. Densities and fill are authored game estimates, not a mesh-volume integration or a claim of measured biological density. Equipment already represented in the authored mixture is not added again from inventory.

All dimensions, fill, heft and densities are finite and positive; fill cannot exceed one. The enclosing horizontal radius is half the larger horizontal dimension, and the pattern's radius and height must match the profile. Validation checks the resolved inheritance chain, so partial child overrides cannot bypass the envelope and mixture checks.

## Damage anatomy

Movement clearance, damage contact and material mass are separate queries. A pattern's registered `hit_body: { profile: id }` component selects a `hit_bodies` definition. The player's profile comes from the validated `body.player_hit_profile` reference. A profile is a union of volumes with an `aim` index naming the volume to aim at. Volumes have a registered `shape`, local `forward`, `left`, `up`, `radius` and `half_height`. All dimensions multiply live Transform height; facing rotates the horizontal offsets. Uniform stature changes, including rarity, therefore scale the anatomy. Retuning navigation radius alone cannot change damage contact. Nonuniform resizing requires an authored profile with the intended proportions; navigation radius is not a damage scaling control.

Built-in damage primitives are vertical cylinders and ellipsoids with circular horizontal slices. Several overlapping primitives approximate a broad or long torso; separate limbs leave actual gaps. The authored rest pose is the authority, independently of animation, mesh tessellation and decorative equipment. Banners, ears, tusks, weapons and empty saddles do not enlarge the damage body. A body without a profile falls back to its navigation cylinder, bounded vertically from its feet to its height. These are explicit approximations, not per-triangle or animated skeletal collision.

`Combat.HitBodies` supplies shape contact, aim point, directional extent and broad-phase bounds. Contact observation accepts an optional estimated position for pre-commit AI checks, translating the existing anatomy without changing the live Transform or its facing. Bounds are sampled once per simulation tick. Pattern/player creation invalidates them; code that resizes or replaces a body within a tick must call `HitBodies.InvalidateBounds`. Position and facing do not change the enclosing bounds. Sweeps reject distant volumes before detailed intersection and solve constant-height disks analytically. `HitVolumeShapes` is the open registry, with a horizontal slice function and an opt-in convexity guarantee for continuous collision. Mods must provide a matching client diagnostic renderer for new primitive shapes; unsupported shapes are marked unavailable rather than guessed. The simulation reads no `meta` or appearance data.

The developer hit-body option draws the server's profile with live size, position and facing in cyan, and navigation clearance in yellow, in both clients. It uses the authoritative snapshot pose without interpolation; animated artwork can visibly differ. Profiles arrive through the negotiated catalog described in [networking](networking-and-persistence.md), never through locally inferred mesh bounds. Shared native/browser line fixtures and the entity-hitbox gallery check the diagnostic geometry.

## Mass and modifiers

`Stats.Get("mass")` is the canonical mass value. `Physicality.Mass`, expression reads and links all use that ordinary attribute fold. The content formula reads geometry input gauges:

`volume * fill * heft * sum(material fraction * density)`

The physical binder supplies rest-envelope volume and material inputs. A Transform radius change scales both horizontal axes; a height change scales the vertical axis. A dimension-change notification updates volume and invalidates the attribute cache immediately, including rarity resizing. Uniform explicit resizing therefore changes geometric mass cubically. There is no automatically applied summon-size multiplier in this pass. Bodies without profiles use the formula's legacy base.

An authored or runtime `SetBase("mass", value)` wins over the geometric formula until cleared; ordinary flat, increased, more, override, bounds and single-hop links keep their standard meanings. Links out of mass follow the engine's no-link-chaining rule. Default mass queries use the ordinary empty query context for both profiled and legacy bodies. To condition mass on the owner's tags, use `condition: has(...)`; scoped modifiers require an explicit query context and are never silently activated only for physical profiles. Changing `Stats.Tags` invalidates cached conditions.

`fill` means occupied envelope fraction. `heft` is a separate authored gameplay adjustment after material density, not another volume fraction or a physical measurement. A `size.*` classifier is an authored encounter category, not a computed mass or height threshold; numeric consumers read dimensions/mass. Partial inherited profiles must still restate matching pattern radius and height under the existing pattern contract.

## Displacement

Rule expressions `physicality.weight` and `physicality.shove` own the weight and force mathematics. The configured `anchor_pool` and `anchor_broken_flag` select the anchoring resource and its broken state. The shipped policy divides mass by `reference_mass`, adds remaining poise anchoring, and floors weight at `minimum_weight`. Consequently very light unanchored bodies below that floor share the same resistance; their distinct mass remains available to other mechanics.

Knockback, pull and buffet use the effect's `Source` body as the pusher, including when a projectile, field or trap delivered the effect. They do not use delivery-entity mass or automatically climb to the credited owner. Source poise contributes to authority. The source mass exponent is bounded, then `shove.authority`, knockback's own multiplier (when applicable), and victim weight are included before the final `shove_scale_min`/`shove_scale_max` clamp. This ceiling limits the multiplier on an authored effect distance; it is not an absolute travel-distance cap. Pull clamping follows the adjustment, so caster-anchored hooks can stop short when resistance is high.

Continuous field pull uses the same weight/authority policy and final clamp before its per-tick anchor limit. Immovable bodies reject both impulse shoves and field pulls. The impulse integrator and field nudges traverse every crossed terrain cell, stop before a blocked cell, and preserve seeded buffet direction. This closes endpoint-only tunneling through walls, trees and placed blockers. Clearance remains the current movement center/cell model; anatomical body collision belongs to the separate hitbox work.

Dash, leap, teleport and direct velocity APIs do not multiply force again. Existing unprofiled bodies intentionally gain poise anchoring. Momentum damage, bowling, trample, mass-dependent falling, mounted rider aggregation and inventory load remain future consumers of the canonical mass attribute.

## Verification

Physicality tests cover mixture volume, cubic resizing and rarity, modifier layers, neutral legacy bases, poise break, source authority, displacement distance, seeded buffet replay, pull clamping and invalid inherited profiles. Entity combat tests exercise the authored kits through AI. Native/browser fixtures pin the same reviewed geometry and equipment, with shared-scale and individual silhouette captures beside the content experiment.
