import { describe, expect, it } from 'vitest';
import { DEFAULTS } from '../config';
import type { EntityState } from '../net/protocol';
import { colorFor, shapeFor, titleFor } from './entities';

const palette = DEFAULTS.entities;

function entity(overrides: Partial<EntityState>): EntityState {
  return {
    key: 1, name: 'Thing', x: 0, y: 0, z: 0, yaw: 0, radius: 0.4, height: 1.8, is_player: false, level: 0,
    resources: {}, resource_max: {}, flags: [], statuses: [], dead: false, cast_progress: 0, grounded: true, vx: 0, vy: 0, vz: 0,
    ...overrides,
  };
}

describe('entity bodies', () => {
  it('chooses the body by what the entity is', () => {
    expect(shapeFor(entity({ resources: { life: 5 }, resource_max: { life: 5 } }), palette).key).toMatch(/^capsule:/);
    expect(shapeFor(entity({ look: 'drop:short_blade' }), palette).key).toBe('drop');
    expect(shapeFor(entity({ look: 'projectile' }), palette).key).toMatch(/^sphere:/);
    expect(shapeFor(entity({ look: 'field', radius: 3 }), palette).key).toBe('field:3');
    expect(shapeFor(entity({ look: 'poi:hamlet', radius: 10 }), palette).key).toBe('poi');
    expect(shapeFor(entity({ look: 'fount' }), palette).key).toMatch(/^fount:/);
    expect(shapeFor(entity({ look: 'anvil', interactable: 'station' }), palette).key).toMatch(/^box:/);
    const tree = shapeFor(entity({ look: 'oak', height: 12 }), palette);
    expect(tree.key).toMatch(/^trunk:/);
    expect(tree.canopy?.radius).toBeGreaterThan(0.8);
    expect(shapeFor(entity({ look: 'boulder' }), palette).key).toMatch(/^box:/);
  });

  it('picks by the drawn body, not the simulation radius', () => {
    expect(shapeFor(entity({ look: 'poi:hamlet', radius: 10 }), palette).pick).toEqual({ radius: 0.6, height: 3.5 });
    expect(shapeFor(entity({ look: 'field', radius: 3 }), palette).pick).toBeNull();
    expect(shapeFor(entity({ look: 'projectile' }), palette).pick).toBeNull();
  });

  it('colours by player, faction and rarity', () => {
    expect(colorFor(entity({ is_player: true, resources: { life: 1 } }), palette)).toBe(palette.player);
    expect(colorFor(entity({ faction: 'the_loosed', resources: { life: 1 } }), palette)).toBe(palette.factions.the_loosed);
    expect(colorFor(entity({ faction: 'wilds', rarity: 'champion', resources: { life: 1 } }), palette)).toBe(palette.rarities.champion);
    expect(colorFor(entity({ look: 'drop:x', rarity: 'rare' }), palette)).toBe(palette.drops.rare);
    expect(colorFor(entity({ look: 'iron_vein' }), palette)).toBe('#808080');
  });

  it('labels with level, death, casting and statuses as the desktop does', () => {
    expect(titleFor(entity({ name: 'Hob', level: 1, resources: { life: 3 } }))).toBe('Hob [1]');
    expect(titleFor(entity({ name: 'Wren', level: 1, is_player: true, resources: { life: 3 } }))).toBe('Wren');
    expect(titleFor(entity({ name: 'Hob', level: 2, resources: { life: 0 }, dead: true }))).toBe('Hob [2] (dead)');
    expect(titleFor(entity({ name: 'Kindler', casting: 'Ember', cast_progress: 0.5 }))).toBe('Kindler  Ember 50%');
  });
});
