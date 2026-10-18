import type { Rgb } from '../palette';

/**
 * A built body, the browser twin of the desktop's World/Logic/BodyPlan.cs: parts (primitives with a size,
 * a centre, a turn and a colour) on bones (pivots in a hierarchy), with sockets gear attaches at. Body space
 * is x forward, y up, z to the body's right, in world units. The geometry here is built in the same order
 * with the same arithmetic as the desktop's, so a player sees one body on either client.
 */

export type Vec3 = readonly [number, number, number];

export const ZERO: Vec3 = [0, 0, 0];

export const v3 = (x: number, y: number, z: number): Vec3 => [x, y, z];
export const add = (a: Vec3, b: Vec3): Vec3 => [a[0] + b[0], a[1] + b[1], a[2] + b[2]];
export const sub = (a: Vec3, b: Vec3): Vec3 => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
export const mul = (a: Vec3, s: number): Vec3 => [a[0] * s, a[1] * s, a[2] * s];
export const len = (a: Vec3): number => Math.hypot(a[0], a[1], a[2]);

export type PartShape = 'box' | 'ellipsoid' | 'cone' | 'cylinder' | 'wedge';

export interface BodyPart {
  bone: string;
  shape: PartShape;
  size: Vec3;
  at: Vec3;
  /** Euler degrees: roll about x, pitch about z, yaw about y, applied in that order. */
  turn: Vec3;
  color: Rgb;
  /** For cylinders: the top radius as a fraction of the bottom one. */
  taper: number;
  glow: boolean;
  segments: number;
}

export interface BodyBone {
  name: string;
  parent: string | null;
  pivot: Vec3;
  rest: Vec3;
}

export interface BodySocket {
  bone: string;
  at: Vec3;
  turn: Vec3;
  size: number;
}

export interface PartOptions {
  turn?: Vec3;
  taper?: number;
  glow?: boolean;
  segments?: number;
}

export class BodyPlan {
  family = '';
  rig = 'static';
  readonly bones: BodyBone[] = [];
  readonly parts: BodyPart[] = [];
  readonly sockets = new Map<string, BodySocket>();
  height = 0;
  width = 0;
  hover = 0;
  glow: Rgb | null = null;
  swings = false;

  bone(name: string, parent: string | null, pivot: Vec3, rest: Vec3 = ZERO): BodyBone {
    const bone = { name, parent, pivot, rest };
    this.bones.push(bone);
    return bone;
  }

  hasBone(name: string): boolean {
    return this.bones.some((b) => b.name === name);
  }

  add(bone: string, shape: PartShape, size: Vec3, at: Vec3, color: Rgb, options: PartOptions = {}): void {
    this.parts.push({
      bone: this.hasBone(bone) ? bone : 'root',
      shape,
      size,
      at,
      turn: options.turn ?? ZERO,
      color,
      taper: options.taper ?? 1,
      glow: options.glow ?? false,
      segments: options.segments ?? 8,
    });
  }

  /** The bounds of every part's centre and size, ignoring turns: enough for cards and picking. */
  bounds(): { min: Vec3; max: Vec3 } {
    if (this.parts.length === 0) {
      return { min: ZERO, max: ZERO };
    }
    let min: Vec3 = [Infinity, Infinity, Infinity];
    let max: Vec3 = [-Infinity, -Infinity, -Infinity];
    for (const p of this.parts) {
      const turned = p.turn[0] !== 0 || p.turn[1] !== 0 || p.turn[2] !== 0;
      const reach: Vec3 = turned ? mul([1, 1, 1], len(p.size) * 0.5) : mul(p.size, 0.5);
      min = [Math.min(min[0], p.at[0] - reach[0]), Math.min(min[1], p.at[1] - reach[1]), Math.min(min[2], p.at[2] - reach[2])];
      max = [Math.max(max[0], p.at[0] + reach[0]), Math.max(max[1], p.at[1] + reach[1]), Math.max(max[2], p.at[2] + reach[2])];
    }
    return { min, max };
  }
}

const DEG = Math.PI / 180;

/** Roll about x, then pitch about z, then yaw about y (radians). */
export function turnVec(v: Vec3, roll: number, pitch: number, yaw: number): Vec3 {
  let [x, y, z] = v;
  if (roll !== 0) {
    const s = Math.sin(roll);
    const c = Math.cos(roll);
    [y, z] = [y * c - z * s, y * s + z * c];
  }
  if (pitch !== 0) {
    const s = Math.sin(pitch);
    const c = Math.cos(pitch);
    [x, y] = [x * c - y * s, x * s + y * c];
  }
  if (yaw !== 0) {
    const s = Math.sin(yaw);
    const c = Math.cos(yaw);
    [x, z] = [x * c + z * s, -x * s + z * c];
  }
  return [x, y, z];
}

function normal(a: Vec3, b: Vec3, c: Vec3): Vec3 {
  const u = sub(b, a);
  const v = sub(c, a);
  const n: Vec3 = [u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0]];
  const l = len(n);
  return l < 1e-8 ? [0, 1, 0] : mul(n, 1 / l);
}

function quad(tris: Vec3[], a: Vec3, b: Vec3, c: Vec3, d: Vec3): void {
  // a, b, c, d around the face as listed clockwise seen from outside; emitted counter-clockwise like every other part
  tris.push(a, c, b, a, d, c);
}

