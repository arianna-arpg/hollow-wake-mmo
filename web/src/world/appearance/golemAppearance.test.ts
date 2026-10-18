import { describe, expect, it } from 'vitest';
import fixture from '../../../../tests/fixtures/golems-appearance.json';
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

describe('golems appearance parity', () => {
  it.each(fixture)('$id matches the shared native/browser plan including worn gear', row => {
    const def = catalog.byIdOf(row.id)!;
    const p = buildBody(def, row.key, row.height, row.radius);
    for (const [slot, item] of catalog.defaultGear(def, row.id)) {
      const gear = catalog.gearFor(slot, item.itemClass, item.tags);
      expect(gear, `${row.id}:${slot}`).not.toBeNull();
      wear(p, gear!, slot, row.key);
    }
    same({family:p.family,rig:p.rig,height:p.height,width:p.width,hover:p.hover,swings:p.swings,
      bones:p.bones,parts:p.parts,sockets:Object.fromEntries(p.sockets)}, row.plan);
  });
});
