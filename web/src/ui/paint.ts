import { css, type Rgba } from './skin';

/**
 * Canvas drawing shared by the interface's drawn surfaces (the orbs and their rings): arcs and tracks around a
 * circle, etched ticks and runes, cracks and rivets. Colours come from the skin. The desktop client's
 * UI/Widgets/Paint.cs, on a 2D canvas whose y grows down, so angles run clockwise from +x as on the desktop.
 */

const TAU = Math.PI * 2;

/** A band around a circle from the top, clockwise, or counter-clockwise when mirrored. */
export function arc(g: CanvasRenderingContext2D, cx: number, cy: number, radius: number, width: number, fraction: number, color: string, mirror = false): void {
  const f = Math.min(1, Math.max(0, fraction));
  if (f <= 0.0005) {
    return;
  }
  const start = -Math.PI / 2;
  g.beginPath();
  g.arc(cx, cy, radius, start, start + f * TAU * (mirror ? -1 : 1), mirror);
  g.strokeStyle = color;
  g.lineWidth = width;
  g.lineCap = 'butt';
  g.stroke();
}

/** A full track for a ring. */
export function track(g: CanvasRenderingContext2D, cx: number, cy: number, radius: number, width: number, color: string): void {
  g.beginPath();
  g.arc(cx, cy, radius, 0, TAU);
  g.strokeStyle = color;
  g.lineWidth = width;
  g.stroke();
}

/** Radial ticks etched across a band, every stepDegrees. */
export function ticks(g: CanvasRenderingContext2D, cx: number, cy: number, inner: number, outer: number, stepDegrees: number, color: string, width = 1): void {
  g.beginPath();
  for (let a = 0; a < 360; a += stepDegrees) {
    const t = ((a - 90) * Math.PI) / 180;
    const dx = Math.cos(t);
    const dy = Math.sin(t);
    g.moveTo(cx + dx * inner, cy + dy * inner);
    g.lineTo(cx + dx * outer, cy + dy * outer);
  }
  g.strokeStyle = color;
  g.lineWidth = width;
  g.stroke();
}

/** Runes cut along a band: a stem across it with a branch varied by position, so the band reads as script. */
export function runeBand(g: CanvasRenderingContext2D, cx: number, cy: number, radius: number, width: number, count: number, color: string): void {
  g.beginPath();
  for (let i = 0; i < count; i++) {
    const angle = ((360 * i) / count - 90 + 360 / count / 2) * (Math.PI / 180);
    const dx = Math.cos(angle);
    const dy = Math.sin(angle);
    const tx = -dy;
    const ty = dx;
    const ix = cx + dx * (radius - width * 0.38);
    const iy = cy + dy * (radius - width * 0.38);
    const ox = cx + dx * (radius + width * 0.38);
    const oy = cy + dy * (radius + width * 0.38);
    g.moveTo(ix, iy);
    g.lineTo(ox, oy);
    const mx = (ix + ox) / 2;
    const my = (iy + oy) / 2;
    const b = width * 0.32 * 0.7;
    switch (i % 4) {
      case 0:
        g.moveTo(mx, my);
        g.lineTo(mx + (tx + dx) * b, my + (ty + dy) * b);
        break;
      case 1:
        g.moveTo(ox, oy);
        g.lineTo(ox + (tx - dx) * b, oy + (ty - dy) * b);
        g.moveTo(mx, my);
        g.lineTo(mx + (tx - dx) * b, my + (ty - dy) * b);
        break;
      case 2:
        g.moveTo(mx - tx * width * 0.16, my - ty * width * 0.16);
        g.lineTo(mx + tx * width * 0.16, my + ty * width * 0.16);
        break;
      default:
        g.moveTo(ix, iy);
        g.lineTo(ix + (-tx + dx) * b, iy + (-ty + dy) * b);
        break;
    }
  }
  g.strokeStyle = color;
  g.lineWidth = 1.05;
  g.stroke();
}

/** A broken ring cracks: jagged dark cuts across the band at fixed places. */
export function cracks(g: CanvasRenderingContext2D, cx: number, cy: number, radius: number, width: number, color: string): void {
  g.beginPath();
  for (const deg of [28, 141, 233, 312]) {
    const a = ((deg - 90) * Math.PI) / 180;
    const dx = Math.cos(a);
    const dy = Math.sin(a);
    const tx = -dy;
    const ty = dx;
    g.moveTo(cx + dx * (radius - width * 0.7), cy + dy * (radius - width * 0.7));
    g.lineTo(cx + dx * radius + tx * 2.2, cy + dy * radius + ty * 2.2);
    g.lineTo(cx + dx * (radius + width * 0.7) - tx * 1.4, cy + dy * (radius + width * 0.7) - ty * 1.4);
  }
  g.strokeStyle = color;
  g.lineWidth = 1.6;
  g.lineJoin = 'round';
  g.stroke();
}

/** A soft round shadow under a setting. */
export function roundShadow(g: CanvasRenderingContext2D, cx: number, cy: number, radius: number): void {
  for (let i = 6; i >= 1; i--) {
    g.beginPath();
    g.arc(cx, cy + 2, radius + i * 1.5, 0, TAU);
    g.fillStyle = 'rgba(0, 0, 0, 0.05)';
    g.fill();
  }
}

/** A colour as a CSS string with a different alpha. */
export const withAlpha = (c: Rgba, alpha: number): string => css(c, alpha);

/** A small noise tile for liquids and glass, made once. */
let noiseTile: HTMLCanvasElement | null = null;
export function noise(): HTMLCanvasElement {
  if (noiseTile !== null) {
    return noiseTile;
  }
  const size = 96;
  const c = document.createElement('canvas');
  c.width = size;
  c.height = size;
  const g = c.getContext('2d');
  if (g !== null) {
    const img = g.createImageData(size, size);
    // A deterministic hash, so the tile is the same on every load.
    let s = 1234567;
    for (let i = 0; i < img.data.length; i += 4) {
      s = (s * 1103515245 + 12345) & 0x7fffffff;
      const v = (s >> 16) & 0xff;
      img.data[i] = v;
      img.data[i + 1] = v;
      img.data[i + 2] = v;
      img.data[i + 3] = 255;
    }
    g.putImageData(img, 0, 0);
  }
  noiseTile = c;
  return c;
}
