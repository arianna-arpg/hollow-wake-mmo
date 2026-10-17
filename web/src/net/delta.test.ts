import { describe, expect, it } from 'vitest';
import { applyDelta } from './delta';
import type { EntityState } from './protocol';

const hob: EntityState = {
  key: 7, name: 'Hob', pattern: 'hob', look: 'hob', x: 1, y: 2, z: 3, yaw: 0.5, radius: 0.45, height: 1.1,
  faction: 'wilds', rarity: 'magic', is_player: false, level: 1,
  resources: { life: 70, stamina: 20 }, resource_max: { life: 78, stamina: 20 },
  flags: ['monster'], statuses: ['burning'], interactable: undefined, dead: false,
  casting: 'Bite', cast_progress: 0.4, grounded: true, vx: 1, vy: 0, vz: 0,
};

describe('entity deltas', () => {
  it('leaves absent fields as they were', () => {
    expect(applyDelta(hob, { key: 7 })).toEqual(hob);
  });

  it('replaces what the delta carries', () => {
    const next = applyDelta(hob, { key: 7, x: 4, vx: 0, grounded: false, level: 2, dead: true, cast_progress: 0.8 });
    expect(next).toMatchObject({ x: 4, y: 2, vx: 0, grounded: false, level: 2, dead: true, cast_progress: 0.8 });
  });

  it('clears the strings that arrive empty', () => {
    const next = applyDelta(hob, { key: 7, casting: '', rarity: '', look: 'hob_angry' });
    expect(next.casting).toBeUndefined();
    expect(next.rarity).toBeUndefined();
    expect(next.look).toBe('hob_angry');
    expect(next.faction).toBe('wilds');
  });

  it('carries a shape with its size, heading and spin, and clears the size with the shape', () => {
    const bolt: EntityState = { ...hob, key: 9, shape: 'orb', shape_size: { radius: 0.4 }, heading: 1.2, spin: 0 };
    const turned = applyDelta(bolt, { key: 9, heading: 2.4, spin: 3 });
    expect(turned).toMatchObject({ shape: 'orb', shape_size: { radius: 0.4 }, heading: 2.4, spin: 3 });
    const fan = applyDelta(bolt, { key: 9, shape: 'cone', shape_size: { radius: 5, angle: 60 } });
    expect(fan.shape).toBe('cone');
    expect(fan.shape_size).toEqual({ radius: 5, angle: 60 });
    const none = applyDelta(bolt, { key: 9, shape: '' });
    expect(none.shape).toBeUndefined();
    expect(none.shape_size).toBeUndefined();
    expect(applyDelta(bolt, { key: 9 }).shape_size).toEqual({ radius: 0.4 });
  });

  it('merges resource entries and replaces lists whole', () => {
    const next = applyDelta(hob, { key: 7, resources: { life: 60 }, resource_max: { life: 80 }, statuses: [], flags: ['monster', 'enraged'] });
    expect(next.resources).toEqual({ life: 60, stamina: 20 });
    expect(next.resource_max).toEqual({ life: 80, stamina: 20 });
    expect(next.statuses).toEqual([]);
    expect(next.flags).toEqual(['monster', 'enraged']);
    expect(hob.resources.life).toBe(70);
  });
});
