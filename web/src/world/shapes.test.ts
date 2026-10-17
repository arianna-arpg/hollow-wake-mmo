import { describe, expect, it } from 'vitest';
import { inside, reachOf, regionOf, specOf } from './shapes';

describe('delivery shapes', () => {
  it('fills in the defaults the wire leaves out and falls back to a circle', () => {
    expect(specOf('crescent', { radius: 4 }, 1)).toMatchObject({ name: 'crescent', radius: 4, inner: 2 });
    expect(specOf('crescent', { radius: 4 }, 1).angle).toBeCloseTo(Math.PI);
    expect(specOf('band', { radius: 3 }, 1).width).toBe(3);
    expect(specOf('mystery', undefined, 2)).toMatchObject({ name: 'circle', radius: 2 });
    expect(specOf('sector', { radius: 5, angle: 1 }, 1).angle).toBe(1);
  });

  it('draws centered shapes around the anchor', () => {
    const ring = regionOf(specOf('circle', { radius: 3, inner: 1 }, 1));
    expect(inside(ring, 2, 0)).toBe(true);
    expect(inside(ring, 0.5, 0)).toBe(false);
    expect(inside(ring, 3.2, 0)).toBe(false);
    const square = regionOf(specOf('square', { radius: 2 }, 1));
    expect(inside(square, 1.9, -1.9)).toBe(true);
    expect(reachOf(square)).toBeCloseTo(2 * Math.SQRT2);
    const tri = regionOf(specOf('triangle', { radius: 2 }, 1));
    expect(inside(tri, 1.5, 0)).toBe(true);
    expect(inside(tri, -1.5, 0)).toBe(false);
    const bar = regionOf(specOf('bar', { radius: 3, width: 1 }, 1));
    expect(inside(bar, 0, 2.5)).toBe(true);
    expect(inside(bar, 1, 0)).toBe(false);
  });

  it('extends anchored shapes from the anchor along the facing', () => {
    const cone = regionOf(specOf('cone', { radius: 5, angle: Math.PI / 2 }, 1));
    expect(inside(cone, 4, 0)).toBe(true);
    expect(inside(cone, 3, 2.5)).toBe(true);
    expect(inside(cone, 1, 3)).toBe(false);
    expect(inside(cone, -1, 0)).toBe(false);
    const crescent = regionOf(specOf('crescent', { radius: 4 }, 1));
    expect(inside(crescent, 3, 0)).toBe(true);
    expect(inside(crescent, 1, 0)).toBe(false);
    const line = regionOf(specOf('line', { radius: 6, width: 1, inner: 1 }, 1));
    expect(inside(line, 5, 0.4)).toBe(true);
    expect(inside(line, 0.5, 0)).toBe(false);
    expect(inside(line, 5, 0.6)).toBe(false);
  });

  it('bows arcs and waves forward from a center behind the anchor', () => {
    const arc = regionOf(specOf('arc', { radius: 3, angle: Math.PI / 2, width: 0.6 }, 1));
    expect(inside(arc, 0, 0)).toBe(true);
    expect(inside(arc, -1, 0)).toBe(false);
    const wave = regionOf(specOf('wave', { radius: 2, width: 0.5 }, 1));
    expect(inside(wave, 0, 0)).toBe(true);
    expect(reachOf(wave)).toBeGreaterThan(2);
    // The server's curvature (in radii) places the circle the blade bows along.
    expect(specOf('wave', { radius: 2 }, 1).curvature).toBe(2);
    expect(specOf('arc', { radius: 2, curvature: 3 }, 1).curvature).toBe(3);
    const flat = regionOf(specOf('arc', { radius: 2, angle: 0.5, width: 0.4, curvature: 3 }, 1));
    expect(inside(flat, 0, 0)).toBe(true);
    expect(inside(flat, -0.5, 0)).toBe(false);
  });
});
