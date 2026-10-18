import { describe, expect, it } from 'vitest';
import { DEFAULTS } from '../config';
import type { EntityState } from '../net/protocol';
import { isShaped, shapeColor, titleFor, appearanceKey, AppearanceService } from './entities';
import { applyDelta } from '../net/delta';
import { bundledKinds } from '../ui/logic/content';
import { buildBody } from './appearance/bodyFamilies';

const palette = DEFAULTS.entities;

function entity(over: Partial<EntityState>): EntityState {
  return {
    key: 1, name: 'Thing', x: 0, y: 0, z: 0, yaw: 0, radius: 0.4, height: 1.8, level: 1, resources: {}, resource_max: {},
    flags: [], statuses: [], dead: false, cast_progress: 0, grounded: true, vx: 0, vy: 0, vz: 0, is_player: false, ...over,
  };
}

describe('entity bodies', () => {
  it('clears public gear after a delta and distinguishes a naked spawn from legacy absence', () => {
    const service = new AppearanceService(bundledKinds(), null!);
    const legacy = entity({ look: 'holdfast', is_player: true });
    const clothed = { ...legacy, appearance: ['main_hand=weapon.sword'] };
    const bare = applyDelta(clothed, { key: legacy.key, appearance: [] });
    const look = service.lookFor(legacy)!;
    expect(service.planFor(clothed, look, false, legacy.key).gearKey).toContain('+');
    expect(service.planFor(bare, look, false, legacy.key).gearKey).toBe(look.id);
    expect(service.planFor({ ...legacy, appearance: [] }, look, false, legacy.key).gearKey).toBe(look.id);
    expect(service.planFor(legacy, look, false, legacy.key).gearKey).toContain('+');
    expect(appearanceKey(legacy)).not.toBe(appearanceKey(bare));
  });

  it('draws an explicitly empty self equipment list without default gear', () => {
    // Plan construction is pure; this test does not ask the baker to use a renderer.
    const service = new AppearanceService(bundledKinds(), null!);
    const state = entity({ look: 'holdfast', is_player: true });
    const look = service.lookFor(state)!;
    const fallback = service.planFor(state, look, false, state.key);
    expect(fallback.gearKey).toContain('+');
    service.selfEquipment = () => ({});
    const self = service.planFor(state, look, true, state.key);
    expect(self.gearKey).toBe(look.id);
    expect(self.plan.parts.length).toBe(buildBody(look, state.key, state.height, state.radius).parts.length);
    expect(service.planFor(state, look, false, state.key).gearKey).toBe(fallback.gearKey);
  });

  it('draws deliveries with a shape as shapes and everything else as a built body', () => {
    expect(isShaped(entity({ look: 'telegraph' }))).toBe(true);
    expect(isShaped(entity({ look: 'projectile', shape: 'line', shape_size: { radius: 3, width: 0.5 } }))).toBe(true);
    expect(isShaped(entity({ look: 'hob', resources: { life: 5 } }))).toBe(false);
    expect(isShaped(entity({ look: 'oak' }))).toBe(false);
  });

  it('colours a shape by its Thread, else by the player or the faction', () => {
    expect(shapeColor(entity({ look: 'telegraph' }), palette)).toBe(palette.telegraph);
    expect(shapeColor(entity({ look: '#e8622a' }), palette)).toBe('#e8622a');
    expect(shapeColor(entity({ look: 'field' }), palette)).toBe(palette.field);
    expect(shapeColor(entity({ is_player: true }), palette)).toBe(palette.player);
    expect(shapeColor(entity({ look: 'projectile', faction: 'the_loosed' }), palette)).toBe(palette.factions.the_loosed);
  });

  it('titles creatures with their level and players without', () => {
    expect(titleFor(entity({ name: 'Hob', level: 1, resources: { life: 3 } }))).toBe('Hob [1]');
    expect(titleFor(entity({ name: 'Wren', level: 1, is_player: true, resources: { life: 3 } }))).toBe('Wren');
    expect(titleFor(entity({ name: 'Hob', level: 2, resources: { life: 3 }, dead: true }))).toBe('Hob [2] (dead)');
    expect(titleFor(entity({ name: 'Hob', level: 2, resources: { life: 3 }, casting: 'Bite', cast_progress: 0.5 }))).toBe('Hob [2]  Bite 50%');
  });
});
