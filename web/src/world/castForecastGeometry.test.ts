import { describe, expect, it } from 'vitest';
import fixture from '../../../tests/fixtures/cast-forecasts/slabs.json';
import type { CastForecastRegion } from '../net/protocol';
import { forecastGeometry, sameForecastMesh } from './castForecastGeometry';

function sameEndpoints(expected: number[][], actual: number[][]): void {
  expect(actual).toHaveLength(expected.length);
  expected.forEach((segment, i) => segment.forEach((value, axis) => {
    expect(actual[i][axis], 'segment ' + i + ' coordinate ' + axis).toBeCloseTo(value, 6);
  }));
}

describe('paired forecast slab geometry used by the renderer', () => {
  for (const sample of fixture.geometry) it(sample.name, () => {
    const actual = forecastGeometry(sample.region as CastForecastRegion, sample.committed, fixture.style);
    sameEndpoints(sample.expected_segments, actual);
  });
  for (const sample of fixture.cache) it('mesh reuse: ' + sample.name, () => {
    const before = sample.before as CastForecastRegion | undefined | null;
    const after = sample.after as CastForecastRegion | undefined | null;
    expect(sameForecastMesh(before ?? undefined, sample.before_committed, after ?? undefined, sample.after_committed)).toBe(sample.reuse);
    if (sample.reuse && before && after) {
      sameEndpoints(forecastGeometry(before, sample.before_committed, fixture.style),
        forecastGeometry(after, sample.after_committed, fixture.style));
    }
  });
});
