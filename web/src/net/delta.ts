import type { EntityDelta, EntityState } from './protocol';

/**
 * What a client does with an entity delta, exactly as EntityDelta.ApplyTo in src/HollowWake.Protocol/Messages.cs:
 * the known state with every field the delta carries replaced. An absent field is unchanged; the strings that can
 * become empty (pattern, look, faction, rarity, interactable, casting, shape) arrive as "" when they are cleared, and a
 * cleared shape takes its size with it; flags, statuses and what a player wears arrive whole (an empty appearance means
 * nothing is worn, and the state then carries none, as the server sends none); resources and their maxima carry only
 * the entries that changed.
 */
export function applyDelta(state: EntityState, delta: EntityDelta): EntityState {
  const cleared = (value: string | undefined, before: string | undefined) => (value === undefined ? before : value.length === 0 ? undefined : value);
  return {
    ...state,
    name: delta.name ?? state.name,
    pattern: cleared(delta.pattern, state.pattern),
    look: cleared(delta.look, state.look),
    x: delta.x ?? state.x,
    y: delta.y ?? state.y,
    z: delta.z ?? state.z,
    yaw: delta.yaw ?? state.yaw,
    radius: delta.radius ?? state.radius,
    height: delta.height ?? state.height,
    faction: cleared(delta.faction, state.faction),
    rarity: cleared(delta.rarity, state.rarity),
    level: delta.level ?? state.level,
    resources: delta.resources ? { ...state.resources, ...delta.resources } : state.resources,
    resource_max: delta.resource_max ? { ...state.resource_max, ...delta.resource_max } : state.resource_max,
    flags: delta.flags ?? state.flags,
    statuses: delta.statuses ?? state.statuses,
    interactable: cleared(delta.interactable, state.interactable),
    dead: delta.dead ?? state.dead,
    casting: cleared(delta.casting, state.casting),
    cast_progress: delta.cast_progress ?? state.cast_progress,
    grounded: delta.grounded ?? state.grounded,
    vx: delta.vx ?? state.vx,
    vy: delta.vy ?? state.vy,
    vz: delta.vz ?? state.vz,
    shape: cleared(delta.shape, state.shape),
    shape_size: delta.shape === '' ? undefined : delta.shape_size ?? state.shape_size,
    heading: delta.heading ?? state.heading,
    spin: delta.spin ?? state.spin,
    appearance: delta.appearance === undefined ? state.appearance : delta.appearance.length > 0 ? delta.appearance : undefined,
  };
}
