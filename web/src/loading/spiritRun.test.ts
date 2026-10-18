import { describe, expect, it } from 'vitest';
import reference from '../../../tests/fixtures/loading-reference.json';
import { SpiritRun, SPIRIT_RUN, spiritGateSolids, spiritLayout, type SpiritDirection } from './spiritRun';
import { SeededRng } from './rng';

describe('the crossing at arpg-game-world@64819bd', () => {
  for (const course of reference.cases) it('replays the original ' + course.direction + ' trace', () => {
    const rng = new SeededRng(course.seed);
    const run = new SpiritRun(course.direction as SpiritDirection, () => rng.next());
    for (let frame = 0; frame < 1800; frame++) {
      const phase = frame % 240;
      run.step(1 / 30, { axis: phase < 60 ? 1 : phase < 120 ? -1 : 0, target: phase >= 120 ? (Math.floor(frame / 240) % 2 ? -150 : 150) : undefined });
      const sample = course.samples.find(s => s.frame === frame);
      if (!sample) continue;
      for (const key of ['time', 'lane', 'distance', 'speed', 'passed', 'hits', 'collected', 'boostGates', 'currentsTaken', 'dash', 'hinder'] as const)
        expect(run[key], key + ' at ' + frame).toBeCloseTo(sample[key], 7);
      expect(run.gates).toEqual(sample.gates);
    }
  });

  it('checks central piers and prevents a current from cancelling an impact', () => {
    const run = new SpiritRun('down', () => .5);
    run.time = 1; run.lane = 0; run.streak = 20; run.dash = 2;
    run.gates = [{ id: 99, u: SPIRIT_RUN.playerU, openings: [{ lane: -130, width: 114 }, { lane: 130, width: 114 }], resolved: false, hit: false }];
    run.currents = [{ id: 1, gate: 99, placement: 'opening', u: SPIRIT_RUN.playerU, lane: 0, taken: false, fade: 0 }];
    expect(spiritGateSolids(run.gates[0])[1]).toEqual({ low: -73, high: 73 });
    run.step(1 / 120, { axis: 0 });
    expect(run.hits).toBe(1); expect(run.dash).toBe(0); expect(run.streak).toBe(0);
    expect(run.currentsTaken).toBe(0);
  });

  it('takes one paired soul, releases its sibling, and refreshes currents without stacking', () => {
    const run = new SpiritRun('down', () => .5);
    run.time = 1; run.gates = [];
    run.pickups = [
      { id: 1, choice: 1, u: SPIRIT_RUN.playerU, lane: 0, kind: 'wild', state: 'live', fade: 0 },
      { id: 2, choice: 1, u: SPIRIT_RUN.playerU, lane: 0, kind: 'mote', state: 'live', fade: 0 },
    ];
    run.step(1 / 120, { axis: 0 });
    expect(run.collected).toBe(1); expect(run.boostGates).toBe(3);
    expect(run.pickups[1].state).toBe('released');
    for (let i = 0; i < 2; i++) {
      run.currents = [{ id: i, gate: 0, placement: 'between', u: run.playerU, lane: 0, taken: false, fade: 0 }];
      run.step(1 / 120, { axis: 0 });
      expect(run.dash).toBe(SPIRIT_RUN.currentSeconds);
    }
    expect(run.speed).toBeLessThanOrEqual(SPIRIT_RUN.maxBoostSpeed);
  });

  it('uses an invertible draw/pointer transform in every orientation and viewport', () => {
    for (const direction of ['down','left','right'] as const)
    for (const [width,height] of [[320,160],[400,900],[1600,630]]) {
      const layout = spiritLayout(width,height,direction);
      const p = layout.point(230,125);
      expect(layout.lane(p.x,p.y)).toBeCloseTo(125);
    }
  });
});
