import { describe, expect, it } from 'vitest';
import shapes from '../../../tests/fixtures/cast-forecasts/shapes.json';
import { inside, SHAPES } from './shapes';
import { CastForecastTimeline, forecastCaption, forecastOutline, forecastProgress, usableForecast, forecastSegments, forecastVerticalBounds } from './castForecastTimeline';
import type { CastForecastInfo, CastForecastMsg, CastCueMsg, CastForecastRegion } from '../net/protocol';
const cast = (id = 1): CastForecastInfo => ({ cast_id: id, source: 12, name: 'any content', mode: 'cast', started_at: 1, next_emission_at: 3,
  emission_ordinal: 0, direction_policy: 'locked_on_start', direction_committed: true, x: 0, y: 0, z: 0, coverage: 'unavailable' });
const frame = (sequence: number, time: number, casts: CastForecastInfo[] = []): CastForecastMsg => ({ t: 'cast_forecast', epoch: 'zone', sequence,
  server_time: time, valid_until: time + .5, casts, omitted: 0 });
describe('shared forecast state', () => {
  it('requires finite body bounds and preserves the actual elevated slab', () => {
    const r = { shape: 'circle', size: { radius: 2 }, target_test: 'body', height_tolerance: 4, x: 0, y: 0, z: 10, heading: 0 } as CastForecastRegion;
    expect(usableForecast(r)).toBe(false);
    const bounded = { ...r, bottom: 11, top: 12.5 };
    expect(usableForecast(bounded)).toBe(true);
    expect(forecastVerticalBounds(bounded)).toEqual([1, 2.5]);
    expect(usableForecast({ ...bounded, top: 10 })).toBe(false);
    expect(usableForecast({ ...bounded, bottom: NaN })).toBe(false);
    expect(usableForecast({ ...bounded, target_test: 'unknown' })).toBe(false);
    expect(forecastVerticalBounds({ ...bounded, target_test: 'center' })).toEqual([-4, 4]);
  });
  it('repairs baselines, suppresses ended casts, resets epochs and preserves 64-bit order', () => {
    const t = new CastForecastTimeline(); t.reset('zone'); expect(t.receive(frame(1, 1, [cast()]))).toBe(true);
    t.lifecycle({ cast_id: 1, source: 12, phase: 'interrupt', server_time: 1.2 } as CastCueMsg); expect(t.active.size).toBe(0);
    expect(t.receive(frame(2, 1.1, [cast()]))).toBe(true); expect(t.active.size).toBe(0);
    t.receive(frame(3, 1.3)); expect(t.receive(frame(2, 1.1, [cast()]))).toBe(false);
    t.receive({ ...frame(4, 2, [cast(2)]), sequence: '9007199254740993' }); expect(t.active.size).toBe(1);
    expect(t.receive({ ...frame(4, 2, [cast()]), sequence: '9007199254740992' })).toBe(false);
    t.removeEntity(12); expect(t.active.size).toBe(0); t.reset('next-zone'); expect(t.receive(frame(5, 2.2, [cast(2)]))).toBe(false);
  });
  it('uses actual clocks without turning readiness into release', () => {
    const t = new CastForecastTimeline(); t.reset('zone'); t.receive(frame(1, 1, [cast()]));
    expect(t.stale(1.5)).toBe(false); expect(t.stale(1.51)).toBe(true); expect(t.active.size).toBe(1);
    expect(forecastProgress(cast(), 2)).toBe(.5); expect(forecastCaption(cast(), 4, false)).toBe('waiting');
    expect(forecastProgress({ ...cast(), mode: 'channel', next_emission_started_at: 5, next_emission_at: 7 }, 6)).toBe(.5);
    expect(forecastCaption({ ...cast(), ready_at: 2 }, 4, false)).toBe('ready');
    expect(forecastCaption({ ...cast(), mode: 'channel' }, 4, false)).toBe('channel'); expect(forecastCaption(cast(), 4, true)).toBe('stale');
  });
  it('requires the exact unchanged revision and repairs missing details with full records', () => {
    const t = new CastForecastTimeline(); t.reset('zone'); t.receive(frame(1, 1, [cast()]));
    t.receive({ ...frame(2, 1.1), retained: [{ cast_id: 1, revision: 1 }] });
    expect(t.active.size).toBe(1); expect(t.omitted).toBe(0);
    t.receive({ ...frame(4, 1.3), retained: [{ cast_id: 1, revision: 3 }] });
    expect(t.active.size).toBe(0); expect(t.omitted).toBe(1);
    t.receive(frame(5, 1.4, [cast()])); expect(t.active.size).toBe(1); expect(t.omitted).toBe(0);
    t.receive(frame(6, 1.5)); expect(t.active.size).toBe(0);
    t.receive({ ...frame(7, 1.6), retained: [{ cast_id: 1, revision: 5 }] });
    expect(t.active.size).toBe(0); expect(t.omitted).toBe(1);
  });
  it('declines unknown shapes and nonfinite boundaries', () => {
    expect([...new Set(shapes.map(f => f.shape))].sort()).toEqual([...SHAPES].sort());
    const r = { shape: 'modded', size: { radius: 2 }, target_test: 'center', height_tolerance: 4, x: 0, y: 0, z: 0, heading: 0 } as CastForecastRegion;
    expect(usableForecast(r)).toBe(false); expect(usableForecast({ ...r, shape: 'circle' })).toBe(true);
    expect(usableForecast({ ...r, shape: 'circle', heading: NaN })).toBe(false);
  });
  it('increases precision for large modifiers and declines over-budget outlines', () => {
    const r = { shape: 'circle', size: { radius: 50 }, target_test: 'center', height_tolerance: 4, x: 0, y: 0, z: 0, heading: 0 } as CastForecastRegion;
    const segments = forecastSegments(r, 64, 256, .025);
    expect(segments).toBeGreaterThan(64); expect(segments).toBeLessThanOrEqual(256);
    expect(50 * (1 - Math.cos(Math.PI / segments))).toBeLessThanOrEqual(.025);
    expect(forecastSegments({ ...r, size: { radius: 100000 } }, 64, 256, .025)).toBe(0);
  });
  for (const f of shapes) it(`matches combat: ${f.shape}, radius=${f.size.radius}, angle=${f.size.angle}, width=${f.size.width}, inner=${f.size.inner}`, () => {
    const r = { ...f, node: 'root', x: 0, y: 0, z: 0, heading: 0, anchor: 'point', height_tolerance: 4, target_test: 'center',
      requires_line_of_sight: true, has_requirement: false, has_target_limit: false, viewer_eligibility: 'unknown' };
    expect(usableForecast(r)).toBe(f.drawable); if (!f.drawable) return;
    const outline = forecastOutline(r, forecastSegments(r, 64, 256, .025));
    for (const p of f.points) expect(inside(outline, p.x, p.y), `${p.x}, ${p.y}`).toBe(p.inside);
  });
});
