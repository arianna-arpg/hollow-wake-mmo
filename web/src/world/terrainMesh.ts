import { CellFlag, floorDiv, mod, type Chunk } from './chunks';
import { toLinear, type Rgb } from './palette';
import type { SurfaceLook } from './appearance/surfaceLook';

/**
 * Geometry for one chunk, a port of the desktop client's TerrainMesh: one quad per cell coloured by the
 * surfaces content from its surface tags and cell flags (with a seeded value jitter in patches), smooth
 * normals from the height field, vertical skirts wherever a flattened or structure cell steps against a
 * neighbour (in the structure's side colour), a pitched roof over roof cells, and extra quads for layered
 * surfaces (bridges, floors). Neighbouring chunks are read for corner heights so borders meet. Positions are
 * three.js coordinates relative to the chunk's origin cell. Pure arithmetic over arrays; terrain.ts turns the
 * result into a mesh.
 */

export interface MeshArrays {
  positions: Float32Array;
  normals: Float32Array;
  colors: Float32Array;
  indices: Uint16Array | Uint32Array;
  quads: number;
}

export type ChunkLookup = (x: number, y: number) => Chunk | undefined;

class QuadBuilder {
  readonly positions: number[] = [];
  readonly normals: number[] = [];
  readonly colors: number[] = [];
  readonly indices: number[] = [];
  quads = 0;

  /**
   * One quad with corners a, b, c, d given in simulation space (x east, y north, z up) going around it, its
   * front on the side of (b - a) x (d - a). Corner normals, when given, are already in three.js space.
   */
  add(corners: readonly number[], color: Rgb, cornerNormals?: readonly number[]): void {
    const start = this.positions.length / 3;
    for (let k = 0; k < 4; k++) {
      const x = corners[k * 3];
      const y = corners[k * 3 + 1];
      const z = corners[k * 3 + 2];
      this.positions.push(x, z, -y);
      this.colors.push(color[0], color[1], color[2]);
    }
    if (cornerNormals) {
      for (let k = 0; k < 12; k++) {
        this.normals.push(cornerNormals[k]);
      }
    } else {
      this.flatNormal(start, 4);
    }
    // three.js takes counter-clockwise triangles as front faces, and the axis mapping is a proper rotation.
    this.indices.push(start, start + 1, start + 2, start, start + 2, start + 3);
    this.quads++;
  }

  /** One triangle with corners a, b, c in simulation space going around it, its front on the side of (b - a) x (c - a). */
  tri(corners: readonly number[], color: Rgb): void {
    const start = this.positions.length / 3;
    for (let k = 0; k < 3; k++) {
      this.positions.push(corners[k * 3], corners[k * 3 + 2], -corners[k * 3 + 1]);
      this.colors.push(color[0], color[1], color[2]);
    }
    this.flatNormal(start, 3);
    this.indices.push(start, start + 1, start + 2);
  }

  private flatNormal(start: number, count: number): void {
    const p = this.positions;
    const a = start * 3;
    const ux = p[a + 3] - p[a];
    const uy = p[a + 4] - p[a + 1];
    const uz = p[a + 5] - p[a + 2];
    const last = count === 4 ? 9 : 6;
    const vx = p[a + last] - p[a];
    const vy = p[a + last + 1] - p[a + 1];
    const vz = p[a + last + 2] - p[a + 2];
    let nx = uy * vz - uz * vy;
    let ny = uz * vx - ux * vz;
    let nz = ux * vy - uy * vx;
    const len = Math.hypot(nx, ny, nz);
    if (len < 1e-6) {
      nx = 0;
      ny = 1;
      nz = 0;
    } else {
      nx /= len;
      ny /= len;
      nz /= len;
    }
    for (let k = 0; k < count; k++) {
      this.normals.push(nx, ny, nz);
    }
  }

  finish(): MeshArrays {
    const vertexCount = this.positions.length / 3;
    return {
      positions: Float32Array.from(this.positions),
      normals: Float32Array.from(this.normals),
      colors: Float32Array.from(this.colors),
      indices: vertexCount < 65536 ? Uint16Array.from(this.indices) : Uint32Array.from(this.indices),
      quads: this.quads,
    };
  }
}