function boxTris(tris: Vec3[], size: Vec3, pinch: number): void {
  const hx = size[0] / 2;
  const hy = size[1] / 2;
  const hz = size[2] / 2;
  const tz = hz * (1 - pinch);
  const b0: Vec3 = [-hx, -hy, -hz];
  const b1: Vec3 = [hx, -hy, -hz];
  const b2: Vec3 = [hx, -hy, hz];
  const b3: Vec3 = [-hx, -hy, hz];
  const t0: Vec3 = [-hx, hy, -tz];
  const t1: Vec3 = [hx, hy, -tz];
  const t2: Vec3 = [hx, hy, tz];
  const t3: Vec3 = [-hx, hy, tz];
  quad(tris, b0, b3, b2, b1);
  quad(tris, t0, t1, t2, t3);
  quad(tris, b1, b2, t2, t1);
  quad(tris, b3, b0, t0, t3);
  quad(tris, b2, b3, t3, t2);
  quad(tris, b0, b1, t1, t0);
}

function ellipsoidTris(tris: Vec3[], size: Vec3, segments: number): void {
  const rings = Math.max(3, Math.floor(segments / 2));
  const rx = size[0] / 2;
  const ry = size[1] / 2;
  const rz = size[2] / 2;
  const p = (ring: number, seg: number): Vec3 => {
    const phi = (Math.PI * ring) / rings;
    const theta = (2 * Math.PI * seg) / segments;
    return [rx * Math.sin(phi) * Math.cos(theta), ry * Math.cos(phi), rz * Math.sin(phi) * Math.sin(theta)];
  };
  for (let r = 0; r < rings; r++) {
    for (let s = 0; s < segments; s++) {
      const a = p(r, s);
      const b = p(r + 1, s);
      const c = p(r + 1, s + 1);
      const d = p(r, s + 1);
      if (r === 0) {
        tris.push(a, c, b);
      } else if (r === rings - 1) {
        tris.push(a, d, b);
      } else {
        tris.push(a, d, c, a, c, b);
      }
    }
  }
}

function coneTris(tris: Vec3[], size: Vec3, segments: number): void {
  const rx = size[0] / 2;
  const rz = size[2] / 2;
  const hy = size[1] / 2;
  const apex: Vec3 = [0, hy, 0];
  const centre: Vec3 = [0, -hy, 0];
  for (let s = 0; s < segments; s++) {
    const t0 = (2 * Math.PI * s) / segments;
    const t1 = (2 * Math.PI * (s + 1)) / segments;
    const a: Vec3 = [rx * Math.cos(t0), -hy, rz * Math.sin(t0)];
    const b: Vec3 = [rx * Math.cos(t1), -hy, rz * Math.sin(t1)];
    tris.push(a, apex, b, a, b, centre);
  }
}

function cylinderTris(tris: Vec3[], size: Vec3, taper: number, segments: number): void {
  const rx = size[0] / 2;
  const rz = size[2] / 2;
  const hy = size[1] / 2;
  const top: Vec3 = [0, hy, 0];
  const bottom: Vec3 = [0, -hy, 0];
  for (let s = 0; s < segments; s++) {
    const t0 = (2 * Math.PI * s) / segments;
    const t1 = (2 * Math.PI * (s + 1)) / segments;
    const a: Vec3 = [rx * Math.cos(t0), -hy, rz * Math.sin(t0)];
    const b: Vec3 = [rx * Math.cos(t1), -hy, rz * Math.sin(t1)];
    const c: Vec3 = [rx * taper * Math.cos(t1), hy, rz * taper * Math.sin(t1)];
    const d: Vec3 = [rx * taper * Math.cos(t0), hy, rz * taper * Math.sin(t0)];
    tris.push(a, d, c, a, c, b, d, top, c, a, b, bottom);
  }
}

/**
 * Triangles for a part, flat shaded, as positions and normals (three floats each, three vertices per
 * triangle, counter-clockwise seen from outside), with the part's turn and centre applied.
 */
export function buildPart(part: BodyPart): { positions: Float32Array; normals: Float32Array } {
  const tris: Vec3[] = [];
  switch (part.shape) {
    case 'box':
      boxTris(tris, part.size, 0);
      break;
    case 'wedge':
      boxTris(tris, part.size, 0.85);
      break;
    case 'ellipsoid':
      ellipsoidTris(tris, part.size, part.segments);
      break;
    case 'cone':
      coneTris(tris, part.size, part.segments);
      break;
    case 'cylinder':
      cylinderTris(tris, part.size, part.taper, part.segments);
      break;
  }
  const [rx, ry, rz] = [part.turn[0] * DEG, part.turn[1] * DEG, part.turn[2] * DEG];
  const positions = new Float32Array(tris.length * 3);
  const normals = new Float32Array(tris.length * 3);
  for (let i = 0; i < tris.length; i += 3) {
    const a = turnVec(tris[i], rx, ry, rz);
    const b = turnVec(tris[i + 1], rx, ry, rz);
    const c = turnVec(tris[i + 2], rx, ry, rz);
    const n = normal(a, b, c);
    const put = (k: number, v: Vec3) => {
      positions[k * 3] = v[0] + part.at[0];
      positions[k * 3 + 1] = v[1] + part.at[1];
      positions[k * 3 + 2] = v[2] + part.at[2];
      normals[k * 3] = n[0];
      normals[k * 3 + 1] = n[1];
      normals[k * 3 + 2] = n[2];
    };
    put(i, a);
    put(i + 1, b);
    put(i + 2, c);
  }
  return { positions, normals };
}
