import { describe, expect, it } from 'vitest';
import { Board } from './board';

const sample = () => new Board(6, 4, [
  { uid: 1, x: 0, y: 0, w: 1, h: 1 },
  { uid: 2, x: 2, y: 0, w: 2, h: 3 },
  { uid: 3, x: 4, y: 3, w: 1, h: 1 },
]);

describe('board', () => {
  it('finds items by any cell of their footprint', () => {
    const b = sample();
    expect(b.itemAt(3, 2)?.uid).toBe(2);
    expect(b.itemAt(4, 0)).toBeNull();
  });

  it('centres a footprint under the pointer and pulls it inside', () => {
    const b = sample();
    expect(b.target(99, 2, 2, 1.4, 2.6)).toMatchObject({ x: 0, y: 2, fit: 'fits' });
    expect(b.target(99, 1, 1, 5.2, 1.9)).toMatchObject({ x: 5, y: 1 });
    expect(b.target(99, 2, 2, 5.9, 3.9)).toMatchObject({ x: 4, y: 2 });
  });

  it('calls one occupant a swap and two blocked, and never blocks the carried item itself', () => {
    const b = sample();
    expect(b.test(99, 1, 0, 2, 1)).toEqual({ x: 1, y: 0, fit: 'swap', swap: 2 });
    expect(b.test(99, 0, 0, 3, 1).fit).toBe('blocked');
    expect(b.test(2, 2, 1, 2, 3).fit).toBe('fits');
  });

  it('reads masks as rows of X and dots, and blocks the dots', () => {
    const mask = Board.maskFromRows(['XXXXXX', 'XXXXX.'], 6, 4);
    const b = new Board(6, 4, [], mask);
    expect(b.test(0, 4, 1, 2, 1).fit).toBe('blocked');
    expect(b.test(0, 3, 1, 2, 1).fit).toBe('fits');
    expect(b.isBlocked(5, 1)).toBe(true);
  });

  it('is outside off the board and scans row by row for a first fit', () => {
    const b = sample();
    expect(b.target(99, 1, 1, -0.5, 1).fit).toBe('outside');
    expect(b.firstFit(1, 1)).toEqual([1, 0]);
    expect(b.firstFit(2, 2)).toEqual([4, 0]);
    expect(b.firstFit(5, 3)).toBeNull();
  });
});
