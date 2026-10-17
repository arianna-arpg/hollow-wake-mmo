import { describe, expect, it } from 'vitest';
import { DEFAULTS } from '../config';
import { CellFlag } from './chunks';
import { SurfacePalette, darken, lerp, parseHex, srgbToLinear } from './palette';

const tags = (text: string) => new Set(text.split(' '));
const hex = (c: [number, number, number]) => '#' + c.map((v) => Math.round(v * 255).toString(16).padStart(2, '0')).join('');

describe('palette', () => {
  const palette = new SurfacePalette(DEFAULTS.terrain);
  const walk = CellFlag.walkable;

  it('parses hex colours', () => {
    expect(parseHex('#ff8000')).toEqual([1, 128 / 255, 0]);
    expect(parseHex('#fff')).toEqual([1, 1, 1]);
  });

  it('mixes like Godot colours', () => {
    expect(lerp([0, 0, 0], [1, 1, 1], 0.25)).toEqual([0.25, 0.25, 0.25]);
    expect(darken([1, 0.5, 0], 0.5)).toEqual([0.5, 0.25, 0]);
    expect(srgbToLinear(1)).toBeCloseTo(1);
    expect(srgbToLinear(0.5)).toBeCloseTo(0.214, 3);
  });

  it('colours each named surface by its first matching rule', () => {
    expect(hex(palette.cell(tags('ground grass'), walk))).toBe('#5b8c3a');
    expect(hex(palette.cell(tags('ground grass meadow'), walk))).toBe('#79b84a');
    expect(hex(palette.cell(tags('ground sand'), walk))).toBe('#d8c89a');
    expect(hex(palette.cell(tags('ground rock grass'), walk))).toBe('#8c8a7a');
    expect(hex(palette.cell(tags('ground rock snow'), walk))).toBe('#e8ecf0');
    expect(hex(palette.cell(tags('ground bog'), walk))).toBe('#4f6b3a');
    expect(hex(palette.cell(tags('ground ash scorched'), walk))).toBe('#6b4a3a');
    expect(hex(palette.cell(tags('ground grass forest_floor deep'), walk))).toBe('#1f4d22');
  });

  it('takes structure colours for structure cells and ignores tints there', () => {
    expect(hex(palette.cell(tags('ground road paved structure'), CellFlag.structure | walk))).toBe('#9e8f75');
    expect(hex(palette.cell(tags('ground wall structure'), CellFlag.structure | CellFlag.blocked))).toBe('#807a80');
    expect(hex(palette.cell(tags('ground paved structure'), CellFlag.structure | walk))).toBe('#99948a');
  });

  it('tints water by depth only where the cell is still water', () => {
    const dry = palette.cell(tags('ground grass water shallow'), walk);
    const shallow = palette.cell(tags('ground grass water shallow'), walk | CellFlag.water);
    const deep = palette.cell(tags('ground grass water deep'), CellFlag.water);
    expect(hex(dry)).toBe('#5b8c3a');
    expect(shallow[2]).toBeGreaterThan(dry[2]);
    expect(deep[2]).toBeGreaterThan(dry[2]);
    expect(deep[1]).toBeLessThan(shallow[1]);
  });

  it('darkens cliffs and blocked ground', () => {
    const grass = palette.cell(tags('ground grass'), walk);
    const cliff = palette.cell(tags('ground grass cliff'), walk | CellFlag.blocked);
    expect(cliff[1]).toBeCloseTo(grass[1] * 0.65 * 0.8, 5);
  });

  it('colours layers and skirts', () => {
    expect(hex(palette.layer(tags('bridge structure')))).toBe('#8c6b4d');
    expect(hex(palette.layer(tags('floor')))).toBe('#99948c');
    expect(palette.skirt([1, 1, 1])).toEqual([0.7, 0.7, 0.7]);
  });
});
