import { describe, expect, it } from 'vitest';
import fixture from '../../../../tests/fixtures/kobolds-appearance.json';
import { bundledKinds } from '../../ui/logic/content';
import { AppearanceCatalog } from './looks';
import { buildBody } from './bodyFamilies';
import { wear } from './gearFamilies';

const catalog = new AppearanceCatalog(bundledKinds());
function same(actual: unknown, expected: unknown): void {
  if (typeof expected === 'number') {
    expect(actual).toBeCloseTo(expected, 4);
  } else if (expected !== null && typeof expected === 'object') {
    expect(Object.keys(actual as object)).toEqual(Object.keys(expected));
    for (const key of Object.keys(expected)) same((actual as Record<string, unknown>)[key], (expected as Record<string, unknown>)[key]);
  } else expect(actual).toEqual(expected);
}

describe('kobold scaleclans appearance parity', () => {
  it.each(fixture)('$id matches the shared native/browser plan including worn gear', row => {
    const def = catalog.byIdOf(row.id)!;
    const p = buildBody(def, row.key, row.height, row.radius);
    for (const [slot, item] of catalog.defaultGear(def, row.id)) {
      const gear = catalog.gearFor(slot, item.itemClass, item.tags);
      expect(gear, `${row.id}:${slot}`).not.toBeNull();
      const before = p.parts.length;
      wear(p, gear!, slot, row.key);
      expect(p.parts.length, `${row.id}:${slot} did not attach equipment`).toBeGreaterThan(before);
    }
    same({family:p.family,rig:p.rig,height:p.height,width:p.width,hover:p.hover,swings:p.swings,
      bones:p.bones,parts:p.parts,sockets:Object.fromEntries(p.sockets)}, row.plan);
  });
});

it.each(['biped', 'skeleton', 'scaled_biped'])('field equipment fits %s sockets and scales with its body', family => {
  const source = catalog.byIdOf('kobold_scavenger')!;
  for (const row of fixture) {
    const def = catalog.byIdOf(row.id)!;
    const item = catalog.defaultGear(def, row.id).get('chest');
    if (!item) continue;
    const gear = catalog.gearFor('chest', item.itemClass, item.tags)!;
    const body = { ...source, body: family, features: [], gear: {} };
    const plan = buildBody(body, 17, 1.8, 0.4);
    const before = plan.parts.length;
    wear(plan, gear, 'chest', 17);
    const added = plan.parts.slice(before);
    expect(added.length).toBeGreaterThan(0);
    expect(added.every(p => ['torso', 'hips', 'head'].includes(p.bone))).toBe(true);
    if (gear.features.some(f => ['robe', 'vials', 'drum'].includes(f))) expect(added.some(p => p.bone === 'hips')).toBe(true);
    if (gear.features.includes('crown')) expect(added.some(p => p.bone === 'head')).toBe(true);
    const enlarged = buildBody(body, 17, 3.6, 0.8);
    const enlargedBefore = enlarged.parts.length;
    wear(enlarged, gear, 'chest', 17);
    const larger = enlarged.parts.slice(enlargedBefore);
    expect(larger.length).toBe(added.length);
    added.forEach((p, i) => p.size.forEach((v, axis) => expect(larger[i].size[axis]).toBeCloseTo(v * 2, 5)));
  }
});
