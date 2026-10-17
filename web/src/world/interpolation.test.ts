import { describe, expect, it } from 'vitest';
import { ServerClock, lerpAngle, pushSample, sampleAt, type Sample } from './interpolation';

const sample = (t: number, x: number, vx = 0): Sample => ({ t, x, y: 0, z: 0, yaw: 0, vx, vy: 0, vz: 0 });

describe('interpolation', () => {
  it('blends between the snapshots around the render time', () => {
    const samples: Sample[] = [];
    pushSample(samples, sample(1, 0));
    pushSample(samples, sample(1.1, 1));
    expect(sampleAt(samples, 1.05, 0.25)?.x).toBeCloseTo(0.5);
    expect(sampleAt(samples, 0.5, 0.25)?.x).toBe(0);
  });

  it('runs on with velocity past the newest snapshot, for a while only', () => {
    const samples: Sample[] = [];
    pushSample(samples, sample(1, 0, 2));
    expect(sampleAt(samples, 1.1, 0.25)?.x).toBeCloseTo(0.2);
    expect(sampleAt(samples, 5, 0.25)?.x).toBeCloseTo(0.5);
  });

  it('drops snapshots a newer one overtakes and keeps a bounded history', () => {
    const samples: Sample[] = [];
    pushSample(samples, sample(2, 0));
    pushSample(samples, sample(1, 5));
    expect(samples).toHaveLength(1);
    for (let i = 0; i < 40; i++) {
      pushSample(samples, sample(3 + i, i));
    }
    expect(samples.length).toBeLessThanOrEqual(12);
  });

  it('turns the short way round', () => {
    expect(lerpAngle(3, -3, 0.5)).toBeCloseTo(Math.PI, 1);
  });

  it('follows the server clock smoothly and snaps after a jump', () => {
    const clock = new ServerClock();
    clock.observe(100, 10);
    expect(clock.now(10)).toBeCloseTo(100);
    clock.observe(100.2, 10.1);
    expect(clock.now(10.1)).toBeCloseTo(100.11, 2);
    clock.observe(500, 11);
    expect(clock.now(11)).toBeCloseTo(500);
  });
});
