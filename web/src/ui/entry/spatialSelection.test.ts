import { describe, expect, it } from 'vitest';
import data from '../../../../content/core/ui/entry.json';
import reference from '../../../../tests/fixtures/mu-rings.json';
import { SpatialSelection, selectionRing, type VesselChoice, type EntrySceneSpec } from './spatialSelection';
import { entryChoices } from './entryChoices';
const spec = data.entries[0]!.meta as EntrySceneSpec;
const choice = (available = true): VesselChoice => ({ id: 'test', calling: 'test', name: 'Test', description: '', look: 'test', character: null, available, rank: 0 });
describe('shared spatial choice mechanism', () => {
  it('matches the prototype for crescents, full rings and growing rosters', () => {
    for (const row of reference.cases) {
      const ring = selectionRing(row.count, spec.radius, spec.seatGap, spec.arcFrom, spec.arcTo);
      expect(ring.radius).toBeCloseTo(row.radius, 8); expect(ring.closed).toBe(row.closed);
      row.angles.forEach((a, i) => expect(ring.angles[i]).toBeCloseTo(a, 8));
      for (let i = 0; i < row.count; i++) for (let j = i + 1; j < row.count; j++)
        expect(2 * ring.radius * Math.abs(Math.sin((ring.angles[i]! - ring.angles[j]!) / 2))).toBeGreaterThanOrEqual(spec.seatGap - 1e-7);
    }
  });
  it('requires stillness and step-out before another dwell', () => {
    const m = new SpatialSelection(spec); m.setChoices([choice()]); m.moveTo(m.seats[0]!.x, m.seats[0]!.y);
    for (let i = 0; i < 110; i++) m.step(1 / 60, 0, 0); expect(m.opened).toBe(-1);
    for (let i = 0; i < 50; i++) m.step(1 / 60, 0, 0); expect(m.opened).toBe(0); m.close();
    for (let i = 0; i < 120; i++) m.step(1 / 60, 0, 0); expect(m.opened).toBe(-1);
    for (let i = 0; i < 60; i++) m.step(1 / 60, 0, 1);
    m.moveTo(m.seats[0]!.x, m.seats[0]!.y);
    for (let i = 0; i < 180; i++) m.step(1 / 60, 0, 0); expect(m.opened).toBe(0);
  });
  it('does not commune with unavailable vessels and cancels travel on suspension', () => {
    const m = new SpatialSelection(spec); m.setChoices([choice(false)]); m.moveTo(m.seats[0]!.x, m.seats[0]!.y);
    for (let i = 0; i < 300; i++) m.step(1 / 60, 0, 0); expect(m.opened).toBe(-1);
    m.moveTo(100, 100); const before = [m.x, m.y]; m.step(1, 1, 1, true); m.step(1 / 60, 0, 0);
    expect([m.x, m.y]).toEqual(before); m.open(0); expect(m.opened).toBe(0);
  });
  it('wraps the void around a grown roster and invalidates old cards', () => {
    const m = new SpatialSelection(spec); m.setChoices(Array.from({ length: 80 }, () => choice())); expect(m.rim).toBeGreaterThan(spec.wrapRadius);
    for (let i = 0; i < 1000 && m.x >= 0; i++) m.step(.05, 1, 0);
    expect(m.x).toBeLessThan(0); expect(Math.abs(m.x)).toBeLessThan(m.rim);
    m.open(4); m.setChoices([choice()]); expect(m.opened).toBe(-1); expect(m.x).toBe(0);
  });
  it('keeps every current calling open and existing lives resumable at capacity', () => {
    const list = { t: 'character_list' as const, characters: [], callings: ['one', 'two'] };
    expect(entryChoices(list, [], spec).every(v => v.available)).toBe(true);
    const choices = entryChoices({ ...list, characters: [{ id: 'c', name: 'Wren', calling: 'one', level: 3, zone: '0,0' }], entry: { flow: 'waking', stage: 'mu', kind: 'vessels', presentation: 'mu', can_create: false, callings: [{ id: 'one', available: true }] } }, [], spec);
    expect(choices.map(v => v.available)).toEqual([true, false]);
  });
});
