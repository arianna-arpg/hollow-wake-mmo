import { describe, expect, it } from 'vitest';
import { backingSize, clampBox, drawnScale, inside, localPoint, ordinal, wheelFactor } from './geometry';

const box = { left: 100, top: 50, right: 300, bottom: 150, width: 200, height: 100 };

describe('page geometry', () => {
  it('reads the scale an element is drawn at, and one where it has no size', () => {
    expect(drawnScale(200, 160)).toBe(1.25);
    expect(drawnScale(200, 0)).toBe(1);
    expect(drawnScale(0, 160)).toBe(1);
  });

  it('tests client points against a box grown by interface pixels at the drawn scale', () => {
    expect(inside(box, 100, 50)).toBe(true);
    expect(inside(box, 300, 100)).toBe(false);
    expect(inside(box, 98, 100)).toBe(false);
    expect(inside(box, 98, 100, 2)).toBe(true);
    expect(inside(box, 96, 100, 2, 2)).toBe(true);
  });

  it('turns client points into a box own interface pixels', () => {
    expect(localPoint(box, 1.25, 150, 75)).toEqual([40, 20]);
    expect(localPoint(box, 0, 150, 75)).toEqual([50, 25]);
  });

  it('holds a canvas backing store to the screen pixels it covers', () => {
    expect(backingSize(300, 200, 1, 1)).toEqual([300, 200]);
    expect(backingSize(300, 200, 1.25, 2)).toEqual([750, 500]);
    expect(backingSize(0, 0, 1, 1)).toEqual([1, 1]);
  });

  it('keeps a box inside an area, at the top left when it does not fit', () => {
    expect(clampBox(50, 40, 100, 100, 400, 300)).toEqual([50, 40]);
    expect(clampBox(350, 250, 100, 100, 400, 300)).toEqual([300, 200]);
    expect(clampBox(-20, 10, 500, 100, 400, 300)).toEqual([0, 10]);
  });

  it('zooms one step a wheel notch, part of a step for small deltas, and at most three steps', () => {
    expect(wheelFactor(-100, 0)).toBeCloseTo(1.12);
    expect(wheelFactor(100, 0)).toBeCloseTo(1 / 1.12);
    expect(wheelFactor(-3, 1)).toBeCloseTo(1.12);
    expect(wheelFactor(-1, 2)).toBeCloseTo(1.12);
    expect(wheelFactor(-50, 0)).toBeCloseTo(Math.sqrt(1.12));
    expect(wheelFactor(-1000, 0)).toBeCloseTo(1.12 ** 3);
  });

  it('orders ids by code unit', () => {
    expect(['b', 'B', 'a'].sort(ordinal)).toEqual(['B', 'a', 'b']);
  });
});
