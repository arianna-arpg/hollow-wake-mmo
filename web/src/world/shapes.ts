import type { ShapeSizeInfo } from '../net/protocol';

/**
 * The outlines of the delivery shapes the server names (docs/design/deliveries.md), for drawing projectiles,
 * areas, fields and telegraph marks by shape: circle, square, triangle, sector (cone), crescent, line, band, bar,
 * arc and wave. Outlines are in the shape's own frame on the ground: x along its facing, y to its left, the anchor
 * at the origin. Centered shapes sit on the anchor; anchored ones (sector, crescent, line, band) extend from it.
 * Sizes are as the wire sends them (angle in radians, inner as a distance); a size the wire leaves out takes the
 * shape's default, as Effects/Shapes.cs gives it. The desktop client's UI/Logic/ShapeOutline.cs.
 */

export type Point = [number, number];

/** A filled region: its outer loop and any holes, each loop counter-clockwise. */
export interface Region {
  outer: Point[];
  holes: Point[][];
}

export interface ShapeSpec {
  name: string;
  radius: number;
  /** Full angle in radians (sectors, crescents, arcs, waves). */
  angle: number;
  /** Full width (lines, bands, bars) or thickness (arcs, waves). */
  width: number;
  /** The distance from the anchor left empty (rings, hollow sectors, a line that starts out). */
  inner: number;
  /** For arcs and waves, in radii: how far behind the anchor the circle they bow along centers. */
  curvature: number;
  /** Core angular allowance, clipped by outer and inner radial bounds. */
  centerAllowance?: number;
}

const DEG = Math.PI / 180;

/** Shapes the client draws; any other name falls back to a circle. */
export const SHAPES = ['circle', 'square', 'triangle', 'sector', 'cone', 'crescent', 'line', 'band', 'bar', 'arc', 'wave'] as const;
export const WIDTH_SHAPES: readonly string[] = ['line', 'band', 'bar', 'arc', 'wave'];
export const ANGLE_SHAPES: readonly string[] = ['sector', 'cone', 'crescent', 'arc', 'wave'];

/** A shape's full specification from what the wire sent, with the shape's defaults for what it left out. */
export function specOf(name: string, size: ShapeSizeInfo | undefined, fallbackRadius: number): ShapeSpec {
  const known = (SHAPES as readonly string[]).includes(name) ? name : 'circle';
  const radius = Math.max(0.05, size?.radius ?? fallbackRadius);
  const defaults: Record<string, { angle: number; width: number; inner: number }> = {
    circle: { angle: 0, width: 0, inner: 0 },
    square: { angle: 0, width: 0, inner: 0 },
    triangle: { angle: 0, width: 0, inner: 0 },
    sector: { angle: 90 * DEG, width: 0, inner: 0 },
    cone: { angle: 90 * DEG, width: 0, inner: 0 },
    crescent: { angle: 180 * DEG, width: 0, inner: radius * 0.5 },
    line: { angle: 0, width: Math.max(0.3, radius * 0.25), inner: 0 },
    band: { angle: 0, width: radius, inner: 0 },
    bar: { angle: 0, width: Math.max(0.3, radius * 0.25), inner: 0 },
    arc: { angle: 90 * DEG, width: Math.max(0.3, radius * 0.25), inner: 0 },
    wave: { angle: 120 * DEG, width: Math.max(0.3, radius * 0.25), inner: 0 },
  };
  const d = defaults[known];
  const bow = known === 'wave' ? 2 : known === 'arc' ? 1 : 0;
  return {
    name: known,
    radius,
    angle: Math.min(Math.PI * 2, Math.max(0.01, size?.angle ?? d.angle)),
    width: Math.max(0.02, size?.width ?? d.width),
    inner: Math.min(radius * 0.98, Math.max(0, size?.inner ?? d.inner)),
    curvature: size?.curvature !== undefined && size.curvature > 0 ? size.curvature : bow,
  };
}

const circle = (r: number, segments: number, cx = 0): Point[] => {
  const pts: Point[] = [];
  for (let i = 0; i < segments; i++) {
    const a = (i / segments) * Math.PI * 2;
    pts.push([cx + Math.cos(a) * r, Math.sin(a) * r]);
  }
  return pts;
};
const ring = (cx: number, inner: number, outer: number, segments: number): Region => ({
  outer: circle(outer, segments * 2, cx), holes: inner > 0 ? [circle(inner, segments * 2, cx).reverse()] : [],
});