export function hasWater(chunk: Chunk): boolean {
  if (chunk.waterLevel === null) {
    return false;
  }
  return chunk.flags.some((f) => (f & CellFlag.water) !== 0);
}

const isFlat = (flags: number) => (flags & (CellFlag.structure | CellFlag.flattened)) !== 0;

/** A pitched cap over a roof cell: a low hip to the cell's centre, two faces lit and two shaded. */
function roof(out: QuadBuilder, lx: number, ly: number, h: number, top: Rgb, shade: Rgb): void {
  const rise = 0.55;
  const apex = [lx + 0.5, ly + 0.5, h + rise];
  const a = [lx, ly, h];
  const b = [lx + 1, ly, h];
  const c = [lx + 1, ly + 1, h];
  const d = [lx, ly + 1, h];
  out.tri([...a, ...b, ...apex], top);
  out.tri([...b, ...c, ...apex], shade);
  out.tri([...c, ...d, ...apex], top);
  out.tri([...d, ...a, ...apex], shade);
}

export function buildGround(chunk: Chunk, lookup: ChunkLookup, look: SurfaceLook): MeshArrays {
  const size = chunk.size;
  const originX = chunk.x * size;
  const originY = chunk.y * size;
  const out = new QuadBuilder();

  let cellHeight = 0;
  let cellFlags = 0;
  const readCell = (lx: number, ly: number): boolean => {
    if (lx >= 0 && ly >= 0 && lx < size && ly < size) {
      const i = ly * size + lx;
      cellHeight = chunk.height[i];
      cellFlags = chunk.flags[i];
      return true;
    }
    const wx = originX + lx;
    const wy = originY + ly;
    const other = lookup(floorDiv(wx, size), floorDiv(wy, size));
    if (other === undefined) {
      return false;
    }
    const j = mod(wy, size) * size + mod(wx, size);
    cellHeight = other.height[j];
    cellFlags = other.flags[j];
    return true;
  };

  const stride = size + 3;
  const corners = new Float32Array(stride * stride);
  for (let j = -1; j <= size + 1; j++) {
    for (let i = -1; i <= size + 1; i++) {
      let sum = 0;
      let n = 0;
      for (let dy = -1; dy <= 0; dy++) {
        for (let dx = -1; dx <= 0; dx++) {
          if (readCell(i + dx, j + dy)) {
            sum += cellHeight;
            n++;
          }
        }
      }
      corners[(j + 1) * stride + (i + 1)] = n === 0 ? 0 : sum / n;
    }
  }
  const corner = (i: number, j: number) => corners[(j + 1) * stride + (i + 1)];
  const normals: number[] = new Array(12);
  const cornerNormal = (i: number, j: number, k: number) => {
    const dzdx = (corner(i + 1, j) - corner(i - 1, j)) * 0.5;
    const dzdy = (corner(i, j + 1) - corner(i, j - 1)) * 0.5;
    const len = Math.hypot(dzdx, 1, dzdy);
    normals[k] = -dzdx / len;
    normals[k + 1] = 1 / len;
    normals[k + 2] = dzdy / len;
  };

  // the colours of a cell: its top, the side its skirts take, and a roof when the content raises one
  const colorCache = new Map<number, { top: Rgb; side: Rgb; roof: Rgb | null }>();
  const colorsOf = (i: number) => {
    const key = chunk.surface[i] * 256 + chunk.flags[i];
    let c = colorCache.get(key);
    if (c === undefined) {
      const tags = chunk.tagTable[chunk.surface[i]] ?? chunk.tagTable[0];
      const flags = chunk.flags[i];
      const structure = look.structure(tags, flags);
      const top = structure?.top ?? look.cell(tags, flags);
      c = { top, side: structure?.side ?? look.skirt(top), roof: structure?.roof ?? null };
      colorCache.set(key, c);
    }
    return c;
  };

  const skirt = (x0: number, y0: number, x1: number, y1: number, bottom: number, top: number, color: Rgb) => {
    if (top - bottom < 0.02) {
      return;
    }
    out.add([x0, y0, bottom, x1, y1, bottom, x1, y1, top, x0, y0, top], toLinear(color));
  };

  for (let ly = 0; ly < size; ly++) {
    for (let lx = 0; lx < size; lx++) {
      const i = ly * size + lx;
      const h = chunk.height[i];
      const flags = chunk.flags[i];
      const flat = isFlat(flags);
      const { top, side, roof: roofColor } = colorsOf(i);
      const jitter = look.jitter(originX + lx, originY + ly, flags);
      const color: Rgb = jitter === 1 ? top : [Math.min(1, top[0] * jitter), Math.min(1, top[1] * jitter), Math.min(1, top[2] * jitter)];
      const linear = toLinear(color);
      if (flat) {
        out.add([lx, ly, h, lx + 1, ly, h, lx + 1, ly + 1, h, lx, ly + 1, h], linear);
        if (roofColor !== null) {
          roof(out, lx, ly, h, toLinear(roofColor), toLinear([roofColor[0] * 0.75, roofColor[1] * 0.75, roofColor[2] * 0.75]));
        }
      } else {
        cornerNormal(lx, ly, 0);
        cornerNormal(lx + 1, ly, 3);
        cornerNormal(lx + 1, ly + 1, 6);
        cornerNormal(lx, ly + 1, 9);
        out.add(
          [lx, ly, corner(lx, ly), lx + 1, ly, corner(lx + 1, ly), lx + 1, ly + 1, corner(lx + 1, ly + 1), lx, ly + 1, corner(lx, ly + 1)],
          linear,
          normals,
        );
      }
      // Flattened cells keep their own height on every corner, so an edge they share can step; a skirt closes
      // the gap, in the structure's side colour so walls read as stone or wood below their tops.
      const skirtColor = (flags & CellFlag.structure) !== 0 ? side : look.skirt(top);
      if (readCell(lx + 1, ly)) {
        const theirs = cellHeight;
        const theirFlat = isFlat(cellFlags);
        if (flat || theirFlat) {
          const c0 = corner(lx + 1, ly);
          const c1 = corner(lx + 1, ly + 1);
          const mineLow = flat ? h : Math.min(c0, c1);
          const mineHigh = flat ? h : Math.max(c0, c1);
          const theirsLow = theirFlat ? theirs : Math.min(c0, c1);
          const theirsHigh = theirFlat ? theirs : Math.max(c0, c1);
          skirt(lx + 1, ly, lx + 1, ly + 1, Math.min(mineLow, theirsLow), Math.max(mineHigh, theirsHigh), h >= theirs ? skirtColor : look.skirt(top));
        }
      }
      if (readCell(lx, ly + 1)) {
        const theirs = cellHeight;
        const theirFlat = isFlat(cellFlags);
        if (flat || theirFlat) {
          const c0 = corner(lx, ly + 1);
          const c1 = corner(lx + 1, ly + 1);
          const mineLow = flat ? h : Math.min(c0, c1);
          const mineHigh = flat ? h : Math.max(c0, c1);
          const theirsLow = theirFlat ? theirs : Math.min(c0, c1);
          const theirsHigh = theirFlat ? theirs : Math.max(c0, c1);
          skirt(lx, ly + 1, lx + 1, ly + 1, Math.min(mineLow, theirsLow), Math.max(mineHigh, theirsHigh), h >= theirs ? skirtColor : look.skirt(top));
        }
      }
    }
  }

  for (const [index, surfaces] of chunk.layers) {
    const lx = index % size;
    const ly = Math.floor(index / size);
    for (const s of surfaces) {
      out.add([lx, ly, s.height, lx + 1, ly, s.height, lx + 1, ly + 1, s.height, lx, ly + 1, s.height], toLinear(look.layer(s.tags)));
    }
  }
  return out.finish();
}
