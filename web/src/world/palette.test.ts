import { describe, expect, it } from 'vitest';
import { darken, lerp, parseHex, srgbToLinear, toLinear } from './palette';

describe('palette arithmetic', () => {
  it('parses short and long hex colours and falls back on nonsense', () => {
    expect(parseHex('#ffffff')).toEqual([1, 1, 1]);
    expect(parseHex('#fff')).toEqual([1, 1, 1]);
    expect(parseHex('#000000ff')).toEqual([0, 0, 0]);
    expect(parseHex('nope')).toEqual([1, 0, 1]);
  });

  it('mixes in sRGB as the desktop client does', () => {
    expect(lerp([0, 0, 0], [1, 1, 1], 0.5)).toEqual([0.5, 0.5, 0.5]);
    expect(darken([1, 0.5, 0.2], 0.5)).toEqual([0.5, 0.25, 0.1]);
  });

  it('converts to linear for the renderer', () => {
    expect(srgbToLinear(0)).toBe(0);
    expect(srgbToLinear(1)).toBeCloseTo(1);
    expect(srgbToLinear(0.5)).toBeCloseTo(0.214, 3);
    expect(toLinear([0.5, 0.5, 0.5])[0]).toBeCloseTo(0.214, 3);
  });
});