/** A ring sector between two radii and two angles about a center, counter-clockwise. */
const annulus = (cx: number, r0: number, r1: number, a0: number, a1: number, segments: number): Point[] => {
  const pts: Point[] = [];
  for (let i = 0; i <= segments; i++) {
    const a = a0 + ((a1 - a0) * i) / segments;
    pts.push([cx + Math.cos(a) * r1, Math.sin(a) * r1]);
  }
  for (let i = segments; i >= 0; i--) {
    const a = a0 + ((a1 - a0) * i) / segments;
    pts.push([cx + Math.cos(a) * r0, Math.sin(a) * r0]);
  }
  return pts;
};

/** The region a shape covers, in its own frame (x along the facing, y to the left). */
export function regionOf(spec: ShapeSpec, segments = 32): Region {
  const r = spec.radius;
  switch (spec.name) {
    case 'square': {
      const outer: Point[] = [[-r, -r], [r, -r], [r, r], [-r, r]];
      const i = spec.inner;
      return { outer, holes: i > 0 ? [[[-i, -i], [-i, i], [i, i], [i, -i]]] : [] };
    }
    case 'triangle':
      // Centered, its apex along the facing, inscribed in a circle of the radius.
      return { outer: [[r, 0], [r * Math.cos(120 * DEG), r * Math.sin(120 * DEG)], [r * Math.cos(240 * DEG), r * Math.sin(240 * DEG)]], holes: [] };
    case 'sector':
    case 'cone':
    case 'crescent': {
      const angle = Math.min(Math.PI * 2, Math.max(0, spec.angle)), half = angle / 2;
      const center = Math.min(r, spec.centerAllowance ?? 0);
      // Saturated angles cover a full turn. The core's central allowance obeys both radial bounds.
      if (angle >= Math.PI * 2 || center >= r) return ring(0, spec.inner, r, segments);
      if (angle <= 0) return spec.inner < center ? ring(0, spec.inner, center, segments) : { outer: [], holes: [] };
      if (spec.inner >= center && spec.inner > 0) {
        return { outer: annulus(0, spec.inner, r, -half, half, segments), holes: [] };
      }
      const pts: Point[] = [];
      for (let i = 0; i <= segments; i++) {
        const a = -half + (angle * i) / segments;
        pts.push([Math.cos(a) * r, Math.sin(a) * r]);
      }
      if (center <= 0) pts.push([0, 0]);
      else for (let i = 0; i <= segments; i++) {
        const a = half + ((Math.PI * 2 - angle) * i) / segments;
        pts.push([Math.cos(a) * center, Math.sin(a) * center]);
      }
      return { outer: pts, holes: spec.inner > 0 ? [circle(spec.inner, segments * 2).reverse()] : [] };
    }
    case 'line':
    case 'band': {
      const w = spec.width / 2;
      return { outer: [[spec.inner, -w], [r, -w], [r, w], [spec.inner, w]], holes: [] };
    }
    case 'bar': {
      // Centered and laid across the facing: the radius to each side, the width thick.
      const w = spec.width / 2;
      return { outer: [[-w, -r], [w, -r], [w, r], [-w, r]], holes: [] };
    }
    case 'arc':
    case 'wave': {
      // A blade bowed forward: part of a circle whose center lies behind the anchor by curvature times the radius.
      const bend = r * Math.max(0.1, spec.curvature);
      const angle = Math.min(Math.PI * 2, Math.max(0, spec.angle)), half = angle / 2;
      const inner = Math.max(0, bend - spec.width / 2), outer = bend + spec.width / 2;
      return angle >= Math.PI * 2 ? ring(-bend, inner, outer, segments) :
        { outer: annulus(-bend, inner, outer, -half, half, segments), holes: [] };
    }
    default:
      return { outer: circle(r, segments * 2), holes: spec.inner > 0 ? [circle(spec.inner, segments * 2).reverse()] : [] };
  }
}

/** The farthest any point of a region lies from the anchor, for picking and culling. */
export function reachOf(region: Region): number {
  return region.outer.reduce((m, [x, y]) => Math.max(m, Math.hypot(x, y)), 0);
}

/** Whether a point (in the shape's frame) lies inside the region: inside its outer loop and outside its holes. */
export function inside(region: Region, x: number, y: number): boolean {
  const within = (loop: Point[]) => {
    let hit = false;
    for (let i = 0, j = loop.length - 1; i < loop.length; j = i++) {
      const [xi, yi] = loop[i];
      const [xj, yj] = loop[j];
      if (yi > y !== yj > y && x < ((xj - xi) * (y - yi)) / (yj - yi) + xi) {
        hit = !hit;
      }
    }
    return hit;
  };
  return within(region.outer) && !region.holes.some(within);
}
