import type { SurfaceRule, TerrainPaletteSettings } from '../config';
import { CellFlag, type CellFlagName } from './chunks';

/**
 * Terrain colours from surface tags and cell flags, by rules in config.json. Mixing happens in sRGB like the
 * desktop client's Color.Lerp and Darkened; the mesh converts to linear for the renderer.
 */

export type Rgb = [number, number, number];

export function parseHex(hex: string): Rgb {
  const h = hex.trim().replace(/^#/, '');
  const full = h.length === 3 ? h.split('').map((c) => c + c).join('') : h;
  const n = Number.parseInt(full.slice(0, 6), 16);
  if (!Number.isFinite(n) || full.length < 6) {
    return [1, 0, 1];
  }
  return [((n >> 16) & 255) / 255, ((n >> 8) & 255) / 255, (n & 255) / 255];
}

export function lerp(a: Rgb, b: Rgb, t: number): Rgb {
  return [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t];
}

/** Godot's Color.Darkened: every channel times (1 - amount). */
export function darken(c: Rgb, amount: number): Rgb {
  const k = 1 - amount;
  return [c[0] * k, c[1] * k, c[2] * k];
}

export function srgbToLinear(c: number): number {
  return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
}

export function toLinear(c: Rgb): Rgb {
  return [srgbToLinear(c[0]), srgbToLinear(c[1]), srgbToLinear(c[2])];
}

interface CompiledRule {
  tags: string[];
  flagMask: number;
  color: Rgb | null;
  mix: number;
  darken: number;
}

function compile(rule: SurfaceRule): CompiledRule {
  let flagMask = 0;
  for (const name of rule.flags ?? []) {
    const bit = CellFlag[name as CellFlagName];
    if (bit === undefined) {
      console.warn(`terrain palette: unknown cell flag '${name}'`);
      continue;
    }
    flagMask |= bit;
  }
  return {
    tags: rule.tags ?? [],
    flagMask,
    color: rule.color ? parseHex(rule.color) : null,
    mix: rule.mix ?? 1,
    darken: rule.darken ?? 0,
  };
}

function matches(rule: CompiledRule, tags: ReadonlySet<string>, flags: number): boolean {
  return (flags & rule.flagMask) === rule.flagMask && rule.tags.every((t) => tags.has(t));
}

export class SurfacePalette {
  private readonly base: CompiledRule[];
  private readonly structure: CompiledRule[];
  private readonly tints: CompiledRule[];
  private readonly layers: CompiledRule[];
  private readonly fallback: Rgb;
  private readonly structureFallback: Rgb;
  private readonly layerFallback: Rgb;
  readonly skirtDarken: number;
  readonly water: Rgb;
  readonly waterOpacity: number;

  constructor(settings: TerrainPaletteSettings) {
    this.base = settings.base.map(compile);
    this.structure = settings.structure.map(compile);
    this.tints = settings.tints.map(compile);
    this.layers = settings.layers.map(compile);
    this.fallback = parseHex(settings.fallback);
    this.structureFallback = parseHex(settings.structureFallback);
    this.layerFallback = parseHex(settings.layerFallback);
    this.skirtDarken = settings.skirtDarken;
    this.water = parseHex(settings.waterColor);
    this.waterOpacity = settings.waterOpacity;
  }

  /**
   * The colour of a ground cell. Structure cells take the first matching structure rule and nothing else;
   * other cells take the first matching base rule, then every matching tint in order.
   */
  cell(tags: ReadonlySet<string>, flags: number): Rgb {
    if ((flags & CellFlag.structure) !== 0 || tags.has('structure')) {
      return this.structure.find((r) => matches(r, tags, flags))?.color ?? this.structureFallback;
    }
    let color = this.base.find((r) => r.color !== null && matches(r, tags, flags))?.color ?? this.fallback;
    for (const tint of this.tints) {
      if (!matches(tint, tags, flags)) {
        continue;
      }
      if (tint.color !== null) {
        color = lerp(color, tint.color, tint.mix);
      }
      if (tint.darken > 0) {
        color = darken(color, tint.darken);
      }
    }
    return color;
  }

  layer(tags: ReadonlySet<string>): Rgb {
    return this.layers.find((r) => matches(r, tags, 0))?.color ?? this.layerFallback;
  }

  skirt(color: Rgb): Rgb {
    return darken(color, this.skirtDarken);
  }
}
