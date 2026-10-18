/**
 * Colour arithmetic the world shares: hex parsing, mixing in sRGB like the desktop client's Color.Lerp and
 * Darkened, and the conversion to linear the renderer wants. Which colour a cell or a body gets is the
 * appearance content's business (world/appearance), the same on both clients.
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
